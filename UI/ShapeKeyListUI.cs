using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using DenEmo.Models;
using DenEmo.Core;

namespace DenEmo.UI
{
    public partial class ShapeKeyListUI
    {
        private const double APPLY_INTERVAL_SEC = 0.05;
        private bool _throttleActive = false;
        private Dictionary<int, float> _pendingApplies = new Dictionary<int, float>();
        private double _lastApplyTime = 0;

        private bool isSliderDragging = false;
        private int currentDraggingIndex = -1;

        public Action OnIncludeFlagsChanged;
        public Action<string, bool> OnFavoriteChanged;

        private void QueuePendingApply(int index, float value)
        {
            _pendingApplies[index] = value;
            _throttleActive = true;
        }

        public void ApplyPending(SkinnedMeshRenderer target)
        {
            if (!_throttleActive || _pendingApplies.Count == 0) return;
            double now = EditorApplication.timeSinceStartup;
            if (now - _lastApplyTime >= APPLY_INTERVAL_SEC)
            {
                if (target)
                {
                    foreach (var kv in _pendingApplies)
                    {
                        if (kv.Key >= 0) target.SetBlendShapeWeight(kv.Key, kv.Value);
                    }
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

        public void DrawList(ShapeKeyModel model, ref Vector2 scroll, bool treatAsGroupUI, HashSet<string> collapsedGroups, bool symmetryMode, EditorWindow window, AnimationDrawContext animContext = null)
        {
            DenEmoTheme.Initialize();
            ApplyPending(model.TargetSkinnedMesh);

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

            bool anyVisible = false;
            foreach (var seg in model.GroupSegments)
            {
                int start = seg.Start;
                int end   = seg.Start + seg.Length;

                int enabledCount  = 0;
                int visibleCount  = 0;
                for (int i = start; i < end; i++)
                {
                    if (model.Items[i].IsVisible) { visibleCount++; if (model.Items[i].IsIncluded) enabledCount++; }
                }

                if (visibleCount == 0) continue;
                anyVisible = true;

                bool treatAsGroup = seg.Length > 3;
                if (treatAsGroup)
                {
                    DrawGroupHeader(seg, enabledCount, visibleCount, model, start, end, collapsedGroups);
                    if (collapsedGroups.Contains(seg.Key)) continue;
                }

                if (symmetryMode) DrawSymmetrySegment(model, start, end, treatAsGroup, window, animContext);
                else              DrawNormalSegment(model, start, end, treatAsGroup, window, animContext);
            }

            if (!anyVisible && model.Items.Count > 0)
            {
                GUILayout.Space(8);
                GUILayout.Label(DenEmoLoc.EnglishMode ? "No results match the current filter." : "フィルター条件に一致するシェイプキーがありません。", DenEmoTheme.CaptionStyle);
                GUILayout.Space(8);
            }

            EditorGUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        public Action OnSnapshotCreate;
        public Action OnSnapshotRestore;

        private void DrawGroupHeader(GroupSegment seg, int enabledCount, int visibleCount, ShapeKeyModel model, int start, int end, HashSet<string> collapsedGroups)
        {
            bool collapsed   = collapsedGroups.Contains(seg.Key);
            bool groupAllOn  = enabledCount == visibleCount;
            bool groupAllOff = enabledCount == 0;

            // Surface2 背景のグループヘッダー行
            var headerRect = GUILayoutUtility.GetRect(0, 26, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(headerRect, DenEmoTheme.Surface2);

            // 折りたたみアイコン
            var foldRect   = new Rect(headerRect.x + 4,  headerRect.y + 5, 24, 16);
            var labelRect  = new Rect(headerRect.x + 32, headerRect.y + 4, headerRect.width - 112, 18);
            var countRect  = new Rect(headerRect.xMax - 100, headerRect.y + 5, 76, 16);
            var checkRect  = new Rect(headerRect.xMax - 20, headerRect.y + 5, 16, 16);

            if (GUI.Button(foldRect, collapsed ? "▶" : "▼", DenEmoTheme.MiniButtonStyle))
            {
                if (collapsed) collapsedGroups.Remove(seg.Key);
                else           collapsedGroups.Add(seg.Key);
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

    }
}
