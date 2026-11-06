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
        // 上書き保存時に書き込み先の blendShape 以外のカーブを残す
        private bool preserveExtraCurves = true;
        // アニメーション参照の読み込み時に blendShape 以外のカーブも取り込む
        private bool importExtraCurves   = true;
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
        private bool _hasManuallyCleared = false;

        // -1 = All targets, 0+ = GetAllTargetMeshes()[index]
        private int _meshFilterIndex = -1;

        private AnimationClip loadedClip          = null;
        private AnimationClip overwriteTargetClip = null;

        // 「アニメーションを読み込む」で取り込んだ blendShape 以外のカーブ。保存時に書き出す。
        private DenEmo.Core.ExtraCurveSet _importedExtraCurves = null;

        private HashSet<string> collapsedGroups = new HashSet<string>();

        private string statusMessage      = null;
        private int    statusLevel        = 0;
        private double statusSetAt        = 0;
        private double statusAutoClearSec = 0;

        private bool   includeFlagsDirty         = false;
        private double lastIncludeFlagsChangeTime = 0;
        private Dictionary<string, float> snapshotValues = null;

        // ─── UI Toolkit chrome (CreateGUI で構築) ────────────────────────────
        private const string ToolName          = "DenEmo";
        private const string DenEmoManualUrl   = "https://github.com/dennoko/DenEmo";
        private const string DefaultReleaseUrl = "https://github.com/dennoko/DenEmo/releases";

        private DennokoStatus _status;
        private DennokoAppBar _appBar;
        private Label  _versionLabel;
        // LocalVersion は OnEnable で設定する（コンストラクタ／フィールド初期化子から
        // AssetDatabase.GUIDToAssetPath を呼ぶと Unity が例外を投げるため）。
        private Dennoko.DennokoVersionChecker.Result _versionResult =
            new Dennoko.DennokoVersionChecker.Result { State = Dennoko.DennokoVersionChecker.State.Checking };
        private Button _tabPose;
        private Button _tabAnim;
        private Button _tabFx;

        // Pose モード用コンテンツ（pose-page 配下: 上部スクロール + リストの置き場所）
        private VisualElement  _posePage;
        private ScrollView     _poseScroll;
        private VisualElement  _poseTargetHost;
        private VisualElement  _poseListHost;

        // Animation モード用コンテンツ（anim-page 配下）
        private VisualElement  _animPage;
        private VisualElement  _animTargetHost;
        private VisualElement    _animTimelineHost;
        private TimelineUITKView _animTimelineView;
        private VisualElement  _animListHost;
        private VisualElement  _animClipCard;
        private VisualElement  _animTimelineNotice;
        private Label          _animTimelineNoticeLabel;
        private Button         _animTimelineFocus;
        private Button         _animTimelineClose;
        // 録画中バナー（タブの直下に固定）
        private VisualElement  _animRecBanner;
        private Label          _animRecBannerState;
        private Label          _animRecBannerLabel;
        private Button         _animRecStop;

        // FX モード用コンテンツ（fx-page 配下: 上部スクロール + 差し替え一覧。カード内容は FxSetupModeUI がバインド）
        private VisualElement  _fxPage;
        private VisualElement  _fxTargetHost;

        // ─── Lifecycle ───────────────────────────────────────────────────────

        public static DenEmoWindow Instance { get; private set; }

        private void OnEnable()
        {
            Instance = this;
            _versionResult.LocalVersion = DenEmoVersion.Current;
            DenEmoLoc.LoadPrefs();
            saveFolder           = DenEmoProjectPrefs.GetString("DenEmo_SaveFolder", saveFolder);
            searchText           = DenEmoProjectPrefs.GetString("DenEmo_SearchText", string.Empty);
            showOnlyIncluded     = DenEmoProjectPrefs.GetBool("DenEmo_ShowOnlyIncluded",  false);
            showOnlyNonZero      = DenEmoProjectPrefs.GetBool("DenEmo_ShowOnlyNonZero",   false);
            showOnlyFavorites    = DenEmoProjectPrefs.GetBool("DenEmo_ShowOnlyFavorites", false);
            symmetryMode         = DenEmoProjectPrefs.GetBool("DenEmo_SymmetryMode",      false);
            autoBackup           = DenEmoProjectPrefs.GetBool("DenEmo_AutoBackup",        true);
            overwriteSaveEnabled = DenEmoProjectPrefs.GetBool("DenEmo_OverwriteSaveEnabled", false);
            preserveExtraCurves  = DenEmoProjectPrefs.GetBool("DenEmo_PreserveExtraCurves", true);
            importExtraCurves    = DenEmoProjectPrefs.GetBool("DenEmo_ImportExtraCurves",   true);
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
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            SetStatus(DenEmoLoc.T("status.ready"), 0, 0);
            LoadCollapsedGroupsPrefs();

            _currentMode = (EditorMode)DenEmoProjectPrefs.GetInt("DenEmo_Mode", 0);
            if (!System.Enum.IsDefined(typeof(EditorMode), _currentMode))
                _currentMode = EditorMode.Pose; // 旧バージョンの無効なモード（削除された実験タブ等）への保険
            _model.IsAnimationMode = _currentMode == EditorMode.Animation;

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
            _listUI.IsUnchanged       = IsShapeKeyUnchanged;
            _listUI.GetClipDiffChecker     = BuildClipDiffChecker;
            _listUI.GetSnapshotDiffChecker = BuildSnapshotDiffChecker;
            _listUI.OnPickFromSelection    = PickTargetFromSelection;
            _listUI.OnClearFilters         = ClearAllFilters;
            TryAutoSelectBodyMesh();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.update -= OnEditorUpdate;
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            _listUI.StopThrottle();
            _animModeUI.OnDisable();
            _fxSetupUI.OnDisable();
            vertexPickMode = false;
            ClearVertexGuideCache();

            if (includeFlagsDirty || _model.Items.Count > 0) SaveIncludeFlagsPrefsImmediate();

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
            DenEmoProjectPrefs.SetBool("DenEmo_PreserveExtraCurves",  preserveExtraCurves);
            DenEmoProjectPrefs.SetBool("DenEmo_ImportExtraCurves",    importExtraCurves);
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

            SaveSnapshotPrefs();

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
            UpdateVisibility();
            _listUI.SyncNow();
            if (_currentMode == EditorMode.Animation)
            {
                _animModeUI.OnUndoRedo();
                _animTimelineView?.RefreshStructure();
            }
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

            _tabPose     = root.Q<Button>("tab-pose");
            _tabAnim     = root.Q<Button>("tab-anim");
            _tabFx       = root.Q<Button>("tab-fx");
            _versionLabel = root.Q<Label>("version-label");
            // Ready への自動復帰は TickStatusAutoClear が現在の言語で行うため、DennokoStatus 側の自動復帰は使わない
            _status = new DennokoStatus(root.Q<Label>("status-label"), DenEmoLoc.T("status.ready"), int.MaxValue);

            _posePage       = root.Q<VisualElement>("pose-page");
            _poseScroll     = root.Q<ScrollView>("pose-scroll");
            _poseTargetHost = root.Q<VisualElement>("pose-target-host");
            _poseListHost   = root.Q<VisualElement>("pose-list-host");

            _animPage       = root.Q<VisualElement>("anim-page");
            _animTargetHost = root.Q<VisualElement>("anim-target-host");
            _animTimelineHost = root.Q<VisualElement>("anim-timeline-host");
            _animListHost   = root.Q<VisualElement>("anim-list-host");
            _animClipCard   = root.Q<VisualElement>("anim-clip-card");

            _animTimelineNotice      = root.Q<VisualElement>("anim-timeline-notice");
            _animTimelineNoticeLabel = root.Q<Label>("anim-timeline-notice-label");
            _animTimelineFocus       = root.Q<Button>("anim-timeline-focus");
            _animTimelineClose       = root.Q<Button>("anim-timeline-close");
            _animRecBanner           = root.Q<VisualElement>("anim-rec-banner");
            _animRecBannerState      = root.Q<Label>("anim-rec-banner-state");
            _animRecBannerLabel      = root.Q<Label>("anim-rec-banner-label");
            _animRecStop             = root.Q<Button>("anim-rec-stop");

            _fxPage       = root.Q<VisualElement>("fx-page");
            _fxTargetHost = root.Q<VisualElement>("fx-target-host");

            // シェイプキーリスト（B4 リストカード。検索行・絞り込みチップを含む）。モード切替時にホスト間を移動する
            var listRoot = _listUI.Bind(_model, collapsedGroups, () => symmetryMode);
            _poseListHost.Add(listRoot);

            // セクションカード（対象メッシュ / 参照 / 検索 / 保存）と標準フッターの配線
            BindSectionCards(root, listRoot);
            BindFooter(root);
            _fxSetupUI.BindUI(root, _model, () => saveFolder, this);
            RegisterRootDragAndDrop(root);

            _animTimelineFocus.clicked += () => DenEmoTimelineWindow.ShowWindow();
            _animTimelineClose.clicked += () =>
            {
                if (HasOpenInstances<DenEmoTimelineWindow>())
                    GetWindow<DenEmoTimelineWindow>().Close();
            };
            _animRecStop.clicked += () =>
            {
                _animModeUI.IsRecording = false;
                UpdateAnimSectionsVisibility();
                Repaint();
            };

            // Animation モードのタイムライン（UI Toolkit）。クリップカードがあるためクリップ欄は非表示
            _animTimelineView = new TimelineUITKView();
            _animTimelineHost.Add(_animTimelineView.Build(
                _animModeUI, _model, this,
                showClipField: false, isSeparateWindow: false, requireZoomModifier: true));

            _animModeUI.BindClipSectionUI(_animClipCard, _model, () => saveFolder, this);
            _animModeUI.CorrectionUI.Bind(
                root.Q<VisualElement>("anim-correction-card"),
                _animModeUI.ClipModel, _animModeUI.Editor, _animModeUI.Preview,
                HasUsableTarget, (msg, lvl) => SetStatus(msg, lvl), this);

            // 対象メッシュの有無・IMGUI / SceneView 側で変わる状態はポーリングで表示に反映する
            root.schedule.Execute(OnUiPoll).Every(250);

            BindAppBar(root);
            _tabPose.clicked += () => SwitchMode(EditorMode.Pose);
            _tabAnim.clicked += () => SwitchMode(EditorMode.Animation);
            _tabFx.clicked   += () => SwitchMode(EditorMode.FxSetup);

            RefreshChromeLabels();
            UpdateModeTabVisuals();
            UpdateModeContentVisibility();
            UpdateStatusBar();

            StartVersionCheck();
            TryAutoSelectBodyMesh();
        }

        /// <summary>標準ヘッダー（トップバー）を配線する。言語・マニュアル・⋮ メニュー・↻ は DennokoAppBar が扱う。</summary>
        private void BindAppBar(VisualElement root)
        {
            _appBar = new DennokoAppBar(root, ToolName, DenEmoLoc.EnglishMode)
            {
                ManualUrl  = DenEmoManualUrl,
                ReleaseUrl = DefaultReleaseUrl,
                // 全設定のリセット機能は DenEmo に無いため ResetAll は付けない（⋮ に項目を出さない）
            };
            _appBar.LanguageChanged += english =>
            {
                DenEmoLoc.EnglishMode = english;
                RefreshChromeLabels();
                Repaint();
            };
            _appBar.CheckUpdateRequested += () =>
            {
                // 明示的に再取得する。前回の結果（成功/失敗）を破棄して再チェックし、
                // 即座に「確認中...」表示へ切り替える。完了時に OnVersionChecked から再描画される。
                DenEmoVersion.ForceRecheck();
                LoadVersionResultFromSessionState();
            };
        }

        // ─── バージョン表記 / アップデートチェック ───────────────────────────

        /// <summary>起動時にリモートの version.json を取得して更新有無を判定する。
        /// 同一 Unity セッション中はキャッシュ結果を再利用しネットワークアクセスを抑制する。</summary>
        private void StartVersionCheck()
        {
            LoadVersionResultFromSessionState();
            // 取得の要否は StartCheckBackgroundTask 内で判定する（成功済みなら何もしない／
            // 前回エラーなら再試行）。ウィンドウを開き直すたびに一時的な失敗から自己回復できる。
            DenEmoVersion.StartCheckBackgroundTask();
        }

        internal void LoadVersionResultFromSessionState()
        {
            // State（更新有無）はキャッシュせず、常に「現在のローカル版 vs 取得済みの最新版」で
            // 再計算する。こうしないと、取得時のローカル版が後から正しく解決された場合に
            // 「v3.0.0 更新あり 3.0.0」のような矛盾表示が残ってしまう。
            string local = DenEmoVersion.Current;
            string latest = SessionState.GetString(DenEmoVersion.VerCheckLatestKey, string.Empty);
            bool done = SessionState.GetBool(DenEmoVersion.VerCheckDoneKey, false);
            bool error = SessionState.GetBool(DenEmoVersion.VerCheckErrorKey, false);

            Dennoko.DennokoVersionChecker.State state;
            if (!done)
                state = Dennoko.DennokoVersionChecker.State.Checking;
            else if (error || string.IsNullOrEmpty(latest))
                state = Dennoko.DennokoVersionChecker.State.Error;
            else if (Dennoko.DennokoVersionChecker.IsUpdateAvailable(latest, local))
                state = Dennoko.DennokoVersionChecker.State.UpdateAvailable;
            else
                state = Dennoko.DennokoVersionChecker.State.UpToDate;

            _versionResult = new Dennoko.DennokoVersionChecker.Result
            {
                State = state,
                LocalVersion = local,
                LatestVersion = latest,
                Url = SessionState.GetString(DenEmoVersion.VerCheckUrlKey, string.Empty),
                Message = SessionState.GetString(DenEmoVersion.VerCheckMessageKey, string.Empty)
            };
            ApplyVersionLabel();
        }

        /// <summary>最後に受け取ったチェック結果を現在の言語でバージョンラベルへ反映する。</summary>
        private void ApplyVersionLabel()
        {
            if (_versionLabel == null) return;

            var r = _versionResult;
            string baseText = "v" + r.LocalVersion;
            string text;
            bool update = false, error = false;
            switch (r.State)
            {
                case Dennoko.DennokoVersionChecker.State.UpdateAvailable:
                    text = baseText + "  " + DenEmoLoc.Tf("ui.version.update", r.LatestVersion);
                    update = true;
                    break;
                case Dennoko.DennokoVersionChecker.State.Error:
                    text = baseText + "  " + DenEmoLoc.T("ui.version.error");
                    error = true;
                    break;
                case Dennoko.DennokoVersionChecker.State.Checking:
                    text = baseText + "  " + DenEmoLoc.T("ui.version.checking");
                    break;
                default: // UpToDate
                    text = baseText;
                    break;
            }
            _versionLabel.text = text;
            _versionLabel.EnableInClassList("dennoko-version-label--update", update);
            _versionLabel.EnableInClassList("dennoko-version-label--error", error);
            _versionLabel.EnableInClassList("dennoko-version-label--checking",
                r.State == Dennoko.DennokoVersionChecker.State.Checking);

            if (_appBar != null)
            {
                // 更新ありのときだけラベルのクリックで配布ページを開く（version.json の url を優先）
                _appBar.ReleaseUrl = !string.IsNullOrEmpty(r.Url) ? r.Url : DefaultReleaseUrl;
                _appBar.SetVersionClickable(update, update ? DenEmoLoc.T("ui.version.clickable.tip") : null);
            }
        }

        /// <summary>言語設定に依存するウィンドウ外枠のラベルを更新する。</summary>
        private void RefreshChromeLabels()
        {
            if (_appBar == null) return;
            _appBar.TextResetAll     = DenEmoLoc.T("ui.appbar.resetAll");
            _appBar.TextResetConfirm = DenEmoLoc.T("ui.appbar.resetConfirm");
            _appBar.TextResetOk      = DenEmoLoc.T("ui.appbar.resetOk");
            _appBar.TextCancel       = DenEmoLoc.T("dlg.cancel");
            _appBar.TextCheckUpdate  = DenEmoLoc.T("ui.appbar.checkUpdate");
            _appBar.TextOpenRelease  = DenEmoLoc.T("ui.appbar.openRelease");
            _appBar.TextManual       = DenEmoLoc.T("ui.appbar.manual");
            _appBar.TextSettings     = DenEmoLoc.T("ui.appbar.settings");
            _appBar.RefreshTexts(DenEmoLoc.EnglishMode);
            ApplyVersionLabel();
            _tabPose.text    = DenEmoLoc.T("ui.animMode.tab.pose");
            _tabAnim.text    = DenEmoLoc.T("ui.animMode.tab.anim");
            _tabFx.text      = DenEmoLoc.T("ui.fx.tab");
            _animModeUI.RefreshClipSectionLabels();
            _listUI.RefreshLabels();
            RefreshSectionLabels();
            _fxSetupUI.RefreshLabels();
            _animTimelineView?.RefreshLabels();
            _animTimelineNoticeLabel.text = DenEmoLoc.T("ui.timeline.separate.notice");
            _animTimelineFocus.text       = DenEmoLoc.T("ui.timeline.separate.focus");
            _animTimelineClose.text       = DenEmoLoc.T("ui.timeline.separate.close");
            _animRecBannerState.text      = DenEmoLoc.T("ui.animMode.rec.state");
            _animRecBannerLabel.text      = DenEmoLoc.T("ui.animMode.rec.hint");
            _animRecBannerLabel.tooltip   = DenEmoLoc.T("ui.animMode.rec.hint");
            _animRecStop.text             = DenEmoLoc.T("ui.animMode.rec.stop");
            _animRecStop.tooltip          = DenEmoLoc.T("ui.animMode.rec.stop.tip");
            UpdateStatusBar();
        }

        private void UpdateModeTabVisuals()
        {
            if (_tabPose == null) return;
            // 選択中は dennoko-button-active（テーマ側で枠が info の青になる）
            _tabPose.EnableInClassList("dennoko-button-active", _currentMode == EditorMode.Pose);
            _tabAnim.EnableInClassList("dennoko-button-active", _currentMode == EditorMode.Animation);
            _tabFx.EnableInClassList("dennoko-button-active",   _currentMode == EditorMode.FxSetup);
        }

        /// <summary>モードに応じて Pose / Animation / FX 用コンテンツの表示を切り替える。</summary>
        private void UpdateModeContentVisibility()
        {
            if (_fxPage == null || _animPage == null || _posePage == null) return;
            bool pose = _currentMode == EditorMode.Pose;
            bool anim = _currentMode == EditorMode.Animation;
            bool fx   = _currentMode == EditorMode.FxSetup;
            DennokoBlockUI.Show(_posePage, pose);
            DennokoBlockUI.Show(_animPage, anim);
            DennokoBlockUI.Show(_fxPage, fx);

            // モード間で共有する要素を現在モードのホストへ移動する（リストカードは検索行ごと移動する）
            var listRoot = _listUI.Root;
            if (listRoot != null)
            {
                var host = anim ? _animListHost : _poseListHost;
                if (listRoot.parent != host) host.Add(listRoot);
            }
            var targetHost = pose ? _poseTargetHost : anim ? _animTargetHost : _fxTargetHost;
            if (_targetCard.parent != targetHost) targetHost.Add(_targetCard);

            UpdateAnimSectionsVisibility();
            UpdatePoseSectionsVisibility();
            UpdateSectionCards();
            UpdateFooter();
            _fxSetupUI.PollUI(fx, HasUsableTarget());
        }

        /// <summary>IMGUI / SceneView 側の操作で変わる状態を UI Toolkit 表示へ反映する（250ms ごと）。</summary>
        private void OnUiPoll()
        {
            UpdateAnimSectionsVisibility();
            UpdatePoseSectionsVisibility();
            UpdateSectionCards();
            UpdateFooter();
            _fxSetupUI.PollUI(_currentMode == EditorMode.FxSetup, HasUsableTarget());
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
            DennokoBlockUI.Show(_animClipCard, hasTarget);
            // リストカードは対象が無くても表示し、空の状態（「選択から」）を出す
            // 補正カードの表示は CorrectionUI.Refresh がクリップの有無も見て決める

            // タイムライン: 別窓化中は通知カード、それ以外は UI Toolkit タイムライン（クリップ未設定時は非表示）
            bool detached = DenEmoTimelineWindow.Instance != null;
            bool hasClip  = _animModeUI.ClipModel.Clip != null;
            bool showInlineTimeline = hasTarget && !detached && hasClip;
            DennokoBlockUI.Show(_animTimelineNotice, hasTarget && detached);
            DennokoBlockUI.Show(_animTimelineHost, showInlineTimeline);
            _animTimelineView?.SetActive(_currentMode == EditorMode.Animation && showInlineTimeline);

            // 録画中バナー（タブの直下に固定。録画状態はタイムライン内の操作で変わるためポーリングで反映）
            bool showRecBanner = _currentMode == EditorMode.Animation && hasTarget && _animModeUI.IsRecording && hasClip;
            DennokoBlockUI.Show(_animRecBanner, showRecBanner);

            UpdateListAnimContext();
        }

        /// <summary>対象メッシュの有無で Pose モード内のセクション表示を切り替える。</summary>
        private void UpdatePoseSectionsVisibility()
        {
            if (_poseSourceCard == null) return;
            // 保存設定カードは対象が無くても表示する（先に保存先だけ決めたい場合がある）
            DennokoBlockUI.Show(_poseSourceCard, HasUsableTarget());
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

        /// <summary>ステータスバー（DennokoStatus）へ現在のメッセージを反映する。空なら現在の言語の Ready。</summary>
        private void UpdateStatusBar()
        {
            if (_status == null) return;
            string text = string.IsNullOrEmpty(statusMessage)
                ? DenEmoLoc.T("status.ready")
                : statusMessage;
            var type = statusLevel switch
            {
                1 => DennokoStatusType.Success,
                2 => DennokoStatusType.Warning,
                3 => DennokoStatusType.Error,
                _ => DennokoStatusType.Info,
            };
            _status.Set(text, type);
        }

        // 別ウィンドウ（DenEmoTimelineWindow）が共有する Animation 状態
        internal AnimationModeUI SeparateTimelineMode => _animModeUI;
        internal ShapeKeyModel   SeparateTimelineModel => _model;
        // 別ウィンドウのタイムラインはプレビュー整合のため Animation モード時のみ有効
        internal bool IsAnimationModeActive => _currentMode == EditorMode.Animation;

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
            _model.IsAnimationMode = _currentMode == EditorMode.Animation;
            _model.BuildGroups();

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
            UpdateVisibility();
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
            VrcBindingExclusionRule.ApplyExclusion(_model.TargetSkinnedMesh, _model.Items);
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
                if (item.IsVrcExcluded(_model.IsAnimationMode)) { item.IsIncluded = false; continue; }
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
            // Ready は言語切替に追従させるため、文言ではなく「メッセージなし」として持つ
            if (level == 0 && msg == DenEmoLoc.T("status.ready")) msg = null;
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

        private bool IsShapeKeyUnchanged(ShapeKeyItem item)
        {
            if (_currentMode == EditorMode.Animation)
            {
                var clip = _animModeUI.ClipModel.Clip;
                if (clip != null)
                {
                    var binding = new EditorCurveBinding
                    {
                        type = typeof(SkinnedMeshRenderer),
                        path = !string.IsNullOrEmpty(item.SmrPath) ? item.SmrPath : ShapeKeyModel.ComputeSmrPath(item.OwnerSmr ?? _model.TargetSkinnedMesh),
                        propertyName = "blendShape." + item.Name
                    };
                    var curve = AnimationUtility.GetEditorCurve(clip, binding);
                    if (curve != null)
                    {
                        foreach (var key in curve.keys)
                        {
                            if (!Mathf.Approximately(key.value, 0f))
                                return false;
                        }
                    }
                    return true; // No curve or all keys are 0
                }
            }
            return Mathf.Approximately(item.Value, 0f);
        }

        /// <summary>
        /// アニメーション参照で読み込んだクリップとの差分判定を返す。
        /// クリップ未収録のシェイプは初期値 0 と比較する。クリップ未設定時は null。
        /// </summary>
        private Func<ShapeKeyItem, bool> BuildClipDiffChecker()
        {
            if (loadedClip == null) return null;

            var baseline = new Dictionary<string, float>();
            foreach (var b in AnimationUtility.GetCurveBindings(loadedClip))
            {
                if (b.type != typeof(SkinnedMeshRenderer)) continue;
                if (!b.propertyName.StartsWith("blendShape.")) continue;
                var curve = AnimationUtility.GetEditorCurve(loadedClip, b);
                if (curve == null) continue;
                baseline[b.propertyName.Substring("blendShape.".Length)] = curve.Evaluate(0f);
            }

            return item =>
            {
                baseline.TryGetValue(item.Name, out float baseVal);
                return !Mathf.Approximately(item.Value, baseVal);
            };
        }

        private void OnHierarchyChanged()
        {
            _hasManuallyCleared = false;
            TryAutoSelectBodyMesh();
        }

        private void TryAutoSelectBodyMesh()
        {
            if (_model.TargetSkinnedMesh != null || _hasManuallyCleared) return;

            var bodyMesh = FindBodyMeshInSingleActiveAvatar();
            if (bodyMesh != null)
            {
                if (_targetMainField != null)
                {
                    SetMainTargetFromUI(bodyMesh);
                }
                else
                {
                    _model.SetTarget(bodyMesh);
                    RefreshListAndCache();
                }
            }
        }

        private SkinnedMeshRenderer FindBodyMeshInSingleActiveAvatar()
        {
            var activeAvatars = new List<GameObject>();
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var rootGo in scene.GetRootGameObjects())
                {
                    if (rootGo == null || !rootGo.activeInHierarchy) continue;

                    var comps = rootGo.GetComponentsInChildren<Component>(false);
                    foreach (var c in comps)
                    {
                        if (c == null) continue;
                        var t = c.GetType();
                        var name = t.Name;
                        var full = t.FullName;
                        if (name == "VRC_AvatarDescriptor" || name == "VRCAvatarDescriptor" ||
                            (full != null && (full.Contains("VRC_AvatarDescriptor") || full.Contains("VRCAvatarDescriptor"))))
                        {
                            var avatarGo = c.gameObject;
                            if (!activeAvatars.Contains(avatarGo))
                            {
                                activeAvatars.Add(avatarGo);
                            }
                        }
                    }
                }
            }

            if (activeAvatars.Count == 1)
            {
                var avatarGo = activeAvatars[0];
                var smrs = avatarGo.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                foreach (var smr in smrs)
                {
                    if (smr != null && string.Equals(smr.name, "Body", StringComparison.OrdinalIgnoreCase))
                    {
                        return smr;
                    }
                }
            }

            return null;
        }
    }
}
