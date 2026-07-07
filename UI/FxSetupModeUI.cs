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
    /// UI は DenEmoWindow.uxml の fx-avatar-card / fx-list-card / fx-apply-card へバインドする。
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

        // ─── プレビュー / 適用 ────────────────────────────────────────────────
        private readonly FxHoverPreview _hover = new FxHoverPreview();
        private bool            _pickerOpen;      // ピッカー表示中はメイン側のホバー判定を止める
        private bool            _applyDirect;     // false = 複製モード（推奨）
        private FxReplaceResult _lastResult;
        private string          _pickerFolder;

        // ─── UI Toolkit 要素 ─────────────────────────────────────────────────
        private VisualElement _avatarCard;
        private Label         _avatarFieldLabel;
        private ObjectField   _avatarField;
        private Label         _controllerFieldLabel;
        private ObjectField   _controllerField;
        private Label         _statusLabel;
        private Label         _avatarNotFound;
        private Label         _readFailedLabel;
        private Label         _overrideLabel;
        private Label         _missingLabel;
        private Button        _rescanButton;

        private VisualElement _listCard;
        private TextField     _searchField;
        private Button        _assignedChip;
        private Label         _listEmptyLabel;
        private Button        _showAllButton;
        private Label         _listEmptyFilteredLabel;
        private ScrollView    _entryList;
        private VisualElement _hoverBand;
        private Label         _hoverPlayingLabel;
        private Button        _hoverStopButton;
        private Label         _hoverHintLabel;

        private VisualElement _applyCard;
        private Label         _applyCountLabel;
        private Button        _modeDuplicateChip;
        private Button        _modeDirectChip;
        private Label         _modeDescLabel;
        private Label         _manualNoteLabel;
        private Button        _applyButton;
        private VisualElement _resultBand;
        private Label         _resultSuccessLabel;
        private VisualElement _resultControllerRow;
        private Label         _resultControllerLabel;
        private Button        _resultPingButton;
        private Label         _resultNoteLabel;
        private Label         _resultBackupLabel;

        private ShapeKeyModel        _boundModel;
        private EditorWindow         _window;
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
                _entries = FxLayerScanner.Scan(_controller, _targetSmrPaths);

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

        /// <summary>DenEmoWindow.uxml 内の FX カード群へバインドする。CreateGUI から一度だけ呼ぶ。</summary>
        public void BindUI(VisualElement root, ShapeKeyModel model, System.Func<string> getSaveFolder, EditorWindow window)
        {
            _boundModel    = model;
            _getSaveFolder = getSaveFolder;
            _window        = window;

            _avatarCard           = root.Q<VisualElement>("fx-avatar-card");
            _avatarFieldLabel     = root.Q<Label>("fx-avatar-field-label");
            _avatarField          = root.Q<ObjectField>("fx-avatar-field");
            _controllerFieldLabel = root.Q<Label>("fx-controller-field-label");
            _controllerField      = root.Q<ObjectField>("fx-controller-field");
            _statusLabel          = root.Q<Label>("fx-status-label");
            _avatarNotFound       = root.Q<Label>("fx-avatar-notfound");
            _readFailedLabel      = root.Q<Label>("fx-readfailed");
            _overrideLabel        = root.Q<Label>("fx-override-unsupported");
            _missingLabel         = root.Q<Label>("fx-missing");
            _rescanButton         = root.Q<Button>("fx-rescan");

            _listCard               = root.Q<VisualElement>("fx-list-card");
            _searchField            = root.Q<TextField>("fx-search");
            _assignedChip           = root.Q<Button>("fx-chip-assigned");
            _listEmptyLabel         = root.Q<Label>("fx-list-empty");
            _showAllButton          = root.Q<Button>("fx-show-all");
            _listEmptyFilteredLabel = root.Q<Label>("fx-list-empty-filtered");
            _entryList              = root.Q<ScrollView>("fx-entry-list");
            _hoverBand              = root.Q<VisualElement>("fx-hover-band");
            _hoverPlayingLabel      = root.Q<Label>("fx-hover-playing");
            _hoverStopButton        = root.Q<Button>("fx-hover-stop");
            _hoverHintLabel         = root.Q<Label>("fx-hover-hint");

            _applyCard             = root.Q<VisualElement>("fx-apply-card");
            _applyCountLabel       = root.Q<Label>("fx-apply-count");
            _modeDuplicateChip     = root.Q<Button>("fx-mode-duplicate");
            _modeDirectChip        = root.Q<Button>("fx-mode-direct");
            _modeDescLabel         = root.Q<Label>("fx-mode-desc");
            _manualNoteLabel       = root.Q<Label>("fx-manual-note");
            _applyButton           = root.Q<Button>("fx-apply-button");
            _resultBand            = root.Q<VisualElement>("fx-result-band");
            _resultSuccessLabel    = root.Q<Label>("fx-result-success");
            _resultControllerRow   = root.Q<VisualElement>("fx-result-controller-row");
            _resultControllerLabel = root.Q<Label>("fx-result-controller");
            _resultPingButton      = root.Q<Button>("fx-result-ping");
            _resultNoteLabel       = root.Q<Label>("fx-result-note");
            _resultBackupLabel     = root.Q<Label>("fx-result-backup");

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

            _searchField.RegisterValueChangedCallback(evt =>
            {
                _search = evt.newValue ?? string.Empty;
                RebuildEntryList();
            });
            _assignedChip.clicked += () =>
            {
                _showOnlyAssigned = !_showOnlyAssigned;
                RebuildEntryList();
            };
            _showAllButton.clicked += () =>
            {
                _showAllClips = true;
                RebuildEntryList();
            };
            _hoverStopButton.clicked += () => _hover.Stop();

            _modeDuplicateChip.clicked += () => { _applyDirect = false; UpdateApplyCard(); };
            _modeDirectChip.clicked    += () => { _applyDirect = true;  UpdateApplyCard(); };
            _applyButton.clicked += () =>
            {
                var jobs = BuildJobs(out int slotCount);
                if (jobs.Count > 0 && _controller != null)
                    ExecuteApply(_boundModel, _window, jobs, slotCount);
            };
            _resultPingButton.clicked += () =>
            {
                if (_lastResult == null || string.IsNullOrEmpty(_lastResult.NewControllerPath)) return;
                var asset = AssetDatabase.LoadAssetAtPath<AnimatorController>(_lastResult.NewControllerPath);
                if (asset != null) EditorGUIUtility.PingObject(asset);
            };

            RefreshLabels();
        }

        /// <summary>言語設定に依存するラベルを更新し、動的テキストも作り直す。</summary>
        public void RefreshLabels()
        {
            if (_avatarCard == null) return;
            _avatarCard.Q<Label>("fx-avatar-title").text = DenEmoLoc.T("ui.fx.section.avatar");
            _avatarFieldLabel.text = DenEmoLoc.T("ui.fx.avatar.fieldLabel");
            _controllerFieldLabel.text = DenEmoLoc.T("ui.fx.fx.fieldLabel");
            _avatarNotFound.text  = DenEmoLoc.T("ui.fx.avatar.notFound");
            _readFailedLabel.text = DenEmoLoc.T("ui.fx.fx.readFailed");
            _overrideLabel.text   = DenEmoLoc.T("ui.fx.fx.overrideUnsupported");
            _missingLabel.text    = DenEmoLoc.T("ui.fx.fx.missing");
            _rescanButton.text    = DenEmoLoc.T("ui.fx.rescan");

            _listCard.Q<Label>("fx-list-title").text = DenEmoLoc.T("ui.fx.section.list");
            _assignedChip.text           = DenEmoLoc.T("ui.fx.filter.assignedOnly");
            _showAllButton.text          = DenEmoLoc.T("ui.fx.filter.showAll");
            _listEmptyLabel.text         = DenEmoLoc.T("ui.fx.list.empty");
            _listEmptyFilteredLabel.text = DenEmoLoc.T("ui.fx.list.emptyFiltered");
            _hoverStopButton.text        = DenEmoLoc.T("ui.fx.hover.stop");
            _hoverHintLabel.text         = DenEmoLoc.T("ui.fx.hover.hint");

            _applyCard.Q<Label>("fx-apply-title").text = DenEmoLoc.T("ui.fx.section.apply");
            _modeDuplicateChip.text = DenEmoLoc.T("ui.fx.mode.duplicate");
            _modeDirectChip.text    = DenEmoLoc.T("ui.fx.mode.direct");
            _manualNoteLabel.text   = DenEmoLoc.T("ui.fx.mode.manualNote");
            _resultPingButton.text  = DenEmoLoc.T("ui.fx.result.ping");

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
        /// DenEmoWindow の 250ms ポーリングから呼ばれる。
        /// </summary>
        public void PollUI(bool visible)
        {
            if (_avatarCard == null) return;
            _avatarCard.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            bool showContent = visible && _controller != null;
            var contentDisplay = showContent ? DisplayStyle.Flex : DisplayStyle.None;
            _listCard.style.display  = contentDisplay;
            _applyCard.style.display = contentDisplay;
            if (!visible) return;

            // プレビュー状態バー
            bool playing = _hover.IsActive && _hover.ActiveClip != null;
            _hoverBand.style.display     = playing  ? DisplayStyle.Flex : DisplayStyle.None;
            _hoverHintLabel.style.display = playing ? DisplayStyle.None : DisplayStyle.Flex;
            if (playing)
                _hoverPlayingLabel.text = DenEmoLoc.Tf("ui.fx.hover.playing", _hover.ActiveClip.name);

            // 再生中クリップに対応する行のハイライト
            var active = _hover.ActiveClip;
            foreach (var (entry, row) in _rowElements)
            {
                _mappings.TryGetValue(entry.Clip, out var mapping);
                bool hit = active != null &&
                           (active == entry.Clip || (mapping != null && active == mapping.NewClip));
                row.EnableInClassList("dennoko-fx-row--playing", hit);
            }
        }

        // ─── アバター / FX セクション ─────────────────────────────────────────

        private void UpdateAvatarCard()
        {
            bool found = _descriptor != null;
            _avatarNotFound.style.display = found ? DisplayStyle.None : DisplayStyle.Flex;

            _readFailedLabel.style.display = (found && _fxReadFailed) ? DisplayStyle.Flex : DisplayStyle.None;
            _overrideLabel.style.display   = (found && !_fxReadFailed && _fxOverrideUnsupported)
                ? DisplayStyle.Flex : DisplayStyle.None;
            _missingLabel.style.display    = (found && !_fxReadFailed && !_fxOverrideUnsupported
                                              && _fxMissing && _controller == null)
                ? DisplayStyle.Flex : DisplayStyle.None;

            bool hasController = _controller != null;
            if (hasController)
                _statusLabel.text = DenEmoLoc.Tf("ui.fx.fx.stats", _entries.Count);
            else
                _statusLabel.text = string.Empty;
        }

        // ─── 差し替えマッピング一覧 ───────────────────────────────────────────

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

            var visible = CollectVisibleEntries();

            int matchCount = 0;
            foreach (var entry in _entries)
                if (entry.MatchesTargetMesh) matchCount++;

            bool noMatch  = matchCount == 0 && _entries.Count > 0 && !_showAllClips;
            bool noEntry  = _entries.Count == 0;
            bool filtered = !noMatch && !noEntry && visible.Count == 0;

            _listEmptyLabel.style.display         = (noMatch || noEntry) ? DisplayStyle.Flex : DisplayStyle.None;
            _showAllButton.style.display          = noMatch  ? DisplayStyle.Flex : DisplayStyle.None;
            _listEmptyFilteredLabel.style.display = filtered ? DisplayStyle.Flex : DisplayStyle.None;
            _entryList.style.display              = visible.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            foreach (var entry in visible)
                _entryList.Add(MakeEntryRow(entry));
        }

        private List<FxExpressionEntry> CollectVisibleEntries()
        {
            var tokens = ShapeKeyModel.BuildSearchTokens(_search);
            var list = new List<FxExpressionEntry>();
            foreach (var entry in _entries)
            {
                if (entry.Clip == null) continue;
                if (!_showAllClips && !entry.MatchesTargetMesh) continue;
                if (_showOnlyAssigned && !_mappings.ContainsKey(entry.Clip)) continue;
                if (tokens.Length > 0)
                {
                    bool match = true;
                    var nameLower = entry.Clip.name.ToLowerInvariant();
                    foreach (var t in tokens)
                        if (nameLower.IndexOf(t.ToLowerInvariant(), System.StringComparison.Ordinal) < 0) { match = false; break; }
                    if (!match) continue;
                }
                list.Add(entry);
            }
            return list;
        }

        /// <summary>1 エントリぶんの行（＋不一致注記・展開スロット）を生成する。</summary>
        private VisualElement MakeEntryRow(FxExpressionEntry entry)
        {
            _mappings.TryGetValue(entry.Clip, out var mapping);
            bool multi    = entry.Slots.Count > 1;
            bool expanded = multi && _expanded.Contains(entry.Clip);

            var container = new VisualElement();

            var row = new VisualElement();
            row.AddToClassList("dennoko-fx-row");
            if (mapping != null && mapping.NewClip != null)
                row.AddToClassList("dennoko-fx-row--assigned");

            // 複数参照の展開トグル（単一参照は目立たせない）
            if (multi)
            {
                var fold = new Button(() =>
                {
                    if (_expanded.Contains(entry.Clip)) _expanded.Remove(entry.Clip);
                    else                                _expanded.Add(entry.Clip);
                    RebuildEntryList();
                }) { text = expanded ? "▾" : "▸" };
                fold.AddToClassList("dennoko-mini-button");
                fold.AddToClassList("dennoko-fx-fold");
                row.Add(fold);
            }
            else
            {
                var spacer = new VisualElement();
                spacer.AddToClassList("dennoko-fx-fold-spacer");
                row.Add(spacer);
            }

            // クリップ名（ホバーでシーンプレビュー）
            string label = entry.Clip.name;
            if (multi) label += " " + DenEmoLoc.Tf("ui.fx.row.usage", entry.Slots.Count);
            if (!entry.MatchesTargetMesh) label = "⚠ " + label;
            var name = new Label(label) { tooltip = BuildSlotTooltip(entry) };
            name.AddToClassList("dennoko-fx-name");
            row.Add(name);

            var arrow = new Label("→");
            arrow.AddToClassList("dennoko-fx-arrow");
            row.Add(arrow);

            // 差し替え先スロット（クリックでピッカー、D&D 受付、ホバーで新クリップをプレビュー）
            var slot = new Button
            {
                text = mapping != null && mapping.NewClip != null
                    ? mapping.NewClip.name
                    : DenEmoLoc.T("ui.fx.slot.none"),
            };
            slot.clicked += () => OpenPicker(entry, slot);
            slot.AddToClassList("dennoko-mini-button");
            slot.AddToClassList("dennoko-fx-slot");
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

            // 割当て解除
            if (mapping != null)
            {
                var clear = new Button(() =>
                {
                    _mappings.Remove(entry.Clip);
                    UpdateUI();
                }) { text = "✕", tooltip = DenEmoLoc.T("ui.fx.slot.clear.tip") };
                clear.AddToClassList("dennoko-mini-button");
                clear.AddToClassList("dennoko-icon-mini");
                row.Add(clear);
            }
            else
            {
                var spacer = new VisualElement();
                spacer.AddToClassList("dennoko-fx-fold-spacer");
                row.Add(spacer);
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
                note.AddToClassList("dennoko-text-warning");
                note.AddToClassList("dennoko-wrap");
                note.AddToClassList("dennoko-fx-indent");
                container.Add(note);
            }

            // 参照箇所ごとの対象チェック（展開時のみ）
            if (expanded)
            {
                foreach (var slotInfo in entry.Slots)
                {
                    var s = slotInfo;
                    bool enabled = mapping == null || !mapping.DisabledSlotKeys.Contains(s.SlotKey);
                    var toggle = new Toggle(s.DisplayPath) { value = enabled };
                    toggle.AddToClassList("dennoko-fx-slot-toggle");
                    toggle.RegisterValueChangedCallback(evt =>
                    {
                        if (!_mappings.TryGetValue(entry.Clip, out var m)) return;
                        if (evt.newValue) m.DisabledSlotKeys.Remove(s.SlotKey);
                        else              m.DisabledSlotKeys.Add(s.SlotKey);
                        UpdateApplyCard();
                    });
                    container.Add(toggle);
                }
            }

            return container;
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
            foreach (var slot in entry.Slots) lines.Add(slot.DisplayPath);
            return string.Join("\n", lines.ToArray());
        }

        private static AnimationClip FindDraggedClip()
        {
            foreach (var obj in DragAndDrop.objectReferences)
                if (obj is AnimationClip c) return c;
            return null;
        }

        private static void SetChipState(Button chip, bool on)
        {
            chip.EnableInClassList("dennoko-button-active", on);
            chip.EnableInClassList("dennoko-chip--on", on);
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

        // ─── 適用セクション ───────────────────────────────────────────────────

        private void UpdateApplyCard()
        {
            if (_applyCard == null) return;
            var jobs = BuildJobs(out _);

            _applyCountLabel.text = DenEmoLoc.Tf("ui.fx.apply.count", jobs.Count);

            SetChipState(_modeDuplicateChip, !_applyDirect);
            SetChipState(_modeDirectChip,    _applyDirect);

            _modeDescLabel.text = _applyDirect
                ? DenEmoLoc.T("ui.fx.mode.direct.desc")
                : DenEmoLoc.T("ui.fx.mode.duplicate.desc");
            _modeDescLabel.EnableInClassList("dennoko-text-warning",  _applyDirect);
            _modeDescLabel.EnableInClassList("dennoko-text-tertiary", !_applyDirect);

            _manualNoteLabel.style.display = (!_applyDirect && _descriptor == null)
                ? DisplayStyle.Flex : DisplayStyle.None;

            _applyButton.text = DenEmoLoc.Tf("ui.fx.apply.button", jobs.Count);
            _applyButton.SetEnabled(jobs.Count > 0 && _controller != null);

            UpdateResultBand();
        }

        private void UpdateResultBand()
        {
            bool show = _lastResult != null && _lastResult.Success;
            _resultBand.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;

            _resultSuccessLabel.text = DenEmoLoc.Tf("ui.fx.result.success", _lastResult.ReplacedCount);

            bool hasNewController = !string.IsNullOrEmpty(_lastResult.NewControllerPath);
            _resultControllerRow.style.display = hasNewController ? DisplayStyle.Flex : DisplayStyle.None;
            _resultNoteLabel.style.display     = hasNewController ? DisplayStyle.Flex : DisplayStyle.None;
            if (hasNewController)
            {
                _resultControllerLabel.text = DenEmoLoc.Tf("ui.fx.result.newController",
                    EllipsizedPath(_lastResult.NewControllerPath));
                _resultControllerLabel.tooltip = _lastResult.NewControllerPath;

                _resultNoteLabel.text = _lastResult.DescriptorUpdated
                    ? DenEmoLoc.T("ui.fx.result.descriptorSet")
                    : DenEmoLoc.T("ui.fx.result.manualSet");
                _resultNoteLabel.EnableInClassList("dennoko-text-warning",  !_lastResult.DescriptorUpdated);
                _resultNoteLabel.EnableInClassList("dennoko-text-tertiary", _lastResult.DescriptorUpdated);
            }

            bool hasBackup = !string.IsNullOrEmpty(_lastResult.BackupPath);
            _resultBackupLabel.style.display = hasBackup ? DisplayStyle.Flex : DisplayStyle.None;
            if (hasBackup)
            {
                _resultBackupLabel.text    = DenEmoLoc.Tf("ui.fx.result.backup", EllipsizedPath(_lastResult.BackupPath));
                _resultBackupLabel.tooltip = _lastResult.BackupPath;
            }
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
                StatusSink?.Invoke(DenEmoLoc.T("status.fx.applied"), 1);
            }
            else
            {
                StatusSink?.Invoke(result.Error ?? "error", 3);
            }
            window.Repaint();
        }

        private static string EllipsizedPath(string path, int maxLen = 35)
        {
            if (string.IsNullOrEmpty(path) || path.Length <= maxLen) return path;
            return "..." + path.Substring(path.Length - (maxLen - 3));
        }
    }
}
