using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using DenEmo.Models;
using DenEmo.Core;
using DenEmo.UI;

namespace DenEmo
{
    /// <summary>
    /// セクションカード（対象メッシュ / アニメーション参照 / 保存設定）と、リストカード内の検索・絞り込み、
    /// 標準フッター（出力ドック・主ボタン・情報欄）の UI Toolkit バインディング。
    /// 要素は DenEmoWindow.uxml / ShapeKeyList.uxml に定義し、ここで配線する。
    /// 対象メッシュカードとリストカード（検索行を含む）はモード間で共有し、C# がホスト間を移動する。
    /// </summary>
    public partial class DenEmoWindow
    {
        // ─── Target mesh card (B1) ────────────────────────────────────────────
        private VisualElement _targetCard;
        private ObjectField   _targetMainField;
        private Button        _targetMainClear;
        private Button        _targetRefresh;
        private VisualElement _targetInactiveWarn;
        private Label         _targetInactiveText;
        private Button        _targetInactiveFix;
        private VisualElement _targetNoShapes;
        private Label         _targetNoShapesText;
        private VisualElement _targetAdditionalRows;
        private int           _additionalRowsSignature;

        // ─── Animation source card (Pose) ─────────────────────────────────────
        private VisualElement _poseSourceCard;
        private Label         _poseSourceTitle;
        private ObjectField   _poseSourceField;
        private Toggle        _poseSourceExtraToggle;
        private Button        _poseSourceApply;
        private Button        _poseSourceAlign;

        // ─── Search & filter (リストカード内。Pose / Animation 共有) ─────────
        private TextField     _searchField;
        private Label         _searchPlaceholder;
        private Button        _searchClear;
        private Button        _chipFav;
        private Button        _chipEnabled;
        private Button        _chipNonZero;
        private Button        _chipSymmetry;
        private Button        _chipVertex;
        private Button        _chipVertexClear;
        private Button        _chipKeyed;
        private DropdownField _meshFilterDropdown;
        private Button        _previewOptionsButton;
        private string        _meshFilterSignature;

        // ─── Save settings card (Pose, B5 出力カード) ─────────────────────────
        private VisualElement _poseSaveCard;
        private Label         _poseSaveTitle;
        private TextField     _poseSaveFolderField;
        private Button        _poseSaveBrowse;
        private Toggle        _poseOverwriteToggle;
        private VisualElement _poseOverwriteGroup;
        private ObjectField   _poseOverwriteField;
        private Toggle        _poseBackupToggle;
        private Toggle        _posePreserveExtraToggle;

        // ─── Standard footer (出力ドック → フッター) ──────────────────────────
        private VisualElement _outputDock;
        private VisualElement _outputPoseRow;
        private VisualElement _outputPoseChips;
        private VisualElement _outputAnimRow;
        private Toggle        _animSaveAsNewToggle;
        private VisualElement _footer;
        private Label         _footerInfo;
        private Button        _footerSecondary;
        private Button        _footerPrimary;
        private string        _poseChipSignature;

        /// <summary>
        /// モードごとのフッター（主ボタン・副ボタン・情報欄）の内容。現在モードのものだけが表示される。
        /// Pose / Animation は DenEmoWindow がポーリングで毎回計算する。FX は FxFooter を FX 側の UI が書き換え、
        /// RefreshFooter() で即時反映する（書き換えなければ 250ms 以内のポーリングで反映される）。
        /// </summary>
        internal sealed class ModeFooter
        {
            /// <summary>フッター（と出力ドック）を表示するか。</summary>
            public bool   Visible;
            public string PrimaryText;
            public string PrimaryTooltip;
            public bool   PrimaryEnabled = true;
            public Action OnPrimary;
            /// <summary>null / 空なら副ボタンを出さない。</summary>
            public string SecondaryText;
            public string SecondaryTooltip;
            public Action OnSecondary;
            /// <summary>null / 空なら情報欄を出さない。</summary>
            public string InfoText;
            public string InfoTooltip;
            public bool   InfoWarning;
        }

        private readonly ModeFooter _poseFooter = new ModeFooter { Visible = true };
        private readonly ModeFooter _animFooter = new ModeFooter { Visible = true };
        // FX は FxSetupModeUI が内容を書き、FX ページを表示したときに Visible にする
        private readonly ModeFooter _fxFooter   = new ModeFooter { Visible = false };

        /// <summary>FX モードのフッター内容。FX 側の UI が書き換えてから RefreshFooter() を呼ぶ。</summary>
        internal ModeFooter FxFooter => _fxFooter;

        /// <summary>現在モードのフッター内容を即時に画面へ反映する。</summary>
        internal void RefreshFooter() => UpdateFooter();

        private ModeFooter CurrentFooter =>
            _currentMode == EditorMode.Pose ? _poseFooter :
            _currentMode == EditorMode.Animation ? _animFooter : _fxFooter;

        // ─── Binding ──────────────────────────────────────────────────────────

