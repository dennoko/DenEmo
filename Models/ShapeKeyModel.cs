using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DenEmo.Models
{
    public class GroupSegment
    {
        public string Key      { get; set; }
        public int    Start    { get; set; }
        public int    Length   { get; set; }
        public string MeshName { get; set; } // null = single-mesh mode
    }

    public class ShapeKeyModel
    {
        private const float DefaultVertexMovementThreshold = 0.000001f;
        private Vector3[] _blendShapeDeltaVertices;
        private Vector3[] _blendShapeDeltaNormals;
        private Vector3[] _blendShapeDeltaTangents;

        public List<ShapeKeyItem>        Items            { get; private set; } = new List<ShapeKeyItem>();
        public List<GroupSegment>        GroupSegments    { get; private set; } = new List<GroupSegment>();
        public List<SkinnedMeshRenderer> DiscoveredMeshes { get; private set; } = new List<SkinnedMeshRenderer>();
        public List<SkinnedMeshRenderer> ActiveMeshes     { get; private set; } = new List<SkinnedMeshRenderer>();

        public SkinnedMeshRenderer TargetSkinnedMesh { get; private set; }
        public GameObject          TargetObject      { get; private set; }

        public void SetTarget(SkinnedMeshRenderer smr)
        {
            TargetSkinnedMesh = smr;
            TargetObject      = smr ? smr.gameObject : null;
            DiscoveredMeshes.Clear();
            ActiveMeshes.Clear();
        }

        public void DiscoverMeshes()
        {
            DiscoveredMeshes.Clear();
            if (TargetSkinnedMesh == null) return;

            var root = TargetSkinnedMesh.transform.root;
            var smrs = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in smrs)
            {
                if (smr != null && smr.sharedMesh != null && smr.sharedMesh.blendShapeCount > 0)
                    DiscoveredMeshes.Add(smr);
            }

            // プライマリメッシュを先頭に
            if (DiscoveredMeshes.Contains(TargetSkinnedMesh))
            {
                DiscoveredMeshes.Remove(TargetSkinnedMesh);
                DiscoveredMeshes.Insert(0, TargetSkinnedMesh);
            }
        }

        public void SetActiveMeshes(List<SkinnedMeshRenderer> meshes)
        {
            ActiveMeshes.Clear();
            if (meshes != null)
                foreach (var smr in meshes)
                    if (smr != null) ActiveMeshes.Add(smr);
        }

        public void RefreshList(string searchText, bool showOnlyIncluded)
        {
            Items.Clear();
            GroupSegments.Clear();

            foreach (var smr in ActiveMeshes)
            {
                if (smr == null || smr.sharedMesh == null) continue;
                var    mesh    = smr.sharedMesh;
                int    count   = mesh.blendShapeCount;
                string smrPath = ComputeSmrPath(smr);

                for (int i = 0; i < count; i++)
                {
                    string name  = mesh.GetBlendShapeName(i);
                    float  value = smr.GetBlendShapeWeight(i);
                    var item = new ShapeKeyItem(i, name, value)
                    {
                        IsVrcShape = IsVrcShapeName(name),
                        OwnerSmr   = smr,
                        SmrPath    = smrPath,
                    };
                    Items.Add(item);
                }
            }
        }

        public static string ComputeSmrPath(SkinnedMeshRenderer smr)
        {
            if (smr == null) return "";
            var parts = new List<string>();
            var t    = smr.transform;
            var root = t.root;
            while (t != null && t != root) { parts.Add(t.name); t = t.parent; }
            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }

        public void UpdateVisibility(string[] searchTokens, bool showOnlyIncluded, bool showOnlyNonZero = false, bool showOnlyFavorites = false)
        {
            UpdateVisibility(searchTokens, showOnlyIncluded, showOnlyNonZero, showOnlyFavorites, null, false);
        }

        public void UpdateVisibility(string[] searchTokens, bool showOnlyIncluded, bool showOnlyNonZero, bool showOnlyFavorites, HashSet<int> vertexMovedShapeIndices, bool symmetryMode)
        {
            foreach (var item in Items)
            {
                item.IsVisible = false;

                if (item.IsVrcShape || item.IsLipSyncShape) continue;
                if (!MatchesAllTokens(item.Name, searchTokens)) continue;
                if (showOnlyIncluded  && !item.IsIncluded) continue;
                if (showOnlyNonZero   && Mathf.Approximately(item.Value, 0f)) continue;
                if (showOnlyFavorites && !item.IsFavorite) continue;
                if (vertexMovedShapeIndices != null && !vertexMovedShapeIndices.Contains(item.Index)) continue;

                item.IsVisible = true;
            }

            if (symmetryMode)
            {
                var pairVisible = new Dictionary<string, bool>();
                foreach (var item in Items)
                {
                    if (item.IsVrcShape || item.IsLipSyncShape) continue;
                    if (Core.SymmetryParser.TryParseLRSuffix(item.Name, out var baseName, out var side) && side != Core.LRSide.None)
                    {
                        if (item.IsVisible) pairVisible[baseName] = true;
                    }
                }
                
                foreach (var item in Items)
                {
                    if (item.IsVrcShape || item.IsLipSyncShape) continue;
                    if (Core.SymmetryParser.TryParseLRSuffix(item.Name, out var baseName, out var side) && side != Core.LRSide.None)
                    {
                        if (pairVisible.ContainsKey(baseName) && pairVisible[baseName])
                            item.IsVisible = true;
                    }
                }
            }
        }

        public HashSet<int> CollectShapeIndicesMovingVertex(int vertexIndex, float movementThreshold = DefaultVertexMovementThreshold)
        {
            var result = new HashSet<int>();
            if (TargetSkinnedMesh == null || TargetSkinnedMesh.sharedMesh == null) return result;

            var mesh = TargetSkinnedMesh.sharedMesh;
            int vertexCount = mesh.vertexCount;
            if (vertexIndex < 0 || vertexIndex >= vertexCount) return result;

            int blendShapeCount = mesh.blendShapeCount;
            if (blendShapeCount <= 0) return result;

            EnsureBlendShapeFrameBuffers(vertexCount);
            float thresholdSquared = movementThreshold * movementThreshold;

            for (int blendShapeIndex = 0; blendShapeIndex < blendShapeCount; blendShapeIndex++)
            {
                int frameCount = mesh.GetBlendShapeFrameCount(blendShapeIndex);
                for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    mesh.GetBlendShapeFrameVertices(blendShapeIndex, frameIndex, _blendShapeDeltaVertices, _blendShapeDeltaNormals, _blendShapeDeltaTangents);
                    if (_blendShapeDeltaVertices[vertexIndex].sqrMagnitude > thresholdSquared)
                    {
                        result.Add(blendShapeIndex);
                        break;
                    }
                }
            }

            return result;
        }

        private void EnsureBlendShapeFrameBuffers(int vertexCount)
        {
            if (_blendShapeDeltaVertices == null || _blendShapeDeltaVertices.Length != vertexCount)
                _blendShapeDeltaVertices = new Vector3[vertexCount];
            if (_blendShapeDeltaNormals == null || _blendShapeDeltaNormals.Length != vertexCount)
                _blendShapeDeltaNormals = new Vector3[vertexCount];
            if (_blendShapeDeltaTangents == null || _blendShapeDeltaTangents.Length != vertexCount)
                _blendShapeDeltaTangents = new Vector3[vertexCount];
        }

        public void BuildGroups()
        {
            GroupSegments.Clear();
            bool multiMesh = HasMultipleMeshes();

            string              curKey   = null;
            int                 segStart = -1;
            int                 segLen   = 0;
            SkinnedMeshRenderer curSmr   = null;

            Action flush = () =>
            {
                if (segLen > 0)
                {
                    GroupSegments.Add(new GroupSegment
                    {
                        Key      = curKey ?? "Other",
                        Start    = segStart,
                        Length   = segLen,
                        MeshName = multiMesh ? curSmr?.name : null,
                    });
                }
                curKey = null; segStart = -1; segLen = 0;
            };

            for (int i = 0; i < Items.Count; i++)
            {
                var item = Items[i];
                if (item.IsVrcShape) { flush(); continue; }

                // SMR境界でflush
                if (curSmr != item.OwnerSmr)
                {
                    flush();
                    curSmr = item.OwnerSmr;
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

        private bool HasMultipleMeshes()
        {
            SkinnedMeshRenderer first = null;
            foreach (var item in Items)
            {
                if (item.OwnerSmr == null) continue;
                if (first == null) { first = item.OwnerSmr; continue; }
                if (item.OwnerSmr != first) return true;
            }
            return false;
        }

        public void SyncValuesFromMesh()
        {
            foreach (var item in Items)
            {
                var smr = item.OwnerSmr;
                if (smr == null || smr.sharedMesh == null) continue;
                if (item.Index >= 0 && item.Index < smr.sharedMesh.blendShapeCount)
                    item.Value = smr.GetBlendShapeWeight(item.Index);
            }
        }

        // --- Helpers ---
        private static bool IsVrcShapeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            name = name.Trim();
            if (name.StartsWith("vrc.",  StringComparison.OrdinalIgnoreCase)) return true;
            if (name.StartsWith(".vrc",  StringComparison.OrdinalIgnoreCase)) return true;
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
            return text.Split(new char[] { ' ', '\t', '　' }, StringSplitOptions.RemoveEmptyEntries);
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
