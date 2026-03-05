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
        private bool _throttleActive = false;
        private Dictionary<int, float> _pendingApplies = new Dictionary<int, float>();
        private double _lastApplyTime = 0;
        
        private bool isSliderDragging = false;
        private int currentDraggingIndex = -1;

        public Action OnIncludeFlagsChanged;

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

        public void DrawList(ShapeKeyModel model, ref Vector2 scroll, bool treatAsGroupUI, HashSet<string> collapsedGroups, bool symmetryMode, EditorWindow window)
        {
            // Apply any pending throttled values
            ApplyPending(model.TargetSkinnedMesh);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            scroll = EditorGUILayout.BeginScrollView(scroll);

            if (model.Items.Count == 0)
            {
                EditorGUILayout.HelpBox(DenEmoLoc.T("ui.mesh.noShapes"), MessageType.Info);
            }

            foreach (var seg in model.GroupSegments)
            {
                int start = seg.Start;
                int end = seg.Start + seg.Length;

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

                bool treatAsGroup = seg.Length > 3;
                if (treatAsGroup && visibleCount > 0)
                {
                    bool groupAllOn = enabledCount == visibleCount && visibleCount > 0;
                    bool groupAllOff = enabledCount == 0;
                    bool newGroupVal = groupAllOn;
                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                    bool collapsed = collapsedGroups.Contains(seg.Key);
                    string foldIcon = collapsed ? "\u25B6" : "\u25BC";
                    if (GUILayout.Button(foldIcon, EditorStyles.miniButton, GUILayout.Width(24)))
                    {
                        if (collapsed) collapsedGroups.Remove(seg.Key);
                        else collapsedGroups.Add(seg.Key);
                    }

                    int groupCheckboxId = GUIUtility.GetControlID(FocusType.Passive);
                    Rect groupCheckboxRect = GUILayoutUtility.GetRect(18, 18, GUILayout.Width(18));
                    newGroupVal = GUI.Toggle(groupCheckboxRect, groupAllOn, GUIContent.none);

                    using (new EditorGUI.DisabledGroupScope(true))
                    {
                        string suffix = groupAllOn ? DenEmoLoc.T("ui.group.all") : groupAllOff ? DenEmoLoc.T("ui.group.none") : DenEmoLoc.T("ui.group.some");
                        EditorGUILayout.LabelField($"{seg.Key}  {suffix}  [{enabledCount}/{visibleCount}]", EditorStyles.boldLabel);
                    }
                    EditorGUILayout.EndHorizontal();

                    if (newGroupVal != groupAllOn)
                    {
                        for (int i = start; i < end; i++)
                        {
                            var item = model.Items[i];
                            if (!item.IsVrcShape && !item.IsLipSyncShape)
                            {
                                item.IsIncluded = newGroupVal;
                            }
                        }
                        OnIncludeFlagsChanged?.Invoke();
                    }
                }

                if (treatAsGroup && collapsedGroups.Contains(seg.Key))
                    continue;

                if (symmetryMode)
                {
                    DrawSymmetrySegment(model, start, end, treatAsGroup, window);
                }
                else
                {
                    DrawNormalSegment(model, start, end, treatAsGroup, window);
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawSymmetrySegment(ShapeKeyModel model, int start, int end, bool spaceLeft, EditorWindow window)
        {
            var itemsDict = new Dictionary<string, (ShapeKeyItem L, ShapeKeyItem R)>();
            var singles = new List<ShapeKeyItem>();

            for (int i = start; i < end; ++i)
            {
                var item = model.Items[i];
                if (item.IsVrcShape || item.IsLipSyncShape || !item.IsVisible) continue;

                if (SymmetryParser.TryParseLRSuffix(item.Name, out var baseName, out var side))
                {
                    if (side == LRSide.L)
                    {
                        if (!itemsDict.ContainsKey(baseName)) itemsDict[baseName] = (item, null);
                        else itemsDict[baseName] = (item, itemsDict[baseName].R);
                    }
                    else if (side == LRSide.R)
                    {
                        if (!itemsDict.ContainsKey(baseName)) itemsDict[baseName] = (null, item);
                        else itemsDict[baseName] = (itemsDict[baseName].L, item);
                    }
                    else
                    {
                        singles.Add(item);
                    }
                }
                else
                {
                    singles.Add(item);
                }
            }

            foreach (var kvp in itemsDict)
            {
                var left = kvp.Value.L;
                var right = kvp.Value.R;

                if (left != null && right != null && Mathf.Abs(left.Value - right.Value) <= 0.001f)
                {
                    DrawMergedRow(kvp.Key, left, right, spaceLeft, model, window);
                }
                else
                {
                    if (left != null) DrawSingleRow(left, spaceLeft, model, window);
                    if (right != null) DrawSingleRow(right, spaceLeft, model, window);
                }
            }

            foreach (var s in singles)
            {
                DrawSingleRow(s, spaceLeft, model, window);
            }
        }

        private void DrawNormalSegment(ShapeKeyModel model, int start, int end, bool spaceLeft, EditorWindow window)
        {
            for (int i = start; i < end; i++)
            {
                var item = model.Items[i];
                if (!item.IsVisible || item.IsLipSyncShape) continue;
                DrawSingleRow(item, spaceLeft, model, window);
            }
        }

        private void DrawMergedRow(string baseName, ShapeKeyItem left, ShapeKeyItem right, bool spaceLeft, ShapeKeyModel model, EditorWindow window)
        {
            EditorGUILayout.BeginHorizontal();
            if (spaceLeft) GUILayout.Space(24);

            bool bothInc = left.IsIncluded && right.IsIncluded;
            int checkboxId = GUIUtility.GetControlID(FocusType.Passive);
            Rect checkboxRect = GUILayoutUtility.GetRect(18, 18, GUILayout.Width(18));
            bool newInc = GUI.Toggle(checkboxRect, bothInc, GUIContent.none);
            if (newInc != bothInc)
            {
                left.IsIncluded = newInc;
                right.IsIncluded = newInc;
                OnIncludeFlagsChanged?.Invoke();
            }

            float nameWidth = Mathf.Min(220f, EditorGUIUtility.currentViewWidth * 0.35f);
            EditorGUILayout.LabelField(baseName + "_LR", GUILayout.Width(nameWidth));

            if (GUILayout.Button("0", EditorStyles.miniButton, GUILayout.Width(22)))
            {
                if (model.TargetSkinnedMesh != null) Undo.RecordObject(model.TargetSkinnedMesh, "Reset Shape Key to 0");
                left.Value = 0f;
                right.Value = 0f;
                if (model.TargetSkinnedMesh)
                {
                    model.TargetSkinnedMesh.SetBlendShapeWeight(left.Index, 0f);
                    model.TargetSkinnedMesh.SetBlendShapeWeight(right.Index, 0f);
                }
            }

            float oldValue = left.Value;
            int sliderId = GUIUtility.GetControlID(FocusType.Passive);
            EditorGUI.BeginChangeCheck();
            float newValue = EditorGUILayout.Slider(oldValue, 0f, 100f);
            bool valueChanged = EditorGUI.EndChangeCheck();
            bool isThisSliderHot = (GUIUtility.hotControl == sliderId || GUIUtility.hotControl == sliderId + 1);

            if (valueChanged && isThisSliderHot && (!isSliderDragging || currentDraggingIndex != left.Index))
            {
                if (model.TargetSkinnedMesh != null) Undo.RecordObject(model.TargetSkinnedMesh, "Change Shape Key Value");
                isSliderDragging = true;
                currentDraggingIndex = left.Index;
            }

            if (valueChanged)
            {
                if (!isThisSliderHot && !isSliderDragging && model.TargetSkinnedMesh != null)
                {
                    Undo.RecordObject(model.TargetSkinnedMesh, "Change Shape Key Value");
                }
                left.Value = newValue;
                right.Value = newValue;
                
                if (model.TargetSkinnedMesh)
                {
                    if (isThisSliderHot || isSliderDragging)
                    {
                        QueuePendingApply(left.Index, newValue);
                        QueuePendingApply(right.Index, newValue);
                    }
                    else
                    {
                        model.TargetSkinnedMesh.SetBlendShapeWeight(left.Index, newValue);
                        model.TargetSkinnedMesh.SetBlendShapeWeight(right.Index, newValue);
                    }
                }
            }

            if (isSliderDragging && currentDraggingIndex == left.Index && !isThisSliderHot)
            {
                isSliderDragging = false;
                currentDraggingIndex = -1;
                StopThrottle();
                if (model.TargetSkinnedMesh)
                {
                    model.TargetSkinnedMesh.SetBlendShapeWeight(left.Index, left.Value);
                    model.TargetSkinnedMesh.SetBlendShapeWeight(right.Index, right.Value);
                }
            }

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(3f);
        }

        private void DrawSingleRow(ShapeKeyItem item, bool spaceLeft, ShapeKeyModel model, EditorWindow window)
        {
            EditorGUILayout.BeginHorizontal();
            if (spaceLeft) GUILayout.Space(24);

            int checkboxId = GUIUtility.GetControlID(FocusType.Passive);
            Rect checkboxRect = GUILayoutUtility.GetRect(18, 18, GUILayout.Width(18));
            bool newInc = GUI.Toggle(checkboxRect, item.IsIncluded, GUIContent.none);
            if (newInc != item.IsIncluded)
            {
                item.IsIncluded = newInc;
                OnIncludeFlagsChanged?.Invoke();
            }

            float nameWidth = Mathf.Min(220f, EditorGUIUtility.currentViewWidth * 0.35f);
            EditorGUILayout.LabelField(item.Name, GUILayout.Width(nameWidth));

            if (GUILayout.Button("0", EditorStyles.miniButton, GUILayout.Width(22)))
            {
                if (item.Value != 0f)
                {
                    if (model.TargetSkinnedMesh != null) Undo.RecordObject(model.TargetSkinnedMesh, "Reset Shape Key to 0");
                    item.Value = 0f;
                    if (model.TargetSkinnedMesh) model.TargetSkinnedMesh.SetBlendShapeWeight(item.Index, 0f);
                }
            }

            float oldValue = item.Value;
            int sliderId = GUIUtility.GetControlID(FocusType.Passive);
            EditorGUI.BeginChangeCheck();
            float newValue = EditorGUILayout.Slider(oldValue, 0f, 100f);
            bool valueChanged = EditorGUI.EndChangeCheck();
            bool isThisSliderHot = (GUIUtility.hotControl == sliderId || GUIUtility.hotControl == sliderId + 1);

            if (valueChanged && isThisSliderHot && (!isSliderDragging || currentDraggingIndex != item.Index))
            {
                if (model.TargetSkinnedMesh != null) Undo.RecordObject(model.TargetSkinnedMesh, "Change Shape Key Value");
                isSliderDragging = true;
                currentDraggingIndex = item.Index;
            }

            if (valueChanged)
            {
                if (!isThisSliderHot && !isSliderDragging && model.TargetSkinnedMesh != null)
                {
                    Undo.RecordObject(model.TargetSkinnedMesh, "Change Shape Key Value");
                }
                item.Value = newValue;
                
                if (model.TargetSkinnedMesh)
                {
                    if (isThisSliderHot || isSliderDragging)
                    {
                        QueuePendingApply(item.Index, newValue);
                    }
                    else
                    {
                        model.TargetSkinnedMesh.SetBlendShapeWeight(item.Index, newValue);
                    }
                }
            }

            if (isSliderDragging && currentDraggingIndex == item.Index && !isThisSliderHot)
            {
                isSliderDragging = false;
                currentDraggingIndex = -1;
                StopThrottle();
                if (model.TargetSkinnedMesh)
                {
                    model.TargetSkinnedMesh.SetBlendShapeWeight(item.Index, item.Value);
                }
            }

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(3f);
        }
    }
}