        private void BindSectionCards(VisualElement root, VisualElement listRoot)
        {
            // 対象メッシュ (B1)
            _targetCard           = root.Q<VisualElement>("target-card");
            _targetMainField      = root.Q<ObjectField>("target-main-field");
            _targetMainClear      = root.Q<Button>("target-main-clear");
            _targetRefresh        = root.Q<Button>("target-refresh");
            _targetInactiveWarn   = root.Q<VisualElement>("target-inactive-warn");
            _targetInactiveText   = root.Q<Label>("target-inactive-text");
            _targetInactiveFix    = root.Q<Button>("target-inactive-fix");
            _targetNoShapes       = root.Q<VisualElement>("target-noshapes");
            _targetNoShapesText   = root.Q<Label>("target-noshapes-text");
            _targetAdditionalRows = root.Q<VisualElement>("target-additional-rows");

            _targetMainField.objectType        = typeof(SkinnedMeshRenderer);
            _targetMainField.allowSceneObjects = true;
            _targetMainField.RegisterValueChangedCallback(evt =>
                SetMainTargetFromUI(evt.newValue as SkinnedMeshRenderer));
            _targetMainClear.clicked   += ClearMainTargetFromUI;
            _targetRefresh.clicked     += RefreshListAndCache;
            _targetInactiveFix.clicked += ActivateMainTarget;

            // アニメーション参照（Pose）
            _poseSourceCard        = root.Q<VisualElement>("pose-source-card");
            _poseSourceTitle       = root.Q<Label>("pose-source-title");
            _poseSourceField       = root.Q<ObjectField>("pose-source-field");
            _poseSourceExtraToggle = root.Q<Toggle>("pose-source-extra-toggle");
            _poseSourceApply       = root.Q<Button>("pose-source-apply");
            _poseSourceAlign       = root.Q<Button>("pose-source-align");

            _poseSourceField.objectType        = typeof(AnimationClip);
            _poseSourceField.allowSceneObjects = false;
            _poseSourceField.RegisterValueChangedCallback(evt =>
            {
                loadedClip = evt.newValue as AnimationClip;
                // 参照クリップが変わったら、前のクリップから取り込んだカーブは破棄する
                _importedExtraCurves = null;
                UpdatePoseSourceCard();
            });

            _poseSourceExtraToggle.SetValueWithoutNotify(importExtraCurves);
            _poseSourceExtraToggle.RegisterValueChangedCallback(evt =>
            {
                importExtraCurves = evt.newValue;
                if (!importExtraCurves) _importedExtraCurves = null;
            });

            _poseSourceApply.clicked += () =>
            {
                if (loadedClip == null) return;
                SetStatus(DenEmoLoc.T("status.applying"), 0, 0);

                _importedExtraCurves = importExtraCurves ? ExtraCurveSet.Capture(loadedClip) : null;
                int extraCount = _importedExtraCurves != null ? _importedExtraCurves.Count : 0;

                string res = AnimationExporter.ApplyAnimationToMesh(loadedClip, _model);
                if (res == "SUCCESS")
                {
                    SaveBlendValuesPrefs();
                    string msg = DenEmoLoc.T("dlg.apply.done.msg");
                    if (extraCount > 0) msg += DenEmoLoc.Tf("status.extraImported", extraCount);
                    SetStatus(msg, 1);
                }
                else SetStatus(res, 2);
            };
            _poseSourceAlign.clicked += () =>
            {
                if (loadedClip == null) return;
                AlignToBaseClip();
            };

            // 検索・絞り込み（リストカードの中にある）
            var searchRoot = listRoot ?? root;
            _searchField          = searchRoot.Q<TextField>("search-field");
            _searchClear          = searchRoot.Q<Button>("search-clear");
            _chipFav              = searchRoot.Q<Button>("chip-fav");
            _chipEnabled          = searchRoot.Q<Button>("chip-enabled");
            _chipNonZero          = searchRoot.Q<Button>("chip-nonzero");
            _chipSymmetry         = searchRoot.Q<Button>("chip-symmetry");
            _chipVertex           = searchRoot.Q<Button>("chip-vertex");
            _chipVertexClear      = searchRoot.Q<Button>("chip-vertex-clear");
            _chipKeyed            = searchRoot.Q<Button>("chip-keyed");
            _meshFilterDropdown   = searchRoot.Q<DropdownField>("mesh-filter-dropdown");
            _previewOptionsButton = searchRoot.Q<Button>("search-preview-options");

            _searchField.SetValueWithoutNotify(searchText);
            DennokoBlockUI.AddPlaceholder(_searchField, DenEmoLoc.T("ui.search.placeholder"));
            _searchPlaceholder = _searchField.Q<Label>(className: "dennoko-searchbar__placeholder");
            _searchField.RegisterValueChangedCallback(evt =>
            {
                searchText = evt.newValue;
                DennokoBlockUI.Show(_searchClear, !string.IsNullOrEmpty(searchText));
                // フィルター反映は OnEditorUpdate の TickListMaintenance が差分検知で行う
            });
            _searchClear.clicked += () =>
            {
                searchText = string.Empty;
                DenEmoProjectPrefs.SetString("DenEmo_SearchText", searchText);
                _searchField.SetValueWithoutNotify(string.Empty);
                UpdateSearchCard();
                Repaint();
            };

            _chipFav.clicked      += () => ToggleFilterChip(ref showOnlyFavorites, "DenEmo_ShowOnlyFavorites");
            _chipEnabled.clicked  += () => ToggleFilterChip(ref showOnlyIncluded,  "DenEmo_ShowOnlyIncluded");
            _chipNonZero.clicked  += () => ToggleFilterChip(ref showOnlyNonZero,   "DenEmo_ShowOnlyNonZero");
            _chipSymmetry.clicked += () => ToggleFilterChip(ref symmetryMode,      "DenEmo_SymmetryMode");

            _chipVertex.clicked += () =>
            {
                vertexPickMode = !vertexPickMode;
                ClearVertexGuideCache();
                SceneView.RepaintAll();
                UpdateSearchCard();
                Repaint();
            };
            _chipVertexClear.clicked += () =>
            {
                ClearVertexFilter();
                UpdateSearchCard();
                Repaint();
            };
            _chipKeyed.clicked += () =>
            {
                _animModeUI.TrackFilterEnabled = !_animModeUI.TrackFilterEnabled;
                UpdateListAnimContext();
                UpdateSearchCard();
                Repaint();
            };

            _meshFilterDropdown.RegisterValueChangedCallback(_ =>
            {
                int newFilterIdx = _meshFilterDropdown.index <= 0 ? -1 : _meshFilterDropdown.index - 1;
                if (newFilterIdx == _meshFilterIndex) return;
                _meshFilterIndex = newFilterIdx;
                DenEmoProjectPrefs.SetInt("DenEmo_MeshFilter", _meshFilterIndex);
                RefreshListAndCache();
                Repaint();
            });

            _previewOptionsButton.clicked += () =>
                UnityEditor.PopupWindow.Show(_previewOptionsButton.worldBound, new VertexPreviewOptionsPopup(this));

            // 保存設定（Pose、B5 出力カード）
            _poseSaveCard        = root.Q<VisualElement>("pose-save-card");
            _poseSaveTitle       = root.Q<Label>("pose-save-title");
            _poseSaveFolderField = root.Q<TextField>("pose-save-folder");
            _poseSaveBrowse      = root.Q<Button>("pose-save-browse");
            _poseOverwriteToggle = root.Q<Toggle>("pose-overwrite-toggle");
            _poseOverwriteGroup  = root.Q<VisualElement>("pose-overwrite-group");
            _poseOverwriteField  = root.Q<ObjectField>("pose-overwrite-field");
            _poseBackupToggle        = root.Q<Toggle>("pose-backup-toggle");
            _posePreserveExtraToggle = root.Q<Toggle>("pose-preserve-extra-toggle");

            _poseSaveFolderField.SetValueWithoutNotify(saveFolder);
            _poseSaveFolderField.RegisterValueChangedCallback(evt => saveFolder = evt.newValue);
            _poseSaveBrowse.clicked += () =>
            {
                var newPath = EditorUtility.OpenFolderPanel(DenEmoLoc.T("ui.footer.browse.title"), Application.dataPath, "");
                if (!string.IsNullOrEmpty(newPath))
                {
                    saveFolder = newPath.StartsWith(Application.dataPath)
                        ? "Assets" + newPath.Substring(Application.dataPath.Length) : newPath;
                    _poseSaveFolderField.SetValueWithoutNotify(saveFolder);
                }
            };

            _poseOverwriteToggle.SetValueWithoutNotify(overwriteSaveEnabled);
            DennokoBlockUI.Show(_poseOverwriteGroup, overwriteSaveEnabled);
            _poseOverwriteToggle.RegisterValueChangedCallback(evt =>
            {
                overwriteSaveEnabled = evt.newValue;
                DennokoBlockUI.Show(_poseOverwriteGroup, overwriteSaveEnabled);
            });

            _poseOverwriteField.objectType        = typeof(AnimationClip);
            _poseOverwriteField.allowSceneObjects = false;
            _poseOverwriteField.SetValueWithoutNotify(overwriteTargetClip);
            _poseOverwriteField.RegisterValueChangedCallback(evt =>
                overwriteTargetClip = evt.newValue as AnimationClip);

            _poseBackupToggle.SetValueWithoutNotify(autoBackup);
            _poseBackupToggle.RegisterValueChangedCallback(evt => autoBackup = evt.newValue);

            _posePreserveExtraToggle.SetValueWithoutNotify(preserveExtraCurves);
            _posePreserveExtraToggle.RegisterValueChangedCallback(evt => preserveExtraCurves = evt.newValue);

            RebuildAdditionalTargetRows();
        }

