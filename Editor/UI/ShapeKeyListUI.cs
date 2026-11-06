using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using DenEmo.Models;

namespace DenEmo.UI
{
    /// <summary>
    /// シェイプキーリスト（UI Toolkit）。
    /// ShapeKeyList.uxml をルートとして生成し、行は ShapeKeyRow.uxml から動的生成する。
    /// 構造（行の集合）は行プランのシグネチャをポーリングで比較して差分時のみ再構築し、
    /// 値・アイコン・カウント等の動的状態は行バインディング経由で同期する。
    /// </summary>
    public partial class ShapeKeyListUI
    {
        private const double APPLY_INTERVAL_SEC = 0.05;
        private bool   _throttleActive = false;
        private double _lastApplyTime  = 0;

        // key = "smrInstanceId_blendIndex"
        private Dictionary<string, (SkinnedMeshRenderer smr, int idx, float value)> _pendingApplies
            = new Dictionary<string, (SkinnedMeshRenderer, int, float)>();

        public Action                  OnIncludeFlagsChanged;
        public Action<string, bool>    OnFavoriteChanged;
        public Action                  OnSnapshotCreate;
        public Action                  OnSnapshotRestore;
        public Func<ShapeKeyItem, bool> IsUnchanged;
        /// <summary>空の状態（対象なし）の「選択から」ボタン。</summary>
        public Action                  OnPickFromSelection;
        /// <summary>絞り込みで 0 件のときの「絞り込みを解除」ボタン。</summary>
        public Action                  OnClearFilters;

        // 差分チェック用の判定関数を返す（比較元が未用意なら null → メニュー無効化）
        public Func<Func<ShapeKeyItem, bool>> GetClipDiffChecker;
        public Func<Func<ShapeKeyItem, bool>> GetSnapshotDiffChecker;

        // ─── Bind state ───────────────────────────────────────────────────────

        private ShapeKeyModel        _model;
        private HashSet<string>      _collapsedGroups;
        private Func<bool>           _getSymmetryMode;
        private AnimationDrawContext _animContext;

        public VisualElement Root { get; private set; }

        private Label           _title;
        private Button          _bulkMenu;
        private Button          _snapCreate, _snapRestore;
        private VisualElement   _emptyBox, _noMatchBox;
        private Label           _emptyMessage, _noMatchMessage, _searchCount;
        private Button          _emptyAction, _noMatchClear;
        private ScrollView      _rowsScroll;
        private VisualTreeAsset _rowTemplate;

        // ─── Structure diff state ─────────────────────────────────────────────

        private readonly List<RowEntry>     _plan          = new List<RowEntry>();
        private readonly List<RowBinding>   _rowBindings   = new List<RowBinding>();
        private readonly List<GroupBinding> _groupBindings = new List<GroupBinding>();
        private int  _planSignature  = 0;
        private bool _structureDirty = true;
        private bool _anyDragging    = false;

        // ─── Bind ─────────────────────────────────────────────────────────────

