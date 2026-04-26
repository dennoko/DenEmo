using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using DenEmo.Models;
using DenEmo.Core;
using DenEmo.UI;

namespace DenEmo
{
    public class DenEmoWindow : EditorWindow
    {
        // ─── Mode ─────────────────────────────────────────────────────────────

        private enum EditorMode { Pose, Animation }
        private EditorMode _currentMode = EditorMode.Pose;

        // ─── Pose mode state ──────────────────────────────────────────────────
        [MenuItem("Tools/DenEmo")]
        public static void ShowWindow()
        {
            var w = GetWindow<DenEmoWindow>("DenEmo");
            w.minSize = new Vector2(380, 400);
        }

        private ShapeKeyModel  _model   = new ShapeKeyModel();
        private ShapeKeyListUI _listUI  = new ShapeKeyListUI();
        private AnimationModeUI _animModeUI = new AnimationModeUI();

        private string saveFolder = "Assets/Generated_Animations";
        private string searchText = string.Empty;
        private string lastSearchText = null;

        private bool showOnlyIncluded   = false;
        private bool showOnlyNonZero    = false;
        private bool showOnlyFavorites  = false;
        private bool symmetryMode       = false;
        private bool autoBackup         = true;
        private bool overwriteSaveEnabled = false;

        private bool lastShowOnlyIncluded  = false;
        private bool lastShowOnlyNonZero   = false;
        private bool lastShowOnlyFavorites = false;
        private bool lastSymmetryMode      = false;

        private Vector2 scroll;

        private AnimationClip loadedClip        = null;
        private AnimationClip overwriteTargetClip = null;

        private HashSet<string> collapsedGroups = new HashSet<string>();

        private string statusMessage  = null;
        private int    statusLevel    = 0;
        private double statusSetAt    = 0;
        private double statusAutoClearSec = 0;

        private bool   includeFlagsDirty         = false;
        private double lastIncludeFlagsChangeTime = 0;
        private List<float> snapshotValues        = null;

        // ─── Lifecycle ───────────────────────────────────────────────────────

        private void OnEnable()
        {
            DenEmoLoc.LoadPrefs();
            saveFolder            = DenEmoProjectPrefs.GetString("DenEmo_SaveFolder", saveFolder);
            searchText            = DenEmoProjectPrefs.GetString("DenEmo_SearchText", string.Empty);
            showOnlyIncluded      = DenEmoProjectPrefs.GetBool("DenEmo_ShowOnlyIncluded", false);
            showOnlyNonZero       = DenEmoProjectPrefs.GetBool("DenEmo_ShowOnlyNonZero", false);
            showOnlyFavorites     = DenEmoProjectPrefs.GetBool("DenEmo_ShowOnlyFavorites", false);
            symmetryMode          = DenEmoProjectPrefs.GetBool("DenEmo_SymmetryMode", false);
            autoBackup            = DenEmoProjectPrefs.GetBool("DenEmo_AutoBackup", true);
            overwriteSaveEnabled  = DenEmoProjectPrefs.GetBool("DenEmo_OverwriteSaveEnabled", false);

            var overwriteGuid = DenEmoProjectPrefs.GetString("DenEmo_OverwriteClipGuid", "");
            if (!string.IsNullOrEmpty(overwriteGuid))
            {
                var clipAssetPath = AssetDatabase.GUIDToAssetPath(overwriteGuid);
                if (!string.IsNullOrEmpty(clipAssetPath))
                    overwriteTargetClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipAssetPath);
            }

            var last = DenEmoProjectPrefs.GetString("DenEmo_LastTarget", string.Empty);
            if (!string.IsNullOrEmpty(last))
            {
                var lastObj = EditorUtility.InstanceIDToObject(Convert.ToInt32(last)) as SkinnedMeshRenderer;
                if (lastObj != null)
                {
                    _model.SetTarget(lastObj);
                    RefreshListAndCache();
                }
            }

            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.update += OnEditorUpdate;
            SetStatus(DenEmoLoc.T("status.ready"), 0, 0);
            LoadCollapsedGroupsPrefs();