        /// <summary>標準フッター（出力ドック・情報欄・副ボタン・主ボタン）を配線する。</summary>
        private void BindFooter(VisualElement root)
        {
            _outputDock          = root.Q<VisualElement>("output-dock");
            _outputPoseRow       = root.Q<VisualElement>("output-pose-row");
            _outputPoseChips     = root.Q<VisualElement>("output-pose-chips");
            _outputAnimRow       = root.Q<VisualElement>("output-anim-row");
            _animSaveAsNewToggle = root.Q<Toggle>("anim-save-asnew");
            _footer              = root.Q<VisualElement>("footer");
            _footerInfo          = root.Q<Label>("footer-info");
            _footerSecondary     = root.Q<Button>("footer-secondary");
            _footerPrimary       = root.Q<Button>("footer-primary");

            _animSaveAsNewToggle.SetValueWithoutNotify(_animSaveAsNew);
            _animSaveAsNewToggle.RegisterValueChangedCallback(evt => _animSaveAsNew = evt.newValue);

            _poseFooter.OnPrimary = SavePoseAnimation;
            _animFooter.OnPrimary = () =>
                _animModeUI.SaveClip(saveFolder, _model, (msg, lvl) => SetStatus(msg, lvl), _animSaveAsNew);

            // ボタンは 1 つずつで、押したときの現在モードの処理を呼ぶ
            _footerPrimary.clicked   += () => CurrentFooter.OnPrimary?.Invoke();
            _footerSecondary.clicked += () => CurrentFooter.OnSecondary?.Invoke();

            // 出力カードが見え隠れしたらドックの要約チップを出し入れする
            _poseScroll.verticalScroller.valueChanged += _ => UpdateFooter();
            _poseScroll.RegisterCallback<GeometryChangedEvent>(_ => UpdateFooter());
        }

