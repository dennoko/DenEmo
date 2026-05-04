using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using DenEmo.Models;
using DenEmo.Core;
using DenEmo.UI;

namespace DenEmo
{
    public partial class DenEmoWindow
    {
        // ─── Target mesh section ──────────────────────────────────────────────

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

            DrawAdditionalTargets();

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

            if (_additionalTargets.RemoveAll(item => item == null) > 0)
                changed = true;

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

            if (_model.TargetSkinnedMesh != null)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                var newSmr = EditorGUILayout.ObjectField(
                    new GUIContent("  +"),
                    null, typeof(SkinnedMeshRenderer), true) as SkinnedMeshRenderer;
                if (EditorGUI.EndChangeCheck() && newSmr != null)
                {
                    _additionalTargets.Add(newSmr);
                    changed = true;
                }
                using (new EditorGUI.DisabledGroupScope(true))
                    GUILayout.Button("✕", DenEmoTheme.MiniButtonStyle, GUILayout.Width(20));
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(DenEmoLoc.T("ui.footer.refresh"), DenEmoTheme.MiniButtonStyle, GUILayout.Width(60)))
                RefreshListAndCache();
            EditorGUILayout.EndHorizontal();

            if (changed)
            {
                ClampMeshFilterIndex();
                RefreshListAndCache();
                Repaint();
            }
        }

        // ─── Animation source section ─────────────────────────────────────────

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

        // ─── Search & filter section ──────────────────────────────────────────

        private void DrawSearchFilterSection()
        {
            DenEmoTheme.BeginSection(DenEmoLoc.EnglishMode ? "SEARCH & FILTER" : "検索・絞り込み");

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

            EditorGUILayout.BeginHorizontal();

            showOnlyFavorites = DrawFilterChip(showOnlyFavorites, DenEmoLoc.EnglishMode ? "★ Fav"      : "★ お気に入り", "DenEmo_ShowOnlyFavorites");
            GUILayout.Space(4);
            showOnlyIncluded  = DrawFilterChip(showOnlyIncluded,  DenEmoLoc.EnglishMode ? "✓ Enabled"  : "✓ 有効のみ",   "DenEmo_ShowOnlyIncluded");
            GUILayout.Space(4);
            showOnlyNonZero   = DrawFilterChip(showOnlyNonZero,   DenEmoLoc.EnglishMode ? "≠0 Non-zero": "≠0 非ゼロ",    "DenEmo_ShowOnlyNonZero");
            GUILayout.Space(4);
            symmetryMode      = DrawFilterChip(symmetryMode,      DenEmoLoc.EnglishMode ? "↔ Symmetry" : "↔ 左右同期",  "DenEmo_SymmetryMode");
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
                var  trackStyle  = trackFilter ? DenEmoTheme.ChipOnStyle : DenEmoTheme.ChipOffStyle;
                var  trackLabel  = DenEmoLoc.EnglishMode ? "◆ Keyed Only" : "◆ キー有りのみ";
                var  trackTip    = DenEmoLoc.EnglishMode
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

            var optBtnLabel = new GUIContent(DenEmoLoc.EnglishMode ? "⚙ Preview" : "⚙ 表示設定");
            var optBtnRect  = GUILayoutUtility.GetRect(optBtnLabel, DenEmoTheme.MiniButtonStyle, GUILayout.Width(66));
            if (GUI.Button(optBtnRect, optBtnLabel, DenEmoTheme.MiniButtonStyle))
                PopupWindow.Show(optBtnRect, new VertexPreviewOptionsPopup(this));

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

        // ─── Footer / save sections ───────────────────────────────────────────

        private void DrawFooterSection()
        {
            DenEmoTheme.BeginSection(DenEmoLoc.EnglishMode ? "SAVE SETTINGS" : "保存設定");

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
                new GUIContent(DenEmoLoc.T("ui.footer.overwriteEnable"), DenEmoLoc.T("ui.footer.overwriteEnable.tip")),
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
                if (!HasIncludedShapeKeys())
                {
                    EditorUtility.DisplayDialog(
                        DenEmoLoc.T("dlg.save.noIncluded.title"),
                        DenEmoLoc.T("dlg.save.noIncluded.msg"),
                        DenEmoLoc.T("dlg.ok"));
                }
                else if (overwriteSaveEnabled && overwriteTargetClip != null)
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

            GUILayout.Space(2);
            _animSaveAsNew = EditorGUILayout.ToggleLeft(
                new GUIContent(
                    DenEmoLoc.EnglishMode ? "Save as new clip" : "新規クリップとして保存",
                    DenEmoLoc.EnglishMode
                        ? "Opens a file dialog to save as a new animation clip. The original clip's folder is used as the default path."
                        : "元クリップのフォルダをデフォルトパスとしてファイルダイアログを開き、新規クリップとして保存します。"),
                _animSaveAsNew, DenEmoTheme.CaptionStyle);

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUILayout.Space(6);
            if (GUILayout.Button(
                DenEmoLoc.EnglishMode ? "Save Animation" : "アニメーションを保存",
                DenEmoTheme.ActionButtonStyle, GUILayout.ExpandWidth(true)))
            {
                _animModeUI.SaveClip(saveFolder, _model, (msg, lvl) => SetStatus(msg, lvl), _animSaveAsNew);
            }
            GUILayout.Space(6);
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
            DenEmoTheme.EndSection();
        }

        private bool HasIncludedShapeKeys()
        {
            foreach (var item in _model.Items)
                if (item.IsIncluded && !item.IsVrcShape && !item.IsLipSyncShape) return true;
            return false;
        }

        // ─── Drag and drop ────────────────────────────────────────────────────

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
    }
}
