using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using DenEmo.Models;
using DenEmo.Core;

namespace DenEmo.UI
{
    public class ShapeKeyListUI
    {
        private const double APPLY_INTERVAL_SEC = 0.05;
        private bool   _throttleActive = false;
        private double _lastApplyTime  = 0;

        // key = "smrInstanceId_blendIndex"
        private Dictionary<string, (SkinnedMeshRenderer smr, int idx, float value)> _pendingApplies
            = new Dictionary<string, (SkinnedMeshRenderer, int, float)>();

        private bool          isSliderDragging = false;
        private ShapeKeyItem  _draggingItem    = null;

        public Action                  OnIncludeFlagsChanged;
        public Action<string, bool>    OnFavoriteChanged;
        public Action                  OnSnapshotCreate;
        public Action                  OnSnapshotRestore;

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

        // ─── DrawList ─────────────────────────────────────────────────────────

        public void DrawList(ShapeKeyModel model, ref Vector2 scroll, bool treatAsGroupUI, HashSet<string> collapsedGroups, bool symmetryMode, EditorWindow window)
        {
            DenEmoTheme.Initialize();
            ApplyPending();

            GUILayout.BeginVertical(DenEmoTheme.CardOuterStyle);

            // リストヘッダーツールバー
            GUILayout.BeginHorizontal(DenEmoTheme.ToolbarStyle);
            GUILayout.Label(DenEmoLoc.EnglishMode ? "SHAPE KEYS" : "シェイプキー", DenEmoTheme.SectionHeaderStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(DenEmoLoc.T("ui.snapshot.create"), DenEmoTheme.MiniButtonStyle, GUILayout.Height(18)))
                OnSnapshotCreate?.Invoke();
            GUILayout.Space(4);
            if (GUILayout.Button(DenEmoLoc.T("ui.snapshot.restore"), DenEmoTheme.MiniButtonStyle, GUILayout.Height(18)))
                OnSnapshotRestore?.Invoke();
            GUILayout.Space(4);
            GUILayout.EndHorizontal();

            scroll = EditorGUILayout.BeginScrollView(scroll);

            if (model.Items.Count == 0)
            {
                GUILayout.Space(8);
                GUILayout.Label(DenEmoLoc.T("ui.mesh.noShapes"), DenEmoTheme.SecondaryTextStyle);
                GUILayout.Space(8);
            }

            bool   anyVisible   = false;
            string lastMeshName = null;

            foreach (var seg in model.GroupSegments)
            {
                int start = seg.Start;
                int end   = seg.Start + seg.Length;

                int enabledCount = 0;
                int visibleCount = 0;
                for (int i = start; i < end; i++)
                {
                    if (model.Items[i].IsVisible)
                    {
                        visibleCount++;
                        if (model.Items[i].IsIncluded) enabledCount++;
                    }
                }

                if (visibleCount == 0) continue;
                anyVisible = true;

                // マルチメッシュ時のメッシュヘッダー
                if (seg.MeshName != null && seg.MeshName != lastMeshName)
                {
                    DrawMeshHeader(seg.MeshName);
                    lastMeshName = seg.MeshName;
                }

                // グループキーはメッシュ名プレフィックス付き（折りたたみ衝突回避）
                string collapseKey  = seg.MeshName != null ? seg.MeshName + "|" + seg.Key : seg.Key;
                bool   treatAsGroup = seg.Length > 3;

                if (treatAsGroup)
                {
                    DrawGroupHeader(seg, enabledCount, visibleCount, model, start, end, collapsedGroups, collapseKey);
                    if (collapsedGroups.Contains(collapseKey)) continue;
                }

                if (symmetryMode) DrawSymmetrySegment(model, start, end, treatAsGroup, window);
                else              DrawNormalSegment(model, start, end, treatAsGroup, window);
            }

            if (!anyVisible && model.Items.Count > 0)
            {
                GUILayout.Space(8);
                GUILayout.Label(
                    DenEmoLoc.EnglishMode
                        ? "No results match the current filter."
                        : "フィルター条件に一致するシェイプキーがありません。",
                    DenEmoTheme.CaptionStyle);
                GUILayout.Space(8);
            }

            EditorGUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        // ─── Mesh Header ──────────────────────────────────────────────────────

        private void DrawMeshHeader(string meshName)
        {
            var headerRect = GUILayoutUtility.GetRect(0, 24, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(headerRect, DenEmoTheme.Surface1);
            var labelRect = new Rect(headerRect.x + 8, headerRect.y + 4, headerRect.width - 16, 16);
            GUI.Label(labelRect, meshName, DenEmoTheme.SectionHeaderStyle);
        }

        // ─── Group Header ─────────────────────────────────────────────────────

        private void DrawGroupHeader(GroupSegment seg, int enabledCount, int visibleCount, ShapeKeyModel model, int start, int end, HashSet<string> collapsedGroups, string collapseKey)
        {
            bool collapsed   = collapsedGroups.Contains(collapseKey);
            bool groupAllOn  = enabledCount == visibleCount;
            bool groupAllOff = enabledCount == 0;

            var headerRect = GUILayoutUtility.GetRect(0, 26, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(headerRect, DenEmoTheme.Surface2);

            var foldRect  = new Rect(headerRect.x + 4,   headerRect.y + 5, 24,                       16);
            var labelRect = new Rect(headerRect.x + 32,  headerRect.y + 4, headerRect.width - 112,   18);
            var countRect = new Rect(headerRect.xMax - 100, headerRect.y + 5, 76,                    16);
            var checkRect = new Rect(headerRect.xMax - 20,  headerRect.y + 5, 16,                    16);

            if (GUI.Button(foldRect, collapsed ? "▶" : "▼", DenEmoTheme.MiniButtonStyle))
            {
                if (collapsed) collapsedGroups.Remove(collapseKey);
                else           collapsedGroups.Add(collapseKey);
            }

            bool newGroupVal = GUI.Toggle(checkRect, groupAllOn, GUIContent.none);
            if (newGroupVal != groupAllOn)
            {
                for (int i = start; i < end; i++)
                {
                    var item = model.Items[i];
                    if (!item.IsVrcShape && !item.IsLipSyncShape) item.IsIncluded = newGroupVal;
                }
                OnIncludeFlagsChanged?.Invoke();
            }

            GUI.Label(labelRect, seg.Key, DenEmoTheme.GroupLabelStyle);

            string suffix = groupAllOn ? DenEmoLoc.T("ui.group.all") : groupAllOff ? DenEmoLoc.T("ui.group.none") : DenEmoLoc.T("ui.group.some");
            GUI.Label(countRect, $"{enabledCount}/{visibleCount}  {suffix}", DenEmoTheme.CaptionStyle);
        }

        // ─── Segment Drawing ──────────────────────────────────────────────────

        private void DrawSymmetrySegment(ShapeKeyModel model, int start, int end, bool spaceLeft, EditorWindow window)
        {
            var itemsDict = new Dictionary<string, (ShapeKeyItem L, ShapeKeyItem R)>();
            var singles   = new List<ShapeKeyItem>();

            for (int i = start; i < end; ++i)
            {
                var item = model.Items[i];
                if (item.IsVrcShape || item.IsLipSyncShape || !item.IsVisible) continue;

                if (SymmetryParser.TryParseLRSuffix(item.Name, out var baseName, out var side))
                {
                    if (side == LRSide.L)
                    {
                        itemsDict[baseName] = itemsDict.TryGetValue(baseName, out var existing)
                            ? (item, existing.R) : (item, null);
                    }
                    else if (side == LRSide.R)
                    {
                        itemsDict[baseName] = itemsDict.TryGetValue(baseName, out var existing)
                            ? (existing.L, item) : (null, item);
                    }
                    else singles.Add(item);
                }
                else singles.Add(item);
            }

            foreach (var kvp in itemsDict)
            {
                var left  = kvp.Value.L;
                var right = kvp.Value.R;
                if (left != null && right != null && Mathf.Abs(left.Value - right.Value) <= 0.001f)
                    DrawMergedRow(kvp.Key, left, right, spaceLeft, model);
                else
                {
                    if (left  != null) DrawSingleRow(left,  spaceLeft, model);
                    if (right != null) DrawSingleRow(right, spaceLeft, model);
                }
            }

            foreach (var s in singles) DrawSingleRow(s, spaceLeft, model);
        }

        private void DrawNormalSegment(ShapeKeyModel model, int start, int end, bool spaceLeft, EditorWindow window)
        {
            for (int i = start; i < end; i++)
            {
                var item = model.Items[i];
                if (!item.IsVisible || item.IsLipSyncShape) continue;
                DrawSingleRow(item, spaceLeft, model);
            }
        }

        // ─── 行描画 ───────────────────────────────────────────────────────────

        private void DrawMergedRow(string baseName, ShapeKeyItem left, ShapeKeyItem right, bool spaceLeft, ShapeKeyModel model)
        {
            var smr = left.OwnerSmr ?? model.TargetSkinnedMesh;

            EditorGUILayout.BeginHorizontal();
            if (spaceLeft) GUILayout.Space(20);

            DrawFavButton(left, model);

            bool bothInc = left.IsIncluded && right.IsIncluded;
            bool newInc  = EditorGUILayout.Toggle(bothInc, GUILayout.Width(16));
            if (newInc != bothInc)
            {
                left.IsIncluded  = newInc;
                right.IsIncluded = newInc;
                OnIncludeFlagsChanged?.Invoke();
            }

            float nameWidth = Mathf.Min(200f, EditorGUIUtility.currentViewWidth * 0.32f);
            EditorGUILayout.LabelField(baseName + "_LR", DenEmoTheme.SecondaryTextStyle, GUILayout.Width(nameWidth));

            if (GUILayout.Button("0", DenEmoTheme.MiniButtonStyle, GUILayout.Width(20)))
            {
                if (smr != null) Undo.RecordObject(smr, "Reset Shape Key");
                left.Value  = 0f; right.Value = 0f;
                if (smr != null)
                {
                    smr.SetBlendShapeWeight(left.Index,  0f);
                    smr.SetBlendShapeWeight(right.Index, 0f);
                }
            }

            float oldValue = left.Value;
            int   sliderId = GUIUtility.GetControlID(FocusType.Passive);
            EditorGUI.BeginChangeCheck();
            float newValue = EditorGUILayout.Slider(oldValue, 0f, 100f);
            bool  changed  = EditorGUI.EndChangeCheck();
            bool  isHot    = GUIUtility.hotControl == sliderId || GUIUtility.hotControl == sliderId + 1;

            if (changed && isHot && (!isSliderDragging || _draggingItem != left))
            {
                if (smr != null) Undo.RecordObject(smr, "Change Shape Key");
                isSliderDragging = true;
                _draggingItem    = left;
            }

            if (changed)
            {
                if (!isHot && !isSliderDragging && smr != null)
                    Undo.RecordObject(smr, "Change Shape Key");
                left.Value  = newValue;
                right.Value = newValue;
                if (smr != null)
                {
                    if (isHot || isSliderDragging)
                    {
                        QueuePendingApply(left,  newValue);
                        QueuePendingApply(right, newValue);
                    }
                    else
                    {
                        smr.SetBlendShapeWeight(left.Index,  newValue);
                        smr.SetBlendShapeWeight(right.Index, newValue);
                    }
                }
            }

            if (isSliderDragging && _draggingItem == left && !isHot)
            {
                isSliderDragging = false; _draggingItem = null; StopThrottle();
                if (smr != null)
                {
                    smr.SetBlendShapeWeight(left.Index,  left.Value);
                    smr.SetBlendShapeWeight(right.Index, right.Value);
                }
            }

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(5f);
        }

        private void DrawSingleRow(ShapeKeyItem item, bool spaceLeft, ShapeKeyModel model)
        {
            var smr = item.OwnerSmr ?? model.TargetSkinnedMesh;

            EditorGUILayout.BeginHorizontal();
            if (spaceLeft) GUILayout.Space(20);

            DrawFavButton(item, model);

            bool newInc = EditorGUILayout.Toggle(item.IsIncluded, GUILayout.Width(16));
            if (newInc != item.IsIncluded)
            {
                item.IsIncluded = newInc;
                OnIncludeFlagsChanged?.Invoke();
            }

            float nameWidth = Mathf.Min(200f, EditorGUIUtility.currentViewWidth * 0.32f);
            var nameStyle = item.IsIncluded ? DenEmoTheme.SecondaryTextStyle : DenEmoTheme.CaptionStyle;
            EditorGUILayout.LabelField(item.Name, nameStyle, GUILayout.Width(nameWidth));

            if (GUILayout.Button("0", DenEmoTheme.MiniButtonStyle, GUILayout.Width(20)))
            {
                if (item.Value != 0f)
                {
                    if (smr != null) Undo.RecordObject(smr, "Reset Shape Key");
                    item.Value = 0f;
                    if (smr != null) smr.SetBlendShapeWeight(item.Index, 0f);
                }
            }

            float oldValue = item.Value;
            int   sliderId = GUIUtility.GetControlID(FocusType.Passive);
            EditorGUI.BeginChangeCheck();
            float newValue = EditorGUILayout.Slider(oldValue, 0f, 100f);
            bool  changed  = EditorGUI.EndChangeCheck();
            bool  isHot    = GUIUtility.hotControl == sliderId || GUIUtility.hotControl == sliderId + 1;

            if (changed && isHot && (!isSliderDragging || _draggingItem != item))
            {
                if (smr != null) Undo.RecordObject(smr, "Change Shape Key");
                isSliderDragging = true;
                _draggingItem    = item;
            }

            if (changed)
            {
                if (!isHot && !isSliderDragging && smr != null)
                    Undo.RecordObject(smr, "Change Shape Key");
                item.Value = newValue;
                if (smr != null)
                {
                    if (isHot || isSliderDragging) QueuePendingApply(item, newValue);
                    else smr.SetBlendShapeWeight(item.Index, newValue);
                }
            }

            if (isSliderDragging && _draggingItem == item && !isHot)
            {
                isSliderDragging = false; _draggingItem = null; StopThrottle();
                if (smr != null) smr.SetBlendShapeWeight(item.Index, item.Value);
            }

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(5f);
        }

        private void DrawFavButton(ShapeKeyItem item, ShapeKeyModel model)
        {
            var style = item.IsFavorite ? DenEmoTheme.FavOnStyle : DenEmoTheme.FavOffStyle;
            string icon = item.IsFavorite ? "★" : "☆";
            if (GUILayout.Button(icon, style, GUILayout.Width(18), GUILayout.Height(18)))
            {
                item.IsFavorite = !item.IsFavorite;
                OnFavoriteChanged?.Invoke(item.Name, item.IsFavorite);
            }
        }
    }
}