        /// <summary>言語設定に依存するセクションカードのラベルを更新する。</summary>
        private void RefreshSectionLabels()
        {
            if (_targetCard == null) return;

            _targetMainField.tooltip    = DenEmoLoc.T("ui.mesh.tooltip");
            DennokoIcons.Apply(_targetRefresh,   DennokoIcons.Refresh, "↻", DenEmoLoc.T("ui.mesh.refresh.tip"));
            DennokoIcons.Apply(_targetMainClear, DennokoIcons.Close,   "✕", DenEmoLoc.T("ui.mesh.clear.tip"));
            _targetInactiveText.text    = DenEmoLoc.T("ui.mesh.inactive.warn");
            _targetInactiveFix.text     = DenEmoLoc.T("ui.mesh.inactive.fix");
            _targetInactiveFix.tooltip  = DenEmoLoc.T("ui.mesh.inactive.fix.tip");
            _targetNoShapesText.text    = DenEmoLoc.T("ui.mesh.noShapes");

            _poseSourceTitle.text     = DenEmoLoc.T("ui.section.animSource");
            _poseSourceField.label    = DenEmoLoc.T("ui.animSource.clip.label");
            _poseSourceField.tooltip  = DenEmoLoc.T("ui.animSource.clip.tip");
            _poseSourceExtraToggle.text    = DenEmoLoc.T("ui.animSource.loadExtra");
            _poseSourceExtraToggle.tooltip = DenEmoLoc.T("ui.animSource.loadExtra.tip");
            _poseSourceApply.text     = DenEmoLoc.T("ui.animSource.loadAnim.button");
            _poseSourceApply.tooltip  = DenEmoLoc.T("ui.applyAnim.tip");
            _poseSourceAlign.text     = DenEmoLoc.T("ui.animSource.alignKeys.button");
            _poseSourceAlign.tooltip  = DenEmoLoc.T("ui.align.apply.tip");

            if (_searchPlaceholder != null) _searchPlaceholder.text = DenEmoLoc.T("ui.search.placeholder");
            DennokoIcons.Apply(_searchClear, DennokoIcons.Close, "✕", DenEmoLoc.T("ui.search.clear"));
            _chipFav.text               = DenEmoLoc.T("ui.filter.fav");
            _chipEnabled.text           = DenEmoLoc.T("ui.filter.enabled");
            _chipEnabled.tooltip        = DenEmoLoc.T("ui.filter.showIncluded.tip");
            _chipNonZero.text           = DenEmoLoc.T("ui.filter.nonzero");
            _chipSymmetry.text          = DenEmoLoc.T("ui.filter.symmetry");
            _chipSymmetry.tooltip       = DenEmoLoc.T("ui.symmetry.tip");
            _chipKeyed.text             = DenEmoLoc.T("ui.filter.keyedOnly");
            _chipKeyed.tooltip          = DenEmoLoc.T("ui.filter.keyedOnly.tip");
            _chipVertex.tooltip         = DenEmoLoc.T("ui.filter.vertex.guide");
            DennokoIcons.Apply(_chipVertexClear, DennokoIcons.Close, "✕", DenEmoLoc.T("ui.filter.vertex.clear.tip"));
            DennokoIcons.Apply(_previewOptionsButton, "d_SettingsIcon|SettingsIcon|d__Popup|_Popup", "⚙",
                DenEmoLoc.T("ui.filter.previewOptions.tip"));
            _meshFilterDropdown.tooltip = DenEmoLoc.T("ui.filter.mesh.tip");
            _meshFilterSignature        = null; // 「すべてのメッシュ」の文言を更新するため選択肢を作り直す
            // 頂点フィルターチップの文言は状態依存のため UpdateSearchCard で設定する

            _poseSaveTitle.text           = DenEmoLoc.T("ui.section.saveSettings");
            _poseSaveFolderField.label    = DenEmoLoc.T("ui.footer.saveTo");
            _poseSaveBrowse.text          = DenEmoLoc.T("ui.footer.browse");
            _poseOverwriteToggle.text     = DenEmoLoc.T("ui.footer.overwriteEnable");
            _poseOverwriteToggle.tooltip  = DenEmoLoc.T("ui.footer.overwriteEnable.tip");
            _poseOverwriteField.label     = DenEmoLoc.T("ui.footer.overwriteTarget");
            _poseBackupToggle.text        = DenEmoLoc.T("ui.footer.autoBackup");
            _poseBackupToggle.tooltip     = DenEmoLoc.T("ui.footer.autoBackup.tip");
            _posePreserveExtraToggle.text    = DenEmoLoc.T("ui.footer.preserveExtra");
            _posePreserveExtraToggle.tooltip = DenEmoLoc.T("ui.footer.preserveExtra.tip");

            if (_animSaveAsNewToggle != null)
            {
                _animSaveAsNewToggle.text    = DenEmoLoc.T("ui.animMode.saveAsNew");
                _animSaveAsNewToggle.tooltip = DenEmoLoc.T("ui.animMode.saveAsNew.tip");
            }
            _poseChipSignature = null; // ドックの要約チップの文言を作り直す

            UpdateSectionCards();
            UpdateFooter();
        }

        // ─── State reflection (250ms ポーリング + 操作直後に呼ぶ) ─────────────

        /// <summary>IMGUI・SceneView 側の操作で変わる状態をセクションカードへ反映する。</summary>
        private void UpdateSectionCards()
        {
            if (_targetCard == null) return;
            UpdateTargetCard();
            UpdateSearchCard();
            UpdatePoseSourceCard();
        }

