namespace DenEmo
{
    /// <summary>
    /// DenEmo の現行バージョン。リリース時にこことリポジトリ直下の version.json を
    /// 同時に更新する。アップデートチェックはこの値をリモートの version.json と比較する。
    /// </summary>
    internal static class DenEmoVersion
    {
        internal const string Current = "1.0.0";

        // アップデートチェック先（設定されているリモートリポジトリ）
        internal const string RepoOwner = "dennoko";
        internal const string RepoName  = "DenEmo";
        internal const string RepoBranch = "main";
        internal const string VersionFilePath = "version.json";
    }
}
