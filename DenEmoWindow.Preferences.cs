using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using DenEmo.Models;

namespace DenEmo
{
    public partial class DenEmoWindow
    {
        // ─── Favorites ───────────────────────────────────────────────────────

        private string GetFavoritesKeyForSmr(SkinnedMeshRenderer smr)
        {
            if (smr == null || smr.sharedMesh == null) return null;
            string meshPath = AssetDatabase.GetAssetPath(smr.sharedMesh);
            string guid = string.IsNullOrEmpty(meshPath)
                ? smr.sharedMesh.name
                : AssetDatabase.AssetPathToGUID(meshPath);
            return "DenEmo_Fav|" + guid;
        }

        private void LoadFavoritesPrefs()
        {
            var loaded = new HashSet<SkinnedMeshRenderer>();
            foreach (var item in _model.Items)
            {
                var smr = item.OwnerSmr ?? _model.TargetSkinnedMesh;
                if (smr == null || loaded.Contains(smr)) continue;

                var key = GetFavoritesKeyForSmr(smr);
                if (string.IsNullOrEmpty(key) || !EditorPrefs.HasKey(key)) { loaded.Add(smr); continue; }

                var s = EditorPrefs.GetString(key, "");
                if (!string.IsNullOrEmpty(s))
                {
                    var names = new HashSet<string>(s.Split(','));
                    foreach (var i in _model.Items)
                        if ((i.OwnerSmr ?? _model.TargetSkinnedMesh) == smr)
                            i.IsFavorite = names.Contains(i.Name);
                }
                loaded.Add(smr);
            }
        }

        private void SaveFavoritesPrefs()
        {
            var bySmr   = new Dictionary<SkinnedMeshRenderer, List<string>>();
            var allSmrs = new HashSet<SkinnedMeshRenderer>();

            foreach (var item in _model.Items)
            {
                var smr = item.OwnerSmr ?? _model.TargetSkinnedMesh;
                if (smr == null) continue;
                allSmrs.Add(smr);
                if (item.IsFavorite)
                {
                    if (!bySmr.ContainsKey(smr)) bySmr[smr] = new List<string>();
                    bySmr[smr].Add(item.Name);
                }
            }

            foreach (var smr in allSmrs)
            {
                var key = GetFavoritesKeyForSmr(smr);
                if (string.IsNullOrEmpty(key)) continue;
                EditorPrefs.SetString(key,
                    bySmr.TryGetValue(smr, out var favs) ? string.Join(",", favs) : "");
            }
        }

        private void OnFavoriteChanged(string shapeName, bool isFavorite)
        {
            SaveFavoritesPrefs();
            if (showOnlyFavorites) UpdateVisibility();
            Repaint();
        }

        // ─── Blend value prefs ────────────────────────────────────────────────

        private string GetBlendPrefsKeyForSmr(SkinnedMeshRenderer smr)
        {
            if (smr == null || smr.sharedMesh == null) return null;
            string scene    = smr.gameObject ? smr.gameObject.scene.name : "";
            string meshName = smr.sharedMesh.name;
            return $"DenEmo_Values|{scene}|{meshName}";
        }

        private void SaveBlendValuesPrefs()
        {
            var bySmr = new Dictionary<SkinnedMeshRenderer, List<ShapeKeyItem>>();
            foreach (var item in _model.Items)
            {
                var smr = item.OwnerSmr ?? _model.TargetSkinnedMesh;
                if (smr == null) continue;
                if (!bySmr.ContainsKey(smr)) bySmr[smr] = new List<ShapeKeyItem>();
                bySmr[smr].Add(item);
            }

            foreach (var kv in bySmr)
            {
                var key = GetBlendPrefsKeyForSmr(kv.Key);
                if (string.IsNullOrEmpty(key)) continue;
                var parts = new string[kv.Value.Count];
                for (int i = 0; i < kv.Value.Count; i++)
                    parts[i] = kv.Value[i].Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                EditorPrefs.SetString(key, string.Join(",", parts));
            }
        }

        // ─── Include flag prefs ───────────────────────────────────────────────