        private void UpdateTargetCard()
        {
            var mainSmr = _model.TargetSkinnedMesh;
            if (!ReferenceEquals(_targetMainField.value, mainSmr))
                _targetMainField.SetValueWithoutNotify(mainSmr);

            // 破棄されたサブメッシュを除去し、行構成が変わっていれば再構築する
            if (_additionalTargets.RemoveAll(item => item == null) > 0)
            {
                ClampMeshFilterIndex();
                RefreshListAndCache();
            }
            if (ComputeAdditionalRowsSignature() != _additionalRowsSignature)
                RebuildAdditionalTargetRows();

            bool hasMain   = mainSmr != null;
            bool inactive  = hasMain && (!mainSmr.gameObject.activeInHierarchy || !mainSmr.enabled);
            bool hasShapes = HasUsableTarget();
            // 対象が空のときは対象を選ぶ行だけ（案内はリストカードの空の状態が出す）
            DennokoBlockUI.Show(_targetInactiveWarn, inactive);
            DennokoBlockUI.Show(_targetNoShapes, hasMain && !hasShapes);
            DennokoBlockUI.Show(_targetMainClear, hasMain);
        }

        private void UpdateSearchCard()
        {
            if (_searchField.value != searchText)
                _searchField.SetValueWithoutNotify(searchText);
            bool hasText = !string.IsNullOrEmpty(searchText);
            DennokoBlockUI.Show(_searchClear, hasText);
            // SetValueWithoutNotify では AddPlaceholder の通知が走らないため、ここで表示を合わせる
            DennokoBlockUI.Show(_searchPlaceholder, !hasText);

            SetChipState(_chipFav,      showOnlyFavorites);
            SetChipState(_chipEnabled,  showOnlyIncluded);
            SetChipState(_chipNonZero,  showOnlyNonZero);
            SetChipState(_chipSymmetry, symmetryMode);

            // 頂点フィルター（SceneView でのピック結果もここで追従する）
            if (vertexPickMode)
            {
                _chipVertex.text = DenEmoLoc.T("ui.filter.vertex.cancel");
                SetChipState(_chipVertex, true);
                DennokoBlockUI.Show(_chipVertexClear, false);
            }
            else
            {
                _chipVertex.text = vertexFilterActive
                    ? DenEmoLoc.Tf("ui.filter.vertex.active", selectedVertexIndex)
                    : DenEmoLoc.T("ui.filter.vertex");
                SetChipState(_chipVertex, vertexFilterActive);
                DennokoBlockUI.Show(_chipVertexClear, vertexFilterActive);
            }

            // キー有りのみ（Animation モードでクリップ設定時のみ）
            bool showKeyed = _currentMode == EditorMode.Animation && _animModeUI.ClipModel.Clip != null;
            DennokoBlockUI.Show(_chipKeyed, showKeyed);
            if (showKeyed) SetChipState(_chipKeyed, _animModeUI.TrackFilterEnabled);

            // メッシュ絞り込み（複数対象時のみ）
            var allTargets = GetAllTargetMeshes();
            bool showMeshFilter = allTargets.Count > 1;
            DennokoBlockUI.Show(_meshFilterDropdown, showMeshFilter);
            if (showMeshFilter)
            {
                var opts = new List<string>(BuildMeshFilterOptions(allTargets));
                string sig = string.Join("\n", opts);
                if (sig != _meshFilterSignature)
                {
                    _meshFilterSignature = sig;
                    _meshFilterDropdown.choices = opts;
                }
                int displayIdx = (_meshFilterIndex < 0 || _meshFilterIndex >= allTargets.Count) ? 0 : _meshFilterIndex + 1;
                if (_meshFilterDropdown.index != displayIdx)
                    _meshFilterDropdown.SetValueWithoutNotify(opts[displayIdx]);
            }
        }

        private void UpdatePoseSourceCard()
        {
            if (!ReferenceEquals(_poseSourceField.value, loadedClip))
                _poseSourceField.SetValueWithoutNotify(loadedClip);
            bool hasClip = loadedClip != null;
            _poseSourceApply.SetEnabled(hasClip);
            _poseSourceAlign.SetEnabled(hasClip);
        }

        // ─── Standard footer ──────────────────────────────────────────────────

        /// <summary>現在モードのフッター内容を計算し、出力ドック・フッターへ反映する。</summary>
        private void UpdateFooter()
        {
            if (_footer == null) return;

            bool hasTarget = HasUsableTarget();
            if (_currentMode == EditorMode.Pose) ComputePoseFooter(hasTarget);
            else if (_currentMode == EditorMode.Animation) ComputeAnimFooter(hasTarget);

            var f = CurrentFooter;
            DennokoBlockUI.Show(_footer, f.Visible);

            string primaryText = f.PrimaryText ?? string.Empty;
            if (_footerPrimary.text != primaryText) _footerPrimary.text = primaryText;
            _footerPrimary.tooltip = f.PrimaryTooltip;
            if (_footerPrimary.enabledSelf != f.PrimaryEnabled) _footerPrimary.SetEnabled(f.PrimaryEnabled);

            bool showSecondary = !string.IsNullOrEmpty(f.SecondaryText);
            DennokoBlockUI.Show(_footerSecondary, showSecondary);
            if (showSecondary)
            {
                if (_footerSecondary.text != f.SecondaryText) _footerSecondary.text = f.SecondaryText;
                _footerSecondary.tooltip = f.SecondaryTooltip;
            }

            bool showInfo = !string.IsNullOrEmpty(f.InfoText);
            DennokoBlockUI.Show(_footerInfo, showInfo);
            if (showInfo)
            {
                if (_footerInfo.text != f.InfoText) _footerInfo.text = f.InfoText;
                _footerInfo.tooltip = f.InfoTooltip;
                _footerInfo.EnableInClassList("dennoko-footer__info--warning", f.InfoWarning);
            }

            // 出力ドック: Pose は保存設定の要約チップ（出力カードが見えていないときだけ）、Animation は保存オプション
            bool poseDock = f.Visible && _currentMode == EditorMode.Pose && UpdatePoseDockChips();
            bool animDock = f.Visible && _currentMode == EditorMode.Animation
                            && hasTarget && _animModeUI.ClipModel.Clip != null;
            DennokoBlockUI.Show(_outputPoseRow, poseDock);
            DennokoBlockUI.Show(_outputAnimRow, animDock);
            bool dock = poseDock || animDock;
            DennokoBlockUI.Show(_outputDock, dock);
            _footer.EnableInClassList("dennoko-footer--joined", dock);
        }

