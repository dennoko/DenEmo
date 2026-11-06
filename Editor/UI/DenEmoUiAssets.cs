using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DenEmo.UI
{
    /// <summary>
    /// UI Toolkit アセット (UXML / USS) を GUID 経由でロードするヘルパー。
    /// パス直書きはアセット移動で壊れるため、必ずここを経由すること。
    /// GUID は各アセットの .meta ファイルの値と一致させる。
    /// </summary>
    internal static class DenEmoUiAssets
    {
        internal const string ThemeUssGuid = "3a5c5d4f18364fe28d1e746b348409f8";
        internal const string BlocksUssGuid = "366acbd939f74615ac4912252bb80211";
        internal const string StylesUssGuid = "e1295b68ce4c4cceae516e15953273cf";
        internal const string VertexPreviewPopupUxmlGuid = "8f78837df0074f47b2c0b3c8e34c99f0";
        internal const string MainWindowUxmlGuid = "e7410dd244164667b31e714b0148c3e6";
        internal const string ShapeKeyListUxmlGuid = "4b2f9d31c8ae4d0f9c6a5e11b7d2a940";
        internal const string ShapeKeyRowUxmlGuid = "a95cf17e30b24bd3a7c81f4e6209d5c8";
        internal const string TimelineWindowUxmlGuid = "6c8d2ab54e7f4c31a2d9b0e87f5c1364";
        internal const string FxClipPickerUxmlGuid = "c4e8f2a71d9b4b6e8f3a5c0d7b21e94a";
        internal const string ManualJaPdfGuid = "86edaf1e56d859d42b4aa5eff2e6ef99";
        internal const string ManualEnPdfGuid = "0e86d56c44f127f468d0a3df70365613";

        /// <summary>
        /// 表示言語に合わせた PDF マニュアル (Docs/user_manual_ja|en.pdf) の file:// URL。
        /// Application.OpenURL に渡すと既定の PDF ビューア（通常はブラウザ）で開く。見つからなければ null。
        /// </summary>
        internal static string ManualPdfUrl(bool english)
        {
            var path = AssetDatabase.GUIDToAssetPath(english ? ManualEnPdfGuid : ManualJaPdfGuid);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("[DenEmo] Manual PDF not found.");
                return null;
            }
            return new Uri(Path.GetFullPath(path)).AbsoluteUri;
        }

        internal static StyleSheet LoadTheme() => Load<StyleSheet>(ThemeUssGuid);

        /// <summary>大きい単位 (画面パターン・ブロック) のクラス定義。テーマの後・DenEmo 固有 USS の前に追加する。</summary>
        internal static StyleSheet LoadBlocks() => Load<StyleSheet>(BlocksUssGuid);

        /// <summary>DenEmo 固有クラス定義。テーマの後に styleSheets へ追加すること。</summary>
        internal static StyleSheet LoadStyles() => Load<StyleSheet>(StylesUssGuid);

        /// <summary>
        /// ルート要素に dennoko-root と保険背景色を設定し、テーマ → ブロック → DenEmo 固有 USS の順で USS と
        /// 標準フォントを適用する。すべての EditorWindow / PopupWindowContent はここを通す。
        /// </summary>
        internal static void SetupRoot(VisualElement root)
        {
            root.AddToClassList("dennoko-root");
            // USS ロード失敗時の保険として Surface0 を C# 側でも設定
            root.style.backgroundColor = (Color)new Color32(0x12, 0x12, 0x12, 0xFF);
            var theme = LoadTheme();
            if (theme != null) root.styleSheets.Add(theme);
            var blocks = LoadBlocks();
            if (blocks != null) root.styleSheets.Add(blocks);
            var styles = LoadStyles();
            if (styles != null) root.styleSheets.Add(styles);
            DennokoUIFont.Apply(root);
        }

        internal static VisualTreeAsset LoadVisualTree(string guid) => Load<VisualTreeAsset>(guid);

        private static T Load<T>(string guid) where T : UnityEngine.Object
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning($"[DenEmo] UI asset not found for GUID: {guid}");
                return null;
            }
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }
    }
}
