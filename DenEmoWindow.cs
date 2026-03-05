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
        [MenuItem("Tools/DenEmo")]
        public static void ShowWindow()
        {
            var w = GetWindow<DenEmoWindow>("DenEmo");
            w.minSize = new Vector2(350, 300);
        }

        private ShapeKeyModel _model = new ShapeKeyModel();
        private ShapeKeyListUI _listUI = new ShapeKeyListUI();
        
        private string saveFolder = "Assets/Generated_Animations";
        private string searchText = string.Empty;
        private string lastSearchText = null;
        private bool alignToExistingClipKeys = false;
        private bool showOnlyIncluded = false;
        private bool lastShowOnlyIncluded = false;
        private bool symmetryMode = false;
        private Vector2 scroll;
        
        private AnimationClip loadedClip = null;
        private AnimationClip baseAlignClip = null;
        
        private HashSet<string> collapsedGroups = new HashSet<string>();
        
        private string statusMessage = null;
        private int statusLevel = 0; // 0=Info, 1=Success, 2=Warning, 3=Error
        private double statusSetAt = 0;
        private double statusAutoClearSec = 0;
        
        private bool includeFlagsDirty = false;
        private double lastIncludeFlagsChangeTime = 0;
        private List<float> snapshotValues = null;

        private void OnEnable()
        {
            DenEmoLoc.LoadPrefs();
            saveFolder = DenEmoProjectPrefs.GetString("DenEmo_SaveFolder", saveFolder);
            searchText = DenEmoProjectPrefs.GetString("DenEmo_SearchText", string.Empty);
            alignToExistingClipKeys = DenEmoProjectPrefs.GetBool("DenEmo_AlignToClip", false);
            showOnlyIncluded = DenEmoProjectPrefs.GetBool("DenEmo_ShowOnlyIncluded", false);
            symmetryMode = DenEmoProjectPrefs.GetBool("DenEmo_SymmetryMode", false);
            
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
            SetStatus(DenEmoLoc.T("status.ready"), 0, 0);
            
            LoadCollapsedGroupsPrefs();
            
            _listUI.OnIncludeFlagsChanged = () => {
                includeFlagsDirty = true;
                lastIncludeFlagsChangeTime = EditorApplication.timeSinceStartup;
            };
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            _listUI.StopThrottle();
            
            if (includeFlagsDirty)
                SaveIncludeFlagsPrefsImmediate();

            if (_model.TargetSkinnedMesh) 
                DenEmoProjectPrefs.SetString("DenEmo_LastTarget", _model.TargetSkinnedMesh.GetInstanceID().ToString());
            
            DenEmoProjectPrefs.SetString("DenEmo_SaveFolder", saveFolder);
            DenEmoProjectPrefs.SetString("DenEmo_SearchText", searchText);
            DenEmoProjectPrefs.SetBool("DenEmo_AlignToClip", alignToExistingClipKeys);
            DenEmoProjectPrefs.SetBool("DenEmo_ShowOnlyIncluded", showOnlyIncluded);
            DenEmoProjectPrefs.SetBool("DenEmo_SymmetryMode", symmetryMode);
            
            if (snapshotValues != null && snapshotValues.Count > 0)
            {
                var parts = new string[snapshotValues.Count];
                for (int i = 0; i < snapshotValues.Count; i++) parts[i] = snapshotValues[i].ToString(System.Globalization.CultureInfo.InvariantCulture);
                DenEmoProjectPrefs.SetString("DenEmo_Snapshot", string.Join(",", parts));
            }
            
            SaveBlendValuesPrefs();
            SaveCollapsedGroupsPrefs();
        }

        private void OnUndoRedo()
        {
            _model.SyncValuesFromMesh();
            Repaint();
        }

        private void RefreshListAndCache()
        {
            _model.RefreshList(searchText, showOnlyIncluded);
            LipSyncExclusionRule.ApplyExclusion(_model.TargetSkinnedMesh, _model.Items);
            LoadIncludeFlagsPrefs();
            _model.BuildGroups();
            CreateSnapshot(true);
            UpdateVisibility();
        }

        private void UpdateVisibility()
        {
            var tokens = ShapeKeyModel.BuildSearchTokens(searchText);
            _model.UpdateVisibility(tokens, showOnlyIncluded);
        }

        private void SetStatus(string msg, int level, double autoClearSec = 3.0)
        {
            if (level != 0 && autoClearSec == 3.0) autoClearSec = 6.0;
            statusMessage = msg;
            statusLevel = level;
            statusSetAt = EditorApplication.timeSinceStartup;
            statusAutoClearSec = autoClearSec;
            Repaint();
        }

        private void TickStatusAutoClear()
        {
            if (statusAutoClearSec <= 0) return;
            if (!string.IsNullOrEmpty(statusMessage) && EditorApplication.timeSinceStartup - statusSetAt > statusAutoClearSec)
            {
                statusMessage = null;
                statusLevel = 0;
                statusAutoClearSec = 0;
                Repaint();
            }
        }

        private void OnGUI()
        {
            TickStatusAutoClear();
            DenEmoCommonUI.DrawHeader(this);
            HandleDragAndDrop();

            if (!DrawBasicSettings()) 
            {
                DenEmoCommonUI.DrawStatusBar(statusMessage, statusLevel);
                return;
            }

            DrawSnapshotAndSearch();
            _listUI.DrawList(_model, ref scroll, true, collapsedGroups, symmetryMode, this);
            DrawFooter();
            
            DenEmoCommonUI.DrawStatusBar(statusMessage, statusLevel);
        }

        private bool DrawBasicSettings()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(DenEmoLoc.T("ui.section.basic"), EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUI.BeginChangeCheck();
            var newSmr = EditorGUILayout.ObjectField(new GUIContent(DenEmoLoc.T("ui.mesh.label"), DenEmoLoc.T("ui.mesh.tooltip")), _model.TargetSkinnedMesh, typeof(SkinnedMeshRenderer), true) as SkinnedMeshRenderer;
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
                Repaint();
            }

            if (_model.TargetSkinnedMesh == null)
            {
                EditorGUILayout.HelpBox(DenEmoLoc.T("ui.mesh.missing"), MessageType.Info);
                EditorGUILayout.EndVertical();
                return false;
            }

            if (!_model.TargetSkinnedMesh.gameObject.activeInHierarchy || !_model.TargetSkinnedMesh.enabled)
            {
                EditorGUILayout.HelpBox(DenEmoLoc.T("ui.mesh.inactive.warn"), MessageType.Warning);
            }

            if (_model.Items.Count == 0)
            {
                EditorGUILayout.HelpBox(DenEmoLoc.T("ui.mesh.noShapes"), MessageType.Info);
                EditorGUILayout.EndVertical();
                return false;
            }

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            alignToExistingClipKeys = EditorGUILayout.ToggleLeft(
                new GUIContent(DenEmoLoc.T("ui.align.toggle"), DenEmoLoc.T("ui.align.toggle.tip")), alignToExistingClipKeys);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledGroupScope(!alignToExistingClipKeys))
            {
                EditorGUILayout.LabelField(new GUIContent(DenEmoLoc.T("ui.align.base.label"), DenEmoLoc.T("ui.align.base.tip")), GUILayout.Width(110));
                baseAlignClip = EditorGUILayout.ObjectField(GUIContent.none, baseAlignClip, typeof(AnimationClip), false) as AnimationClip;
                using (new EditorGUI.DisabledGroupScope(baseAlignClip == null))
                {
                    if (GUILayout.Button(new GUIContent(DenEmoLoc.T("ui.align.apply.button"), DenEmoLoc.T("ui.align.apply.tip")), GUILayout.Width(60)))
                    {
                        AlignToBaseClip();
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent(DenEmoLoc.T("ui.applyAnim.label"), DenEmoLoc.T("ui.applyAnim.tip")), GUILayout.Width(120));
            loadedClip = EditorGUILayout.ObjectField(GUIContent.none, loadedClip, typeof(AnimationClip), false) as AnimationClip;
            using (new EditorGUI.DisabledGroupScope(loadedClip == null))
            {
                if (GUILayout.Button(new GUIContent(DenEmoLoc.T("ui.applyAnim.button"), DenEmoLoc.T("ui.applyAnim.button.tip")), GUILayout.Width(60)))
                {
                    SetStatus(DenEmoLoc.T("status.applying"), 0, 0);
                    string applyRes = AnimationExporter.ApplyAnimationToMesh(loadedClip, _model);
                    if (applyRes == "SUCCESS")
                    {
                        SaveBlendValuesPrefs();
                        SetStatus(DenEmoLoc.T("dlg.apply.done.msg"), 1);
                    }
                    else
                    {
                        SetStatus(applyRes, 2);
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            return true;
        }

        private void AlignToBaseClip()
        {
            var pairs = new HashSet<string>();
            string currentSmrPath = ""; // Path relative to root
            if (_model.TargetSkinnedMesh != null)
            {
                var parts = new List<string>();
                var t = _model.TargetSkinnedMesh.transform;
                var root = t.root;
                while (t != null && t != root) { parts.Add(t.name); t = t.parent; }
                parts.Reverse();
                currentSmrPath = string.Join("/", parts.ToArray());
            }

            foreach (var b in AnimationUtility.GetCurveBindings(baseAlignClip))
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

        private void DrawSnapshotAndSearch()
        {
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            var newShowOnly = EditorGUILayout.ToggleLeft(new GUIContent(DenEmoLoc.T("ui.filter.showIncluded"), DenEmoLoc.T("ui.filter.showIncluded.tip")), showOnlyIncluded);
            if (newShowOnly != showOnlyIncluded)
            {
                showOnlyIncluded = newShowOnly;
                DenEmoProjectPrefs.SetBool("DenEmo_ShowOnlyIncluded", showOnlyIncluded);
            }
            GUILayout.Space(12);
            var newSym = EditorGUILayout.ToggleLeft(new GUIContent(DenEmoLoc.T("ui.symmetry.label"), DenEmoLoc.T("ui.symmetry.tip")), symmetryMode);
            if (newSym != symmetryMode)
            {
                symmetryMode = newSym;
                DenEmoProjectPrefs.SetBool("DenEmo_SymmetryMode", symmetryMode);
                Repaint();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(DenEmoLoc.T("ui.snapshot.create"), GUILayout.Height(22))) CreateSnapshot(false);
            if (GUILayout.Button(DenEmoLoc.T("ui.snapshot.restore"), GUILayout.Height(22))) RestoreSnapshot();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4f);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(DenEmoLoc.T("ui.section.search"), EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            GUI.SetNextControlName("SearchField");
            searchText = EditorGUILayout.TextField(searchText, GUILayout.ExpandWidth(true));
            if (GUILayout.Button(DenEmoLoc.T("ui.search.clear"), GUILayout.Width(60)))
            {
                searchText = string.Empty;
                DenEmoProjectPrefs.SetString("DenEmo_SearchText", searchText);
                GUI.FocusControl(null);
                Repaint();
            }
            EditorGUILayout.EndHorizontal();

            if (searchText != lastSearchText || showOnlyIncluded != lastShowOnlyIncluded)
            {
                UpdateVisibility();
                lastSearchText = searchText;
                lastShowOnlyIncluded = showOnlyIncluded;
            }

            if (includeFlagsDirty && EditorApplication.timeSinceStartup - lastIncludeFlagsChangeTime > 0.5)
            {
                SaveIncludeFlagsPrefsImmediate();
            }

            EditorGUILayout.Space(4f);
        }

        private void DrawFooter()
        {
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(DenEmoLoc.T("ui.footer.saveAnim"), GUILayout.Height(30)))
            {
                SetStatus(DenEmoLoc.T("status.saving"), 0, 0);
                var err = AnimationExporter.SaveAnimationClip(_model, saveFolder, out string path);
                if (err != null) SetStatus(err, 3);
                else SetStatus(DenEmoLoc.Tf("dlg.save.done.msg", path), 1);
            }
            if (GUILayout.Button(DenEmoLoc.T("ui.footer.refresh"), GUILayout.Width(80), GUILayout.Height(30)))
            {
                RefreshListAndCache();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(DenEmoLoc.T("ui.footer.saveTo"), GUILayout.Width(100));
            saveFolder = EditorGUILayout.TextField(saveFolder);
            if (GUILayout.Button(DenEmoLoc.T("ui.footer.browse"), GUILayout.Width(80)))
            {
                var newPath = EditorUtility.OpenFolderPanel("フォルダを選択", Application.dataPath, "");
                if (!string.IsNullOrEmpty(newPath))
                {
                    saveFolder = newPath.StartsWith(Application.dataPath) ? "Assets" + newPath.Substring(Application.dataPath.Length) : newPath;
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void HandleDragAndDrop()
        {
            Event evt = Event.current;
            Rect dropArea = new Rect(0, 0, position.width, position.height);
            switch (evt.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!dropArea.Contains(evt.mousePosition)) return;
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        foreach (var obj in DragAndDrop.objectReferences)
                        {
                            if (obj is SkinnedMeshRenderer smr)
                            {
                                _model.SetTarget(smr);
                                RefreshListAndCache();
                                Repaint();
                                break;
                            }
                        }
                    }
                    evt.Use();
                    break;
            }
        }

        // --- Preferences and Snapshots ---

        private string GetBlendPrefsKey()
        {
            if (_model.TargetSkinnedMesh == null || _model.TargetSkinnedMesh.sharedMesh == null) return null;
            string scene = _model.TargetObject ? _model.TargetObject.scene.name : "";
            string meshName = _model.TargetSkinnedMesh.sharedMesh.name;
            return $"DenEmo_Values|{scene}|{meshName}";
        }

        private void SaveBlendValuesPrefs()
        {
            var key = GetBlendPrefsKey();
            if (string.IsNullOrEmpty(key)) return;
            var parts = new string[_model.Items.Count];
            for (int i = 0; i < _model.Items.Count; i++) parts[i] = _model.Items[i].Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
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
            for (int i = 0; i < _model.Items.Count; i++) parts[i] = _model.Items[i].IsIncluded ? "1" : "0";
            EditorPrefs.SetString(key, string.Join(",", parts));
            includeFlagsDirty = false;
        }

        private void LoadCollapsedGroupsPrefs()
        {
            collapsedGroups.Clear();
            var s = DenEmoProjectPrefs.GetString("DenEmo_GroupsCollapsed", "");
            if (string.IsNullOrEmpty(s)) return;
            var parts = s.Split(new char[] { '\n', '\r', ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
            {
                var k = p.Trim();
                if (k.Length > 0) collapsedGroups.Add(k);
            }
        }

        private void SaveCollapsedGroupsPrefs()
        {
            if (collapsedGroups.Count == 0) DenEmoProjectPrefs.SetString("DenEmo_GroupsCollapsed", "");
            else DenEmoProjectPrefs.SetString("DenEmo_GroupsCollapsed", string.Join(",", collapsedGroups));
        }

        private void CreateSnapshot(bool loadTime)
        {
            if (_model.Items.Count == 0) return;
            snapshotValues = new List<float>();
            foreach (var i in _model.Items) snapshotValues.Add(i.Value);
            if (!loadTime)
            {
                var parts = new string[snapshotValues.Count];
                for (int i = 0; i < snapshotValues.Count; i++) parts[i] = snapshotValues[i].ToString(System.Globalization.CultureInfo.InvariantCulture);
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
                    for (int i = 0; i < parts.Length; i++)
                    {
                        if (float.TryParse(parts[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f)) snapshotValues.Add(f);
                        else snapshotValues.Add(0f);
                    }
                }
            }
            if (snapshotValues == null) return;
            int n = Math.Min(snapshotValues.Count, _model.Items.Count);
            for (int i = 0; i < n; i++)
            {
                _model.Items[i].Value = snapshotValues[i];
                if (_model.TargetSkinnedMesh) _model.TargetSkinnedMesh.SetBlendShapeWeight(i, snapshotValues[i]);
            }
            SaveBlendValuesPrefs();
        }
    }
}
