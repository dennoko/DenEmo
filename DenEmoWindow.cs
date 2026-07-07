using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using DenEmo.Models;
using DenEmo.Core;
using DenEmo.UI;

namespace DenEmo
{
    public partial class DenEmoWindow : EditorWindow
    {
        // ─── Mode ─────────────────────────────────────────────────────────────

        private enum EditorMode { Pose, Animation, FxSetup }
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
        private FxSetupModeUI   _fxSetupUI  = new FxSetupModeUI();

        private string saveFolder  = "Assets/DenEmo/GeneratedAnimations";
        private string searchText  = string.Empty;
        private string lastSearchText = null;

        private bool showOnlyIncluded   = false;
        private bool showOnlyNonZero    = false;
        private bool showOnlyFavorites  = false;
        private bool symmetryMode       = false;
        private bool autoBackup         = true;
        private bool overwriteSaveEnabled = false;
        private bool _animSaveAsNew       = false;
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

        // ─── UI Toolkit chrome (CreateGUI で構築) ────────────────────────────
        private Label  _statusLabel;
        private Button _langButton;
        private Button _tabPose;
        private Button _tabAnim;
        private Button _tabFx;

        // Pose モード用コンテンツ（pose-scroll 配下）
        private ScrollView     _poseScroll;
        private VisualElement  _poseTargetHost;
        private VisualElement  _poseSearchHost;
        private VisualElement  _poseListHost;

        // Animation モード用コンテンツ（anim-scroll 配下）
        private ScrollView     _animScroll;
        private VisualElement  _animTargetHost;
        private VisualElement  _animSearchHost;
        private IMGUIContainer _animTimelineGui;
        private VisualElement  _animListHost;
        private VisualElement  _animClipCard;
        private VisualElement  _animTimelineNotice;
        private Label          _animTimelineNoticeLabel;
        private Button         _animTimelineFocus;
        private Button         _animTimelineClose;
        private VisualElement  _animRecBanner;
        private Label          _animRecBannerLabel;

        // FX モード用コンテンツ（fx-scroll 配下。カード内容は FxSetupModeUI がバインド）
        private ScrollView     _fxScroll;
        private VisualElement  _fxTargetHost;

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
            _animSaveAsNew       = DenEmoProjectPrefs.GetBool("DenEmo_AnimSaveAsNew",     false);
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
            _animModeUI.StatusSink = (msg, lvl) => SetStatus(msg, lvl);

            _fxSetupUI.StatusSink = (msg, lvl) => SetStatus(msg, lvl);
            if (_currentMode == EditorMode.FxSetup)
            {
                wantsMouseMove = true;
                wantsMouseEnterLeaveWindow = true;
                _fxSetupUI.OnEnter(_model);
            }

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
            _fxSetupUI.OnDisable();
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
            DenEmoProjectPrefs.SetBool("DenEmo_AnimSaveAsNew",        _animSaveAsNew);
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
            // ステータスバーが UI Toolkit 化され OnGUI を通らなくなったため、自動クリアはここで駆動する
            TickStatusAutoClear();

            // 検索・フィルター変更の反映と Include フラグの遅延保存（セクション UI Toolkit 化により OnGUI を通らない）
            TickListMaintenance();

            if (_currentMode == EditorMode.Animation)
                _animModeUI.OnUpdate(this);
            else if (_currentMode == EditorMode.FxSetup)
                _fxSetupUI.Tick(this);

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
            if (_currentMode == EditorMode.Animation)
                _animModeUI.OnUndoRedo();
            else if (_currentMode == EditorMode.FxSetup)
                _fxSetupUI.OnUndoRedo(_model);
            Repaint();
        }

        // ─── CreateGUI (UI Toolkit chrome + IMGUI content) ────────────────────

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            DenEmoUiAssets.SetupRoot(root);

            var tree = DenEmoUiAssets.LoadVisualTree(DenEmoUiAssets.MainWindowUxmlGuid);
            if (tree == null) return;
            tree.CloneTree(root);

            _langButton  = root.Q<Button>("lang-button");
            _tabPose     = root.Q<Button>("tab-pose");
            _tabAnim     = root.Q<Button>("tab-anim");
            _tabFx       = root.Q<Button>("tab-fx");
            _statusLabel = root.Q<Label>("status-bar");