        /// <summary>
        /// リストのルート要素を生成して返す。CreateGUI から一度だけ呼び、
        /// 返った要素をモードごとのホストへ配置（再配置）する。
        /// </summary>
        public VisualElement Bind(ShapeKeyModel model, HashSet<string> collapsedGroups, Func<bool> getSymmetryMode)
        {
            _model           = model;
            _collapsedGroups = collapsedGroups;
            _getSymmetryMode = getSymmetryMode;

            var tree     = DenEmoUiAssets.LoadVisualTree(DenEmoUiAssets.ShapeKeyListUxmlGuid);
            _rowTemplate = DenEmoUiAssets.LoadVisualTree(DenEmoUiAssets.ShapeKeyRowUxmlGuid);
            Root = tree != null ? (VisualElement)tree.CloneTree() : new VisualElement();
            // CloneTree が返す TemplateContainer にはクラスが付かないため C# で付与する
            Root.AddToClassList("dennoko-list-host");

            _title        = Root.Q<Label>("shape-list-title");
            _bulkMenu     = Root.Q<Button>("shape-list-bulk-menu");
            _snapCreate   = Root.Q<Button>("shape-list-snap-create");
            _snapRestore  = Root.Q<Button>("shape-list-snap-restore");
            _emptyBox       = Root.Q<VisualElement>("shape-list-empty");
            _emptyMessage   = Root.Q<Label>("shape-list-empty-message");
            _emptyAction    = Root.Q<Button>("shape-list-empty-action");
            _noMatchBox     = Root.Q<VisualElement>("shape-list-nomatch");
            _noMatchMessage = Root.Q<Label>("shape-list-nomatch-message");
            _noMatchClear   = Root.Q<Button>("shape-list-nomatch-clear");
            _searchCount    = Root.Q<Label>("search-count");
            _rowsScroll     = Root.Q<ScrollView>("shape-list-scroll");

            if (_rowsScroll == null || _emptyBox == null || _noMatchBox == null)
                return Root; // UXML ロード失敗時は空のまま返す

            if (_bulkMenu != null) _bulkMenu.clicked += ShowBulkMenu;
            _snapCreate.clicked   += () => OnSnapshotCreate?.Invoke();
            _snapRestore.clicked  += () => OnSnapshotRestore?.Invoke();
            _emptyAction.clicked  += () => OnPickFromSelection?.Invoke();
            _noMatchClear.clicked += () => OnClearFilters?.Invoke();

            // スライダードラッグ中の SMR 反映スロットルと、構造/動的状態の同期
            Root.schedule.Execute(ApplyPending).Every(50);
            Root.schedule.Execute(TickStructureAndSync).Every(150);

            RefreshLabels();
            _structureDirty = true;
            return Root;
        }

        /// <summary>
        /// Animation モードの描画コンテキストを設定する（null = Pose 動作）。
        /// クリップの有無やトラックフィルターが変わるため、モード側からポーリングで毎回渡してよい。
        /// </summary>
        public void SetAnimContext(AnimationDrawContext ctx)
        {
            bool presenceChanged = (ctx == null) != (_animContext == null);
            _animContext = ctx;
            if (presenceChanged) _structureDirty = true;
        }

        /// <summary>フィルターや対象メッシュの変更後に呼ぶ。次の同期ティックで行を再構築する。</summary>
        public void MarkStructureDirty() => _structureDirty = true;

        /// <summary>Undo/Redo 直後など、次のポーリングを待たずに UI 表示を即時同期する。</summary>
        public void SyncNow() => TickStructureAndSync();

        /// <summary>言語切替時に呼ぶ。</summary>
        public void RefreshLabels()
        {
            if (_title == null) return;
            _title.text          = DenEmoLoc.T("ui.list.title");
            _snapCreate.text     = DenEmoLoc.T("ui.snapshot.create");
            _snapCreate.tooltip  = DenEmoLoc.T("ui.snapshot.create.tip");
            _snapRestore.text    = DenEmoLoc.T("ui.snapshot.restore");
            _snapRestore.tooltip = DenEmoLoc.T("ui.snapshot.restore.tip");
            DennokoIcons.Apply(_bulkMenu, DennokoIcons.Menu, "⋮", DenEmoLoc.T("ui.list.bulkMenu.tip"));
            if (_emptyAction != null)
            {
                _emptyAction.text    = DenEmoLoc.T("ui.list.empty.fromSelection");
                _emptyAction.tooltip = DenEmoLoc.T("ui.list.empty.fromSelection.tip");
            }
            if (_noMatchMessage != null) _noMatchMessage.text = DenEmoLoc.T("ui.list.noMatch");
            if (_noMatchClear != null)   _noMatchClear.text   = DenEmoLoc.T("ui.list.clearFilter");
            if (_searchCount != null)    _searchCount.tooltip = DenEmoLoc.T("ui.search.count.tip");
            _emptyStateKey = -1; // 空の状態の文言を次のティックで更新する
            _structureDirty = true; // 行内ツールチップの言語を更新するため再構築する
        }

        // ─── Throttle ────────────────────────────────────────────────────────

        private void QueuePendingApply(ShapeKeyItem item, float value)
        {
            var smr = item.OwnerSmr;
            string key = (smr != null ? smr.GetInstanceID().ToString() : "null") + "_" + item.Index;
            _pendingApplies[key] = (smr, item.Index, value);
            _throttleActive = true;
        }

