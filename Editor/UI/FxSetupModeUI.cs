using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using DenEmo.Core;
using DenEmo.Models;

namespace DenEmo.UI
{
    /// <summary>
    /// 「アバターへ適用」モードのオーケストレータ。
    /// アバター/FX レイヤーの自動検出 → 表情アニメーション一覧 → 差し替え先の割当て（マッピング）→
    /// 一括差し替え、までを 1 画面で行う。マッピングはセッション限定で永続化しない。
    /// 画面は P3（一覧編集）: DenEmoWindow.uxml の fx-page（上部 = 対象メッシュ + fx-avatar-card、
    /// 伸びる領域 = fx-list-card、その下 = fx-apply-card）へバインドし、主ボタン（差し替え）は
    /// DenEmoWindow.FxFooter で標準フッターに出す。
    /// </summary>
    public class FxSetupModeUI
    {
        public System.Action<string, int> StatusSink;

        // ─── 検出状態 ─────────────────────────────────────────────────────────
        private Component          _descriptor;
        private AnimatorController _controller;        // 実際にスキャン/書き換えする対象
        private bool _fxMissing;            // Descriptor はあるが FX が未設定
        private bool _fxReadFailed;         // baseAnimationLayers を読めなかった（SDK 差異）
        private bool _fxOverrideUnsupported;
        private Transform _avatarRoot;
        private readonly HashSet<string> _targetSmrPaths = new HashSet<string>();

        // ─── 一覧・マッピング ─────────────────────────────────────────────────
        private List<FxExpressionEntry> _entries = new List<FxExpressionEntry>();
        private readonly Dictionary<AnimationClip, FxMapping> _mappings = new Dictionary<AnimationClip, FxMapping>();
        private readonly HashSet<AnimationClip> _expanded = new HashSet<AnimationClip>();
        private string _search = string.Empty;
        private bool   _showOnlyAssigned;
        private bool   _showAllClips;       // メッシュパスフィルタ解除（0件時の救済）
        private int _filterHand    = 0;  // 0 = 指定なし, 1 = 左手, 2 = 右手
        private int _filterGesture = -1; // -1 = 指定なし, 0〜7 = GestureTraceUtil.GestureNames の番号

        // ─── プレビュー / 適用 ────────────────────────────────────────────────
        private readonly FxHoverPreview _hover = new FxHoverPreview();
        private bool            _pickerOpen;      // ピッカー表示中はメイン側のホバー判定を止める
        private bool            _applyDirect;     // false = 複製モード（推奨）
        private FxReplaceResult _lastResult;
        private string          _pickerFolder;
        private bool            _hasTarget;       // 対象メッシュ（シェイプキーあり）が指定されているか（ポーリングで更新）
        private string          _footerSignature;

        // ─── UI Toolkit 要素 ─────────────────────────────────────────────────
        // 上部: アバター / FX カード（B1 相当）
        private ScrollView    _upperScroll;
        private VisualElement _avatarCard;
        private Label         _avatarTitle;
        private ObjectField   _avatarField;
        private ObjectField   _controllerField;
        private Button        _rescanButton;
        private VisualElement _infoRow;
        private Label         _statusLabel;
        private HelpBox       _problemBox;
        private VisualElement _resultBox;
        private Label         _resultSuccessLabel;
        private VisualElement _resultControllerRow;
        private Label         _resultControllerLabel;
        private Button        _resultPingButton;
        private Label         _resultNoteLabel;
        private Label         _resultBackupLabel;

        // 伸びる領域: 差し替え一覧（B4 リストカード）
        private VisualElement _listSlot;
        private Label         _listTitle;
        private Button        _assignedChip;
        private TextField     _searchField;
        private Label         _searchPlaceholder;
        private Button        _searchClear;
        private Label         _searchCount;
        private VisualElement _gestureFilterRow;
        private Button        _gfLeftChip;
        private Button        _gfRightChip;
        private readonly Button[] _gfGestureChips = new Button[8];
        private ScrollView    _entryList;
        private VisualElement _emptyBox;
        private Label         _emptyMessage;
        private Button        _emptyAction;
        private VisualElement _noMatchBox;
        private Label         _noMatchMessage;
        private Button        _noMatchClear;
        private Label         _hoverHintLabel;
        private Label         _hoverPlayingLabel;
        private Button        _hoverStopButton;

        // 一覧の下: 適用方法（B2 設定カード）
        private VisualElement _applyCard;
        private Label         _modeLabel;
        private Button        _modeDuplicateChip;
        private Button        _modeDirectChip;
        private System.Action<int> _selectMode;
        private Label         _modeDescLabel;
        private VisualElement _manualNote;
        private Label         _manualNoteText;

        private ShapeKeyModel        _boundModel;
        private EditorWindow         _window;
        private DenEmoWindow         _denWindow;      // 標準フッター（FxFooter）の持ち主
        private System.Func<string>  _getSaveFolder;

        // ホバープレビューの解決用（行/スロットのどちらに乗っているか）
        private FxExpressionEntry _hoverRowEntry;
        private FxExpressionEntry _hoverSlotEntry;
        private readonly List<(FxExpressionEntry entry, VisualElement row)> _rowElements
            = new List<(FxExpressionEntry, VisualElement)>();

        public FxHoverPreview Hover => _hover;
        public string PickerFolder
        {
            get => _pickerFolder;
            set => _pickerFolder = value;
        }
        public void SetPickerOpen(bool open) => _pickerOpen = open;

        // ─── ライフサイクル ───────────────────────────────────────────────────

        public void OnEnter(ShapeKeyModel model)
        {
            _applyDirect = DenEmoProjectPrefs.GetBool("DenEmo_Fx_ApplyMode_Direct", false);
            _lastResult  = null;
            if (_avatarField == null || (_avatarField.value == null && _controllerField.value == null))
            {
                AutoDetect(model);
            }
            else if (_avatarField != null && _avatarField.value == null)
            {
                AutoDetect(model);
            }
            ScanAndRebuild(model);
        }

        public void OnExit()
        {
            _hover.Stop();
            _hoverRowEntry  = null;
            _hoverSlotEntry = null;
            DenEmoProjectPrefs.SetBool("DenEmo_Fx_ApplyMode_Direct", _applyDirect);
        }

        public void OnDisable() => OnExit();

        public void Tick(EditorWindow window) => _hover.Tick();

        public void OnUndoRedo(ShapeKeyModel model)
        {
            ScanAndRebuild(model);
        }