        private void ComputePoseFooter(bool hasTarget)
        {
            var f = _poseFooter;
            f.PrimaryText    = DenEmoLoc.T("ui.footer.primary.pose");
            f.PrimaryEnabled = hasTarget;
            f.PrimaryTooltip = hasTarget ? DenEmoLoc.T("ui.footer.saveAnim") : DenEmoLoc.T("ui.footer.primary.needTarget");
            f.InfoWarning    = false;
            f.InfoTooltip    = null;
            if (!hasTarget) { f.InfoText = null; return; }

            int eligible = 0, included = 0;
            foreach (var item in _model.Items)
            {
                if (item.IsVrcExcluded(_model.IsAnimationMode) || item.IsLipSyncShape) continue;
                eligible++;
                if (item.IsIncluded) included++;
            }
            f.InfoText = DenEmoLoc.Tf("ui.footer.info.included", included, eligible);
        }

        private void ComputeAnimFooter(bool hasTarget)
        {
            var f = _animFooter;
            var clip = _animModeUI.ClipModel.Clip;
            f.PrimaryText    = DenEmoLoc.T("ui.footer.primary.anim");
            f.PrimaryEnabled = hasTarget && clip != null;
            f.PrimaryTooltip = !hasTarget ? DenEmoLoc.T("ui.footer.primary.needTarget")
                             : clip == null ? DenEmoLoc.T("ui.footer.primary.needClip")
                             : DenEmoLoc.T("ui.animMode.save.button");

            if (!hasTarget || clip == null)
            {
                f.InfoText = null; f.InfoTooltip = null; f.InfoWarning = false;
                return;
            }
            var tracks = _animModeUI.ClipModel.Tracks;
            if (tracks.Count == 0)
            {
                f.InfoText    = DenEmoLoc.T("ui.animMode.noKeys.short");
                f.InfoTooltip = DenEmoLoc.T("ui.animMode.noKeys.warn");
                f.InfoWarning = true;
            }
            else
            {
                int keyTotal = 0;
                foreach (var track in tracks)
                    keyTotal += track.KeyTimes.Length;
                f.InfoText    = DenEmoLoc.Tf("ui.animMode.keyStats", tracks.Count, keyTotal);
                f.InfoTooltip = null;
                f.InfoWarning = false;
            }
        }

        /// <summary>
        /// Pose の出力ドックに保存設定の要約チップを並べる（B5）。出力カードが上部スクロールの中で見えているときは
        /// 同じ情報を 2 か所に出さないため false を返してドックを隠す。
        /// </summary>
        private bool UpdatePoseDockChips()
        {
            if (_outputPoseChips == null || _poseSaveCard == null || _poseScroll == null) return false;
            if (IsVisibleInScroll(_poseSaveCard, _poseScroll)) return false;

            bool overwrite = overwriteSaveEnabled && overwriteTargetClip != null;
            var chips = new List<(string icon, string text)>();
            if (overwrite)
            {
                chips.Add((DennokoIcons.Save, DenEmoLoc.Tf("ui.output.chip.overwrite", overwriteTargetClip.name)));
                if (!autoBackup)          chips.Add((null, DenEmoLoc.T("ui.output.chip.noBackup")));
                if (!preserveExtraCurves) chips.Add((null, DenEmoLoc.T("ui.output.chip.dropExtra")));
            }
            else
            {
                chips.Add((DennokoIcons.Folder, DenEmoLoc.Tf("ui.output.chip.folder", ShortenPath(saveFolder))));
            }

            var sb = new System.Text.StringBuilder();
            foreach (var c in chips) sb.Append(c.text).Append('\n');
            string sig = sb.ToString();
            if (sig != _poseChipSignature)
            {
                _poseChipSignature = sig;
                _outputPoseChips.Clear();
                string tip = DenEmoLoc.T("ui.output.chip.tip");
                foreach (var c in chips)
                    _outputPoseChips.Add(DennokoBlockUI.Chip(c.icon, c.text, tip, () => _poseScroll.ScrollTo(_poseSaveCard)));
            }
            return true;
        }

        /// <summary>要素が ScrollView のビューポート内に見えているか（一部でも 48px 以上、または全体が見えていれば true）。</summary>
        private static bool IsVisibleInScroll(VisualElement element, ScrollView scroll)
        {
            if (element.resolvedStyle.display == DisplayStyle.None) return false;
            var b  = element.worldBound;
            var vp = scroll.contentViewport.worldBound;
            if (float.IsNaN(b.height) || float.IsNaN(vp.height) || b.height <= 0f || vp.height <= 0f) return false;
            float overlap = Mathf.Min(b.yMax, vp.yMax) - Mathf.Max(b.yMin, vp.yMin);
            return overlap >= Mathf.Min(b.height - 1f, 48f);
        }