        public void ApplyPending()
        {
            if (!_throttleActive || _pendingApplies.Count == 0) return;
            double now = EditorApplication.timeSinceStartup;
            if (now - _lastApplyTime >= APPLY_INTERVAL_SEC)
            {
                foreach (var kv in _pendingApplies.Values)
                {
                    if (kv.smr != null && kv.idx >= 0)
                        kv.smr.SetBlendShapeWeight(kv.idx, kv.value);
                }
                _pendingApplies.Clear();
                _lastApplyTime = now;
            }
        }

        public void StopThrottle()
        {
            _throttleActive = false;
            _pendingApplies.Clear();
        }

        // ─── Structure & dynamic sync ─────────────────────────────────────────

        private void TickStructureAndSync()
        {
            if (_model == null || _rowsScroll == null) return;

            // リストが非表示（FxSetup モード等）またはパネル未接続なら、全アイテム走査を伴う
            // 行プラン構築・シグネチャ計算をスキップする。_structureDirty は保持されるため、
            // 再表示された最初のティックで確実に再構築される。
            if (Root.panel == null || !IsDisplayedInHierarchy()) return;

            BuildRowPlan(_plan);
            int sig = ComputePlanSignature(_plan);

            // ドラッグ中の再構築はスライダーのキャプチャを破壊するため保留する
            if ((sig != _planSignature || _structureDirty) && !_anyDragging)
            {
                RebuildRows();
                _planSignature  = sig;
                _structureDirty = false;
            }

            SyncDynamicState();
            UpdateEmptyStateAndCount();
        }

        // ─── 空の状態 / 件数 ──────────────────────────────────────────────────

        private int    _emptyStateKey = -1;
        private string _countText;

        /// <summary>
        /// B4 の空の状態 2 種（対象なし・シェイプキーなし / 絞り込みで 0 件）と、検索行の「表示中 / 全体」を更新する。
        /// 行プランのシグネチャが変わらなくても対象の有無は変わり得るため、毎ティック評価する。
        /// </summary>
        private void UpdateEmptyStateAndCount()
        {
            bool hasTarget = _model.TargetSkinnedMesh != null;
            bool hasItems  = _model.Items.Count > 0;
            bool noMatch   = hasItems && _plan.Count == 0;

            // 0 = 一覧あり, 1 = 対象なし, 2 = シェイプキーなし, 3 = 絞り込みで 0 件
            int key = !hasItems ? (hasTarget ? 2 : 1) : noMatch ? 3 : 0;
            if (key != _emptyStateKey)
            {
                _emptyStateKey = key;
                DennokoBlockUI.Show(_emptyBox, key == 1 || key == 2);
                DennokoBlockUI.Show(_noMatchBox, key == 3);
                DennokoBlockUI.Show(_emptyAction, key == 1);
                if (key == 1 || key == 2)
                    _emptyMessage.text = DenEmoLoc.T(key == 1 ? "ui.mesh.missing" : "ui.mesh.noShapes");
            }

            if (_searchCount == null) return;
            var trackNames = _animContext?.TrackShapeNames;
            int total = 0, shown = 0;
            foreach (var it in _model.Items)
            {
                if (it.IsLipSyncShape) continue;
                total++;
                if (!it.IsVisible) continue;
                if (trackNames != null && !trackNames.Contains(it.Name)) continue;
                shown++;
            }
            string text = shown + " / " + total;
            if (text != _countText)
            {
                _countText = text;
                _searchCount.text = text;
            }
        }

        /// <summary>自身か祖先のどれかが display: none（非表示のモードのページなど）なら false。</summary>
        private bool IsDisplayedInHierarchy()
        {
            for (var e = Root; e != null; e = e.parent)
                if (e.resolvedStyle.display == DisplayStyle.None) return false;
            return true;
        }

        private static int ComputePlanSignature(List<RowEntry> plan)
        {
            unchecked
            {
                int h = 17;
                foreach (var e in plan)
                {
                    h = h * 31 + (int)e.Kind;
                    h = h * 31 + (e.Text != null ? e.Text.GetHashCode() : 0);
                    h = h * 31 + (e.Item != null ? e.Item.GetHashCode() : 0);
                    h = h * 31 + (e.Right != null ? e.Right.GetHashCode() : 0);
                    h = h * 31 + (e.Indent ? 1 : 0);
                }
                return h * 31 + plan.Count;
            }
        }