        public void OnTargetChanged(ShapeKeyModel model)
        {
            _hover.Stop();
            _mappings.Clear();
            _lastResult = null;
            AutoDetect(model);
            ScanAndRebuild(model);
        }

        /// <summary>アバター/FX の再検出と一覧の再スキャン。</summary>
        public void Refresh(ShapeKeyModel model)
        {
            AutoDetect(model);
            ScanAndRebuild(model);
        }

        private void AutoDetect(ShapeKeyModel model)
        {
            _descriptor = null;
            _avatarRoot = null;
            _controller = null;
            _fxMissing = false;
            _fxReadFailed = false;
            _fxOverrideUnsupported = false;

            var target = model?.TargetSkinnedMesh;
            if (target == null)
            {
                if (_avatarField != null) _avatarField.SetValueWithoutNotify(null);
                if (_controllerField != null) _controllerField.SetValueWithoutNotify(null);
                return;
            }

            _descriptor = VrcAvatarReflection.FindDescriptor(target.transform);
            _avatarRoot = _descriptor != null ? _descriptor.transform : target.transform.root;

            if (_descriptor != null)
            {
                if (!VrcAvatarReflection.TryGetFxController(_descriptor, out var rc, out bool isDefault))
                {
                    _fxReadFailed = true;
                }
                else if (rc == null || isDefault)
                {
                    _fxMissing = rc == null;
                    _controller = rc as AnimatorController;
                    if (rc != null && _controller == null) _fxOverrideUnsupported = true;
                }
                else
                {
                    _controller = rc as AnimatorController;
                    if (_controller == null) _fxOverrideUnsupported = true;
                }
            }

            if (_avatarField != null)
                _avatarField.SetValueWithoutNotify(_avatarRoot != null ? _avatarRoot.gameObject : null);
            if (_controllerField != null)
                _controllerField.SetValueWithoutNotify(_controller);
        }

        private void OnAvatarFieldChanged(GameObject newAvatarGo)
        {
            _avatarRoot = newAvatarGo != null ? newAvatarGo.transform : null;
            _descriptor = _avatarRoot != null ? VrcAvatarReflection.FindDescriptor(_avatarRoot) : null;
            _fxMissing = false;
            _fxReadFailed = false;
            _fxOverrideUnsupported = false;
            _controller = null;

            if (_descriptor != null)
            {
                if (!VrcAvatarReflection.TryGetFxController(_descriptor, out var rc, out bool isDefault))
                {
                    _fxReadFailed = true;
                }
                else if (rc == null || isDefault)
                {
                    _fxMissing = rc == null;
                    _controller = rc as AnimatorController;
                    if (rc != null && _controller == null) _fxOverrideUnsupported = true;
                }
                else
                {
                    _controller = rc as AnimatorController;
                    if (_controller == null) _fxOverrideUnsupported = true;
                }
            }

            if (_controllerField != null)
                _controllerField.SetValueWithoutNotify(_controller);

            ScanAndRebuild(_boundModel);
        }

        private void OnControllerFieldChanged(AnimatorController newController)
        {
            _controller = newController;
            _fxMissing = false;
            _fxReadFailed = false;
            _fxOverrideUnsupported = false;

            ScanAndRebuild(_boundModel);
        }

        private void ScanAndRebuild(ShapeKeyModel model)
        {
            _entries = new List<FxExpressionEntry>();
            _targetSmrPaths.Clear();

            var target = model?.TargetSkinnedMesh;
            if (target == null)
            {
                _hover.SetRoot(null);
                UpdateUI();
                return;
            }

            _hover.SetRoot(_avatarRoot);

            var meshes = (model.ActiveMeshes != null && model.ActiveMeshes.Count > 0)
                ? model.ActiveMeshes
                : new List<SkinnedMeshRenderer> { target };
            foreach (var smr in meshes)
            {
                if (smr == null) continue;
                var root = _avatarRoot != null ? _avatarRoot : target.transform.root;
                _targetSmrPaths.Add(ComputePathFrom(root, smr.transform));
            }

            if (_controller != null)
            {
                _entries = FxLayerScanner.Scan(_controller, _targetSmrPaths);

                // ジェスチャータグ推定: FX 単体では Gesture 等のレイヤーにあるドライバーを拾えないため、
                // ディスクリプタから取得できる全プレイアブルレイヤーのコントローラーを解析対象に含める。
                // ディスクリプタが無ければ FX 単体にフォールバック。
                var controllerList = new List<AnimatorController> { _controller };
                if (_descriptor != null)
                {
                    var tmp = new List<RuntimeAnimatorController>();
                    VrcAvatarReflection.GetAllLayerControllers(_descriptor, tmp);
                    foreach (var rc in tmp)
                        if (rc is AnimatorController ac && !controllerList.Contains(ac))
                            controllerList.Add(ac);
                }
                GestureTraceUtil.PopulateGestureHints(controllerList, _entries);
            }

            var alive = new HashSet<AnimationClip>();
            foreach (var e in _entries) alive.Add(e.Clip);
            var stale = new List<AnimationClip>();
            foreach (var kvp in _mappings)
                if (!alive.Contains(kvp.Key)) stale.Add(kvp.Key);
            foreach (var c in stale) _mappings.Remove(c);

            UpdateUI();
        }

        private static string ComputePathFrom(Transform root, Transform t)
        {
            var parts = new List<string>();
            while (t != null && t != root) { parts.Add(t.name); t = t.parent; }
            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }


        // ─── UI バインド ──────────────────────────────────────────────────────