            // Restore mode
            _currentMode = (EditorMode)DenEmoProjectPrefs.GetInt("DenEmo_Mode", 0);

            // Restore animation clip reference
            var animClipGuid = DenEmoProjectPrefs.GetString("DenEmo_AnimClipGuid", "");
            if (!string.IsNullOrEmpty(animClipGuid))
            {
                var animClipPath = AssetDatabase.GUIDToAssetPath(animClipGuid);
                if (!string.IsNullOrEmpty(animClipPath))
                {
                    var animClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(animClipPath);
                    if (animClip != null) _animModeUI.ClipModel.SetClip(animClip);
                }
            }

            _animModeUI.OnEnable(_model);

            _listUI.OnIncludeFlagsChanged = () =>
            {
                includeFlagsDirty = true;
                lastIncludeFlagsChangeTime = EditorApplication.timeSinceStartup;
            };
            _listUI.OnFavoriteChanged  = OnFavoriteChanged;
            _listUI.OnSnapshotCreate  = () => CreateSnapshot(false);
            _listUI.OnSnapshotRestore = RestoreSnapshot;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.update -= OnEditorUpdate;
            _listUI.StopThrottle();
            _animModeUI.OnDisable();

            if (includeFlagsDirty) SaveIncludeFlagsPrefsImmediate();

            if (_model.TargetSkinnedMesh)
                DenEmoProjectPrefs.SetString("DenEmo_LastTarget", _model.TargetSkinnedMesh.GetInstanceID().ToString());

            DenEmoProjectPrefs.SetString("DenEmo_SaveFolder", saveFolder);
            DenEmoProjectPrefs.SetString("DenEmo_SearchText", searchText);
            DenEmoProjectPrefs.SetBool("DenEmo_ShowOnlyIncluded",      showOnlyIncluded);
            DenEmoProjectPrefs.SetBool("DenEmo_ShowOnlyNonZero",       showOnlyNonZero);
            DenEmoProjectPrefs.SetBool("DenEmo_ShowOnlyFavorites",     showOnlyFavorites);
            DenEmoProjectPrefs.SetBool("DenEmo_SymmetryMode",          symmetryMode);
            DenEmoProjectPrefs.SetBool("DenEmo_AutoBackup",            autoBackup);
            DenEmoProjectPrefs.SetBool("DenEmo_OverwriteSaveEnabled",  overwriteSaveEnabled);
            DenEmoProjectPrefs.SetInt("DenEmo_Mode", (int)_currentMode);

            if (overwriteTargetClip != null)
            {
                var clipAssetPath = AssetDatabase.GetAssetPath(overwriteTargetClip);
                DenEmoProjectPrefs.SetString("DenEmo_OverwriteClipGuid",
                    string.IsNullOrEmpty(clipAssetPath) ? "" : AssetDatabase.AssetPathToGUID(clipAssetPath));
            }
            else
            {
                DenEmoProjectPrefs.SetString("DenEmo_OverwriteClipGuid", "");
            }

            // Save animation clip reference
            if (_animModeUI.ClipModel.Clip != null)
            {
                var animClipPath = AssetDatabase.GetAssetPath(_animModeUI.ClipModel.Clip);
                DenEmoProjectPrefs.SetString("DenEmo_AnimClipGuid",
                    string.IsNullOrEmpty(animClipPath) ? "" : AssetDatabase.AssetPathToGUID(animClipPath));
            }
            else
            {
                DenEmoProjectPrefs.SetString("DenEmo_AnimClipGuid", "");
            }

            if (snapshotValues != null && snapshotValues.Count > 0)
            {
                var parts = new string[snapshotValues.Count];
                for (int i = 0; i < snapshotValues.Count; i++)
                    parts[i] = snapshotValues[i].ToString(System.Globalization.CultureInfo.InvariantCulture);
                DenEmoProjectPrefs.SetString("DenEmo_Snapshot", string.Join(",", parts));
            }

