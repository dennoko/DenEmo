using UnityEditor;
using UnityEngine;
using System;
using System.IO;

namespace DenEmo
{
    /// <summary>
    /// DenEmo の現行バージョン。ローカルの version.json を GUID 経由で取得し、
    /// アップデートチェックの際にリモートの version.json と比較する。
    /// </summary>
    internal static class DenEmoVersion
    {
        private const string VersionJsonGuid = "92a2ece039bec4741829e3eb4c9bb41c";
        private static string _currentCache = null;

        internal static string Current
        {
            get
            {
                if (_currentCache == null)
                {
                    _currentCache = LoadLocalVersion();
                }
                return _currentCache;
            }
        }

        // アップデートチェック先（設定されているリモートリポジトリ）
        internal const string RepoOwner = "dennoko";
        internal const string RepoName  = "DenEmo";
        internal const string RepoBranch = "main";
        internal const string VersionFilePath = "version.json";

        [Serializable]
        private class VersionInfo
        {
            public string version;
        }

        private static string LoadLocalVersion()
        {
            var path = AssetDatabase.GUIDToAssetPath(VersionJsonGuid);
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    var info = JsonUtility.FromJson<VersionInfo>(json);
                    if (info != null && !string.IsNullOrEmpty(info.version))
                    {
                        return info.version;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[DenEmoVersion] Failed to load local version.json: {e.Message}");
                }
            }
            // フォールバック用の初期値
            return "3.0.0";
        }
    }
}
