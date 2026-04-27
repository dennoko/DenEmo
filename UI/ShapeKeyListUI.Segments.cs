using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using DenEmo.Models;
using DenEmo.Core;

namespace DenEmo.UI
{
    public partial class ShapeKeyListUI
    {
        private void DrawSymmetrySegment(ShapeKeyModel model, int start, int end, bool spaceLeft, EditorWindow window, AnimationDrawContext animContext)
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
                var left = kvp.Value.L;
                var right = kvp.Value.R;

                if (animContext?.TrackShapeNames != null)
                {
                    bool lInTrack = left != null && animContext.TrackShapeNames.Contains(left.Name);
                    bool rInTrack = right != null && animContext.TrackShapeNames.Contains(right.Name);
                    if (!lInTrack && !rInTrack) continue;
                }

                if (left != null && right != null && Mathf.Abs(left.Value - right.Value) <= 0.001f)
                    DrawMergedRow(kvp.Key, left, right, spaceLeft, model, animContext);
                else
                {
                    if (left != null) DrawSingleRow(left, spaceLeft, model, animContext);
                    if (right != null) DrawSingleRow(right, spaceLeft, model, animContext);
                }
            }

            foreach (var s in singles)
            {
                if (animContext?.TrackShapeNames != null && !animContext.TrackShapeNames.Contains(s.Name)) continue;
                DrawSingleRow(s, spaceLeft, model, animContext);
            }
        }

        private void DrawNormalSegment(ShapeKeyModel model, int start, int end, bool spaceLeft, EditorWindow window, AnimationDrawContext animContext)
        {
            for (int i = start; i < end; i++)
            {
                var item = model.Items[i];
                if (!item.IsVisible || item.IsLipSyncShape) continue;
                if (animContext?.TrackShapeNames != null && !animContext.TrackShapeNames.Contains(item.Name)) continue;
                DrawSingleRow(item, spaceLeft, model, animContext);
            }
        }
    }
}