            SaveBlendValuesPrefs();
            SaveCollapsedGroupsPrefs();
        }

        private void OnEditorUpdate()
        {
            if (_currentMode == EditorMode.Animation)
                _animModeUI.OnUpdate(this);
        }

        private void OnUndoRedo()
        {
            _model.SyncValuesFromMesh();
            Repaint();
        }

        // ─── OnGUI ───────────────────────────────────────────────────────────

        private void OnGUI()
        {
            DenEmoTheme.Initialize();
            TickStatusAutoClear();

            // ウィンドウ全体を Surface0 で塗る
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), DenEmoTheme.Surface0);

            DenEmoCommonUI.DrawHeader(this);
            DrawModeTabBar();
            HandleDragAndDrop();

            bool hasTarget = DrawTargetMeshSection();
            if (!hasTarget)
            {
                GUILayout.FlexibleSpace();
                DenEmoCommonUI.DrawStatusBar(statusMessage, statusLevel);
                return;
            }

            if (_currentMode == EditorMode.Pose)
            {
                DrawAnimationSourceSection();
                DrawSearchFilterSection();
                _listUI.DrawList(_model, ref scroll, true, collapsedGroups, symmetryMode, this);
                DrawFooterSection();
            }
            else
            {
                _animModeUI.DrawAnimationClipSection(_model, saveFolder, this);
                _animModeUI.DrawTimeline(_model, this);
                DrawSearchFilterSection();
                var animContext = _animModeUI.ClipModel.Clip != null
                    ? _animModeUI.BuildDrawContext(_model)
                    : null;
                _listUI.DrawList(_model, ref scroll, true, collapsedGroups, symmetryMode, this, animContext);
                DrawAnimationSaveSection();
            }

            DenEmoCommonUI.DrawStatusBar(statusMessage, statusLevel);

            // インクルードフラグの遅延保存
            if (includeFlagsDirty && EditorApplication.timeSinceStartup - lastIncludeFlagsChangeTime > 0.5)
                SaveIncludeFlagsPrefsImmediate();

            // フィルター変更検知
            bool filterChanged = searchText != lastSearchText
                || showOnlyIncluded  != lastShowOnlyIncluded
                || showOnlyNonZero   != lastShowOnlyNonZero
                || showOnlyFavorites != lastShowOnlyFavorites
                || symmetryMode      != lastSymmetryMode;

            if (filterChanged)
            {
                UpdateVisibility();
                lastSearchText       = searchText;
                lastShowOnlyIncluded  = showOnlyIncluded;
                lastShowOnlyNonZero   = showOnlyNonZero;
                lastShowOnlyFavorites = showOnlyFavorites;
                lastSymmetryMode      = symmetryMode;
            }
        }

        // ─── Mode tab bar ─────────────────────────────────────────────────────

        private void DrawModeTabBar()
        {
            EditorGUILayout.BeginHorizontal(DenEmoTheme.ToolbarStyle);
            GUILayout.FlexibleSpace();

            var poseStyle = _currentMode == EditorMode.Pose      ? DenEmoTheme.ChipOnStyle : DenEmoTheme.ChipOffStyle;
            var animStyle = _currentMode == EditorMode.Animation  ? DenEmoTheme.ChipOnStyle : DenEmoTheme.ChipOffStyle;

            if (GUILayout.Button(DenEmoLoc.T("ui.animMode.tab.pose"), poseStyle))
                SwitchMode(EditorMode.Pose);
            GUILayout.Space(2);
            if (GUILayout.Button(DenEmoLoc.T("ui.animMode.tab.anim"), animStyle))
                SwitchMode(EditorMode.Animation);

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(2);
        }

        private void SwitchMode(EditorMode mode)
        {
            if (_currentMode == mode) return;

            if (_currentMode == EditorMode.Animation)
            {
                // Leaving animation mode: stop preview and restore snapshot
                _animModeUI.StopPreview();
                if (snapshotValues != null && snapshotValues.Count > 0)
                    RestoreSnapshot();
            }

            _currentMode = mode;

            if (_currentMode == EditorMode.Animation)
            {
                // Entering animation mode: save current state as snapshot
                CreateSnapshot(false);
                if (_animModeUI.ClipModel.Clip != null && _model.TargetSkinnedMesh != null)
                    _animModeUI.StartPreview(_model);
            }

            Repaint();
        }

        // ─── Sections ────────────────────────────────────────────────────────

        private bool DrawTargetMeshSection()
        {
            DenEmoTheme.BeginSection(DenEmoLoc.EnglishMode ? "TARGET MESH" : "対象メッシュ");

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            var newSmr = EditorGUILayout.ObjectField(
                new GUIContent(DenEmoLoc.T("ui.mesh.label"), DenEmoLoc.T("ui.mesh.tooltip")),
                _model.TargetSkinnedMesh, typeof(SkinnedMeshRenderer), true) as SkinnedMeshRenderer;
            if (EditorGUI.EndChangeCheck())
            {
                _listUI.StopThrottle();
                _model.SetTarget(newSmr);
                RefreshListAndCache();
                if (_model.TargetSkinnedMesh != null)
                {
                    CreateSnapshot(false);
                    SetStatus(DenEmoLoc.T("status.ready"), 0, 0);
                }
                if (_currentMode == EditorMode.Animation)
                    _animModeUI.OnTargetChanged(_model);
                Repaint();
            }

            if (GUILayout.Button(DenEmoLoc.T("ui.footer.refresh"), DenEmoTheme.MiniButtonStyle, GUILayout.Width(60)))
            {
                RefreshListAndCache();
            }
            EditorGUILayout.EndHorizontal();

            if (_model.TargetSkinnedMesh == null)
            {
                GUILayout.Space(4);
                GUILayout.Label(DenEmoLoc.T("ui.mesh.missing"), DenEmoTheme.CaptionStyle);
                DenEmoTheme.EndSection();
                return false;
            }

            if (!_model.TargetSkinnedMesh.gameObject.activeInHierarchy || !_model.TargetSkinnedMesh.enabled)
            {
                GUILayout.Space(2);
                var warnStyle = new GUIStyle(DenEmoTheme.CaptionStyle);
                warnStyle.normal.textColor = DenEmoTheme.SemanticWarning;
                GUILayout.Label("⚠ " + DenEmoLoc.T("ui.mesh.inactive.warn"), warnStyle);
            }

            if (_model.Items.Count == 0)
            {
                GUILayout.Space(4);
                GUILayout.Label(DenEmoLoc.T("ui.mesh.noShapes"), DenEmoTheme.CaptionStyle);
                DenEmoTheme.EndSection();
                return false;
            }

            DenEmoTheme.EndSection();
            return true;
        }

        private void DrawAnimationSourceSection()
        {
            DenEmoTheme.BeginSection(DenEmoLoc.EnglishMode ? "ANIMATION SOURCE" : "アニメーション参照");

            loadedClip = EditorGUILayout.ObjectField(
                new GUIContent(DenEmoLoc.T("ui.animSource.clip.label"), DenEmoLoc.T("ui.animSource.clip.tip")),
                loadedClip, typeof(AnimationClip), false) as AnimationClip;

            GUILayout.Space(4);

            using (new EditorGUI.DisabledGroupScope(loadedClip == null))
            {
                if (GUILayout.Button(
                    new GUIContent(DenEmoLoc.T("ui.animSource.loadAnim.button"), DenEmoLoc.T("ui.applyAnim.tip")),
                    DenEmoTheme.ActionButtonStyle))
                {
                    SetStatus(DenEmoLoc.T("status.applying"), 0, 0);
                    string res = AnimationExporter.ApplyAnimationToMesh(loadedClip, _model);
                    if (res == "SUCCESS") { SaveBlendValuesPrefs(); SetStatus(DenEmoLoc.T("dlg.apply.done.msg"), 1); }
                    else SetStatus(res, 2);
                }

                GUILayout.Space(2);

                if (GUILayout.Button(
                    new GUIContent(DenEmoLoc.T("ui.animSource.alignKeys.button"), DenEmoLoc.T("ui.align.apply.tip")),
                    DenEmoTheme.ActionButtonStyle))
                    AlignToBaseClip();
            }

            DenEmoTheme.EndSection();
        }

        private void DrawSearchFilterSection()
        {
            DenEmoTheme.BeginSection(DenEmoLoc.EnglishMode ? "SEARCH & FILTER" : "検索・絞り込み");

            // 検索バー
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("🔍", GUILayout.Width(18));
            GUI.SetNextControlName("SearchField");
            searchText = EditorGUILayout.TextField(searchText, GUILayout.ExpandWidth(true));
            if (!string.IsNullOrEmpty(searchText))
            {
                if (GUILayout.Button("✕", DenEmoTheme.MiniButtonStyle, GUILayout.Width(20)))
                {
                    searchText = string.Empty;
                    DenEmoProjectPrefs.SetString("DenEmo_SearchText", searchText);
                    GUI.FocusControl(null);
                    Repaint();
                }
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(6);

            // フィルターチップ行
            EditorGUILayout.BeginHorizontal();

            showOnlyFavorites = DrawFilterChip(
                showOnlyFavorites,
                DenEmoLoc.EnglishMode ? "★ Fav" : "★ お気に入り",
                "DenEmo_ShowOnlyFavorites");

            GUILayout.Space(4);
            showOnlyIncluded = DrawFilterChip(
                showOnlyIncluded,
                DenEmoLoc.EnglishMode ? "✓ Enabled" : "✓ 有効のみ",
                "DenEmo_ShowOnlyIncluded");

            GUILayout.Space(4);
            showOnlyNonZero = DrawFilterChip(
                showOnlyNonZero,
                DenEmoLoc.EnglishMode ? "≠0 Non-zero" : "≠0 非ゼロ",
                "DenEmo_ShowOnlyNonZero");

            GUILayout.Space(4);
            symmetryMode = DrawFilterChip(
                symmetryMode,
                DenEmoLoc.EnglishMode ? "↔ Symmetry" : "↔ 左右同期",
                "DenEmo_SymmetryMode");

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            DenEmoTheme.EndSection();
        }

        private bool DrawFilterChip(bool current, string label, string prefsKey)
        {
            var style = current ? DenEmoTheme.ChipOnStyle : DenEmoTheme.ChipOffStyle;
            if (GUILayout.Button(label, style, GUILayout.ExpandWidth(false)))
            {
                current = !current;
                DenEmoProjectPrefs.SetBool(prefsKey, current);
                Repaint();
            }
            return current;
        }

        private void DrawFooterSection()
        {
            DenEmoTheme.BeginSection(DenEmoLoc.EnglishMode ? "SAVE SETTINGS" : "保存設定");

            // 保存先フォルダ
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(DenEmoLoc.T("ui.footer.saveTo"), DenEmoTheme.CaptionStyle, GUILayout.Width(90));
            saveFolder = EditorGUILayout.TextField(saveFolder, GUILayout.ExpandWidth(true));
            if (GUILayout.Button(DenEmoLoc.T("ui.footer.browse"), DenEmoTheme.MiniButtonStyle, GUILayout.Width(46)))
            {
                var newPath = EditorUtility.OpenFolderPanel("フォルダを選択", Application.dataPath, "");
                if (!string.IsNullOrEmpty(newPath))
                    saveFolder = newPath.StartsWith(Application.dataPath)
                        ? "Assets" + newPath.Substring(Application.dataPath.Length) : newPath;
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(2);

            overwriteSaveEnabled = EditorGUILayout.ToggleLeft(
                new GUIContent(
                    DenEmoLoc.T("ui.footer.overwriteEnable"),
                    DenEmoLoc.T("ui.footer.overwriteEnable.tip")),
                overwriteSaveEnabled, DenEmoTheme.CaptionStyle);

            if (overwriteSaveEnabled)
            {
                GUILayout.Space(2);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(DenEmoLoc.T("ui.footer.overwriteTarget"), DenEmoTheme.CaptionStyle, GUILayout.Width(52));
                overwriteTargetClip = EditorGUILayout.ObjectField(GUIContent.none, overwriteTargetClip, typeof(AnimationClip), false) as AnimationClip;
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(2);
                autoBackup = EditorGUILayout.ToggleLeft(
                    new GUIContent(
                        DenEmoLoc.EnglishMode ? "Auto backup on overwrite" : "上書き時に自動バックアップ",
                        DenEmoLoc.EnglishMode ? "Copies the existing .anim file to _backups/ before overwriting." : "上書き保存前に既存ファイルを _backups/ フォルダに複製します。"),
                    autoBackup, DenEmoTheme.CaptionStyle);
            }

            DenEmoTheme.DrawSeparator(0);
            GUILayout.Space(6);

            GUILayout.BeginHorizontal();
            GUILayout.Space(6);

            if (GUILayout.Button(DenEmoLoc.T("ui.footer.saveAnim"), DenEmoTheme.ActionButtonStyle, GUILayout.ExpandWidth(true)))
            {
                if (overwriteSaveEnabled && overwriteTargetClip != null)
                {
                    string clipPath = AssetDatabase.GetAssetPath(overwriteTargetClip);
                    SetStatus(DenEmoLoc.T("status.saving"), 0, 0);
                    var err = AnimationExporter.SaveAnimationClipToPath(_model, clipPath, out string path, autoBackup);
                    if (err != null) SetStatus(err, 3);
                    else SetStatus(DenEmoLoc.Tf("dlg.save.done.msg", path), 1);
                }
                else
                {
                    SetStatus(DenEmoLoc.T("status.saving"), 0, 0);
                    var err = AnimationExporter.SaveAnimationClip(_model, saveFolder, out string path, autoBackup);
                    if (err != null) SetStatus(err, 3);
                    else SetStatus(DenEmoLoc.Tf("dlg.save.done.msg", path), 1);
                }
            }

            GUILayout.Space(6);
            GUILayout.EndHorizontal();
            GUILayout.Space(6);

            DenEmoTheme.EndSection();
        }

        // ─── Helpers ─────────────────────────────────────────────────────────

        private void RefreshListAndCache()
        {
            _model.RefreshList(searchText, showOnlyIncluded);
            LipSyncExclusionRule.ApplyExclusion(_model.TargetSkinnedMesh, _model.Items);
            LoadFavoritesPrefs();
            LoadIncludeFlagsPrefs();
            _model.BuildGroups();
            CreateSnapshot(true);
            UpdateVisibility();
        }

        private void UpdateVisibility()
        {
            var tokens = ShapeKeyModel.BuildSearchTokens(searchText);
            _model.UpdateVisibility(tokens, showOnlyIncluded, showOnlyNonZero, showOnlyFavorites);
        }

        private void AlignToBaseClip()
        {
            var pairs = new HashSet<string>();
            string currentSmrPath = "";
            if (_model.TargetSkinnedMesh != null)
            {
                var parts = new List<string>();
                var t = _model.TargetSkinnedMesh.transform;
                var root = t.root;
                while (t != null && t != root) { parts.Add(t.name); t = t.parent; }
                parts.Reverse();
                currentSmrPath = string.Join("/", parts.ToArray());
            }

            foreach (var b in AnimationUtility.GetCurveBindings(loadedClip))
            {
                if (b.type != typeof(SkinnedMeshRenderer)) continue;
                if (!b.propertyName.StartsWith("blendShape.")) continue;
                var shape = b.propertyName.Substring("blendShape.".Length);
                if (string.IsNullOrEmpty(shape)) continue;
                if (string.Equals(b.path, currentSmrPath, StringComparison.Ordinal))
                    pairs.Add(currentSmrPath + "\n" + shape);
            }

            foreach (var item in _model.Items)
            {
                if (item.IsVrcShape) { item.IsIncluded = false; continue; }
                item.IsIncluded = pairs.Contains(currentSmrPath + "\n" + item.Name);
            }
            UpdateVisibility();
            SaveIncludeFlagsPrefsImmediate();
            SetStatus(DenEmoLoc.T("status.alignedSavedTargets"), 1);
        }

        private void HandleDragAndDrop()
        {
            Event evt = Event.current;
            if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform) return;

            Rect dropArea = new Rect(0, 0, position.width, position.height);
            if (!dropArea.Contains(evt.mousePosition)) return;

            SkinnedMeshRenderer foundSmr = null;
            foreach (var obj in DragAndDrop.objectReferences)
            {
                if (obj is GameObject go) foundSmr = go.GetComponent<SkinnedMeshRenderer>();
                else if (obj is SkinnedMeshRenderer smr) foundSmr = smr;
                if (foundSmr != null) break;
            }
            if (foundSmr == null) return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                _listUI.StopThrottle();
                _model.SetTarget(foundSmr);
                RefreshListAndCache();
                if (_model.TargetSkinnedMesh != null)
                {
                    CreateSnapshot(false);
                    SetStatus(DenEmoLoc.T("status.ready"), 0, 0);
                }
                if (_currentMode == EditorMode.Animation)
                    _animModeUI.OnTargetChanged(_model);
                Repaint();
            }
            evt.Use();
        }

        private void SetStatus(string msg, int level, double autoClearSec = 3.0)
        {
            if (level != 0 && autoClearSec == 3.0) autoClearSec = 6.0;
            statusMessage     = msg;
            statusLevel       = level;
            statusSetAt       = EditorApplication.timeSinceStartup;
            statusAutoClearSec = autoClearSec;
            Repaint();
        }

        private void TickStatusAutoClear()
        {
            if (statusAutoClearSec <= 0) return;
            if (!string.IsNullOrEmpty(statusMessage) &&
                EditorApplication.timeSinceStartup - statusSetAt > statusAutoClearSec)
            {
                statusMessage     = null;
                statusLevel       = 0;
                statusAutoClearSec = 0;
                Repaint();
            }
        }

        // ─── Favorites ───────────────────────────────────────────────────────

        private string GetFavoritesKey()
        {
            if (_model.TargetSkinnedMesh == null || _model.TargetSkinnedMesh.sharedMesh == null) return null;
            string meshPath = AssetDatabase.GetAssetPath(_model.TargetSkinnedMesh.sharedMesh);
            string guid = string.IsNullOrEmpty(meshPath) ? _model.TargetSkinnedMesh.sharedMesh.name : AssetDatabase.AssetPathToGUID(meshPath);
            return "DenEmo_Fav|" + guid;
        }

        private void LoadFavoritesPrefs()
        {
            var key = GetFavoritesKey();
            if (string.IsNullOrEmpty(key)) return;
            if (!EditorPrefs.HasKey(key)) return;
            var s = EditorPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(s)) return;
            var names = new HashSet<string>(s.Split(','));
            foreach (var item in _model.Items)
                item.IsFavorite = names.Contains(item.Name);
        }

        private void SaveFavoritesPrefs()
        {
            var key = GetFavoritesKey();
            if (string.IsNullOrEmpty(key)) return;
            var favorites = new List<string>();
            foreach (var item in _model.Items)
                if (item.IsFavorite) favorites.Add(item.Name);
            EditorPrefs.SetString(key, string.Join(",", favorites));
        }

        private void OnFavoriteChanged(string shapeName, bool isFavorite)
        {
            SaveFavoritesPrefs();
            if (showOnlyFavorites) UpdateVisibility();
            Repaint();
        }

        // ─── Preferences & Snapshots ─────────────────────────────────────────

        private string GetBlendPrefsKey()
        {
            if (_model.TargetSkinnedMesh == null || _model.TargetSkinnedMesh.sharedMesh == null) return null;
            string scene    = _model.TargetObject ? _model.TargetObject.scene.name : "";
            string meshName = _model.TargetSkinnedMesh.sharedMesh.name;
            return $"DenEmo_Values|{scene}|{meshName}";
        }

        private void SaveBlendValuesPrefs()
        {
            var key = GetBlendPrefsKey();
            if (string.IsNullOrEmpty(key)) return;
            var parts = new string[_model.Items.Count];
            for (int i = 0; i < _model.Items.Count; i++)
                parts[i] = _model.Items[i].Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            EditorPrefs.SetString(key, string.Join(",", parts));
        }

        private void LoadIncludeFlagsPrefs()
        {
            var key = GetBlendPrefsKey();
            if (string.IsNullOrEmpty(key)) return;
            key += "|IncludeFlags";
            if (!EditorPrefs.HasKey(key)) return;
            var s = EditorPrefs.GetString(key);
            if (string.IsNullOrEmpty(s)) return;
            var parts = s.Split(',');
            for (int i = 0; i < parts.Length && i < _model.Items.Count; i++)
                _model.Items[i].IsIncluded = parts[i] == "1" || parts[i].Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        private void SaveIncludeFlagsPrefsImmediate()
        {
            var key = GetBlendPrefsKey();
            if (string.IsNullOrEmpty(key)) return;
            key += "|IncludeFlags";
            var parts = new string[_model.Items.Count];
            for (int i = 0; i < _model.Items.Count; i++)
                parts[i] = _model.Items[i].IsIncluded ? "1" : "0";
            EditorPrefs.SetString(key, string.Join(",", parts));
            includeFlagsDirty = false;
        }

        private void LoadCollapsedGroupsPrefs()
        {
            collapsedGroups.Clear();
            var s = DenEmoProjectPrefs.GetString("DenEmo_GroupsCollapsed", "");
            if (string.IsNullOrEmpty(s)) return;
            foreach (var p in s.Split(new char[] { '\n', '\r', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var k = p.Trim();
                if (k.Length > 0) collapsedGroups.Add(k);
            }
        }

        private void SaveCollapsedGroupsPrefs()
        {
            DenEmoProjectPrefs.SetString("DenEmo_GroupsCollapsed",
                collapsedGroups.Count == 0 ? "" : string.Join(",", collapsedGroups));
        }

        private void CreateSnapshot(bool loadTime)
        {
            if (_model.Items.Count == 0) return;
            snapshotValues = new List<float>();
            foreach (var i in _model.Items) snapshotValues.Add(i.Value);
            if (!loadTime)
            {
                var parts = new string[snapshotValues.Count];
                for (int i = 0; i < snapshotValues.Count; i++)
                    parts[i] = snapshotValues[i].ToString(System.Globalization.CultureInfo.InvariantCulture);
                DenEmoProjectPrefs.SetString("DenEmo_Snapshot", string.Join(",", parts));
            }
        }

        private void RestoreSnapshot()
        {
            if (snapshotValues == null || snapshotValues.Count == 0)
            {
                var s = DenEmoProjectPrefs.GetString("DenEmo_Snapshot");
                if (!string.IsNullOrEmpty(s))
                {
                    var parts = s.Split(',');
                    snapshotValues = new List<float>();
                    foreach (var p in parts)
                    {
                        snapshotValues.Add(float.TryParse(p, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : 0f);
                    }
                }
            }
            if (snapshotValues == null) return;
            int n = Math.Min(snapshotValues.Count, _model.Items.Count);
            for (int i = 0; i < n; i++)
            {
                _model.Items[i].Value = snapshotValues[i];
                if (_model.TargetSkinnedMesh)
                    _model.TargetSkinnedMesh.SetBlendShapeWeight(i, snapshotValues[i]);
            }
            SaveBlendValuesPrefs();
        }
    }
}
