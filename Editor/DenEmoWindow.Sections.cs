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
    /// セクションカード（対象メッシュ / アニメーション参照 / 検索・絞り込み / 保存）の
    /// UI Toolkit バインディング。要素は DenEmoWindow.uxml に定義し、ここで配線する。
    /// 対象メッシュカードと検索カードはモード間で共有し、C# がホスト間を移動する。
    /// </summary>
    public partial class DenEmoWindow
    {
        // ─── Target mesh card ─────────────────────────────────────────────────
        private VisualElement _targetCard;
        private Label         _targetTitle;
        private Label         _targetMainLabel;
        private ObjectField   _targetMainField;
        private Button        _targetMainClear;
        private Label         _targetMissing;
        private Label         _targetInactiveWarn;
        private VisualElement _targetAdditionalRows;
        private Label         _targetNoShapes;
        private Button        _targetRefresh;
        private int           _additionalRowsSignature;

        // ─── Animation source card (Pose) ─────────────────────────────────────
        private VisualElement _poseSourceCard;
        private Label         _poseSourceTitle;
        private Label         _poseSourceLabel;
        private ObjectField   _poseSourceField;
        private Button        _poseSourceApply;
        private Button        _poseSourceAlign;

        // ─── Search & filter card (Pose / Animation 共有) ─────────────────────
        private VisualElement _searchCard;
        private Label         _searchTitle;
        private Label         _searchKeywordLabel;
        private TextField     _searchField;
        private Button        _searchClear;
        private Button        _chipFav;
        private Button        _chipEnabled;
        private Button        _chipNonZero;
        private Button        _chipSymmetry;
        private Button        _chipVertex;
        private Button        _chipVertexClear;
        private Button        _chipKeyed;
        private Label         _meshFilterLabel;
        private DropdownField _meshFilterDropdown;
        private Button        _previewOptionsButton;
        private string        _meshFilterSignature;

        // ─── Save cards ───────────────────────────────────────────────────────
        private VisualElement _poseSaveCard;
        private Label         _poseSaveTitle;
        private Label         _poseSaveFolderLabel;
        private TextField     _poseSaveFolderField;
        private Button        _poseSaveBrowse;
        private Toggle        _poseOverwriteToggle;
        private VisualElement _poseOverwriteGroup;
        private Label         _poseOverwriteLabel;
        private ObjectField   _poseOverwriteField;
        private Toggle        _poseBackupToggle;
        private Button        _poseSaveButton;

        private VisualElement _animSaveCard;
        private Label         _animSaveTitle;
        private Label         _animSaveStats;
        private Label         _animSaveNoKeys;
        private Toggle        _animSaveAsNewToggle;
        private Button        _animSaveButton;

        // ─── Binding ──────────────────────────────────────────────────────────

        private void BindSectionCards(VisualElement root)
        {
            // 対象メッシュ
            _targetCard           = root.Q<VisualElement>("target-card");
            _targetTitle          = root.Q<Label>("target-title");
            _targetMainLabel      = root.Q<Label>("target-main-label");
            _targetMainField      = root.Q<ObjectField>("target-main-field");
            _targetMainClear      = root.Q<Button>("target-main-clear");
            _targetMissing        = root.Q<Label>("target-missing");
            _targetInactiveWarn   = root.Q<Label>("target-inactive-warn");
            _targetAdditionalRows = root.Q<VisualElement>("target-additional-rows");
            _targetNoShapes       = root.Q<Label>("target-noshapes");
            _targetRefresh        = root.Q<Button>("target-refresh");

            _targetMainField.objectType        = typeof(SkinnedMeshRenderer);
            _targetMainField.allowSceneObjects = true;
            _targetMainField.RegisterValueChangedCallback(evt =>
                SetMainTargetFromUI(evt.newValue as SkinnedMeshRenderer));
            _targetMainClear.clicked += ClearMainTargetFromUI;
            _targetRefresh.clicked   += RefreshListAndCache;

            // アニメーション参照（Pose）
            _poseSourceCard  = root.Q<VisualElement>("pose-source-card");
            _poseSourceTitle = root.Q<Label>("pose-source-title");
            _poseSourceLabel = root.Q<Label>("pose-source-label");
            _poseSourceField = root.Q<ObjectField>("pose-source-field");
            _poseSourceApply = root.Q<Button>("pose-source-apply");
            _poseSourceAlign = root.Q<Button>("pose-source-align");

            _poseSourceField.objectType        = typeof(AnimationClip);
            _poseSourceField.allowSceneObjects = false;
            _poseSourceField.RegisterValueChangedCallback(evt =>
            {
                loadedClip = evt.newValue as AnimationClip;
                UpdatePoseSourceCard();
            });
            _poseSourceApply.clicked += () =>
            {
                if (loadedClip == null) return;
                SetStatus(DenEmoLoc.T("status.applying"), 0, 0);
                string res = AnimationExporter.ApplyAnimationToMesh(loadedClip, _model);
                if (res == "SUCCESS") { SaveBlendValuesPrefs(); SetStatus(DenEmoLoc.T("dlg.apply.done.msg"), 1); }
                else SetStatus(res, 2);
            };
            _poseSourceAlign.clicked += () =>
            {
                if (loadedClip == null) return;
                AlignToBaseClip();
            };

            // 検索・絞り込み
            _searchCard           = root.Q<VisualElement>("search-card");
            _searchTitle          = root.Q<Label>("search-title");
            _searchKeywordLabel   = root.Q<Label>("search-keyword-label");
            _searchField          = root.Q<TextField>("search-field");
            _searchClear          = root.Q<Button>("search-clear");
            _chipFav              = root.Q<Button>("chip-fav");
            _chipEnabled          = root.Q<Button>("chip-enabled");
            _chipNonZero          = root.Q<Button>("chip-nonzero");
            _chipSymmetry         = root.Q<Button>("chip-symmetry");
            _chipVertex           = root.Q<Button>("chip-vertex");
            _chipVertexClear      = root.Q<Button>("chip-vertex-clear");
            _chipKeyed            = root.Q<Button>("chip-keyed");
            _meshFilterLabel      = root.Q<Label>("mesh-filter-label");
            _meshFilterDropdown   = root.Q<DropdownField>("mesh-filter-dropdown");
            _previewOptionsButton = root.Q<Button>("search-preview-options");

            _searchField.SetValueWithoutNotify(searchText);
            _searchField.RegisterValueChangedCallback(evt =>
            {
                searchText = evt.newValue;
                _searchClear.style.display = string.IsNullOrEmpty(searchText)
                    ? DisplayStyle.None : DisplayStyle.Flex;
                // フィルター反映は OnEditorUpdate の TickListMaintenance が差分検知で行う
            });
            _searchClear.clicked += () =>
            {
                searchText = string.Empty;
                DenEmoProjectPrefs.SetString("DenEmo_SearchText", searchText);
                _searchField.SetValueWithoutNotify(string.Empty);
                _searchClear.style.display = DisplayStyle.None;
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

            // 保存設定（Pose）
            _poseSaveCard        = root.Q<VisualElement>("pose-save-card");
            _poseSaveTitle       = root.Q<Label>("pose-save-title");
            _poseSaveFolderLabel = root.Q<Label>("pose-save-folder-label");
            _poseSaveFolderField = root.Q<TextField>("pose-save-folder");
            _poseSaveBrowse      = root.Q<Button>("pose-save-browse");
            _poseOverwriteToggle = root.Q<Toggle>("pose-overwrite-toggle");
            _poseOverwriteGroup  = root.Q<VisualElement>("pose-overwrite-group");
            _poseOverwriteLabel  = root.Q<Label>("pose-overwrite-label");
            _poseOverwriteField  = root.Q<ObjectField>("pose-overwrite-field");
            _poseBackupToggle    = root.Q<Toggle>("pose-backup-toggle");
            _poseSaveButton      = root.Q<Button>("pose-save-button");

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
            _poseOverwriteGroup.style.display = overwriteSaveEnabled ? DisplayStyle.Flex : DisplayStyle.None;
            _poseOverwriteToggle.RegisterValueChangedCallback(evt =>
            {
                overwriteSaveEnabled = evt.newValue;
                _poseOverwriteGroup.style.display = overwriteSaveEnabled ? DisplayStyle.Flex : DisplayStyle.None;
            });

            _poseOverwriteField.objectType        = typeof(AnimationClip);
            _poseOverwriteField.allowSceneObjects = false;
            _poseOverwriteField.SetValueWithoutNotify(overwriteTargetClip);
            _poseOverwriteField.RegisterValueChangedCallback(evt =>
                overwriteTargetClip = evt.newValue as AnimationClip);

            _poseBackupToggle.SetValueWithoutNotify(autoBackup);
            _poseBackupToggle.RegisterValueChangedCallback(evt => autoBackup = evt.newValue);

            _poseSaveButton.clicked += SavePoseAnimation;

            // アニメーション保存（Animation）
            _animSaveCard        = root.Q<VisualElement>("anim-save-card");
            _animSaveTitle       = root.Q<Label>("anim-save-title");
            _animSaveStats       = root.Q<Label>("anim-save-stats");
            _animSaveNoKeys      = root.Q<Label>("anim-save-nokeys");
            _animSaveAsNewToggle = root.Q<Toggle>("anim-save-asnew");
            _animSaveButton      = root.Q<Button>("anim-save-button");

            _animSaveAsNewToggle.SetValueWithoutNotify(_animSaveAsNew);
            _animSaveAsNewToggle.RegisterValueChangedCallback(evt => _animSaveAsNew = evt.newValue);
            _animSaveButton.clicked += () =>
                _animModeUI.SaveClip(saveFolder, _model, (msg, lvl) => SetStatus(msg, lvl), _animSaveAsNew);

            RebuildAdditionalTargetRows();
            RefreshSectionLabels();
            UpdateSectionCards();
        }

        /// <summary>言語設定に依存するセクションカードのラベルを更新する。</summary>
        private void RefreshSectionLabels()
        {
            if (_targetCard == null) return;

            _targetTitle.text        = DenEmoLoc.T("ui.section.targetMesh");
            _targetMainLabel.text    = DenEmoLoc.T("ui.mesh.label");
            _targetMainLabel.tooltip = DenEmoLoc.T("ui.mesh.tooltip");
            _targetMainField.tooltip = DenEmoLoc.T("ui.mesh.tooltip");
            _targetMissing.text      = DenEmoLoc.T("ui.mesh.missing");
            _targetInactiveWarn.text = "⚠ " + DenEmoLoc.T("ui.mesh.inactive.warn");
            _targetNoShapes.text     = DenEmoLoc.T("ui.mesh.noShapes");
            _targetRefresh.text      = DenEmoLoc.T("ui.footer.refresh");

            _poseSourceTitle.text     = DenEmoLoc.T("ui.section.animSource");
            _poseSourceLabel.text     = DenEmoLoc.T("ui.animSource.clip.label");
            _poseSourceField.tooltip  = DenEmoLoc.T("ui.animSource.clip.tip");
            _poseSourceApply.text     = DenEmoLoc.T("ui.animSource.loadAnim.button");
            _poseSourceApply.tooltip  = DenEmoLoc.T("ui.applyAnim.tip");
            _poseSourceAlign.text     = DenEmoLoc.T("ui.animSource.alignKeys.button");
            _poseSourceAlign.tooltip  = DenEmoLoc.T("ui.align.apply.tip");

            _searchTitle.text           = DenEmoLoc.T("ui.section.searchFilter");
            _searchKeywordLabel.text    = DenEmoLoc.T("ui.filter.keyword");
            _chipFav.text               = DenEmoLoc.T("ui.filter.fav");
            _chipEnabled.text           = DenEmoLoc.T("ui.filter.enabled");
            _chipNonZero.text           = DenEmoLoc.T("ui.filter.nonzero");
            _chipSymmetry.text          = DenEmoLoc.T("ui.filter.symmetry");
            _chipSymmetry.tooltip       = DenEmoLoc.T("ui.symmetry.tip");
            _chipKeyed.text             = DenEmoLoc.T("ui.filter.keyedOnly");
            _chipKeyed.tooltip          = DenEmoLoc.T("ui.filter.keyedOnly.tip");
            _previewOptionsButton.text  = DenEmoLoc.T("ui.filter.previewOptions");
            // 頂点フィルターチップの文言は状態依存のため UpdateSearchCard で設定する

            _poseSaveTitle.text           = DenEmoLoc.T("ui.section.saveSettings");
            _poseSaveFolderLabel.text     = DenEmoLoc.T("ui.footer.saveTo");
            _poseSaveBrowse.text          = DenEmoLoc.T("ui.footer.browse");
            _poseOverwriteToggle.text     = DenEmoLoc.T("ui.footer.overwriteEnable");
            _poseOverwriteToggle.tooltip  = DenEmoLoc.T("ui.footer.overwriteEnable.tip");
            _poseOverwriteLabel.text      = DenEmoLoc.T("ui.footer.overwriteTarget");
            _poseBackupToggle.text        = DenEmoLoc.T("ui.footer.autoBackup");
            _poseBackupToggle.tooltip     = DenEmoLoc.T("ui.footer.autoBackup.tip");
            _poseSaveButton.text          = DenEmoLoc.T("ui.footer.saveAnim");

            _animSaveTitle.text          = DenEmoLoc.T("ui.section.saveAnim");
            _animSaveNoKeys.text         = DenEmoLoc.T("ui.animMode.noKeys.warn");
            _animSaveAsNewToggle.text    = DenEmoLoc.T("ui.animMode.saveAsNew");
            _animSaveAsNewToggle.tooltip = DenEmoLoc.T("ui.animMode.saveAsNew.tip");
            _animSaveButton.text         = DenEmoLoc.T("ui.animMode.save.button");

            UpdateSectionCards();
        }

        // ─── State reflection (250ms ポーリング + 操作直後に呼ぶ) ─────────────

        /// <summary>IMGUI・SceneView 側の操作で変わる状態をセクションカードへ反映する。</summary>
        private void UpdateSectionCards()
        {
            if (_targetCard == null) return;
            UpdateTargetCard();
            UpdateSearchCard();
            UpdatePoseSourceCard();
            UpdateAnimSaveCard();
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
            _targetMissing.style.display      = hasMain ? DisplayStyle.None : DisplayStyle.Flex;
            _targetInactiveWarn.style.display = inactive ? DisplayStyle.Flex : DisplayStyle.None;
            _targetNoShapes.style.display     = (hasMain && !hasShapes) ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void UpdateSearchCard()
        {
            if (_searchField.value != searchText)
                _searchField.SetValueWithoutNotify(searchText);
            _searchClear.style.display = string.IsNullOrEmpty(searchText)
                ? DisplayStyle.None : DisplayStyle.Flex;

            SetChipState(_chipFav,      showOnlyFavorites);
            SetChipState(_chipEnabled,  showOnlyIncluded);
            SetChipState(_chipNonZero,  showOnlyNonZero);
            SetChipState(_chipSymmetry, symmetryMode);

            // 頂点フィルター（SceneView でのピック結果もここで追従する）
            if (vertexPickMode)
            {
                _chipVertex.text = DenEmoLoc.T("ui.filter.vertex.cancel");
                SetChipState(_chipVertex, true);
                _chipVertexClear.style.display = DisplayStyle.None;
            }
            else
            {
                _chipVertex.text = vertexFilterActive
                    ? DenEmoLoc.Tf("ui.filter.vertex.active", selectedVertexIndex)
                    : DenEmoLoc.T("ui.filter.vertex");
                SetChipState(_chipVertex, vertexFilterActive);
                _chipVertexClear.style.display = vertexFilterActive ? DisplayStyle.Flex : DisplayStyle.None;
            }

            // キー有りのみ（Animation モードでクリップ設定時のみ）
            bool showKeyed = _currentMode == EditorMode.Animation && _animModeUI.ClipModel.Clip != null;
            _chipKeyed.style.display = showKeyed ? DisplayStyle.Flex : DisplayStyle.None;
            if (showKeyed) SetChipState(_chipKeyed, _animModeUI.TrackFilterEnabled);

            // メッシュ絞り込み（複数対象時のみ）
            var allTargets = GetAllTargetMeshes();
            bool showMeshFilter = allTargets.Count > 1;
            _meshFilterLabel.style.display    = showMeshFilter ? DisplayStyle.Flex : DisplayStyle.None;
            _meshFilterDropdown.style.display = showMeshFilter ? DisplayStyle.Flex : DisplayStyle.None;
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

        private void UpdateAnimSaveCard()
        {
            var clip = _animModeUI.ClipModel.Clip;
            if (clip == null)
            {
                _animSaveStats.style.display  = DisplayStyle.None;
                _animSaveNoKeys.style.display = DisplayStyle.None;
                return;
            }
            var tracks = _animModeUI.ClipModel.Tracks;
            if (tracks.Count == 0)
            {
                _animSaveStats.style.display  = DisplayStyle.None;
                _animSaveNoKeys.style.display = DisplayStyle.Flex;
            }
            else
            {
                int keyTotal = 0;
                foreach (var track in tracks)
                    keyTotal += track.KeyTimes.Length;
                _animSaveStats.text           = DenEmoLoc.Tf("ui.animMode.keyStats", tracks.Count, keyTotal);
                _animSaveStats.style.display  = DisplayStyle.Flex;
                _animSaveNoKeys.style.display = DisplayStyle.None;
            }
        }

        // ─── Target mesh helpers ──────────────────────────────────────────────

        /// <summary>メイン対象メッシュの変更（ObjectField / ドラッグ&ドロップ共通）。</summary>
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

            // 追加用の空行（メイン対象があるときのみ）
            if (_model.TargetSkinnedMesh != null)
            {
                var field = MakeAdditionalRow(null, out var removeButton);
                removeButton.SetEnabled(false);
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
            var row = new VisualElement();
            row.AddToClassList("dennoko-hrow");

            var label = new Label("+");
            label.AddToClassList("dennoko-text-tertiary");
            label.AddToClassList("dennoko-field-label");

            var field = new ObjectField
            {
                objectType        = typeof(SkinnedMeshRenderer),
                allowSceneObjects = true,
            };
            field.AddToClassList("dennoko-clip-field");
            field.SetValueWithoutNotify(value);

            removeButton = new Button { text = "✕" };
            removeButton.AddToClassList("dennoko-mini-button");
            removeButton.AddToClassList("dennoko-icon-mini");

            row.Add(label);
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

        private static void SetChipState(Button chip, bool on)
        {
            chip.EnableInClassList("dennoko-button-active", on);
            chip.EnableInClassList("dennoko-chip--on", on);
        }

        private string[] BuildMeshFilterOptions(List<SkinnedMeshRenderer> targets)
        {
            var opts = new string[targets.Count + 1];
            opts[0] = "All";
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

        private bool HasIncludedShapeKeys()
        {
            foreach (var item in _model.Items)
                if (item.IsIncluded && !item.IsVrcShape && !item.IsLipSyncShape) return true;
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
