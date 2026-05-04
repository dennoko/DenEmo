using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using DenEmo.Models;
using DenEmo.Core;
using DenEmo.UI;

namespace DenEmo
{
    public partial class DenEmoWindow : EditorWindow
    {
        // ─── Mode ─────────────────────────────────────────────────────────────

        private enum EditorMode { Pose, Animation }
        private EditorMode _currentMode = EditorMode.Pose;
        internal static Color VertexPreviewColor         = new Color(0.24f, 0.72f, 1.0f, 0.95f);
        internal static Color VertexPreviewSelectedColor  = Color.yellow;
        internal static float VertexPreviewSizeMultiplier = 1.0f;

        // ─── State ────────────────────────────────────────────────────────────

        [MenuItem("dennokoworks/DenEmo")]
        public static void ShowWindow()
        {
            var w = GetWindow<DenEmoWindow>("DenEmo");
            w.minSize = new Vector2(480, 400);
        }

        private ShapeKeyModel   _model      = new ShapeKeyModel();
        private ShapeKeyListUI  _listUI     = new ShapeKeyListUI();
        private AnimationModeUI _animModeUI = new AnimationModeUI();

        private string saveFolder  = "Assets/DenEmo/GeneratedAnimations";
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
        private bool _vertexResultPending = false;
        private double _vertexResultClearAt = 0;

        private bool lastShowOnlyIncluded  = false;
        private bool lastShowOnlyNonZero   = false;
        private bool lastShowOnlyFavorites = false;
        private bool lastSymmetryMode      = false;

        private List<SkinnedMeshRenderer> _additionalTargets = new List<SkinnedMeshRenderer>();

        // -1 = All targets, 0+ = GetAllTargetMeshes()[index]
        private int _meshFilterIndex = -1;

        private Vector2 scroll;
        private Vector2 _mainScroll;

        private AnimationClip loadedClip          = null;
        private AnimationClip overwriteTargetClip = null;

        private HashSet<string> collapsedGroups = new HashSet<string>();

        private string statusMessage      = null;
        private int    statusLevel        = 0;
        private double statusSetAt        = 0;
        private double statusAutoClearSec = 0;

        private bool   includeFlagsDirty         = false;
        private double lastIncludeFlagsChangeTime = 0;
        private List<float> snapshotValues        = null;

        // ─── Lifecycle ───────────────────────────────────────────────────────

        public static DenEmoWindow Instance { get; private set; }