        /// <summary>DenEmoWindow.uxml の fx-page 内の要素へバインドする。CreateGUI から一度だけ呼ぶ。</summary>
        public void BindUI(VisualElement root, ShapeKeyModel model, System.Func<string> getSaveFolder, EditorWindow window)
        {
            _boundModel    = model;
            _getSaveFolder = getSaveFolder;
            _window        = window;
            _denWindow     = window as DenEmoWindow;

            _upperScroll           = root.Q<ScrollView>("fx-scroll");
            _avatarCard            = root.Q<VisualElement>("fx-avatar-card");
            _avatarTitle           = root.Q<Label>("fx-avatar-title");
            _avatarField           = root.Q<ObjectField>("fx-avatar-field");
            _controllerField       = root.Q<ObjectField>("fx-controller-field");
            _rescanButton          = root.Q<Button>("fx-rescan");
            _infoRow               = root.Q<VisualElement>("fx-info-row");
            _statusLabel           = root.Q<Label>("fx-status-label");
            _problemBox            = root.Q<HelpBox>("fx-problem");
            _resultBox             = root.Q<VisualElement>("fx-result");
            _resultSuccessLabel    = root.Q<Label>("fx-result-success");
            _resultControllerRow   = root.Q<VisualElement>("fx-result-controller-row");
            _resultControllerLabel = root.Q<Label>("fx-result-controller");
            _resultPingButton      = root.Q<Button>("fx-result-ping");
            _resultNoteLabel       = root.Q<Label>("fx-result-note");
            _resultBackupLabel     = root.Q<Label>("fx-result-backup");

            _listSlot          = root.Q<VisualElement>("fx-list-slot");
            _listTitle         = root.Q<Label>("fx-list-title");
            _assignedChip      = root.Q<Button>("fx-chip-assigned");
            _searchField       = root.Q<TextField>("fx-search");
            _searchClear       = root.Q<Button>("fx-search-clear");
            _searchCount       = root.Q<Label>("fx-search-count");
            _gestureFilterRow  = root.Q<VisualElement>("fx-gesture-filter-row");
            _entryList         = root.Q<ScrollView>("fx-entry-list");
            _emptyBox          = root.Q<VisualElement>("fx-list-empty");
            _emptyMessage      = root.Q<Label>("fx-list-empty-message");
            _emptyAction       = root.Q<Button>("fx-list-empty-action");
            _noMatchBox        = root.Q<VisualElement>("fx-list-nomatch");
            _noMatchMessage    = root.Q<Label>("fx-list-nomatch-message");
            _noMatchClear      = root.Q<Button>("fx-list-nomatch-clear");
            _hoverHintLabel    = root.Q<Label>("fx-hover-hint");
            _hoverPlayingLabel = root.Q<Label>("fx-hover-playing");
            _hoverStopButton   = root.Q<Button>("fx-hover-stop");

            _applyCard         = root.Q<VisualElement>("fx-apply-card");
            _modeLabel         = root.Q<Label>("fx-mode-label");
            _modeDuplicateChip = root.Q<Button>("fx-mode-duplicate");
            _modeDirectChip    = root.Q<Button>("fx-mode-direct");
            _modeDescLabel     = root.Q<Label>("fx-mode-desc");
            _manualNote        = root.Q<VisualElement>("fx-manual-note");
            _manualNoteText    = root.Q<Label>("fx-manual-note-text");

            if (_avatarCard == null || _entryList == null || _applyCard == null)
            {
                _avatarCard = null; // UXML の読み込み失敗時は何もしない（各所の null ガードで止まる）
                return;
            }

            _avatarField.objectType = typeof(GameObject);
            _avatarField.allowSceneObjects = true;
            _avatarField.RegisterValueChangedCallback(evt =>
            {
                OnAvatarFieldChanged(evt.newValue as GameObject);
            });

            _controllerField.objectType = typeof(AnimatorController);
            _controllerField.allowSceneObjects = false;
            _controllerField.RegisterValueChangedCallback(evt =>
            {
                OnControllerFieldChanged(evt.newValue as AnimatorController);
            });

            _rescanButton.clicked += () => Refresh(_boundModel);

            DennokoBlockUI.AddPlaceholder(_searchField, DenEmoLoc.T("ui.fx.search.placeholder"));
            _searchPlaceholder = _searchField.Q<Label>(className: "dennoko-searchbar__placeholder");
            _searchField.RegisterValueChangedCallback(evt =>
            {
                _search = evt.newValue ?? string.Empty;
                RebuildEntryList();
            });
            _searchClear.clicked += () =>
            {
                _search = string.Empty;
                _searchField.SetValueWithoutNotify(string.Empty);
                DennokoBlockUI.Show(_searchPlaceholder, true);
                RebuildEntryList();
            };
            _assignedChip.clicked += () =>
            {
                _showOnlyAssigned = !_showOnlyAssigned;
                RebuildEntryList();
            };
            BuildGestureFilterChips();

            // 空の状態の操作: 一致なし → すべて表示 / 表情アニメーションなし → 再スキャン
            _emptyAction.clicked += () =>
            {
                if (HasNoMeshMatch())
                {
                    _showAllClips = true;
                    RebuildEntryList();
                }
                else
                {
                    Refresh(_boundModel);
                }
            };
            _noMatchClear.clicked += ClearFilters;
            _hoverStopButton.clicked += () => _hover.Stop();

            _selectMode = DennokoBlockUI.BindChoice(
                _applyCard.Q<VisualElement>("fx-mode-choice"), _applyDirect ? 1 : 0, index =>
                {
                    _applyDirect = index == 1;
                    DenEmoProjectPrefs.SetBool("DenEmo_Fx_ApplyMode_Direct", _applyDirect);
                    UpdateApplyCard();
                });

            _resultPingButton.clicked += () =>
            {
                if (_lastResult == null || string.IsNullOrEmpty(_lastResult.NewControllerPath)) return;
                var asset = AssetDatabase.LoadAssetAtPath<AnimatorController>(_lastResult.NewControllerPath);
                if (asset != null) EditorGUIUtility.PingObject(asset);
            };

            RefreshLabels();
        }

        /// <summary>ジェスチャー絞り込みチップ（左手/右手 + 8 ジェスチャー）を作り、行へ追加する。BindUI から一度だけ呼ぶ。</summary>
        private void BuildGestureFilterChips()
        {
            if (_gestureFilterRow == null) return;

            _gfLeftChip = new Button(() =>
            {
                _filterHand = (_filterHand == 1) ? 0 : 1;
                UpdateGestureFilterChips();
                RebuildEntryList();
            });
            _gfLeftChip.AddToClassList("dennoko-chip");
            _gestureFilterRow.Add(_gfLeftChip);

            _gfRightChip = new Button(() =>
            {
                _filterHand = (_filterHand == 2) ? 0 : 2;
                UpdateGestureFilterChips();
                RebuildEntryList();
            });
            _gfRightChip.AddToClassList("dennoko-chip");
            _gestureFilterRow.Add(_gfRightChip);

            // 手の指定とジェスチャーの指定の区切り
            var sep = new VisualElement { pickingMode = PickingMode.Ignore };
            sep.AddToClassList("dennoko-fx-filter-sep");
            _gestureFilterRow.Add(sep);

            for (int i = 0; i < _gfGestureChips.Length; i++)
            {
                int gesture = i; // クロージャ用にコピー
                var chip = new Button(() =>
                {
                    _filterGesture = (_filterGesture == gesture) ? -1 : gesture;
                    UpdateGestureFilterChips();
                    RebuildEntryList();
                }) { text = GestureTraceUtil.GestureNames[gesture] }; // VRChat 慣例に合わせ英語固定（非ローカライズ）
                chip.AddToClassList("dennoko-chip");
                _gfGestureChips[gesture] = chip;
                _gestureFilterRow.Add(chip);
            }

            UpdateGestureFilterChips();
        }