        // ─── Bulk Operations ─────────────────────────────────────────────────

        private void ShowBulkMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent(DenEmoLoc.T("ui.bulk.checkAll")), false, BulkCheckAll);
            menu.AddItem(new GUIContent(DenEmoLoc.T("ui.bulk.uncheckAll")), false, BulkUncheckAll);
            menu.AddItem(new GUIContent(DenEmoLoc.T("ui.bulk.uncheckUnchanged")), false, BulkUncheckUnchanged);
            menu.AddItem(new GUIContent(DenEmoLoc.T("ui.bulk.checkFavorites")), false, BulkCheckFavorites);

            var clipDiff = GetClipDiffChecker?.Invoke();
            var snapDiff = GetSnapshotDiffChecker?.Invoke();
            var clipDiffLabel = new GUIContent(DenEmoLoc.T("ui.bulk.checkDiffFromClip"));
            var snapDiffLabel = new GUIContent(DenEmoLoc.T("ui.bulk.checkDiffFromSnapshot"));
            if (clipDiff != null) menu.AddItem(clipDiffLabel, false, () => BulkCheckWhere(clipDiff));
            else                  menu.AddDisabledItem(clipDiffLabel);
            if (snapDiff != null) menu.AddItem(snapDiffLabel, false, () => BulkCheckWhere(snapDiff));
            else                  menu.AddDisabledItem(snapDiffLabel);

            if (_bulkMenu != null) menu.DropDown(_bulkMenu.worldBound);
            else                   menu.ShowAsContext();
        }

        /// <summary>表示中の各キーのチェック状態を shouldCheck の判定結果で置き換える。</summary>
        private void BulkCheckWhere(Func<ShapeKeyItem, bool> shouldCheck)
        {
            if (_model == null) return;
            foreach (var item in _model.Items)
            {
                if (item.IsVrcExcluded(_model.IsAnimationMode) || item.IsLipSyncShape) continue;
                if (item.IsVisible)
                {
                    item.IsIncluded = shouldCheck(item);
                }
            }
            OnIncludeFlagsChanged?.Invoke();
            SyncDynamicState();
        }

        private void BulkCheckAll()
        {
            if (_model == null) return;
            foreach (var item in _model.Items)
            {
                if (item.IsVrcExcluded(_model.IsAnimationMode) || item.IsLipSyncShape) continue;
                if (item.IsVisible)
                {
                    item.IsIncluded = true;
                }
            }
            OnIncludeFlagsChanged?.Invoke();
            SyncDynamicState();
        }

        private void BulkUncheckAll()
        {
            if (_model == null) return;
            foreach (var item in _model.Items)
            {
                if (item.IsVrcExcluded(_model.IsAnimationMode) || item.IsLipSyncShape) continue;
                if (item.IsVisible)
                {
                    item.IsIncluded = false;
                }
            }
            OnIncludeFlagsChanged?.Invoke();
            SyncDynamicState();
        }

        private void BulkUncheckUnchanged()
        {
            if (_model == null) return;
            foreach (var item in _model.Items)
            {
                if (item.IsVrcExcluded(_model.IsAnimationMode) || item.IsLipSyncShape) continue;
                bool unchanged = IsUnchanged != null ? IsUnchanged(item) : Mathf.Approximately(item.Value, 0f);
                if (unchanged)
                {
                    item.IsIncluded = false;
                }
            }
            OnIncludeFlagsChanged?.Invoke();
            SyncDynamicState();
        }

        private void BulkCheckFavorites()
        {
            if (_model == null) return;
            foreach (var item in _model.Items)
            {
                if (item.IsVrcExcluded(_model.IsAnimationMode) || item.IsLipSyncShape) continue;
                if (item.IsVisible)
                {
                    item.IsIncluded = item.IsFavorite;
                }
            }
            OnIncludeFlagsChanged?.Invoke();
            SyncDynamicState();
        }
    }
}