            _poseScroll     = root.Q<ScrollView>("pose-scroll");
            _poseTargetHost = root.Q<VisualElement>("pose-target-host");
            _poseSearchHost = root.Q<VisualElement>("pose-search-host");
            _poseListHost   = root.Q<VisualElement>("pose-list-host");

            _animScroll     = root.Q<ScrollView>("anim-scroll");
            _animTargetHost = root.Q<VisualElement>("anim-target-host");
            _animSearchHost = root.Q<VisualElement>("anim-search-host");
            _animTimelineGui = root.Q<IMGUIContainer>("anim-timeline-imgui");
            _animListHost   = root.Q<VisualElement>("anim-list-host");
            _animClipCard   = root.Q<VisualElement>("anim-clip-card");

            _animTimelineNotice      = root.Q<VisualElement>("anim-timeline-notice");
            _animTimelineNoticeLabel = root.Q<Label>("anim-timeline-notice-label");
            _animTimelineFocus       = root.Q<Button>("anim-timeline-focus");
            _animTimelineClose       = root.Q<Button>("anim-timeline-close");
            _animRecBanner           = root.Q<VisualElement>("anim-rec-banner");
            _animRecBannerLabel      = root.Q<Label>("anim-rec-banner-label");

            _fxScroll     = root.Q<ScrollView>("fx-scroll");
            _fxTargetHost = root.Q<VisualElement>("fx-target-host");

            _animTimelineGui.onGUIHandler = OnAnimTimelineGUI;

            // セクションカード（対象メッシュ / 参照 / 検索 / 保存）の配線
            BindSectionCards(root);
            _fxSetupUI.BindUI(root, _model, () => saveFolder, this);
            RegisterRootDragAndDrop(root);

            _animTimelineFocus.clicked += () => DenEmoTimelineWindow.ShowWindow();
            _animTimelineClose.clicked += () =>
            {
                if (HasOpenInstances<DenEmoTimelineWindow>())
                    GetWindow<DenEmoTimelineWindow>().Close();
            };

            // シェイプキーリスト（UI Toolkit）。モード切替時にホスト間を移動する
            var listRoot = _listUI.Bind(_model, collapsedGroups, () => symmetryMode);
            _poseListHost.Add(listRoot);

            _animModeUI.BindClipSectionUI(_animClipCard, _model, () => saveFolder, this);
            _animModeUI.CorrectionUI.Bind(
                root.Q<VisualElement>("anim-correction-card"),
                _animModeUI.ClipModel, _animModeUI.Editor, _animModeUI.Preview,
                HasUsableTarget, (msg, lvl) => SetStatus(msg, lvl), this);

            // 対象メッシュの有無・IMGUI / SceneView 側で変わる状態はポーリングで表示に反映する
            root.schedule.Execute(OnUiPoll).Every(250);

            _langButton.clicked += () =>
            {
                DenEmoLoc.EnglishMode = !DenEmoLoc.EnglishMode;
                RefreshChromeLabels();
                Repaint();
            };
            _tabPose.clicked += () => SwitchMode(EditorMode.Pose);
            _tabAnim.clicked += () => SwitchMode(EditorMode.Animation);
            _tabFx.clicked   += () => SwitchMode(EditorMode.FxSetup);