        /// <summary>_filterHand / _filterGesture の状態を 10 個のチップの選択表示へ反映する。</summary>
        private void UpdateGestureFilterChips()
        {
            SetChipState(_gfLeftChip,  _filterHand == 1);
            SetChipState(_gfRightChip, _filterHand == 2);
            for (int i = 0; i < _gfGestureChips.Length; i++)
                SetChipState(_gfGestureChips[i], _filterGesture == i);
        }

        /// <summary>検索・割当て済み・ジェスチャーの絞り込みをすべて解除する（メッシュパスの「すべて表示」は残す）。</summary>
        private void ClearFilters()
        {
            _search = string.Empty;
            _searchField?.SetValueWithoutNotify(string.Empty);
            DennokoBlockUI.Show(_searchPlaceholder, true);
            _showOnlyAssigned = false;
            _filterHand    = 0;
            _filterGesture = -1;
            UpdateGestureFilterChips();
            RebuildEntryList();
        }

        /// <summary>言語設定に依存するラベルを更新し、動的テキストも作り直す。</summary>
        public void RefreshLabels()
        {
            if (_avatarCard == null) return;
            _avatarTitle.text      = DenEmoLoc.T("ui.fx.section.avatar");
            _avatarField.label     = DenEmoLoc.T("ui.fx.avatar.fieldLabel");
            _controllerField.label = DenEmoLoc.T("ui.fx.fx.fieldLabel");
            DennokoIcons.Apply(_rescanButton, DennokoIcons.Refresh, "↻", DenEmoLoc.T("ui.fx.rescan"));
            DennokoIcons.Apply(_resultPingButton, DennokoIcons.Search, "→", DenEmoLoc.T("ui.fx.result.ping.tip"));

            _listTitle.text       = DenEmoLoc.T("ui.fx.section.list");
            _assignedChip.tooltip = DenEmoLoc.T("ui.fx.filter.assigned.tip");
            _gfLeftChip.text      = DenEmoLoc.T("ui.fx.gesture.filter.left");
            _gfRightChip.text     = DenEmoLoc.T("ui.fx.gesture.filter.right");
            if (_searchPlaceholder != null) _searchPlaceholder.text = DenEmoLoc.T("ui.fx.search.placeholder");
            DennokoIcons.Apply(_searchClear, DennokoIcons.Close, "✕", DenEmoLoc.T("ui.search.clear"));
            _searchCount.tooltip  = DenEmoLoc.T("ui.search.count.tip");
            _noMatchMessage.text  = DenEmoLoc.T("ui.fx.list.emptyFiltered");
            _noMatchClear.text    = DenEmoLoc.T("ui.list.clearFilter");
            _hoverHintLabel.text    = DenEmoLoc.T("ui.fx.hover.hint");
            _hoverHintLabel.tooltip = DenEmoLoc.T("ui.fx.hover.hint");
            DennokoIcons.Apply(_hoverStopButton, DennokoIcons.Close, "✕", DenEmoLoc.T("ui.fx.hover.stop.tip"));

            _modeLabel.text           = DenEmoLoc.T("ui.fx.mode.label");
            _modeDuplicateChip.text   = DenEmoLoc.T("ui.fx.mode.duplicate");
            _modeDirectChip.text      = DenEmoLoc.T("ui.fx.mode.direct");
            _manualNoteText.text      = DenEmoLoc.T("ui.fx.mode.manualNote");

            _footerSignature = null; // フッターの文言も言語に合わせて作り直す
            UpdateUI();
        }

        /// <summary>検出状態・一覧・適用セクションの表示内容を作り直す。</summary>
        public void UpdateUI()
        {
            if (_avatarCard == null) return;
            UpdateAvatarCard();
            RebuildEntryList();
            UpdateApplyCard();
        }

        /// <summary>
        /// カードの表示切替と、ホバー再生状態などポーリングでしか拾えない表示の反映。
        /// DenEmoWindow の 250ms ポーリング（とモード切替時）から呼ばれる。
        /// active = FX モード表示中、hasTarget = シェイプキーを持つ対象メッシュが指定されているか。
        /// </summary>
        public void PollUI(bool active, bool hasTarget)
        {
            if (_avatarCard == null) return;
            _hasTarget = hasTarget;
            bool visible = active && hasTarget;
            DennokoBlockUI.Show(_avatarCard, visible);
            bool showContent = visible && _controller != null;
            DennokoBlockUI.Show(_listSlot,  showContent);
            DennokoBlockUI.Show(_applyCard, showContent);
            // 一覧を出さないときは上部だけになるので、上部の高さの上限 (60%) を外す
            _upperScroll?.EnableInClassList("dennoko-upper-scroll--full", !showContent);
            if (!active) return;

            UpdateFooter();
            if (!visible) return;

            // プレビュー状態の 1 行（案内 ⇔ 再生中 + 停止）
            bool playing = _hover.IsActive && _hover.ActiveClip != null;
            DennokoBlockUI.Show(_hoverHintLabel,    !playing);
            DennokoBlockUI.Show(_hoverPlayingLabel, playing);
            DennokoBlockUI.Show(_hoverStopButton,   playing);
            if (playing)
            {
                string text = DenEmoLoc.Tf("ui.fx.hover.playing", _hover.ActiveClip.name);
                if (_hoverPlayingLabel.text != text)
                {
                    _hoverPlayingLabel.text    = text;
                    _hoverPlayingLabel.tooltip = text;
                }
            }

            // 再生中クリップに対応する行のハイライト
            var active2 = _hover.ActiveClip;
            foreach (var (entry, row) in _rowElements)
            {
                _mappings.TryGetValue(entry.Clip, out var mapping);
                bool hit = active2 != null &&
                           (active2 == entry.Clip || (mapping != null && active2 == mapping.NewClip));
                row.EnableInClassList("dennoko-fx-row--playing", hit);
            }
        }

        // ─── アバター / FX カード ─────────────────────────────────────────────

