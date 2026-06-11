using UnityEditor;
using UnityEngine;

namespace DenEmo.UI
{
    internal class VertexPreviewOptionsPopup : PopupWindowContent
    {
        private readonly DenEmoWindow _window;

        internal VertexPreviewOptionsPopup(DenEmoWindow window)
        {
            _window = window;
        }

        public override Vector2 GetWindowSize() => new Vector2(270, 116);

        public override void OnGUI(Rect rect)
        {
            DenEmoTheme.Initialize();
            DenEmoTheme.PushEditorTheme();
            try
            {
                EditorGUI.DrawRect(rect, DenEmoTheme.Surface1);

                GUILayout.Space(6);
                GUILayout.Label(
                    DenEmoLoc.EnglishMode ? "Vertex Preview Settings" : "頂点プレビュー設定",
                    DenEmoTheme.GroupLabelStyle);
                GUILayout.Space(2);

                EditorGUI.BeginChangeCheck();

                var newColor = EditorGUILayout.ColorField(
                    new GUIContent(DenEmoLoc.EnglishMode ? "Normal Color" : "通常の色"),
                    DenEmoWindow.VertexPreviewColor, true, false, false);

                var newSelColor = EditorGUILayout.ColorField(
                    new GUIContent(DenEmoLoc.EnglishMode ? "Selected Color" : "選択中の色"),
                    DenEmoWindow.VertexPreviewSelectedColor, true, false, false);

                var newSize = EditorGUILayout.Slider(
                    DenEmoLoc.EnglishMode ? "Size" : "サイズ",
                    DenEmoWindow.VertexPreviewSizeMultiplier, 0.1f, 5f);

                if (EditorGUI.EndChangeCheck())
                {
                    DenEmoWindow.VertexPreviewColor          = newColor;
                    DenEmoWindow.VertexPreviewSelectedColor  = newSelColor;
                    DenEmoWindow.VertexPreviewSizeMultiplier = newSize;
                    SavePrefs();
                    SceneView.RepaintAll();
                    _window?.Repaint();
                }
            }
            finally
            {
                DenEmoTheme.PopEditorTheme();
            }
        }

        private static void SavePrefs()
        {
            DenEmoProjectPrefs.SetString("DenEmo_VertexPreviewColor",
                DenEmoWindow.ColorToPrefsString(DenEmoWindow.VertexPreviewColor));
            DenEmoProjectPrefs.SetString("DenEmo_VertexPreviewSelectedColor",
                DenEmoWindow.ColorToPrefsString(DenEmoWindow.VertexPreviewSelectedColor));
            DenEmoProjectPrefs.SetString("DenEmo_VertexPreviewSize",
                DenEmoWindow.VertexPreviewSizeMultiplier.ToString("F4", System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}