        private void OnEnable()
        {
            Instance = this;
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
            VertexPreviewColor        = ParseColor(DenEmoProjectPrefs.GetString("DenEmo_VertexPreviewColor",         ""), new Color(0.24f, 0.72f, 1.0f, 0.95f));
            VertexPreviewSelectedColor = ParseColor(DenEmoProjectPrefs.GetString("DenEmo_VertexPreviewSelectedColor", ""), Color.yellow);
            if (float.TryParse(DenEmoProjectPrefs.GetString("DenEmo_VertexPreviewSize", "1"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float pvSize))
                VertexPreviewSizeMultiplier = pvSize;

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

            _currentMode = (EditorMode)DenEmoProjectPrefs.GetInt("DenEmo_Mode", 0);

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

            var ids = new List<string>();
            foreach (var smr in _additionalTargets)
                if (smr != null) ids.Add(smr.GetInstanceID().ToString());
            DenEmoProjectPrefs.SetString("DenEmo_AdditionalTargets", string.Join(",", ids));

            DenEmoProjectPrefs.SetString("DenEmo_SaveFolder",         saveFolder);
            DenEmoProjectPrefs.SetString("DenEmo_SearchText",         searchText);
            DenEmoProjectPrefs.SetBool("DenEmo_ShowOnlyIncluded",     showOnlyIncluded);
            DenEmoProjectPrefs.SetBool("DenEmo_ShowOnlyNonZero",      showOnlyNonZero);
            DenEmoProjectPrefs.SetBool("DenEmo_ShowOnlyFavorites",    showOnlyFavorites);
            DenEmoProjectPrefs.SetBool("DenEmo_SymmetryMode",         symmetryMode);
            DenEmoProjectPrefs.SetBool("DenEmo_AutoBackup",           autoBackup);
            DenEmoProjectPrefs.SetBool("DenEmo_OverwriteSaveEnabled", overwriteSaveEnabled);
            DenEmoProjectPrefs.SetInt("DenEmo_MeshFilter",            _meshFilterIndex);
            DenEmoProjectPrefs.SetInt("DenEmo_Mode", (int)_currentMode);
            DenEmoProjectPrefs.SetString("DenEmo_VertexPreviewColor",         ColorToPrefsString(VertexPreviewColor));
            DenEmoProjectPrefs.SetString("DenEmo_VertexPreviewSelectedColor", ColorToPrefsString(VertexPreviewSelectedColor));
            DenEmoProjectPrefs.SetString("DenEmo_VertexPreviewSize",          VertexPreviewSizeMultiplier.ToString("F4", System.Globalization.CultureInfo.InvariantCulture));

            if (overwriteTargetClip != null)
            {
                var path = AssetDatabase.GetAssetPath(overwriteTargetClip);
                DenEmoProjectPrefs.SetString("DenEmo_OverwriteClipGuid",
                    string.IsNullOrEmpty(path) ? "" : AssetDatabase.AssetPathToGUID(path));
            }
            else
            {
                DenEmoProjectPrefs.SetString("DenEmo_OverwriteClipGuid", "");
            }

            if (_animModeUI.ClipModel.Clip != null)
            {
                var path = AssetDatabase.GetAssetPath(_animModeUI.ClipModel.Clip);
                DenEmoProjectPrefs.SetString("DenEmo_AnimClipGuid",
                    string.IsNullOrEmpty(path) ? "" : AssetDatabase.AssetPathToGUID(path));
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

            if (_vertexResultPending && EditorApplication.timeSinceStartup >= _vertexResultClearAt)
            {
                _vertexResultPending = false;
                ClearVertexGuideCache();
                SceneView.RepaintAll();
            }
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

            _mainScroll = EditorGUILayout.BeginScrollView(_mainScroll);

            bool hasTarget = DrawTargetMeshSection();
            if (!hasTarget)
            {
                EditorGUILayout.EndScrollView();
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
                _animModeUI.DrawClipCorrectionSection((msg, lvl) => SetStatus(msg, lvl), this);

                if (HasOpenInstances<DenEmoTimelineWindow>())
                {
                    EditorGUILayout.BeginVertical(DenEmoTheme.CardStyle);
                    GUILayout.Label(DenEmoLoc.EnglishMode ? "Timeline is open in a separate window." : "タイムラインは別ウィンドウで開かれています。", DenEmoTheme.CaptionStyle);

                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button(DenEmoLoc.EnglishMode ? "Focus Timeline Window" : "ウィンドウをフォーカス", DenEmoTheme.SecondaryButtonStyle))
                    {
                        DenEmoTimelineWindow.ShowWindow();
                    }
                    if (GUILayout.Button(DenEmoLoc.EnglishMode ? "Close Timeline Window" : "ウィンドウを閉じて結合", DenEmoTheme.SecondaryButtonStyle))
                    {
                        GetWindow<DenEmoTimelineWindow>().Close();
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.EndVertical();
                }
                else
                {
                    _animModeUI.DrawTimeline(_model, this);
                }

                DrawSearchFilterSection();
                var animContext = _animModeUI.ClipModel.Clip != null
                    ? _animModeUI.BuildDrawContext(_model)
                    : null;
                _listUI.DrawList(_model, ref scroll, true, collapsedGroups, symmetryMode, this, animContext);
                DrawAnimationSaveSection();
            }

            EditorGUILayout.EndScrollView();

            DenEmoCommonUI.DrawStatusBar(statusMessage, statusLevel);
            HandleDragAndDrop();

            if (includeFlagsDirty && EditorApplication.timeSinceStartup - lastIncludeFlagsChangeTime > 0.5)
                SaveIncludeFlagsPrefsImmediate();

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

            var poseStyle = _currentMode == EditorMode.Pose     ? DenEmoTheme.ChipOnStyle : DenEmoTheme.ChipOffStyle;
            var animStyle = _currentMode == EditorMode.Animation ? DenEmoTheme.ChipOnStyle : DenEmoTheme.ChipOffStyle;

            if (GUILayout.Button(DenEmoLoc.T("ui.animMode.tab.pose"), poseStyle, GUILayout.Width(130)))
                SwitchMode(EditorMode.Pose);
            GUILayout.Space(2);
            if (GUILayout.Button(DenEmoLoc.T("ui.animMode.tab.anim"), animStyle, GUILayout.Width(130)))
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
                _animModeUI.StopPreview();
                if (snapshotValues != null && snapshotValues.Count > 0)
                    RestoreSnapshot();
            }

            _currentMode = mode;

            if (_currentMode == EditorMode.Animation)
            {
                CreateSnapshot(false);
                if (_animModeUI.ClipModel.Clip != null && _model.TargetSkinnedMesh != null)
                    _animModeUI.StartPreview(_model);
            }

            Repaint();
        }

        // ─── Mesh helpers ─────────────────────────────────────────────────────

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
            _model.SetActiveMeshes(GetActiveMeshes());
            _model.RefreshList(searchText, showOnlyIncluded);
            LipSyncExclusionRule.ApplyExclusion(_model.TargetSkinnedMesh, _model.Items);
            LoadFavoritesPrefs();
            _model.BuildGroups();
            LoadIncludeFlagsPrefs();
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

        // ─── Status ───────────────────────────────────────────────────────────

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
        // ─── Timeline Separate Window Support ─────────────────────────────────

        public void DrawTimelineForSeparateWindow(EditorWindow timelineWindow)
        {
            if (_currentMode != EditorMode.Animation)
            {
                GUILayout.Label(DenEmoLoc.EnglishMode ? "Multi Frame mode is not active." : "マルチフレームモードではありません。");
                return;
            }
            _animModeUI.DrawTimeline(_model, timelineWindow);
        }

        // ─── Vertex preview prefs helpers ─────────────────────────────────────

        internal static string ColorToPrefsString(Color c)
            => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F4},{1:F4},{2:F4},{3:F4}", c.r, c.g, c.b, c.a);

        private static Color ParseColor(string s, Color def)
        {
            if (string.IsNullOrEmpty(s)) return def;
            var parts = s.Split(',');
            if (parts.Length != 4) return def;
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            if (float.TryParse(parts[0], System.Globalization.NumberStyles.Float, ic, out float r) &&
                float.TryParse(parts[1], System.Globalization.NumberStyles.Float, ic, out float g) &&
                float.TryParse(parts[2], System.Globalization.NumberStyles.Float, ic, out float b) &&
                float.TryParse(parts[3], System.Globalization.NumberStyles.Float, ic, out float a))
                return new Color(r, g, b, a);
            return def;
        }
    }
}
