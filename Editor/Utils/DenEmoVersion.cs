using UnityEditor;
using UnityEngine;
using System;
using System.IO;

namespace DenEmo
{
    /// <summary>
    /// DenEmo の現行バージョン。ローカルの version.json を GUID 経由で取得し、
    /// アップデートチェックの際にリモートの version.json と比較する。
    /// インポート時（コンパイル完了時）や起動時に自動的にアップデートチェックを実行する。
    /// </summary>
    [InitializeOnLoad]
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

        // セッションキー
        internal const string VerCheckDoneKey   = "DenEmo_VerCheck_Done";
        internal const string VerCheckStateKey  = "DenEmo_VerCheck_State";
        internal const string VerCheckLatestKey = "DenEmo_VerCheck_Latest";
        internal const string VerCheckUrlKey    = "DenEmo_VerCheck_Url";
        internal const string VerCheckMessageKey = "DenEmo_VerCheck_Message";

        static DenEmoVersion()
        {
            // インポート時、またはUnity起動時に自動で非同期取得を開始する
            StartCheckBackgroundTask();
        }

        internal static void StartCheckBackgroundTask()
        {
            if (SessionState.GetBool(VerCheckDoneKey, false)) return;

            Dennoko.DennokoVersionChecker.CheckAsync(
                RepoOwner, RepoName, RepoBranch, VersionFilePath, Current, OnVersionChecked);
        }

        private static void OnVersionChecked(Dennoko.DennokoVersionChecker.Result result)
        {
            SessionState.SetBool(VerCheckDoneKey, true);
            SessionState.SetInt(VerCheckStateKey, (int)result.State);
            SessionState.SetString(VerCheckLatestKey, result.LatestVersion ?? string.Empty);
            SessionState.SetString(VerCheckUrlKey, result.Url ?? string.Empty);
            SessionState.SetString(VerCheckMessageKey, result.Message ?? string.Empty);

            // すでにエディタウィンドウが開かれている場合は再描画を促す
            var windows = Resources.FindObjectsOfTypeAll<DenEmoWindow>();
            if (windows != null && windows.Length > 0)
            {
                foreach (var w in windows)
                {
                    if (w != null)
                    {
                        w.LoadVersionResultFromSessionState();
                    }
                }
            }
        }

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