        private void UpdateAvatarCard()
        {
            // OnEnter が BindUI より先に走った場合も欄を検出結果に合わせる
            var avatarGo = _avatarRoot != null ? _avatarRoot.gameObject : null;
            if (_avatarField.value != avatarGo) _avatarField.SetValueWithoutNotify(avatarGo);
            if (_controllerField.value != _controller) _controllerField.SetValueWithoutNotify(_controller);

            // 問題（直す操作が 1 つに決まらないので HelpBox）。同時に出るのは 1 つだけ
            bool found = _descriptor != null;
            string problem = null;
            if (!found)                      problem = DenEmoLoc.T("ui.fx.avatar.notFound");
            else if (_fxReadFailed)          problem = DenEmoLoc.T("ui.fx.fx.readFailed");
            else if (_fxOverrideUnsupported) problem = DenEmoLoc.T("ui.fx.fx.overrideUnsupported");
            else if (_fxMissing && _controller == null) problem = DenEmoLoc.T("ui.fx.fx.missing");
            _problemBox.text = problem ?? string.Empty;
            DennokoBlockUI.Show(_problemBox, problem != null);

            // 情報行（コントローラーがあるとき）
            bool hasController = _controller != null;
            DennokoBlockUI.Show(_infoRow, hasController);
            _statusLabel.text = hasController ? DenEmoLoc.Tf("ui.fx.fx.stats", _entries.Count) : string.Empty;

            UpdateResult();
        }

        /// <summary>最後の適用結果（新しいコントローラー・FX レイヤーへのセット・バックアップ）。</summary>
        private void UpdateResult()
        {
            bool show = _lastResult != null && _lastResult.Success;
            DennokoBlockUI.Show(_resultBox, show);
            if (!show) return;

            _resultSuccessLabel.text = DenEmoLoc.Tf("ui.fx.result.success", _lastResult.ReplacedCount);

            bool hasNewController = !string.IsNullOrEmpty(_lastResult.NewControllerPath);
            DennokoBlockUI.Show(_resultControllerRow, hasNewController);
            DennokoBlockUI.Show(_resultNoteLabel,     hasNewController);
            if (hasNewController)
            {
                _resultControllerLabel.text = DenEmoLoc.Tf("ui.fx.result.newController",
                    EllipsizedPath(_lastResult.NewControllerPath));
                _resultControllerLabel.tooltip = _lastResult.NewControllerPath;

                _resultNoteLabel.text = _lastResult.DescriptorUpdated
                    ? DenEmoLoc.T("ui.fx.result.descriptorSet")
                    : DenEmoLoc.T("ui.fx.result.manualSet");
                _resultNoteLabel.EnableInClassList("dennoko-hint--warning", !_lastResult.DescriptorUpdated);
            }

            bool hasBackup = !string.IsNullOrEmpty(_lastResult.BackupPath);
            DennokoBlockUI.Show(_resultBackupLabel, hasBackup);
            if (hasBackup)
            {
                _resultBackupLabel.text    = DenEmoLoc.Tf("ui.fx.result.backup", EllipsizedPath(_lastResult.BackupPath));
                _resultBackupLabel.tooltip = _lastResult.BackupPath;
            }
        }

        // ─── 差し替えマッピング一覧 ───────────────────────────────────────────

        /// <summary>表情アニメーションはあるが、対象メッシュのパスに一致するものが 1 つも無い（すべて表示で救済）。</summary>
        private bool HasNoMeshMatch()
        {
            if (_entries.Count == 0 || _showAllClips) return false;
            foreach (var entry in _entries)
                if (entry.MatchesTargetMesh) return false;
            return true;
        }

        private void RebuildEntryList()
        {
            if (_entryList == null) return;

            // 行を作り直すため、要素に紐づいたホバー状態は破棄する
            _hoverRowEntry  = null;
            _hoverSlotEntry = null;
            ResolveHover();

            _entryList.Clear();
            _rowElements.Clear();

            SetChipState(_assignedChip, _showOnlyAssigned);
            _assignedChip.text = DenEmoLoc.Tf("ui.fx.filter.assigned", _mappings.Count);
            DennokoBlockUI.Show(_searchClear, !string.IsNullOrEmpty(_search));

            var visible = CollectVisibleEntries(out int total);

            bool noEntry  = _entries.Count == 0;
            bool noMatch  = HasNoMeshMatch();
            bool filtered = !noMatch && !noEntry && visible.Count == 0;

            // 空の状態: 対象が無い（表情アニメーションなし / メッシュ不一致）と、絞り込みで 0 件を区別する
            DennokoBlockUI.Show(_emptyBox,   noEntry || noMatch);
            DennokoBlockUI.Show(_noMatchBox, filtered);
            if (noEntry || noMatch)
            {
                _emptyMessage.text   = DenEmoLoc.T(noMatch ? "ui.fx.list.empty" : "ui.fx.list.empty.none");
                _emptyAction.text    = DenEmoLoc.T(noMatch ? "ui.fx.list.showAll" : "ui.fx.rescan");
                _emptyAction.tooltip = noMatch ? DenEmoLoc.T("ui.fx.filter.showAll") : null;
            }

            _searchCount.text = visible.Count + " / " + total;

            foreach (var entry in visible)
                _entryList.Add(MakeEntryRow(entry));
        }

        /// <summary>表示する行を集める。total = 絞り込み前の件数（メッシュパスの条件だけを掛けたもの）。</summary>
        private List<FxExpressionEntry> CollectVisibleEntries(out int total)
        {
            var tokens = ShapeKeyModel.BuildSearchTokens(_search);
            var list = new List<FxExpressionEntry>();
            total = 0;
            foreach (var entry in _entries)
            {
                if (entry.Clip == null) continue;
                if (!_showAllClips && !entry.MatchesTargetMesh) continue;
                total++;
                if (_showOnlyAssigned && !_mappings.ContainsKey(entry.Clip)) continue;
                if (tokens.Length > 0)
                {
                    bool match = true;
                    var nameLower = entry.Clip.name.ToLowerInvariant();
                    foreach (var t in tokens)
                        if (nameLower.IndexOf(t.ToLowerInvariant(), System.StringComparison.Ordinal) < 0) { match = false; break; }
                    if (!match) continue;
                }
                if (_filterHand != 0 || _filterGesture >= 0)
                {
                    if (!EntryMatchesGestureFilter(entry)) continue;
                }
                list.Add(entry);
            }
            return list;
        }

        /// <summary>ジェスチャー絞り込みに一致するか（いずれかの slot のいずれかの hint が一致すれば表示）。</summary>
        private bool EntryMatchesGestureFilter(FxExpressionEntry entry)
        {
            foreach (var slot in entry.Slots)
                foreach (var h in slot.GestureHints)
                {
                    if (_filterHand == 1)
                    {
                        if (h.Left < 0) continue;
                        if (_filterGesture >= 0 && h.Left != _filterGesture) continue;
                        return true;
                    }
                    if (_filterHand == 2)
                    {
                        if (h.Right < 0) continue;
                        if (_filterGesture >= 0 && h.Right != _filterGesture) continue;
                        return true;
                    }
                    // 手の指定なし・ジェスチャーのみ: どちらかの手が一致すれば良い
                    if (h.Left == _filterGesture || h.Right == _filterGesture) return true;
                }
            return false;
        }

