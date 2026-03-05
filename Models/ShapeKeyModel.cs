using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DenEmo.Models
{
    public class GroupSegment
    {
        public string Key { get; set; }
        public int Start { get; set; }
        public int Length { get; set; }
    }

    public class ShapeKeyModel
    {
        public List<ShapeKeyItem> Items { get; private set; } = new List<ShapeKeyItem>();
        public List<GroupSegment> GroupSegments { get; private set; } = new List<GroupSegment>();
        
        public SkinnedMeshRenderer TargetSkinnedMesh { get; private set; }
        public GameObject TargetObject { get; private set; }

        public void SetTarget(SkinnedMeshRenderer smr)
        {
            TargetSkinnedMesh = smr;
            TargetObject = smr ? smr.gameObject : null;
        }

        public void RefreshList(string searchText, bool showOnlyIncluded)
        {
            Items.Clear();
            GroupSegments.Clear();

            if (TargetSkinnedMesh == null || TargetSkinnedMesh.sharedMesh == null)
            {
                return;
            }

            var mesh = TargetSkinnedMesh.sharedMesh;
            int count = mesh.blendShapeCount;

            for (int i = 0; i < count; i++)
            {
                string name = mesh.GetBlendShapeName(i);
                float value = TargetSkinnedMesh.GetBlendShapeWeight(i);
                
                var item = new ShapeKeyItem(i, name, value);
                item.IsVrcShape = IsVrcShapeName(name);
                Items.Add(item);
            }
        }

        public void UpdateVisibility(string[] searchTokens, bool showOnlyIncluded)
        {
            foreach (var item in Items)
            {
                item.IsVisible = false;
                
                if (item.IsVrcShape || item.IsLipSyncShape)
                    continue;

                if (!MatchesAllTokens(item.Name, searchTokens))
                    continue;

                if (showOnlyIncluded && !item.IsIncluded)
                    continue;

                item.IsVisible = true;
            }
        }

        public void BuildGroups()
        {
            GroupSegments.Clear();
            string curKey = null;
            int segStart = -1;
            int segLen = 0;

            Action flush = () =>
            {
                if (segLen > 0)
                {
                    GroupSegments.Add(new GroupSegment { Key = curKey ?? "Other", Start = segStart, Length = segLen });
                }
                curKey = null; segStart = -1; segLen = 0;
            };

            for (int i = 0; i < Items.Count; i++)
            {
                var item = Items[i];
                if (item.IsVrcShape)
                {
                    flush();
                    continue;
                }

                string key = GetGroupKey(item.Name);
                if (string.IsNullOrEmpty(key)) key = "Other";

                if (curKey == null)
                {
                    curKey = key; segStart = i; segLen = 1;
                }
                else if (key == curKey)
                {
                    segLen++;
                }
                else
                {
                    flush();
                    curKey = key; segStart = i; segLen = 1;
                }
            }
            flush();
        }

        public void SyncValuesFromMesh()
        {
            if (TargetSkinnedMesh == null || TargetSkinnedMesh.sharedMesh == null) return;
            int count = Mathf.Min(Items.Count, TargetSkinnedMesh.sharedMesh.blendShapeCount);
            if (count == 0) return;
            
            for (int i = 0; i < count; i++)
            {
                Items[i].Value = TargetSkinnedMesh.GetBlendShapeWeight(i);
            }
        }

        // --- Helpers ---
        private static bool IsVrcShapeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            name = name.Trim();
            if (name.StartsWith("vrc.", StringComparison.OrdinalIgnoreCase)) return true;
            if (name.StartsWith(".vrc", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string GetGroupKey(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Other";
            name = name.Trim();
            int idx = IndexOfAny(name, new char[] { ' ', '_', '-', '/', '.', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' });
            string token;
            if (idx > 0)
            {
                token = name.Substring(0, idx);
            }
            else
            {
                int cut = -1;
                for (int i = 1; i < name.Length; i++)
                {
                    if (char.IsUpper(name[i]) && char.IsLetter(name[i - 1]) && char.IsLower(name[i - 1]))
                    {
                        cut = i; break;
                    }
                }
                token = cut > 0 ? name.Substring(0, cut) : name;
            }
            token = token.Trim();
            if (token.Length == 0) return "Other";
            return token;
        }

        private static int IndexOfAny(string s, char[] chars)
        {
            int best = -1;
            for (int i = 0; i < chars.Length; i++)
            {
                int p = s.IndexOf(chars[i]);
                if (p >= 0 && (best < 0 || p < best)) best = p;
            }
            return best;
        }

        public static string[] BuildSearchTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return Array.Empty<string>();
            return text.Split(new char[] { ' ', '\t', '\u3000' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static bool MatchesAllTokens(string name, string[] tokens)
        {
            if (tokens == null || tokens.Length == 0) return true;
            if (string.IsNullOrEmpty(name)) return false;

            var nmLower = name.ToLowerInvariant();
            for (int i = 0; i < tokens.Length; i++)
            {
                var t = tokens[i];
                if (string.IsNullOrEmpty(t)) continue;
                if (nmLower.IndexOf(t.ToLowerInvariant(), StringComparison.Ordinal) < 0) return false;
            }
            return true;
        }
    }
}
