using UnityEditor;
using UnityEngine;

namespace DenEmo.UI
{
    public class DenEmoTimelineWindow : EditorWindow
    {
        public static void ShowWindow()
        {
            var w = GetWindow<DenEmoTimelineWindow>("Timeline");
            w.minSize = new Vector2(380, 200);
            w.Show();
        }

        private void OnGUI()
        {
            if (DenEmoWindow.Instance == null)
            {
                GUILayout.Label("DenEmo Window is not open.");
                if (GUILayout.Button("Open DenEmo"))
                {
                    DenEmoWindow.ShowWindow();
                }
                return;
            }

            DenEmoWindow.Instance.DrawTimelineForSeparateWindow(this);
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
