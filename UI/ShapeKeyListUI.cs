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

        // ─── Bind state ───────────────────────────────────────────────────────

        private ShapeKeyModel        _model;
        private HashSet<string>      _collapsedGroups;
        private Func<bool>           _getSymmetryMode;
        private AnimationDrawContext _animContext;

        public VisualElement Root { get; private set; }

        private Label           _title;
        private Button          _snapCreate, _snapRestore;
        private Label           _emptyLabel, _noMatchLabel;
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
            Root.style.flexShrink = 0;

            _title        = Root.Q<Label>("shape-list-title");
            _snapCreate   = Root.Q<Button>("shape-list-snap-create");
            _snapRestore  = Root.Q<Button>("shape-list-snap-restore");
            _emptyLabel   = Root.Q<Label>("shape-list-empty");
            _noMatchLabel = Root.Q<Label>("shape-list-nomatch");
            _rowsScroll   = Root.Q<ScrollView>("shape-list-scroll");

            if (_rowsScroll == null) return Root; // UXML ロード失敗時は空のまま返す

            _snapCreate.clicked  += () => OnSnapshotCreate?.Invoke();
            _snapRestore.clicked += () => OnSnapshotRestore?.Invoke();

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

        /// <summary>言語切替時に呼ぶ。</summary>
        public void RefreshLabels()
        {
            if (_title == null) return;
            _title.text       = DenEmoLoc.T("ui.list.title");
            _snapCreate.text  = DenEmoLoc.T("ui.snapshot.create");
            _snapRestore.text = DenEmoLoc.T("ui.snapshot.restore");
            _emptyLabel.text  = DenEmoLoc.T("ui.mesh.noShapes");
            _noMatchLabel.text = DenEmoLoc.T("ui.list.noMatch");
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
    }
}
