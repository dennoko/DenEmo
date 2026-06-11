using UnityEditor;
using UnityEngine;

namespace DenEmo.UI
{
    /// <summary>タイムラインを別ウィンドウとして表示する（描画は DenEmoWindow に委譲）。</summary>
    public class DenEmoTimelineWindow : EditorWindow
    {
        private Vector2 _scrollPos;

        public static void ShowWindow()
        {
            var w = GetWindow<DenEmoTimelineWindow>("Timeline");
            w.minSize = new Vector2(760, 200);
            w.Show();
        }

        private void OnGUI()
        {
            DenEmoTheme.Initialize();
            DenEmoTheme.PushEditorTheme();
            try
            {
                EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), DenEmoTheme.Surface0);

                if (DenEmoWindow.Instance == null)
                {
                    GUILayout.Space(8);
                    GUILayout.Label(
                        DenEmoLoc.EnglishMode ? "DenEmo Window is not open." : "DenEmo ウィンドウが開かれていません。",
                        DenEmoTheme.SecondaryTextStyle);
                    if (GUILayout.Button(
                        DenEmoLoc.EnglishMode ? "Open DenEmo" : "DenEmo を開く",
                        DenEmoTheme.SecondaryButtonStyle))
                    {
                        DenEmoWindow.ShowWindow();
                    }
                    return;
                }

                _scrollPos = GUILayout.BeginScrollView(_scrollPos);
                DenEmoWindow.Instance.DrawTimelineForSeparateWindow(this);
                GUILayout.EndScrollView();
            }
            finally
            {
                DenEmoTheme.PopEditorTheme();
            }
        }

        private void OnEnable()
        {
            EditorApplication.update += Repaint;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Repaint;
        }
    }
}