        /// <summary>1 エントリぶんの行（＋不一致注記・展開スロット）を生成する（B4 の .dennoko-list-row）。</summary>
        private VisualElement MakeEntryRow(FxExpressionEntry entry)
        {
            _mappings.TryGetValue(entry.Clip, out var mapping);
            bool multi    = entry.Slots.Count > 1;
            bool expanded = multi && _expanded.Contains(entry.Clip);
            bool assigned = mapping != null && mapping.NewClip != null;

            var container = new VisualElement();
            container.AddToClassList("dennoko-fx-entry");

            var row = new VisualElement();
            row.AddToClassList("dennoko-list-row");
            row.AddToClassList("dennoko-fx-row");
            row.EnableInClassList("dennoko-fx-row--assigned", assigned);

            // 複数参照の展開トグル（単一参照は同じ幅の空きで位置を揃える）
            if (multi)
            {
                var fold = new Button(() =>
                {
                    if (_expanded.Contains(entry.Clip)) _expanded.Remove(entry.Clip);
                    else                                _expanded.Add(entry.Clip);
                    RebuildEntryList();
                })
                {
                    text    = expanded ? "▼" : "▶",
                    tooltip = DenEmoLoc.T("ui.fx.row.fold.tip"),
                };
                fold.AddToClassList("dennoko-icon-button");
                fold.AddToClassList("dennoko-fx-fold");
                row.Add(fold);
            }
            else
            {
                row.Add(MakeSpacer());
            }

            // クリップ名（クリックで Ping、ホバーでシーンプレビュー）
            string label = entry.Clip.name;
            if (multi) label += " " + DenEmoLoc.Tf("ui.fx.row.usage", entry.Slots.Count);
            if (!entry.MatchesTargetMesh) label = "⚠ " + label;
            var name = new Label(label) { tooltip = BuildSlotTooltip(entry) };
            name.AddToClassList("dennoko-list-row__name");
            name.AddToClassList("dennoko-fx-name");
            name.EnableInClassList("dennoko-fx-name--mismatch", !entry.MatchesTargetMesh);
            name.RegisterCallback<ClickEvent>(evt =>
            {
                if (entry.Clip != null)
                {
                    Selection.activeObject = entry.Clip;
                    EditorGUIUtility.PingObject(entry.Clip);
                }
            });
            row.Add(name);

            // ジェスチャータグ（全 slot の hint 和集合。最大 3 個、超過分は +n にまとめ tooltip へ全件）
            var hintList = AggregateHints(entry);
            if (hintList.Count > 0)
            {
                var allFormatted = new List<string>();
                foreach (var h in hintList) allFormatted.Add(FormatHint(h));

                if (hintList.Count <= 3)
                {
                    foreach (var text in allFormatted)
                        row.Add(MakeGestureTag(text));
                }
                else
                {
                    row.Add(MakeGestureTag(allFormatted[0]));
                    row.Add(MakeGestureTag(allFormatted[1]));
                    var more = MakeGestureTag(DenEmoLoc.Tf("ui.fx.gesture.more", hintList.Count - 2));
                    more.tooltip = DenEmoLoc.Tf("ui.fx.gesture.tag.tip", string.Join(", ", allFormatted.ToArray()));
                    more.pickingMode = PickingMode.Position; // tooltip を出すためホバー判定を有効化
                    row.Add(more);
                }
            }

            var arrow = new Label("→") { pickingMode = PickingMode.Ignore };
            arrow.AddToClassList("dennoko-list-row__arrow");
            row.Add(arrow);

            // 差し替え先スロット（クリックでピッカー、D&D 受付、ホバーで新クリップをプレビュー）
            var slot = new Button
            {
                text    = assigned ? mapping.NewClip.name : DenEmoLoc.T("ui.fx.slot.none"),
                tooltip = assigned
                    ? mapping.NewClip.name + "\n" + DenEmoLoc.T("ui.fx.slot.tip")
                    : DenEmoLoc.T("ui.fx.slot.tip"),
            };
            slot.clicked += () => OpenPicker(entry, slot);
            slot.AddToClassList("dennoko-fx-slot");
            slot.EnableInClassList("dennoko-fx-slot--empty", !assigned);
            slot.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                if (FindDraggedClip() == null) return;
                DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                evt.StopPropagation();
            });
            slot.RegisterCallback<DragPerformEvent>(evt =>
            {
                var dragged = FindDraggedClip();
                if (dragged == null) return;
                DragAndDrop.AcceptDrag();
                TryAssign(entry, dragged, _window);
                evt.StopPropagation();
            });
            row.Add(slot);

            // 割当て解除（無いときは同じ幅の空きで位置を揃える）
            if (mapping != null)
            {
                var clear = new Button(() =>
                {
                    _mappings.Remove(entry.Clip);
                    UpdateUI();
                });
                clear.AddToClassList("dennoko-icon-button");
                clear.AddToClassList("dennoko-fx-clear");
                DennokoIcons.Apply(clear, DennokoIcons.Close, "✕", DenEmoLoc.T("ui.fx.slot.clear.tip"));
                row.Add(clear);
            }
            else
            {
                row.Add(MakeSpacer());
            }

