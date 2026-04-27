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
        private const float VertexGuideHandleSizeMultiplier = 0.015f;
        private static readonly Color VertexGuideColor = new Color(0.24f, 0.72f, 1.0f, 0.95f);

        // ─── Pose mode state ──────────────────────────────────────────────────
        [MenuItem("dennokoworks/DenEmo")]
        public static void ShowWindow()
        {
            var w = GetWindow<DenEmoWindow>("DenEmo");
            w.minSize = new Vector2(380, 400);
        }

        private ShapeKeyModel   _model      = new ShapeKeyModel();
        private ShapeKeyListUI  _listUI     = new ShapeKeyListUI();
        private AnimationModeUI _animModeUI = new AnimationModeUI();

        private string saveFolder  = "Assets/Generated_Animations";
        private string searchText  = string.Empty;
        private string lastSearchText = null;

        private bool showOnlyIncluded   = false;
        private bool showOnlyNonZero    = false;
        private bool showOnlyFavorites  = false;
        private bool symmetryMode       = false;
        private bool autoBackup         = true;
        private bool overwriteSaveEnabled = false;
        private bool vertexPickMode       = false;
        private bool vertexFilterActive   = false;
        private int  selectedVertexIndex  = -1;
        private HashSet<int> vertexMovedShapeIndices = null;
        private Vector3[] vertexGuideWorldPositions = null;
        private Vector3[] vertexGuideWorldNormals = null;
        private int vertexGuideMeshInstanceId = 0;
        private Matrix4x4 vertexGuideLocalToWorld = Matrix4x4.zero;

        private bool lastShowOnlyIncluded  = false;
        private bool lastShowOnlyNonZero   = false;
        private bool lastShowOnlyFavorites = false;
        private bool lastSymmetryMode      = false;

        // 追加ターゲットメッシュ（プライマリ以外にユーザーが明示指定したもの）
        private List<SkinnedMeshRenderer> _additionalTargets = new List<SkinnedMeshRenderer>();

        // -1 = All targets, 0+ = GetAllTargetMeshes()[index]
        private int _meshFilterIndex = -1;

        private Vector2 scroll;

        private AnimationClip loadedClip         = null;
        private AnimationClip overwriteTargetClip = null;

        private HashSet<string> collapsedGroups = new HashSet<string>();

        private string statusMessage      = null;
        private int    statusLevel        = 0;
        private double statusSetAt        = 0;
        private double statusAutoClearSec = 0;

        private bool   includeFlagsDirty          = false;
        private double lastIncludeFlagsChangeTime  = 0;
        private List<float> snapshotValues         = null;

        // ─── Lifecycle ───────────────────────────────────────────────────────

        private void OnEnable()
        {
            DenEmoLoc.LoadPrefs();
            saveFolder           = DenEmoProjectPrefs.GetString("DenEmo_SaveFolder", saveFolder);
            searchText           = DenEmoProjectPrefs.GetString("DenEmo_SearchText", string.Empty);
            showOnlyIncluded     = DenEmoProjectPrefs.GetBool("DenEmo_ShowOnlyIncluded",  false);
            showOnlyNonZero      = DenEmoProjectPrefs.GetBool("DenEmo_ShowOnlyNonZero",   false);
            showOnlyFavorites    = DenEmoProjectPrefs.GetBool("DenEmo_ShowOnlyFavorites", false);
            symmetryMode         = DenEmoProjectPrefs.GetBool("DenEmo_SymmetryMode",      false);
            autoBackup           = DenEmoProjectPrefs.GetBool("DenEmo_AutoBackup",        true);
            overwriteSaveEnabled = DenEmoProjectPrefs.GetBool("DenEmo_OverwriteSaveEnabled", false);
            _meshFilterIndex     = DenEmoProjectPrefs.GetInt("DenEmo_MeshFilter", -1);

            // 追加ターゲットを復元
            _additionalTargets.Clear();
            var addIds = DenEmoProjectPrefs.GetString("DenEmo_AdditionalTargets", "");
            if (!string.IsNullOrEmpty(addIds))
            {
                foreach (var part in addIds.Split(','))
                {
                    if (string.IsNullOrEmpty(part)) continue;
                    if (int.TryParse(part, out int id))
                    {
                        var smr = EditorUtility.InstanceIDToObject(id) as SkinnedMeshRenderer;
                        if (smr != null) _additionalTargets.Add(smr);
                    }
                }
            }

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
            SceneView.duringSceneGui += OnSceneGUI;
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
            SceneView.duringSceneGui -= OnSceneGUI;
            _listUI.StopThrottle();
            _animModeUI.OnDisable();
            vertexPickMode = false;
            ClearVertexGuideCache();

            if (includeFlagsDirty) SaveIncludeFlagsPrefsImmediate();

            if (_model.TargetSkinnedMesh)
                DenEmoProjectPrefs.SetString("DenEmo_LastTarget", _model.TargetSkinnedMesh.GetInstanceID().ToString());

            // 追加ターゲットを保存
            var ids = new List<string>();
            foreach (var smr in _additionalTargets)
                if (smr != null) ids.Add(smr.GetInstanceID().ToString());
            DenEmoProjectPrefs.SetString("DenEmo_AdditionalTargets", string.Join(",", ids));

            DenEmoProjectPrefs.SetString("DenEmo_SaveFolder",        saveFolder);
            DenEmoProjectPrefs.SetString("DenEmo_SearchText",        searchText);
            DenEmoProjectPrefs.SetBool("DenEmo_ShowOnlyIncluded",    showOnlyIncluded);
            DenEmoProjectPrefs.SetBool("DenEmo_ShowOnlyNonZero",     showOnlyNonZero);
            DenEmoProjectPrefs.SetBool("DenEmo_ShowOnlyFavorites",   showOnlyFavorites);
            DenEmoProjectPrefs.SetBool("DenEmo_SymmetryMode",        symmetryMode);
            DenEmoProjectPrefs.SetBool("DenEmo_AutoBackup",          autoBackup);
            DenEmoProjectPrefs.SetBool("DenEmo_OverwriteSaveEnabled",overwriteSaveEnabled);
            DenEmoProjectPrefs.SetInt("DenEmo_MeshFilter",           _meshFilterIndex);
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

            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), DenEmoTheme.Surface0);

            DenEmoCommonUI.DrawHeader(this);
            DrawModeTabBar();
            HandleDragAndDrop();

            bool hasTarget = DrawTargetMeshSection();
            if (!hasTarget)
            {
                GUILayout.FlexibleSpace();
                DenEmoCommonUI.DrawStatusBar(statusMessage, statusLevel);
                HandleDragAndDrop();
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
            
            HandleDragAndDrop();

            // インクルードフラグの遅延保存
            if (includeFlagsDirty && EditorApplication.timeSinceStartup - lastIncludeFlagsChangeTime > 0.5)
                SaveIncludeFlagsPrefsImmediate();

            // フィルター変更検知
            bool filterChanged = searchText       != lastSearchText
                || showOnlyIncluded  != lastShowOnlyIncluded
                || showOnlyNonZero   != lastShowOnlyNonZero
                || showOnlyFavorites != lastShowOnlyFavorites
                || symmetryMode      != lastSymmetryMode;

            if (filterChanged)
            {
                UpdateVisibility();
                lastSearchText        = searchText;
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

            // プライマリメッシュ
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            var newSmr = EditorGUILayout.ObjectField(
                new GUIContent(DenEmoLoc.T("ui.mesh.label"), DenEmoLoc.T("ui.mesh.tooltip")),
                _model.TargetSkinnedMesh, typeof(SkinnedMeshRenderer), true) as SkinnedMeshRenderer;
            if (EditorGUI.EndChangeCheck())
            {
                _listUI.StopThrottle();
                _model.SetTarget(newSmr);
                vertexPickMode = false;
                vertexFilterActive = false;
                selectedVertexIndex = -1;
                vertexMovedShapeIndices = null;
                ClearVertexGuideCache();
                ClampMeshFilterIndex();
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
            if (GUILayout.Button("✕", DenEmoTheme.MiniButtonStyle, GUILayout.Width(20)))
            {
                _listUI.StopThrottle();
                _model.SetTarget(null);
                ClampMeshFilterIndex();
                RefreshListAndCache();
                Repaint();
            }
            EditorGUILayout.EndHorizontal();

            if (_model.TargetSkinnedMesh == null)
            {
                GUILayout.Space(4);
                GUILayout.Label(DenEmoLoc.T("ui.mesh.missing"), DenEmoTheme.CaptionStyle);
                // 追加メッシュ行は表示したまま（プライマリがなくても追加はできる）
                DrawAdditionalTargets();
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

            // 追加ターゲットメッシュ
            DrawAdditionalTargets();

            // どのターゲットにもシェイプキーがなければ終了
            bool hasShapes = false;
            foreach (var smr in GetAllTargetMeshes())
                if (smr != null && smr.sharedMesh != null && smr.sharedMesh.blendShapeCount > 0)
                { hasShapes = true; break; }

            if (!hasShapes)
            {
                GUILayout.Space(4);
                GUILayout.Label(DenEmoLoc.T("ui.mesh.noShapes"), DenEmoTheme.CaptionStyle);
                DenEmoTheme.EndSection();
                return false;
            }

            DenEmoTheme.EndSection();
            return true;
        }

        private void DrawAdditionalTargets()
        {
            GUILayout.Space(2);

            bool changed = false;

            // 既存の要素のクリーンアップ（null要素があれば削除）
            if (_additionalTargets.RemoveAll(item => item == null) > 0)
            {
                changed = true;
            }

            for (int i = 0; i < _additionalTargets.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                var addSmr = EditorGUILayout.ObjectField(
                    new GUIContent("  +"),
                    _additionalTargets[i], typeof(SkinnedMeshRenderer), true) as SkinnedMeshRenderer;
                if (EditorGUI.EndChangeCheck())
                {
                    if (addSmr == null)
                    {
                        _additionalTargets.RemoveAt(i);
                        changed = true;
                        EditorGUILayout.EndHorizontal();
                        break;
                    }
                    else
                    {
                        _additionalTargets[i] = addSmr;
                        changed = true;
                    }
                }
                if (GUILayout.Button("✕", DenEmoTheme.MiniButtonStyle, GUILayout.Width(20)))
                {
                    _additionalTargets.RemoveAt(i);
                    changed = true;
                    EditorGUILayout.EndHorizontal();
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }

            // 新規追加用の空スロットを常に表示
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            var newSmr = EditorGUILayout.ObjectField(
                new GUIContent("  +"),
                null, typeof(SkinnedMeshRenderer), true) as SkinnedMeshRenderer;
            if (EditorGUI.EndChangeCheck())
            {
                if (newSmr != null)
                {
                    _additionalTargets.Add(newSmr);
                    changed = true;
                }
            }
            
            // 幅を揃えるためのダミー✕ボタン
            using (new EditorGUI.DisabledGroupScope(true))
            {
                GUILayout.Button("✕", DenEmoTheme.MiniButtonStyle, GUILayout.Width(20));
            }
            EditorGUILayout.EndHorizontal();

            // 更新ボタン行
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(DenEmoLoc.T("ui.footer.refresh"), DenEmoTheme.MiniButtonStyle, GUILayout.Width(60)))
            {
                RefreshListAndCache();
            }
            EditorGUILayout.EndHorizontal();

            if (changed)
            {
                ClampMeshFilterIndex();
                RefreshListAndCache();
                Repaint();
            }
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
            GUILayout.Label(DenEmoLoc.EnglishMode ? "🔍 Keyword" : "🔍 キーワード", GUILayout.Width(80));
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

            GUILayout.Space(4);
            if (vertexPickMode)
            {
                if (GUILayout.Button(DenEmoLoc.T("ui.filter.vertex.cancel"), DenEmoTheme.ChipOnStyle, GUILayout.ExpandWidth(false)))
                {
                    vertexPickMode = false;
                    ClearVertexGuideCache();
                    SceneView.RepaintAll();
                    Repaint();
                }
            }
            else
            {
                string vertexFilterLabel = vertexFilterActive
                    ? DenEmoLoc.Tf("ui.filter.vertex.active", selectedVertexIndex)
                    : DenEmoLoc.T("ui.filter.vertex");
                var vertexStyle = vertexFilterActive ? DenEmoTheme.ChipOnStyle : DenEmoTheme.ChipOffStyle;
                if (GUILayout.Button(vertexFilterLabel, vertexStyle, GUILayout.ExpandWidth(false)))
                {
                    vertexPickMode = true;
                    ClearVertexGuideCache();
                    SceneView.RepaintAll();
                    Repaint();
                }

                if (vertexFilterActive)
                {
                    GUILayout.Space(2);
                    if (GUILayout.Button("✕", DenEmoTheme.MiniButtonStyle, GUILayout.Width(20)))
                        ClearVertexFilter();
                }
            }

            if (_currentMode == EditorMode.Animation && _animModeUI.ClipModel.Clip != null)
            {
                GUILayout.Space(4);
                bool trackFilter = _animModeUI.TrackFilterEnabled;
                var trackStyle = trackFilter ? DenEmoTheme.ChipOnStyle : DenEmoTheme.ChipOffStyle;
                var trackLabel = DenEmoLoc.EnglishMode ? "◆ Keyed Only" : "◆ キー有りのみ";
                var trackTip = DenEmoLoc.EnglishMode
                    ? "Show only shape keys that have tracks/keyframes in the current clip"
                    : "現在のクリップでトラック（キーフレーム）があるシェイプキーのみ表示";
                if (GUILayout.Button(new GUIContent(trackLabel, trackTip), trackStyle, GUILayout.ExpandWidth(false)))
                {
                    _animModeUI.TrackFilterEnabled = !trackFilter;
                    Repaint();
                }
            }

            var allTargets = GetAllTargetMeshes();
            if (allTargets.Count > 1)
            {
                GUILayout.Space(6);
                GUILayout.Label("Mesh:", DenEmoTheme.CaptionStyle, GUILayout.Width(38));

                var meshOpts      = BuildMeshFilterOptions(allTargets);
                int displayIdx    = (_meshFilterIndex < 0 || _meshFilterIndex >= allTargets.Count) ? 0 : _meshFilterIndex + 1;
                int newDisplayIdx = EditorGUILayout.Popup(displayIdx, meshOpts, GUILayout.MinWidth(70), GUILayout.ExpandWidth(false));
                int newFilterIdx  = newDisplayIdx <= 0 ? -1 : newDisplayIdx - 1;

                if (newFilterIdx != _meshFilterIndex)
                {
                    _meshFilterIndex = newFilterIdx;
                    DenEmoProjectPrefs.SetInt("DenEmo_MeshFilter", _meshFilterIndex);
                    RefreshListAndCache();
                    Repaint();
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            DenEmoTheme.EndSection();
        }

        private string[] BuildMeshFilterOptions(List<SkinnedMeshRenderer> targets)
        {
            var opts = new string[targets.Count + 1];
            opts[0] = "All";
            for (int i = 0; i < targets.Count; i++)
                opts[i + 1] = targets[i] != null ? targets[i].name : "?";
            return opts;
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

        private void DrawAnimationSaveSection()
        {
            DenEmoTheme.BeginSection(DenEmoLoc.EnglishMode ? "SAVE ANIMATION" : "アニメーション保存");

            GUILayout.BeginHorizontal();
            GUILayout.Space(6);

            if (GUILayout.Button(
                DenEmoLoc.EnglishMode ? "Save Animation" : "アニメーションを保存",
                DenEmoTheme.ActionButtonStyle, GUILayout.ExpandWidth(true)))
            {
                _animModeUI.SaveClip(saveFolder, _model, (msg, lvl) => SetStatus(msg, lvl));
            }

            GUILayout.Space(6);
            GUILayout.EndHorizontal();
            GUILayout.Space(6);

            DenEmoTheme.EndSection();
        }

        // ─── Helpers ─────────────────────────────────────────────────────────

        // 明示的に指定されたターゲットメッシュの一覧（プライマリ + 追加）
        private List<SkinnedMeshRenderer> GetAllTargetMeshes()
        {
            var result = new List<SkinnedMeshRenderer>();
            var seen   = new HashSet<SkinnedMeshRenderer>();

            if (_model.TargetSkinnedMesh != null && seen.Add(_model.TargetSkinnedMesh))
                result.Add(_model.TargetSkinnedMesh);

            foreach (var smr in _additionalTargets)
                if (smr != null && seen.Add(smr))
                    result.Add(smr);

            return result;
        }

        // フィルターに従ってアクティブなメッシュを返す
        private List<SkinnedMeshRenderer> GetActiveMeshes()
        {
            var all = GetAllTargetMeshes();
            if (_meshFilterIndex < 0 || _meshFilterIndex >= all.Count)
                return all;
            return new List<SkinnedMeshRenderer> { all[_meshFilterIndex] };
        }

        private void ClampMeshFilterIndex()
        {
            var all = GetAllTargetMeshes();
            if (_meshFilterIndex >= all.Count) _meshFilterIndex = -1;
        }

        private void RefreshListAndCache()
        {
            ClampMeshFilterIndex();
            var activeMeshes = GetActiveMeshes();
            _model.SetActiveMeshes(activeMeshes);
            _model.RefreshList(searchText, showOnlyIncluded);
            LipSyncExclusionRule.ApplyExclusion(_model.TargetSkinnedMesh, _model.Items);
            LoadFavoritesPrefs();
            LoadIncludeFlagsPrefs();
            _model.BuildGroups();
            if (vertexFilterActive && selectedVertexIndex >= 0)
                vertexMovedShapeIndices = _model.CollectShapeIndicesMovingVertex(selectedVertexIndex);
            CreateSnapshot(true);
            UpdateVisibility();
        }

        private void UpdateVisibility()
        {
            var tokens = ShapeKeyModel.BuildSearchTokens(searchText);
            _model.UpdateVisibility(
                tokens,
                showOnlyIncluded,
                showOnlyNonZero,
                showOnlyFavorites,
                vertexFilterActive ? vertexMovedShapeIndices : null,
                symmetryMode);
        }

        private void AlignToBaseClip()
        {
            var pairs = new HashSet<string>();
            foreach (var b in AnimationUtility.GetCurveBindings(loadedClip))
            {
                if (b.type != typeof(SkinnedMeshRenderer)) continue;
                if (!b.propertyName.StartsWith("blendShape.")) continue;
                var shape = b.propertyName.Substring("blendShape.".Length);
                if (!string.IsNullOrEmpty(shape))
                    pairs.Add(b.path + "\n" + shape);
            }

            foreach (var item in _model.Items)
            {
                if (item.IsVrcShape) { item.IsIncluded = false; continue; }
                string path = item.SmrPath ?? "";
                item.IsIncluded = pairs.Contains(path + "\n" + item.Name);
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
                vertexPickMode = false;
                vertexFilterActive = false;
                selectedVertexIndex = -1;
                vertexMovedShapeIndices = null;
                ClearVertexGuideCache();
                ClampMeshFilterIndex();
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

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!vertexPickMode) return;
            if (_model.TargetSkinnedMesh == null || _model.TargetSkinnedMesh.sharedMesh == null) return;
            DenEmoTheme.Initialize();

            UpdateVertexGuideCache();
            if (vertexGuideWorldPositions == null || vertexGuideWorldPositions.Length == 0) return;

            Handles.BeginGUI();
            GUI.Label(
                new Rect(16, 16, 520, 22),
                DenEmoLoc.T("ui.filter.vertex.guide"),
                DenEmoTheme.SecondaryTextStyle);
            Handles.EndGUI();

            var prevColor = Handles.color;
            var prevZTest = Handles.zTest;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
            
            int pickedIndex = -1;
            Vector3 camPos = sceneView.camera.transform.position;
            bool isOrtho = sceneView.camera.orthographic;
            Vector3 orthoDir = -sceneView.camera.transform.forward;

            for (int i = 0; i < vertexGuideWorldPositions.Length; i++)
            {
                Vector3 world = vertexGuideWorldPositions[i];
                Vector3 viewDir = isOrtho ? orthoDir : (camPos - world).normalized;

                if (vertexGuideWorldNormals != null && vertexGuideWorldNormals.Length > i)
                {
                    if (Vector3.Dot(vertexGuideWorldNormals[i], viewDir) <= 0f) continue;
                }

                float handleSize = HandleUtility.GetHandleSize(world);
                // 遠くにあるときは小さく、近くにあるときは大きく見えるように、ワールド空間ベースの固定サイズにする
                // 以前のサイズが大きすぎたため、ベースサイズと画面サイズブレンドの係数を大幅に縮小
                float size = 0.0002f + handleSize * 0.0002f;
                
                // Z-fighting（めり込み）を防ぐためのオフセットは、深度精度に依存するためカメラ距離(handleSize)に比例させる
                Vector3 drawPos = world + viewDir * (handleSize * 0.002f);

                Handles.color = i == selectedVertexIndex ? Color.yellow : VertexGuideColor;
                if (Handles.Button(drawPos, Quaternion.identity, size, size, Handles.DotHandleCap))
                {
                    pickedIndex = i;
                    break;
                }
            }
            Handles.color = prevColor;
            Handles.zTest = prevZTest;

            if (pickedIndex >= 0)
            {
                selectedVertexIndex = pickedIndex;
                vertexMovedShapeIndices = _model.CollectShapeIndicesMovingVertex(selectedVertexIndex);
                vertexFilterActive = true;
                vertexPickMode = false;
                ClearVertexGuideCache();
                UpdateVisibility();
                SceneView.RepaintAll();
                Repaint();
                Event.current.Use();
            }

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                vertexPickMode = false;
                ClearVertexGuideCache();
                SceneView.RepaintAll();
                Repaint();
                Event.current.Use();
            }
        }

        private void ClearVertexFilter()
        {
            vertexPickMode = false;
            vertexFilterActive = false;
            selectedVertexIndex = -1;
            vertexMovedShapeIndices = null;
            ClearVertexGuideCache();
            UpdateVisibility();
            SceneView.RepaintAll();
            Repaint();
        }

        private void UpdateVertexGuideCache()
        {
            if (_model.TargetSkinnedMesh == null || _model.TargetSkinnedMesh.sharedMesh == null) return;
            var mesh = _model.TargetSkinnedMesh.sharedMesh;
            var smrTransform = _model.TargetSkinnedMesh.transform;
            var localToWorldMatrix = smrTransform.localToWorldMatrix;
            int meshId = mesh.GetInstanceID();

            bool cacheInvalid = vertexGuideWorldPositions == null
                || vertexGuideWorldPositions.Length != mesh.vertexCount
                || vertexGuideMeshInstanceId != meshId
                || vertexGuideLocalToWorld != localToWorldMatrix;

            if (!cacheInvalid) return;

            SkinnedMeshRenderer targetToBake = _model.TargetSkinnedMesh;
            Transform bakeTransform = smrTransform;

            // NDMFのプレビューオブジェクトが存在すれば優先する
            SkinnedMeshRenderer proxySmr = GetNDMFProxySMR(_model.TargetSkinnedMesh);
            if (proxySmr != null)
            {
                targetToBake = proxySmr;
                bakeTransform = proxySmr.transform;
            }

            Mesh bakedMesh = new Mesh();
            targetToBake.BakeMesh(bakedMesh);
            var vertices = bakedMesh.vertices;
            var normals = bakedMesh.normals;

            // NDMF等で動的にメッシュが差し替えられて頂点数が一致しない場合のフォールバック
            if (vertices == null || vertices.Length != mesh.vertexCount)
            {
                vertices = mesh.vertices;
                normals = mesh.normals;
                bakeTransform = smrTransform;
            }

            if (vertices == null || vertices.Length == 0)
            {
                DestroyImmediate(bakedMesh);
                vertexGuideWorldPositions = null;
                vertexGuideWorldNormals = null;
                return;
            }

            vertexGuideWorldPositions = new Vector3[vertices.Length];
            vertexGuideWorldNormals = new Vector3[vertices.Length];
            bool hasNormals = normals != null && normals.Length == vertices.Length;

            for (int i = 0; i < vertices.Length; i++)
            {
                vertexGuideWorldPositions[i] = bakeTransform.TransformPoint(vertices[i]);
                if (hasNormals)
                    vertexGuideWorldNormals[i] = bakeTransform.TransformDirection(normals[i]);
            }

            DestroyImmediate(bakedMesh);

            vertexGuideMeshInstanceId = meshId;
            vertexGuideLocalToWorld = localToWorldMatrix;
        }

        private SkinnedMeshRenderer GetNDMFProxySMR(SkinnedMeshRenderer originalSmr)
        {
            if (originalSmr == null) return null;
            
            System.Type sessionType = null;
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == "nadena.dev.ndmf")
                {
                    sessionType = assembly.GetType("nadena.dev.ndmf.preview.PreviewSession");
                    break;
                }
            }
            if (sessionType == null) return null;

            var currentProp = sessionType.GetProperty("Current", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (currentProp == null) return null;

            var session = currentProp.GetValue(null);
            if (session == null) return null;

            var mapProp = sessionType.GetProperty("OriginalToProxyRenderer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (mapProp == null) return null;

            var map = mapProp.GetValue(session);
            if (map != null)
            {
                var tryGetValueMethod = map.GetType().GetMethod("TryGetValue");
                if (tryGetValueMethod != null)
                {
                    var args = new object[] { originalSmr, null };
                    bool success = (bool)tryGetValueMethod.Invoke(map, args);
                    if (success) return args[1] as SkinnedMeshRenderer;
                }
            }

            return null;
        }

        private void ClearVertexGuideCache()
        {
            vertexGuideWorldPositions = null;
            vertexGuideWorldNormals = null;
            vertexGuideMeshInstanceId = 0;
            vertexGuideLocalToWorld = Matrix4x4.zero;
        }

        private void SetStatus(string msg, int level, double autoClearSec = 3.0)
        {
            if (level != 0 && autoClearSec == 3.0) autoClearSec = 6.0;
            statusMessage      = msg;
            statusLevel        = level;
            statusSetAt        = EditorApplication.timeSinceStartup;
            statusAutoClearSec = autoClearSec;
            Repaint();
        }

        private void TickStatusAutoClear()
        {
            if (statusAutoClearSec <= 0) return;
            if (!string.IsNullOrEmpty(statusMessage) &&
                EditorApplication.timeSinceStartup - statusSetAt > statusAutoClearSec)
            {
                statusMessage      = null;
                statusLevel        = 0;
                statusAutoClearSec = 0;
                Repaint();
            }
        }

        // ─── Favorites ───────────────────────────────────────────────────────

        private string GetFavoritesKeyForSmr(SkinnedMeshRenderer smr)
        {
            if (smr == null || smr.sharedMesh == null) return null;
            string meshPath = AssetDatabase.GetAssetPath(smr.sharedMesh);
            string guid = string.IsNullOrEmpty(meshPath)
                ? smr.sharedMesh.name
                : AssetDatabase.AssetPathToGUID(meshPath);
            return "DenEmo_Fav|" + guid;
        }

        private void LoadFavoritesPrefs()
        {
            var loaded = new HashSet<SkinnedMeshRenderer>();
            foreach (var item in _model.Items)
            {
                var smr = item.OwnerSmr ?? _model.TargetSkinnedMesh;
                if (smr == null || loaded.Contains(smr)) continue;

                var key = GetFavoritesKeyForSmr(smr);
                if (string.IsNullOrEmpty(key) || !EditorPrefs.HasKey(key)) { loaded.Add(smr); continue; }

                var s = EditorPrefs.GetString(key, "");
                if (!string.IsNullOrEmpty(s))
                {
                    var names = new HashSet<string>(s.Split(','));
                    foreach (var i in _model.Items)
                        if ((i.OwnerSmr ?? _model.TargetSkinnedMesh) == smr)
                            i.IsFavorite = names.Contains(i.Name);
                }
                loaded.Add(smr);
            }
        }

        private void SaveFavoritesPrefs()
        {
            var bySmr   = new Dictionary<SkinnedMeshRenderer, List<string>>();
            var allSmrs = new HashSet<SkinnedMeshRenderer>();

            foreach (var item in _model.Items)
            {
                var smr = item.OwnerSmr ?? _model.TargetSkinnedMesh;
                if (smr == null) continue;
                allSmrs.Add(smr);
                if (item.IsFavorite)
                {
                    if (!bySmr.ContainsKey(smr)) bySmr[smr] = new List<string>();
                    bySmr[smr].Add(item.Name);
                }
            }

            foreach (var smr in allSmrs)
            {
                var key = GetFavoritesKeyForSmr(smr);
                if (string.IsNullOrEmpty(key)) continue;
                EditorPrefs.SetString(key,
                    bySmr.TryGetValue(smr, out var favs) ? string.Join(",", favs) : "");
            }
        }

        private void OnFavoriteChanged(string shapeName, bool isFavorite)
        {
            SaveFavoritesPrefs();
            if (showOnlyFavorites) UpdateVisibility();
            Repaint();
        }

        // ─── Preferences & Snapshots ─────────────────────────────────────────

        private string GetBlendPrefsKeyForSmr(SkinnedMeshRenderer smr)
        {
            if (smr == null || smr.sharedMesh == null) return null;
            string scene    = smr.gameObject ? smr.gameObject.scene.name : "";
            string meshName = smr.sharedMesh.name;
            return $"DenEmo_Values|{scene}|{meshName}";
        }

        private void SaveBlendValuesPrefs()
        {
            var bySmr = new Dictionary<SkinnedMeshRenderer, List<ShapeKeyItem>>();
            foreach (var item in _model.Items)
            {
                var smr = item.OwnerSmr ?? _model.TargetSkinnedMesh;
                if (smr == null) continue;
                if (!bySmr.ContainsKey(smr)) bySmr[smr] = new List<ShapeKeyItem>();
                bySmr[smr].Add(item);
            }

            foreach (var kv in bySmr)
            {
                var key = GetBlendPrefsKeyForSmr(kv.Key);
                if (string.IsNullOrEmpty(key)) continue;
                var parts = new string[kv.Value.Count];
                for (int i = 0; i < kv.Value.Count; i++)
                    parts[i] = kv.Value[i].Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                EditorPrefs.SetString(key, string.Join(",", parts));
            }
        }

        private void LoadIncludeFlagsPrefs()
        {
            var bySmr = new Dictionary<SkinnedMeshRenderer, List<ShapeKeyItem>>();
            foreach (var item in _model.Items)
            {
                var smr = item.OwnerSmr ?? _model.TargetSkinnedMesh;
                if (smr == null) continue;
                if (!bySmr.ContainsKey(smr)) bySmr[smr] = new List<ShapeKeyItem>();
                bySmr[smr].Add(item);
            }

            foreach (var kv in bySmr)
            {
                var key = GetBlendPrefsKeyForSmr(kv.Key);
                if (string.IsNullOrEmpty(key)) continue;
                key += "|IncludeFlags";
                if (!EditorPrefs.HasKey(key)) continue;
                var s = EditorPrefs.GetString(key);
                if (string.IsNullOrEmpty(s)) continue;
                var parts = s.Split(',');
                var items = kv.Value;
                for (int i = 0; i < parts.Length && i < items.Count; i++)
                    items[i].IsIncluded = parts[i] == "1" || parts[i].Equals("true", StringComparison.OrdinalIgnoreCase);
            }
        }

        private void SaveIncludeFlagsPrefsImmediate()
        {
            var bySmr = new Dictionary<SkinnedMeshRenderer, List<ShapeKeyItem>>();
            foreach (var item in _model.Items)
            {
                var smr = item.OwnerSmr ?? _model.TargetSkinnedMesh;
                if (smr == null) continue;
                if (!bySmr.ContainsKey(smr)) bySmr[smr] = new List<ShapeKeyItem>();
                bySmr[smr].Add(item);
            }

            foreach (var kv in bySmr)
            {
                var key = GetBlendPrefsKeyForSmr(kv.Key);
                if (string.IsNullOrEmpty(key)) continue;
                key += "|IncludeFlags";
                var parts = new string[kv.Value.Count];
                for (int i = 0; i < kv.Value.Count; i++)
                    parts[i] = kv.Value[i].IsIncluded ? "1" : "0";
                EditorPrefs.SetString(key, string.Join(",", parts));
            }
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
                var item = _model.Items[i];
                item.Value = snapshotValues[i];
                var smr = item.OwnerSmr ?? _model.TargetSkinnedMesh;
                if (smr != null) smr.SetBlendShapeWeight(item.Index, snapshotValues[i]);
            }
            SaveBlendValuesPrefs();
        }
    }
}
