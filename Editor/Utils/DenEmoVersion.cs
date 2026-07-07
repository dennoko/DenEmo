using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Runtime.CompilerServices;

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
        // version.json をどうしても読めなかった場合の最終フォールバック。
        // 通常は下記 2 経路（GUID / スクリプト相対）のどちらかで解決するので使われない。
        private const string FallbackVersion = "0.0.0";
        private static string _currentCache = null;

        internal static string Current
        {
            get
            {
                // 失敗（null）はキャッシュしない。インポート直後で version.json が
                // まだ読めなかった場合でも、次回アクセス時に再試行できるようにする。
                if (string.IsNullOrEmpty(_currentCache))
                {
                    _currentCache = LoadLocalVersion();
                }
                return string.IsNullOrEmpty(_currentCache) ? FallbackVersion : _currentCache;
            }
        }

        // アップデートチェック先（設定されているリモートリポジトリ）
        internal const string RepoOwner = "dennoko";
        internal const string RepoName  = "DenEmo";
        internal const string RepoBranch = "main";
        internal const string VersionFilePath = "version.json";

        // セッションキー。State（比較結果）は保存しない — ローカル版が後から正しく
        // 解決され得るため、表示のたびに「保存した最新版 vs 現在のローカル版」で
        // 更新有無を再計算する。ここでは取得が成功したか（Error だったか）だけ保存する。
        internal const string VerCheckDoneKey   = "DenEmo_VerCheck_Done";
        internal const string VerCheckErrorKey  = "DenEmo_VerCheck_Error";
        internal const string VerCheckLatestKey = "DenEmo_VerCheck_Latest";
        internal const string VerCheckUrlKey    = "DenEmo_VerCheck_Url";
        internal const string VerCheckMessageKey = "DenEmo_VerCheck_Message";

        static DenEmoVersion()
        {
            // 静的コンストラクタはドメインリロード中（アセットインポートやコンパイル完了
            // 直後）に走るため、この時点では version.json が AssetDatabase に未登録で
            // GUIDToAssetPath が空を返すことがある。delayCall で 1 tick 遅らせ、
            // AssetDatabase が使える状態になってから取得を開始する。
            EditorApplication.delayCall += StartCheckBackgroundTask;
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
            SessionState.SetBool(VerCheckErrorKey, result.State == Dennoko.DennokoVersionChecker.State.Error);
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

        /// <summary>ローカルの version.json を読む。読めなければ null（呼び出し側で
        /// フォールバックし、次回アクセス時に再試行する）。</summary>
        private static string LoadLocalVersion()
        {
            // 1) GUID 経由（アセット移動に追従。ただし AssetDatabase 準備前は空を返し得る）
            var v = TryReadVersion(AssetDatabase.GUIDToAssetPath(VersionJsonGuid));
            if (v != null) return v;

            // 2) スクリプト位置からの相対探索（AssetDatabase 未準備でも解決できる保険）
            return TryReadVersion(ResolveVersionJsonByScriptPath());
        }

        private static string TryReadVersion(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try
            {
                var info = JsonUtility.FromJson<VersionInfo>(File.ReadAllText(path));
                if (info != null && !string.IsNullOrEmpty(info.version)) return info.version;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DenEmoVersion] Failed to read version.json ({path}): {e.Message}");
            }
            return null;
        }

        /// <summary>
        /// このスクリプトの位置を起点に上位フォルダを辿って version.json を探す。
        /// [CallerFilePath] はコンパイル時パスなので、パッケージを他プロジェクトへ
        /// インポートして再コンパイルされれば、そのプロジェクト内の正しいパスに解決される
        /// （AssetDatabase のインポート完了状況に依存しない）。
        /// </summary>
        private static string ResolveVersionJsonByScriptPath([CallerFilePath] string scriptPath = null)
        {
            if (string.IsNullOrEmpty(scriptPath)) return null;
            var dir = Path.GetDirectoryName(scriptPath);
            for (int i = 0; i < 5 && !string.IsNullOrEmpty(dir); i++)
            {
                var candidate = Path.Combine(dir, "version.json");
                if (File.Exists(candidate)) return candidate;
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }
    }
}
