using System.Collections.Generic;
using DenEmo.Core;
using DenEmo.Models;
using UnityEngine;

namespace DenEmo.UI
{
    public partial class ShapeKeyListUI
    {
        // ─── 行プラン ─────────────────────────────────────────────────────────
        // 表示すべき行（ヘッダー含む）の一覧。ポーリングごとに構築してシグネチャ比較し、
        // 差分があるときのみ VisualElement を再構築する。

        private enum RowKind { MeshHeader, GroupHeader, Single, Merged }

        private struct RowEntry
        {
            public RowKind      Kind;
            public ShapeKeyItem Item;   // Single / Merged(L)
            public ShapeKeyItem Right;  // Merged(R)
            public string       Text;   // MeshHeader: メッシュ名 / GroupHeader: グループ名 / Merged: ベース名
            public bool         Indent;
            public string       CollapseKey; // GroupHeader
            public int          SegStart, SegEnd; // GroupHeader
        }

        // 左右対称ペアリング用の再利用バッファ
        private readonly Dictionary<string, (ShapeKeyItem L, ShapeKeyItem R)> _pairBuffer
            = new Dictionary<string, (ShapeKeyItem, ShapeKeyItem)>();
        private readonly List<ShapeKeyItem> _singleBuffer = new List<ShapeKeyItem>();

        private void BuildRowPlan(List<RowEntry> plan)
        {
            plan.Clear();

            bool   symmetryMode = _getSymmetryMode != null && _getSymmetryMode();
            var    trackNames   = _animContext?.TrackShapeNames;
            string lastMeshName = null;

            foreach (var seg in _model.GroupSegments)
            {
                int start = seg.Start;
                int end   = seg.Start + seg.Length;
                if (end > _model.Items.Count) continue; // モデル更新直後の古いセグメント保護

                int visibleCount = 0;
                for (int i = start; i < end; i++)
                {
                    var it = _model.Items[i];
                    if (!it.IsVisible || it.IsLipSyncShape) continue;
                    if (trackNames != null && !trackNames.Contains(it.Name)) continue;
                    visibleCount++;
                }
                if (visibleCount == 0) continue;

                // マルチメッシュ時のメッシュヘッダー
                if (seg.MeshName != null && seg.MeshName != lastMeshName)
                {
                    plan.Add(new RowEntry { Kind = RowKind.MeshHeader, Text = seg.MeshName });
                    lastMeshName = seg.MeshName;
                }

                // グループキーはメッシュ名プレフィックス付き（折りたたみ衝突回避）
                string collapseKey  = seg.MeshName != null ? seg.MeshName + "|" + seg.Key : seg.Key;
                bool   treatAsGroup = seg.Length > 3;

                if (treatAsGroup)
                {
                    plan.Add(new RowEntry
                    {
                        Kind = RowKind.GroupHeader, Text = seg.Key,
                        CollapseKey = collapseKey, SegStart = start, SegEnd = end,
                    });
                    if (_collapsedGroups.Contains(collapseKey)) continue;
                }

                if (symmetryMode) BuildSymmetrySegment(plan, start, end, treatAsGroup, trackNames);
                else              BuildNormalSegment(plan, start, end, treatAsGroup, trackNames);
            }
        }

        private void BuildNormalSegment(List<RowEntry> plan, int start, int end, bool indent, HashSet<string> trackNames)
        {
            for (int i = start; i < end; i++)
            {
                var item = _model.Items[i];
                if (!item.IsVisible || item.IsLipSyncShape) continue;
                if (trackNames != null && !trackNames.Contains(item.Name)) continue;
                plan.Add(new RowEntry { Kind = RowKind.Single, Item = item, Indent = indent });
            }
        }

        private void BuildSymmetrySegment(List<RowEntry> plan, int start, int end, bool indent, HashSet<string> trackNames)
        {
            _pairBuffer.Clear();
            _singleBuffer.Clear();

            for (int i = start; i < end; ++i)
            {
                var item = _model.Items[i];
                if (item.IsVrcExcluded(_model.IsAnimationMode) || item.IsLipSyncShape || !item.IsVisible) continue;

                if (SymmetryParser.TryParseLRSuffix(item.Name, out var baseName, out var side))
                {
                    if (side == LRSide.L)
                    {
                        _pairBuffer[baseName] = _pairBuffer.TryGetValue(baseName, out var existing)
                            ? (item, existing.R) : (item, null);
                    }
                    else if (side == LRSide.R)
                    {
                        _pairBuffer[baseName] = _pairBuffer.TryGetValue(baseName, out var existing)
                            ? (existing.L, item) : (null, item);
                    }
                    else _singleBuffer.Add(item);
                }
                else _singleBuffer.Add(item);
            }

            foreach (var kvp in _pairBuffer)
            {
                var left  = kvp.Value.L;
                var right = kvp.Value.R;

                if (trackNames != null)
                {
                    bool lInTrack = left  != null && trackNames.Contains(left.Name);
                    bool rInTrack = right != null && trackNames.Contains(right.Name);
                    if (!lInTrack && !rInTrack) continue;
                }

                if (left != null && right != null && Mathf.Abs(left.Value - right.Value) <= 0.001f)
                {
                    plan.Add(new RowEntry { Kind = RowKind.Merged, Item = left, Right = right, Text = kvp.Key, Indent = indent });
                }
                else
                {
                    if (left  != null) plan.Add(new RowEntry { Kind = RowKind.Single, Item = left,  Indent = indent });
                    if (right != null) plan.Add(new RowEntry { Kind = RowKind.Single, Item = right, Indent = indent });
                }
            }

            foreach (var s in _singleBuffer)
            {
                if (trackNames != null && !trackNames.Contains(s.Name)) continue;
                plan.Add(new RowEntry { Kind = RowKind.Single, Item = s, Indent = indent });
            }
        }
    }
}
