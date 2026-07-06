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
        internal const string StylesUssGuid = "e1295b68ce4c4cceae516e15953273cf";
        internal const string VertexPreviewPopupUxmlGuid = "8f78837df0074f47b2c0b3c8e34c99f0";
        internal const string MainWindowUxmlGuid = "e7410dd244164667b31e714b0148c3e6";
        internal const string ShapeKeyListUxmlGuid = "4b2f9d31c8ae4d0f9c6a5e11b7d2a940";
        internal const string ShapeKeyRowUxmlGuid = "a95cf17e30b24bd3a7c81f4e6209d5c8";

        internal static StyleSheet LoadTheme() => Load<StyleSheet>(ThemeUssGuid);

        /// <summary>DenEmo 固有クラス定義。テーマの後に styleSheets へ追加すること。</summary>
        internal static StyleSheet LoadStyles() => Load<StyleSheet>(StylesUssGuid);

        /// <summary>
        /// ルート要素に dennoko-root と保険背景色を設定し、テーマ + DenEmo 固有 USS を適用する。
        /// </summary>
        internal static void SetupRoot(VisualElement root)
        {
            root.AddToClassList("dennoko-root");
            // USS ロード失敗時の保険として Surface0 を C# 側でも設定
            root.style.backgroundColor = (Color)new Color32(0x12, 0x12, 0x12, 0xFF);
            var theme = LoadTheme();
            if (theme != null) root.styleSheets.Add(theme);
            var styles = LoadStyles();
            if (styles != null) root.styleSheets.Add(styles);
        }

        internal static VisualTreeAsset LoadVisualTree(string guid) => Load<VisualTreeAsset>(guid);

        private static T Load<T>(string guid) where T : Object
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