        /// <summary>長いパスは末尾の 2 階層だけを残して省略する（チップ用）。</summary>
        private static string ShortenPath(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Length <= 36) return path;
            var parts = path.Replace('\\', '/').TrimEnd('/').Split('/');
            if (parts.Length <= 2) return path;
            return "…/" + parts[parts.Length - 2] + "/" + parts[parts.Length - 1];
        }

        // ─── Target mesh helpers ──────────────────────────────────────────────

        /// <summary>メイン対象メッシュの変更（ObjectField / ドラッグ&ドロップ / 選択から 共通）。</summary>
        private void SetMainTargetFromUI(SkinnedMeshRenderer newSmr)
        {
            _hasManuallyCleared = false;
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
            else if (_currentMode == EditorMode.FxSetup)
                _fxSetupUI.OnTargetChanged(_model);
            UpdateSectionCards();
            UpdateAnimSectionsVisibility();
            UpdatePoseSectionsVisibility();
            UpdateFooter();
            Repaint();
        }

        private void ClearMainTargetFromUI()
        {
            _hasManuallyCleared = true;
            _listUI.StopThrottle();
            _model.SetTarget(null);
            ClampMeshFilterIndex();
            RefreshListAndCache();
            // ホバープレビューのウェイト復元と検出状態のクリア
            if (_currentMode == EditorMode.FxSetup)
                _fxSetupUI.OnTargetChanged(_model);
            UpdateSectionCards();
            UpdateAnimSectionsVisibility();
            UpdatePoseSectionsVisibility();
            UpdateFooter();
            Repaint();
        }

        /// <summary>リストカードの空の状態「選択から」: Hierarchy の選択からシェイプキーを持つ SMR を探して対象にする。</summary>
        private void PickTargetFromSelection()
        {
            SkinnedMeshRenderer found = null;
            var go = Selection.activeGameObject;
            if (go != null)
            {
                var own = go.GetComponent<SkinnedMeshRenderer>();
                if (own != null && own.sharedMesh != null && own.sharedMesh.blendShapeCount > 0)
                {
                    found = own;
                }
                else
                {
                    // アバターのルートなどを選んだときは Body を優先し、無ければ最初に見つかったもの
                    foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        if (smr == null || smr.sharedMesh == null || smr.sharedMesh.blendShapeCount == 0) continue;
                        if (string.Equals(smr.name, "Body", StringComparison.OrdinalIgnoreCase)) { found = smr; break; }
                        if (found == null) found = smr;
                    }
                }
            }

