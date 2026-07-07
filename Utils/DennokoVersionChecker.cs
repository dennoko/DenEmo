using System;
using UnityEngine;
using UnityEngine.Networking;

namespace Dennoko
{
    /// <summary>
    /// GitHub Public リポジトリ上の version.json を取得し、ローカル版と比較する
    /// エディタ専用の自己完結アップデートチェッカー。
    ///
    /// version.json の形式:
    ///   { "version": "1.2.0", "url": "https://.../releases", "message": "" }
    ///
    /// 使い方（owner / repo は各プロジェクトに「設定されているリモートリポジトリ」に合わせる。
    /// ここではハードコードせず呼び出し側から渡す）:
    ///   DennokoVersionChecker.CheckAsync(
    ///       "your-owner", "your-repo", "main", "version.json",
    ///       YourVersion.Current, OnResult);
    ///
    /// ローカライズ非依存: 文言は返さず「状態(State)」だけ返す。表示側の i18n で文言化する。
    /// 共通仕様: dennokoworks_color_schema/forUnity/topbar_version_template.md
    /// </summary>
    public static class DennokoVersionChecker
    {
        /// <summary>true にすると取得URL・HTTPステータス・レスポンス・比較結果を Console に出力する。</summary>
        public static bool VerboseLog = false;

        public enum State { Checking, UpToDate, UpdateAvailable, Error }

        public struct Result
        {
            public State State;
            public string LocalVersion;
            public string LatestVersion;
            public string Url;
            public string Message;
        }

        [Serializable]
        private class VersionInfo
        {
            public string version;
            public string url;
            public string message;
        }

        /// <summary>
        /// version.json を非同期取得して結果を onResult に渡す。例外は投げず、失敗時は
        /// State.Error を返す。onResult は Unity のメインスレッド上で呼ばれる。
        /// </summary>
        public static void CheckAsync(
            string owner, string repo, string branch, string filePath,
            string localVersion, Action<Result> onResult)
        {
            if (onResult == null) return;

            UnityWebRequest req;
            string requestUrl;
            try
            {
                requestUrl = $"https://raw.githubusercontent.com/{owner}/{repo}/{branch}/{filePath}";
                req = UnityWebRequest.Get(requestUrl);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DennokoVersionChecker] request build failed: {e.Message}");
                onResult(Error(localVersion));
                return;
            }

            Log($"GET {requestUrl}  (local={localVersion})");
            var op = req.SendWebRequest();
            op.completed += _ =>
            {
                try
                {
                    onResult(BuildResult(req, localVersion));
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[DennokoVersionChecker] callback failed: {e.Message}");
                    onResult(Error(localVersion));
                }
                finally
                {
                    req.Dispose();
                }
            };
        }

        private static Result BuildResult(UnityWebRequest req, string localVersion)
        {
#if UNITY_2020_2_OR_NEWER
            bool hasError = req.result != UnityWebRequest.Result.Success;
            Log($"completed: result={req.result} httpCode={req.responseCode} error={req.error}");
#else
            bool hasError = req.isNetworkError || req.isHttpError;
            Log($"completed: httpCode={req.responseCode} netErr={req.isNetworkError} httpErr={req.isHttpError} error={req.error}");
#endif
            if (hasError)
            {
                Log("→ Error (network/HTTP error). version.json が push 済みか・URL・ブランチ名を確認。");
                return Error(localVersion);
            }

            var json = req.downloadHandler != null ? req.downloadHandler.text : null;
            Log($"response body: {Truncate(json)}");
            if (string.IsNullOrEmpty(json))
            {
                Log("→ Error (empty body).");
                return Error(localVersion);
            }

            VersionInfo info;
            try { info = JsonUtility.FromJson<VersionInfo>(json); }
            catch (Exception e) { Log($"→ Error (JSON parse failed): {e.Message}"); return Error(localVersion); }

            if (info == null || string.IsNullOrEmpty(info.version))
            {
                Log("→ Error (version フィールドが空)。");
                return Error(localVersion);
            }

            var state = IsNewer(info.version, localVersion) ? State.UpdateAvailable : State.UpToDate;
            Log($"→ {state} (remote={info.version}, local={localVersion})");
            return new Result
            {
                State = state,
                LocalVersion = localVersion,
                LatestVersion = info.version,
                Url = info.url,
                Message = info.message,
            };
        }

        private static Result Error(string localVersion) => new Result
        {
            State = State.Error,
            LocalVersion = localVersion,
            LatestVersion = null,
            Url = null,
            Message = null,
        };

        /// <summary>latest がローカル版より新しいか。SemVer 優先、パース不能時は文字列不一致で判定。</summary>
        private static bool IsNewer(string latest, string local)
        {
            var l = Normalize(latest);
            var c = Normalize(local);
            if (Version.TryParse(l, out var vLatest) && Version.TryParse(c, out var vLocal))
                return vLatest > vLocal;
            // フォールバック: 文字列が異なれば「更新あり」とみなす
            return !string.Equals(l, c, StringComparison.OrdinalIgnoreCase);
        }

        private static string Normalize(string v)
        {
            if (string.IsNullOrEmpty(v)) return "0";
            v = v.Trim();
            if (v.StartsWith("v") || v.StartsWith("V")) v = v.Substring(1);
            return v;
        }

        private static void Log(string msg)
        {
            if (VerboseLog) Debug.Log($"[DennokoVersionChecker] {msg}");
        }

        private static string Truncate(string s, int max = 300)
        {
            if (string.IsNullOrEmpty(s)) return "(empty)";
            s = s.Replace("\n", " ").Replace("\r", " ");
            return s.Length <= max ? s : s.Substring(0, max) + "…";
        }
    }
}