            // ホバープレビュー: 行 = 差し替え元、スロット上 = 差し替え先
            row.RegisterCallback<PointerEnterEvent>(_ => { _hoverRowEntry = entry; ResolveHover(); });
            row.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                if (_hoverRowEntry == entry) _hoverRowEntry = null;
                ResolveHover();
            });
            slot.RegisterCallback<PointerEnterEvent>(_ => { _hoverSlotEntry = entry; ResolveHover(); });
            slot.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                if (_hoverSlotEntry == entry) _hoverSlotEntry = null;
                ResolveHover();
            });

            container.Add(row);
            _rowElements.Add((entry, row));

            // パス不一致の注記
            if (!entry.MatchesTargetMesh)
            {
                var note = new Label(DenEmoLoc.Tf("ui.fx.row.pathMismatch", entry.FirstBindingPath ?? ""));
                note.AddToClassList("dennoko-fx-note");
                container.Add(note);
            }

            // 参照箇所ごとの対象チェック（展開時のみ。割当て前は選べない）
            if (expanded)
            {
                var slots = new VisualElement();
                slots.AddToClassList("dennoko-fx-slots");
                foreach (var slotInfo in entry.Slots)
                {
                    var s = slotInfo;
                    bool enabled = mapping == null || !mapping.DisabledSlotKeys.Contains(s.SlotKey);
                    var toggle = new Toggle { text = s.DisplayPath + SlotHintSuffix(s), value = enabled };
                    toggle.AddToClassList("dennoko-fx-slot-toggle");
                    toggle.SetEnabled(mapping != null);
                    toggle.RegisterValueChangedCallback(evt =>
                    {
                        if (!_mappings.TryGetValue(entry.Clip, out var m)) return;
                        if (evt.newValue) m.DisabledSlotKeys.Remove(s.SlotKey);
                        else              m.DisabledSlotKeys.Add(s.SlotKey);
                        UpdateApplyCard();
                    });
                    slots.Add(toggle);
                }
                container.Add(slots);
            }

            return container;
        }

        private static VisualElement MakeSpacer()
        {
            var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
            spacer.AddToClassList("dennoko-fx-spacer");
            return spacer;
        }

        private void OpenPicker(FxExpressionEntry entry, VisualElement anchor)
        {
            if (string.IsNullOrEmpty(_pickerFolder))
                _pickerFolder = _getSaveFolder != null ? _getSaveFolder() : "Assets";
            _pickerOpen     = true;
            _hoverRowEntry  = null;
            _hoverSlotEntry = null;
            _hover.SetHover(null);
            UnityEditor.PopupWindow.Show(anchor.worldBound, new FxClipPickerPopup(this, entry, _window));
        }

        private void ResolveHover()
        {
            if (_pickerOpen) return;
            AnimationClip clip = null;
            if (_hoverSlotEntry != null &&
                _mappings.TryGetValue(_hoverSlotEntry.Clip, out var m) && m.NewClip != null)
                clip = m.NewClip;
            else if (_hoverRowEntry != null)
                clip = _hoverRowEntry.Clip;
            _hover.SetHover(clip);
        }

        private static string BuildSlotTooltip(FxExpressionEntry entry)
        {
            var lines = new List<string>();
            foreach (var slot in entry.Slots) lines.Add(slot.DisplayPath + SlotHintSuffix(slot));
            return string.Join("\n", lines.ToArray());
        }

        // ─── ジェスチャータグ ─────────────────────────────────────────────────

        /// <summary>
        /// class 付きの小さなジェスチャータグ Label を作る（見た目は USS 側で完結）。
        /// 通常タグはホバー判定を妨げないよう Ignore のまま。+n オーバーフロータグのみ
        /// 呼び出し側で pickingMode = Position に切り替え、tooltip を出せるようにする。
        /// </summary>
        private static Label MakeGestureTag(string text)
        {
            var tag = new Label(text);
            tag.AddToClassList("dennoko-fx-gesture-tag");
            tag.pickingMode = PickingMode.Ignore; // ホバー判定を妨げない
            return tag;
        }

        /// <summary>エントリ内の全 slot の hint を和集合・重複排除し、Left 単独→Right 単独→コンボの順に整列する。</summary>
        private static List<FxGestureHint> AggregateHints(FxExpressionEntry entry)
        {
            var seen = new HashSet<FxGestureHint>();
            var list = new List<FxGestureHint>();
            foreach (var slot in entry.Slots)
                foreach (var h in slot.GestureHints)
                    if (!h.IsEmpty && seen.Add(h)) list.Add(h);
            list.Sort(CompareHint);
            return list;
        }

        private static int CompareHint(FxGestureHint a, FxGestureHint b)
        {
            int ra = HintRank(a), rb = HintRank(b);
            if (ra != rb) return ra.CompareTo(rb);
            if (ra == 0) return a.Left.CompareTo(b.Left);   // Left 単独
            if (ra == 1) return a.Right.CompareTo(b.Right); // Right 単独
            int c = a.Left.CompareTo(b.Left);               // コンボ: Left → Right
            return c != 0 ? c : a.Right.CompareTo(b.Right);
        }

        private static int HintRank(FxGestureHint h)
        {
            if (h.Left >= 0 && h.Right >= 0) return 2; // コンボ
            if (h.Right >= 0) return 1;                // Right 単独
            return 0;                                  // Left 単独
        }

        /// <summary>hint を表示文字列に（Left 単独 / Right 単独 / コンボ）。</summary>
        private static string FormatHint(FxGestureHint h)
        {
            if (h.Left >= 0 && h.Right >= 0)
                return DenEmoLoc.Tf("ui.fx.gesture.both", GestureName(h.Left), GestureName(h.Right));
            if (h.Left >= 0)
                return DenEmoLoc.Tf("ui.fx.gesture.left", GestureName(h.Left));
            return DenEmoLoc.Tf("ui.fx.gesture.right", GestureName(h.Right));
        }

        private static string GestureName(int g)
        {
            return (g >= 0 && g < GestureTraceUtil.GestureNames.Length)
                ? GestureTraceUtil.GestureNames[g]
                : g.ToString();
        }

        /// <summary>スロット表示に付ける " [L:Fist, R:Victory]" 形式のサフィックス（hint 無しは空）。</summary>
        private static string SlotHintSuffix(FxMotionSlot slot)
        {
            if (slot.GestureHints.Count == 0) return string.Empty;
            var parts = new List<string>();
            foreach (var h in slot.GestureHints)
                if (!h.IsEmpty) parts.Add(FormatHint(h));
            if (parts.Count == 0) return string.Empty;
            return "  [" + string.Join(", ", parts.ToArray()) + "]";
        }

        private static AnimationClip FindDraggedClip()
        {
            foreach (var obj in DragAndDrop.objectReferences)
                if (obj is AnimationClip c) return c;
            return null;
        }

        private static void SetChipState(Button chip, bool on)
        {
            chip?.EnableInClassList(DennokoBlockUI.ActiveClass, on);
        }

        /// <summary>差し替え先クリップの割当て（検証込み）。ピッカー / D&D の両方から呼ばれる。</summary>
        public void TryAssign(FxExpressionEntry entry, AnimationClip newClip, EditorWindow window)
        {
            if (newClip == null)
            {
                _mappings.Remove(entry.Clip);
                UpdateUI();
                window?.Repaint();
                return;
            }
            if (newClip == entry.Clip)
            {
                StatusSink?.Invoke(DenEmoLoc.T("ui.fx.err.selfAssign"), 3);
                return;
            }
            if (!EditorUtility.IsPersistent(newClip))
            {
                StatusSink?.Invoke(DenEmoLoc.T("ui.fx.err.sceneClip"), 3);
                return;
            }

            if (!_mappings.TryGetValue(entry.Clip, out var mapping))
            {
                mapping = new FxMapping();
                _mappings.Add(entry.Clip, mapping);
            }
            mapping.NewClip = newClip;
            UpdateUI();
            window?.Repaint();
        }

        // ─── 適用方法（B2）と標準フッター ─────────────────────────────────────

        private void UpdateApplyCard()
        {
            if (_applyCard == null) return;

            _selectMode?.Invoke(_applyDirect ? 1 : 0);

            _modeDescLabel.text = _applyDirect
                ? DenEmoLoc.T("ui.fx.mode.direct.desc")
                : DenEmoLoc.T("ui.fx.mode.duplicate.desc");
            _modeDescLabel.EnableInClassList("dennoko-hint--warning", _applyDirect);

            DennokoBlockUI.Show(_manualNote, !_applyDirect && _descriptor == null);

            UpdateFooter();
        }

        /// <summary>
        /// 主ボタン（差し替え）・情報欄を DenEmoWindow.FxFooter に書き、変わったときだけ即時反映する。
        /// 割当てが無い・コントローラーが無いなどの間は主ボタンを無効にし、ツールチップに理由を出す。
        /// </summary>
        private void UpdateFooter()
        {
            if (_denWindow == null) return;
            var f = _denWindow.FxFooter;
            var jobs = BuildJobs(out int slotCount);

            string reason = !_hasTarget         ? DenEmoLoc.T("ui.footer.primary.needTarget")
                          : _controller == null ? DenEmoLoc.T("ui.fx.footer.needController")
                          : jobs.Count == 0     ? DenEmoLoc.T("ui.fx.footer.needAssign")
                          : null;

            f.Visible        = true;
            f.OnPrimary      = OnApplyClicked;
            f.PrimaryText    = DenEmoLoc.T("ui.fx.footer.primary");
            f.PrimaryEnabled = reason == null;
            f.PrimaryTooltip = reason ?? DenEmoLoc.Tf("ui.fx.apply.button", jobs.Count);
            f.SecondaryText  = null;
            f.OnSecondary    = null;
            if (reason != null)
            {
                f.InfoText    = null;
                f.InfoTooltip = null;
                f.InfoWarning = false;
            }
            else
            {
                f.InfoText = DenEmoLoc.Tf("ui.fx.footer.info", jobs.Count, slotCount)
                             + (_applyDirect ? DenEmoLoc.T("ui.fx.footer.info.direct") : string.Empty);
                f.InfoTooltip = _applyDirect
                    ? DenEmoLoc.T("ui.fx.mode.direct.desc")
                    : DenEmoLoc.T("ui.fx.mode.duplicate.desc");
                f.InfoWarning = _applyDirect;
            }

            string signature = f.PrimaryText + "|" + f.PrimaryEnabled + "|" + f.PrimaryTooltip + "|" + f.InfoText + "|" + f.InfoWarning;
            if (signature == _footerSignature) return;
            _footerSignature = signature;
            _denWindow.RefreshFooter();
        }

        private void OnApplyClicked()
        {
            var jobs = BuildJobs(out int slotCount);
            if (jobs.Count > 0 && _controller != null)
                ExecuteApply(_boundModel, _window, jobs, slotCount);
        }

        private List<(FxExpressionEntry entry, FxMapping mapping)> BuildJobs(out int slotCount)
        {
            var jobs = new List<(FxExpressionEntry, FxMapping)>();
            slotCount = 0;
            foreach (var entry in _entries)
            {
                if (!_mappings.TryGetValue(entry.Clip, out var mapping)) continue;
                if (mapping.NewClip == null) continue;

                int enabled = 0;
                foreach (var slot in entry.Slots)
                    if (!mapping.DisabledSlotKeys.Contains(slot.SlotKey)) enabled++;
                if (enabled == 0) continue;

                slotCount += enabled;
                jobs.Add((entry, mapping));
            }
            return jobs;
        }

        private void ExecuteApply(ShapeKeyModel model, EditorWindow window,
            List<(FxExpressionEntry entry, FxMapping mapping)> jobs, int slotCount)
        {
            // 確認ダイアログ（モード・対象一覧を明記）
            var lines = new List<string>();
            foreach (var (entry, mapping) in jobs)
            {
                if (lines.Count >= 10)
                {
                    lines.Add(DenEmoLoc.Tf("ui.fx.confirm.more", jobs.Count - 10));
                    break;
                }
                lines.Add(entry.Clip.name + " → " + mapping.NewClip.name);
            }

            string head = _applyDirect
                ? DenEmoLoc.T("ui.fx.confirm.direct.head")
                : DenEmoLoc.T("ui.fx.confirm.duplicate.head");
            string msg = head + "\n\n" + string.Join("\n", lines.ToArray()) + "\n\n"
                + DenEmoLoc.Tf("ui.fx.confirm.slotCount", slotCount);

            if (!EditorUtility.DisplayDialog(DenEmoLoc.T("ui.fx.confirm.title"), msg,
                    DenEmoLoc.T("ui.fx.confirm.ok"), DenEmoLoc.T("dlg.cancel")))
                return;

            _hover.Stop();

            var result = _applyDirect
                ? FxMotionReplacer.ReplaceDirect(_controller, jobs, backup: false)
                : FxMotionReplacer.ReplaceWithDuplicate(_controller, jobs, _descriptor, _targetSmrPaths);

            _lastResult = result;

            if (result.Success)
            {
                _mappings.Clear();
                _expanded.Clear();

                // 複製モードなら、以降は複製側を作業対象にする
                if (!_applyDirect && !string.IsNullOrEmpty(result.NewControllerPath))
                {
                    var copy = AssetDatabase.LoadAssetAtPath<AnimatorController>(result.NewControllerPath);
                    if (copy != null)
                    {
                        _controllerField.SetValueWithoutNotify(copy);
                        _controller = copy;
                    }
                }

                ScanAndRebuild(model);
                StatusSink?.Invoke(DenEmoLoc.Tf("status.fx.appliedCount", result.ReplacedCount), 1);
            }
            else
            {
                StatusSink?.Invoke(result.Error ?? "error", 3);
            }
            window?.Repaint();
        }

        private static string EllipsizedPath(string path, int maxLen = 48)
        {
            if (string.IsNullOrEmpty(path) || path.Length <= maxLen) return path;
            return "..." + path.Substring(path.Length - (maxLen - 3));
        }
    }
}
