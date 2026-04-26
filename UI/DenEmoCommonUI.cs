using UnityEditor;
using UnityEngine;
using DenEmo.Models;

namespace DenEmo.UI
{
    public static class DenEmoCommonUI
    {
        public static void DrawHeader(EditorWindow window)
        {
            DenEmoTheme.Initialize();

            EditorGUI.DrawRect(new Rect(0, 0, window.position.width, 40), DenEmoTheme.Surface0);

            EditorGUILayout.BeginHorizontal(GUILayout.Height(36));
            GUILayout.Space(10);
            GUILayout.Label("DenEmo", DenEmoTheme.TitleStyle, GUILayout.Height(36));
            GUILayout.FlexibleSpace();

            var langLabel = DenEmoLoc.EnglishMode ? "JA" : "EN";
            if (GUILayout.Button(langLabel, DenEmoTheme.MiniButtonStyle, GUILayout.Width(28), GUILayout.Height(20)))
            {
                DenEmoLoc.EnglishMode = !DenEmoLoc.EnglishMode;
                window.Repaint();
            }
            GUILayout.Space(8);
            EditorGUILayout.EndHorizontal();

            DenEmoTheme.DrawSeparator(0);
        }

        public static void DrawStatusBar(string statusMessage, int statusLevel)
        {
            DenEmoTheme.Initialize();

            string icon = statusLevel switch
            {
                1 => "✓ ",
                2 => "⚠ ",
                3 => "✕ ",
                _ => "",
            };
            string text = string.IsNullOrEmpty(statusMessage)
                ? DenEmoLoc.T("status.ready")
                : statusMessage;

            GUILayout.Box(icon + text, DenEmoTheme.GetStatusStyle(statusLevel), GUILayout.ExpandWidth(true), GUILayout.Height(24));
        }
    }
}
