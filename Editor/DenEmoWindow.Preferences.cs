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

        private const string IncludeFlagsV2Prefix = "v2\n";

        private void LoadIncludeFlagsPrefs()
        {
            var bySmr = BuildItemsBySmr();
            bool migrated = false;

            foreach (var kv in bySmr)
            {
                var key = GetBlendPrefsKeyForSmr(kv.Key);
                if (string.IsNullOrEmpty(key)) continue;
                key += "|IncludeFlags";
                if (!EditorPrefs.HasKey(key)) continue;
                var s = EditorPrefs.GetString(key);
                if (string.IsNullOrEmpty(s)) continue;

                var items = kv.Value;
                if (s.StartsWith(IncludeFlagsV2Prefix, StringComparison.Ordinal))
                {
                    var body = s.Substring(IncludeFlagsV2Prefix.Length);
                    var includedNames = new HashSet<string>(
                        body.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries),
                        StringComparer.Ordinal);
                    foreach (var item in items)
                        item.IsIncluded = includedNames.Contains(item.Name);
                }
                else
                {
                    // 旧フォーマット ("1,0,1,...") からの移行:
                    // シェイプキー数が一致する場合のみインデックスで復元し、直ちに v2 形式へ変換保存する。
                    // FBX 更新等でシェイプキー数が変化している場合は位置ずれを防ぐためインデックス復元を行わない。
                    var parts = s.Split(',');
                    if (parts.Length == items.Count)
                    {
                        for (int i = 0; i < items.Count; i++)
                            items[i].IsIncluded = parts[i] == "1" || parts[i].Equals("true", StringComparison.OrdinalIgnoreCase);
                    }
                    migrated = true;
                }
            }

            if (migrated)
                SaveIncludeFlagsPrefsImmediate();
        }

        private void SaveIncludeFlagsPrefsImmediate()
        {
            var bySmr = BuildItemsBySmr();
            var sb = new System.Text.StringBuilder();

            foreach (var kv in bySmr)
            {
                var key = GetBlendPrefsKeyForSmr(kv.Key);
                if (string.IsNullOrEmpty(key)) continue;
                key += "|IncludeFlags";

                sb.Clear();
                sb.Append(IncludeFlagsV2Prefix);
                var items = kv.Value;
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i].IsIncluded)
                        sb.Append(items[i].Name).Append('\n');
                }
                EditorPrefs.SetString(key, sb.ToString());
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

        private const string SnapshotV2Prefix = "v2\n";

        private static string MakeSnapshotKey(ShapeKeyItem item)
            => (item.SmrPath ?? "") + "\t" + item.Name;

        private void CreateSnapshot(bool loadTime)
        {
            if (_model.Items.Count == 0) return;
            snapshotValues = new Dictionary<string, float>(_model.Items.Count, StringComparer.Ordinal);
            foreach (var item in _model.Items)
                snapshotValues[MakeSnapshotKey(item)] = item.Value;
            if (!loadTime)
                SaveSnapshotPrefs();
        }

        private void SaveSnapshotPrefs()
        {
            if (snapshotValues == null || snapshotValues.Count == 0) return;
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            var sb = new System.Text.StringBuilder();
            sb.Append(SnapshotV2Prefix);
            foreach (var kv in snapshotValues)
                sb.Append(kv.Key).Append('\t').Append(kv.Value.ToString(ic)).Append('\n');
            DenEmoProjectPrefs.SetString("DenEmo_Snapshot", sb.ToString());
        }

        /// <summary>メモリ上にスナップショットが無ければプロジェクト設定から復元する。</summary>
        private void EnsureSnapshotLoaded()
        {
            if (snapshotValues != null && snapshotValues.Count > 0) return;
            var s = DenEmoProjectPrefs.GetString("DenEmo_Snapshot");
            if (string.IsNullOrEmpty(s)) return;
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            snapshotValues = new Dictionary<string, float>(StringComparer.Ordinal);

            if (s.StartsWith(SnapshotV2Prefix, StringComparison.Ordinal))
            {
                var body = s.Substring(SnapshotV2Prefix.Length);
                foreach (var line in body.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    int sep = line.LastIndexOf('\t');
                    if (sep <= 0) continue;
                    string k = line.Substring(0, sep);
                    string vStr = line.Substring(sep + 1);
                    if (float.TryParse(vStr, System.Globalization.NumberStyles.Float, ic, out var f))
                        snapshotValues[k] = f;
                }
            }
            else
            {
                // 旧フォーマット (カンマ区切り数値列) はシェイプキー数が一致する場合のみ復元
                var parts = s.Split(',');
                if (parts.Length == _model.Items.Count)
                {
                    for (int i = 0; i < parts.Length; i++)
                    {
                        float f = float.TryParse(parts[i], System.Globalization.NumberStyles.Float, ic, out var v) ? v : 0f;
                        snapshotValues[MakeSnapshotKey(_model.Items[i])] = f;
                    }
                }
            }
        }

        private void RestoreSnapshot()
        {
            EnsureSnapshotLoaded();
            if (snapshotValues == null || snapshotValues.Count == 0) return;

            var recordedSmrs = new HashSet<SkinnedMeshRenderer>();
            foreach (var item in _model.Items)
            {
                var smr = item.OwnerSmr ?? _model.TargetSkinnedMesh;
                if (smr != null && recordedSmrs.Add(smr))
                    Undo.RecordObject(smr, "Restore Snapshot");

                float val = snapshotValues.TryGetValue(MakeSnapshotKey(item), out var v) ? v : 0f;
                item.Value = val;
                if (smr != null) smr.SetBlendShapeWeight(item.Index, val);
            }
            SaveBlendValuesPrefs();
        }

        /// <summary>
        /// スナップショットとの差分判定を返す。シェイプキー名ベースで比較し、
        /// スナップショット未収録の新規シェイプは初期値 0 と比較する。
        /// スナップショット未作成時は null。
        /// </summary>
        private Func<ShapeKeyItem, bool> BuildSnapshotDiffChecker()
        {
            EnsureSnapshotLoaded();
            if (snapshotValues == null || snapshotValues.Count == 0) return null;

            return item =>
            {
                snapshotValues.TryGetValue(MakeSnapshotKey(item), out float baseVal);
                return !Mathf.Approximately(item.Value, baseVal);
            };
        }
    }
}