            if (found == null)
            {
                SetStatus(DenEmoLoc.T("status.mesh.selectionNone"), 2);
                return;
            }
            SetMainTargetFromUI(found);
        }

        /// <summary>対象カードの警告行「有効にする」: 非アクティブな自身・親と無効なコンポーネントを有効にする（Undo 対応）。</summary>
        private void ActivateMainTarget()
        {
            var smr = _model.TargetSkinnedMesh;
            if (smr == null) return;

            var targets = new List<UnityEngine.Object>();
            for (var t = smr.transform; t != null; t = t.parent)
                if (!t.gameObject.activeSelf) targets.Add(t.gameObject);
            if (!smr.enabled) targets.Add(smr);
            if (targets.Count == 0) return;

            Undo.RecordObjects(targets.ToArray(), "Activate Target Mesh");
            foreach (var o in targets)
            {
                if (o is GameObject g) g.SetActive(true);
                else if (o is SkinnedMeshRenderer r) r.enabled = true;
            }
            SetStatus(DenEmoLoc.T("status.mesh.activated"), 1);
            UpdateSectionCards();
            Repaint();
        }

        /// <summary>サブメッシュ行 + 追加用の空行を再構築する。</summary>
        private void RebuildAdditionalTargetRows()
        {
            _targetAdditionalRows.Clear();

            for (int i = 0; i < _additionalTargets.Count; i++)
            {
                int index = i;
                var field = MakeAdditionalRow(_additionalTargets[index], out var removeButton);
                field.RegisterValueChangedCallback(evt =>
                {
                    var smr = evt.newValue as SkinnedMeshRenderer;
                    if (smr == null) _additionalTargets.RemoveAt(index);
                    else _additionalTargets[index] = smr;
                    OnAdditionalTargetsChanged();
                });
                removeButton.clicked += () =>
                {
                    _additionalTargets.RemoveAt(index);
                    OnAdditionalTargetsChanged();
                };
            }

            // 追加用の空行（メイン対象があるときのみ）。外すボタンは位置合わせのため場所だけ残す
            if (_model.TargetSkinnedMesh != null)
            {
                var field = MakeAdditionalRow(null, out var removeButton);
                removeButton.style.visibility = Visibility.Hidden;
                field.RegisterValueChangedCallback(evt =>
                {
                    var smr = evt.newValue as SkinnedMeshRenderer;
                    if (smr == null) return;
                    _additionalTargets.Add(smr);
                    OnAdditionalTargetsChanged();
                });
            }

            _additionalRowsSignature = ComputeAdditionalRowsSignature();
        }

        private ObjectField MakeAdditionalRow(SkinnedMeshRenderer value, out Button removeButton)
        {
            // 動的な行なので C# で組むが、見た目は USS クラス（.dennoko-target-row）に任せる
            var row = new VisualElement();
            row.AddToClassList("dennoko-target-row");
            row.AddToClassList("dennoko-target-row--sub");

            var prefix = new Label("+") { tooltip = DenEmoLoc.T("ui.mesh.additional.tip") };
            prefix.AddToClassList("dennoko-target-row__prefix");

            var field = new ObjectField
            {
                objectType        = typeof(SkinnedMeshRenderer),
                allowSceneObjects = true,
                tooltip           = DenEmoLoc.T("ui.mesh.additional.tip"),
            };
            field.SetValueWithoutNotify(value);

            removeButton = new Button();
            removeButton.AddToClassList("dennoko-icon-button");
            DennokoIcons.Apply(removeButton, DennokoIcons.Close, "✕", DenEmoLoc.T("ui.mesh.additional.remove.tip"));

            row.Add(prefix);
            row.Add(field);
            row.Add(removeButton);
            _targetAdditionalRows.Add(row);
            return field;
        }

        private void OnAdditionalTargetsChanged()
        {
            ClampMeshFilterIndex();
            RefreshListAndCache();
            RebuildAdditionalTargetRows();
            UpdateSectionCards();
            UpdateFooter();
            Repaint();
        }

        private int ComputeAdditionalRowsSignature()
        {
            // メイン対象の有無（追加用空行の有無）+ サブメッシュの構成
            int sig = _model.TargetSkinnedMesh != null ? 17 : 3;
            foreach (var smr in _additionalTargets)
                sig = sig * 31 + (smr != null ? smr.GetInstanceID() : 0);
            return sig;
        }

        // ─── Filter helpers ───────────────────────────────────────────────────

        private void ToggleFilterChip(ref bool state, string prefsKey)
        {
            state = !state;
            DenEmoProjectPrefs.SetBool(prefsKey, state);
            UpdateSearchCard();
            // フィルター反映は OnEditorUpdate の TickListMaintenance が差分検知で行う
            Repaint();
        }

        /// <summary>リストカードの空の状態「絞り込みを解除」: 検索語・絞り込みチップ・頂点・キー有りのみを解除する。</summary>
        private void ClearAllFilters()
        {
            searchText        = string.Empty;
            showOnlyFavorites = false;
            showOnlyIncluded  = false;
            showOnlyNonZero   = false;
            DenEmoProjectPrefs.SetString("DenEmo_SearchText", searchText);
            DenEmoProjectPrefs.SetBool("DenEmo_ShowOnlyFavorites", false);
            DenEmoProjectPrefs.SetBool("DenEmo_ShowOnlyIncluded",  false);
            DenEmoProjectPrefs.SetBool("DenEmo_ShowOnlyNonZero",   false);
            if (vertexFilterActive || vertexPickMode) ClearVertexFilter();
            _animModeUI.TrackFilterEnabled = false;
            UpdateListAnimContext();
            UpdateSearchCard();
            UpdateVisibility();
            Repaint();
        }

        private static void SetChipState(Button chip, bool on)
        {
            chip.EnableInClassList("dennoko-button-active", on);
        }

        private string[] BuildMeshFilterOptions(List<SkinnedMeshRenderer> targets)
        {
            var opts = new string[targets.Count + 1];
            opts[0] = DenEmoLoc.T("ui.filter.mesh.all");
            for (int i = 0; i < targets.Count; i++)
                opts[i + 1] = targets[i] != null ? targets[i].name : "?";
            return opts;
        }

        // ─── Save (Pose) ──────────────────────────────────────────────────────

        private void SavePoseAnimation()
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
                var err = AnimationExporter.SaveAnimationClipToPath(_model, clipPath, out string path, autoBackup,
                                                                    preserveExtraCurves, _importedExtraCurves);
                if (err != null) SetStatus(err, 3);
                else SetStatus(DenEmoLoc.Tf("dlg.save.done.msg", path), 1);
            }
            else
            {
                SetStatus(DenEmoLoc.T("status.saving"), 0, 0);
                var err = AnimationExporter.SaveAnimationClip(_model, saveFolder, out string path, autoBackup,
                                                              preserveExtraCurves, _importedExtraCurves);
                if (err != null) SetStatus(err, 3);
                else SetStatus(DenEmoLoc.Tf("dlg.save.done.msg", path), 1);
            }
        }

        private bool HasIncludedShapeKeys()
        {
            foreach (var item in _model.Items)
                if (item.IsIncluded && !item.IsVrcExcluded(_model.IsAnimationMode) && !item.IsLipSyncShape) return true;
            return false;
        }

        // ─── Drag and drop ────────────────────────────────────────────────────

        /// <summary>UI Toolkit 領域全体への SkinnedMeshRenderer ドロップを受け付ける。</summary>
        private void RegisterRootDragAndDrop(VisualElement root)
        {
            root.RegisterCallback<DragUpdatedEvent>(_ =>
            {
                if (FindDraggedSkinnedMesh() != null)
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            });
            root.RegisterCallback<DragPerformEvent>(_ =>
            {
                var smr = FindDraggedSkinnedMesh();
                if (smr == null) return;
                DragAndDrop.AcceptDrag();
                SetMainTargetFromUI(smr);
            });
        }

        private static SkinnedMeshRenderer FindDraggedSkinnedMesh()
        {
            foreach (var obj in DragAndDrop.objectReferences)
            {
                if (obj is GameObject go)
                {
                    var smr = go.GetComponent<SkinnedMeshRenderer>();
                    if (smr != null) return smr;
                }
                else if (obj is SkinnedMeshRenderer smr2)
                {
                    return smr2;
                }
            }
            return null;
        }
    }
}