            RefreshChromeLabels();
            UpdateModeTabVisuals();
            UpdateModeContentVisibility();
            UpdateStatusBar();
        }

        /// <summary>言語設定に依存するウィンドウ外枠のラベルを更新する。</summary>
        private void RefreshChromeLabels()
        {
            if (_langButton == null) return;
            _langButton.text = DenEmoLoc.EnglishMode ? "JA" : "EN";
            _tabPose.text    = DenEmoLoc.T("ui.animMode.tab.pose");
            _tabAnim.text    = DenEmoLoc.T("ui.animMode.tab.anim");
            _tabFx.text      = DenEmoLoc.T("ui.fx.tab");
            _animModeUI.RefreshClipSectionLabels();
            _listUI.RefreshLabels();
            RefreshSectionLabels();
            _fxSetupUI.RefreshLabels();
            _animTimelineNoticeLabel.text = DenEmoLoc.T("ui.timeline.separate.notice");
            _animTimelineFocus.text       = DenEmoLoc.T("ui.timeline.separate.focus");
            _animTimelineClose.text       = DenEmoLoc.T("ui.timeline.separate.close");
            _animRecBannerLabel.text      = DenEmoLoc.T("ui.animMode.rec.banner");
            UpdateStatusBar();
        }

        private void UpdateModeTabVisuals()
        {
            if (_tabPose == null) return;
            _tabPose.EnableInClassList("dennoko-tab--active", _currentMode == EditorMode.Pose);
            _tabAnim.EnableInClassList("dennoko-tab--active", _currentMode == EditorMode.Animation);
            _tabFx.EnableInClassList("dennoko-tab--active",   _currentMode == EditorMode.FxSetup);
        }

        /// <summary>モードに応じて Pose / Animation / FX 用コンテンツの表示を切り替える。</summary>
        private void UpdateModeContentVisibility()
        {
            if (_fxScroll == null || _animScroll == null || _poseScroll == null) return;
            bool pose = _currentMode == EditorMode.Pose;
            bool anim = _currentMode == EditorMode.Animation;
            bool fx   = _currentMode == EditorMode.FxSetup;
            _poseScroll.style.display = pose ? DisplayStyle.Flex : DisplayStyle.None;
            _animScroll.style.display = anim ? DisplayStyle.Flex : DisplayStyle.None;
            _fxScroll.style.display   = fx   ? DisplayStyle.Flex : DisplayStyle.None;

            // モード間で共有する要素を現在モードのホストへ移動する
            var listRoot = _listUI.Root;
            if (listRoot != null)
            {
                var host = anim ? _animListHost : _poseListHost;
                if (listRoot.parent != host) host.Add(listRoot);
            }
            var targetHost = pose ? _poseTargetHost : anim ? _animTargetHost : _fxTargetHost;
            if (_targetCard.parent != targetHost) targetHost.Add(_targetCard);
            var searchHost = anim ? _animSearchHost : _poseSearchHost;
            if (_searchCard.parent != searchHost) searchHost.Add(_searchCard);
            if (fx) _searchCard.style.display = DisplayStyle.None;

            UpdateAnimSectionsVisibility();
            UpdatePoseSectionsVisibility();
            UpdateSectionCards();
            _fxSetupUI.PollUI(fx && HasUsableTarget());
        }

        /// <summary>IMGUI / SceneView 側の操作で変わる状態を UI Toolkit 表示へ反映する（250ms ごと）。</summary>
        private void OnUiPoll()
        {
            UpdateAnimSectionsVisibility();
            UpdatePoseSectionsVisibility();
            UpdateSectionCards();
            _fxSetupUI.PollUI(_currentMode == EditorMode.FxSetup && HasUsableTarget());
        }

        /// <summary>ブレンドシェイプを持つ対象メッシュが指定されているか（対象メッシュカードと同条件）。</summary>
        private bool HasUsableTarget()
        {
            if (_model.TargetSkinnedMesh == null) return false;
            foreach (var smr in GetAllTargetMeshes())
                if (smr != null && smr.sharedMesh != null && smr.sharedMesh.blendShapeCount > 0)
                    return true;
            return false;
        }

        /// <summary>対象メッシュの有無で Animation モード内のセクション表示を切り替える。</summary>
        private void UpdateAnimSectionsVisibility()
        {
            if (_animClipCard == null) return;
            bool hasTarget = HasUsableTarget();
            var display = hasTarget ? DisplayStyle.Flex : DisplayStyle.None;
            _animClipCard.style.display = display;
            _animListHost.style.display = display;
            _animSaveCard.style.display = display;
            // 検索カードは共有のため、現在モードの側だけが表示を制御する
            if (_currentMode == EditorMode.Animation)
                _searchCard.style.display = display;
            // 補正カードの表示は CorrectionUI.Refresh がクリップの有無も見て決める

            // タイムライン: 別窓化中は通知カード、それ以外は IMGUI 描画（クリップ未設定時は高さ 0）
            bool detached = DenEmoTimelineWindow.Instance != null;
            _animTimelineNotice.style.display = (hasTarget && detached)  ? DisplayStyle.Flex : DisplayStyle.None;
            _animTimelineGui.style.display    = (hasTarget && !detached) ? DisplayStyle.Flex : DisplayStyle.None;

            // REC 中バナー（録画状態はタイムライン内の IMGUI 操作で変わるためポーリングで反映）
            bool showRecBanner = hasTarget && _animModeUI.IsRecording && _animModeUI.ClipModel.Clip != null;
            _animRecBanner.style.display = showRecBanner ? DisplayStyle.Flex : DisplayStyle.None;

            UpdateListAnimContext();
        }

        /// <summary>対象メッシュの有無で Pose モード内のセクション表示を切り替える。</summary>
        private void UpdatePoseSectionsVisibility()
        {
            if (_poseListHost == null) return;
            var display = HasUsableTarget() ? DisplayStyle.Flex : DisplayStyle.None;
            _poseSourceCard.style.display = display;
            _poseListHost.style.display   = display;
            _poseSaveCard.style.display   = display;
            // 検索カードは共有のため、現在モードの側だけが表示を制御する
            if (_currentMode == EditorMode.Pose)
                _searchCard.style.display = display;
        }

        /// <summary>
        /// シェイプキーリストへ Animation 描画コンテキストを供給する。
        /// クリップの有無・トラックフィルターが変わるためポーリングで毎回更新する。
        /// </summary>
        private void UpdateListAnimContext()
        {
            bool animWithClip = _currentMode == EditorMode.Animation && _animModeUI.ClipModel.Clip != null;
            _listUI.SetAnimContext(animWithClip ? _animModeUI.BuildDrawContext(_model) : null);
        }

        private void UpdateStatusBar()
        {
            if (_statusLabel == null) return;
            string icon = statusLevel switch
            {
                1 => "✓ ",
                2 => "⚠ ",
                3 => "✕ ",
                _ => "",
            };
            string text = string.IsNullOrEmpty(statusMessage)
                ? DenEmoLoc.T("status.ready")
                : statusMessage;
            _statusLabel.text = icon + text;
            _statusLabel.EnableInClassList("dennoko-status--success", statusLevel == 1);
            _statusLabel.EnableInClassList("dennoko-status--warning", statusLevel == 2);
            _statusLabel.EnableInClassList("dennoko-status--error",   statusLevel == 3);
        }

        // Animation モード: タイムライン（anim-timeline-imgui。IMGUI カプセル化）
        private void OnAnimTimelineGUI()
        {
            DenEmoTheme.Initialize();
            DenEmoTheme.PushEditorTheme();
            try
            {
                _animModeUI.DrawTimeline(_model, this);
            }
            finally
            {
                DenEmoTheme.PopEditorTheme();
            }
        }

        // 検索・フィルター変更の反映と Include フラグの遅延保存（OnEditorUpdate から呼ぶ）
        private void TickListMaintenance()
        {
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

        // ─── Mode switching ───────────────────────────────────────────────────

        private void SwitchMode(EditorMode mode)
        {
            if (_currentMode == mode) return;

            if (_currentMode == EditorMode.Animation)
            {
                _animModeUI.StopPreview();
                if (snapshotValues != null && snapshotValues.Count > 0)
                    RestoreSnapshot();
            }
            else if (_currentMode == EditorMode.FxSetup)
            {
                _fxSetupUI.OnExit();
                wantsMouseMove = false;
                wantsMouseEnterLeaveWindow = false;
            }

            _currentMode = mode;

            if (_currentMode == EditorMode.Animation)
            {
                CreateSnapshot(false);
                if (_animModeUI.ClipModel.Clip != null && _model.TargetSkinnedMesh != null)
                    _animModeUI.StartPreview(_model);
            }
            else if (_currentMode == EditorMode.FxSetup)
            {
                wantsMouseMove = true;
                wantsMouseEnterLeaveWindow = true;
                _fxSetupUI.OnEnter(_model);
            }

            UpdateModeTabVisuals();
            UpdateModeContentVisibility();
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
            _listUI.MarkStructureDirty();
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
            UpdateStatusBar();
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
                UpdateStatusBar();
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