        private void LoadIncludeFlagsPrefs()
        {
            var bySmr = BuildItemsBySmr();

            foreach (var kv in bySmr)
            {
                var key = GetBlendPrefsKeyForSmr(kv.Key);
                if (string.IsNullOrEmpty(key)) continue;
                key += "|IncludeFlags";
                if (!EditorPrefs.HasKey(key)) continue;
                var s = EditorPrefs.GetString(key);
                if (string.IsNullOrEmpty(s)) continue;
                var parts = s.Split(',');
                var items = kv.Value;
                for (int i = 0; i < parts.Length && i < items.Count; i++)
                    items[i].IsIncluded = parts[i] == "1" || parts[i].Equals("true", StringComparison.OrdinalIgnoreCase);
            }
        }

        private void SaveIncludeFlagsPrefsImmediate()
        {
            var bySmr = BuildItemsBySmr();

            foreach (var kv in bySmr)
            {
                var key = GetBlendPrefsKeyForSmr(kv.Key);
                if (string.IsNullOrEmpty(key)) continue;
                key += "|IncludeFlags";
                var parts = new string[kv.Value.Count];
                for (int i = 0; i < kv.Value.Count; i++)
                    parts[i] = kv.Value[i].IsIncluded ? "1" : "0";
                EditorPrefs.SetString(key, string.Join(",", parts));
            }
            includeFlagsDirty = false;
        }

        private Dictionary<SkinnedMeshRenderer, List<ShapeKeyItem>> BuildItemsBySmr()
        {
            var bySmr = new Dictionary<SkinnedMeshRenderer, List<ShapeKeyItem>>();
            foreach (var item in _model.Items)
            {
                var smr = item.OwnerSmr ?? _model.TargetSkinnedMesh;
                if (smr == null) continue;
                if (!bySmr.ContainsKey(smr)) bySmr[smr] = new List<ShapeKeyItem>();
                bySmr[smr].Add(item);
            }
            return bySmr;
        }

        // ─── Collapsed groups prefs ───────────────────────────────────────────

        private void LoadCollapsedGroupsPrefs()
        {
            collapsedGroups.Clear();
            var s = DenEmoProjectPrefs.GetString("DenEmo_GroupsCollapsed", "");
            if (string.IsNullOrEmpty(s)) return;
            foreach (var p in s.Split(new char[] { '\n', '\r', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var k = p.Trim();
                if (k.Length > 0) collapsedGroups.Add(k);
            }
        }

        private void SaveCollapsedGroupsPrefs()
        {
            DenEmoProjectPrefs.SetString("DenEmo_GroupsCollapsed",
                collapsedGroups.Count == 0 ? "" : string.Join(",", collapsedGroups));
        }

        // ─── Snapshot ─────────────────────────────────────────────────────────

        private void CreateSnapshot(bool loadTime)
        {
            if (_model.Items.Count == 0) return;
            snapshotValues = new List<float>();
            foreach (var i in _model.Items) snapshotValues.Add(i.Value);
            if (!loadTime)
            {
                var parts = new string[snapshotValues.Count];
                for (int i = 0; i < snapshotValues.Count; i++)
                    parts[i] = snapshotValues[i].ToString(System.Globalization.CultureInfo.InvariantCulture);
                DenEmoProjectPrefs.SetString("DenEmo_Snapshot", string.Join(",", parts));
            }
        }

        private void RestoreSnapshot()
        {
            if (snapshotValues == null || snapshotValues.Count == 0)
            {
                var s = DenEmoProjectPrefs.GetString("DenEmo_Snapshot");
                if (!string.IsNullOrEmpty(s))
                {
                    var parts = s.Split(',');
                    snapshotValues = new List<float>();
                    foreach (var p in parts)
                    {
                        snapshotValues.Add(float.TryParse(p, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : 0f);
                    }
                }
            }
            if (snapshotValues == null) return;
            int n = Math.Min(snapshotValues.Count, _model.Items.Count);
            for (int i = 0; i < n; i++)
            {
                var item = _model.Items[i];
                item.Value = snapshotValues[i];
                var smr = item.OwnerSmr ?? _model.TargetSkinnedMesh;
                if (smr != null) smr.SetBlendShapeWeight(item.Index, snapshotValues[i]);
            }
            SaveBlendValuesPrefs();
        }
    }
}
