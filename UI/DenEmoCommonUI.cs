using UnityEditor;
using UnityEngine;
using DenEmo.Models;

namespace DenEmo.UI
{
    public static class DenEmoCommonUI
    {
        public static void DrawHeader(EditorWindow window)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("DenEmo", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            var newLang = EditorGUILayout.ToggleLeft(DenEmoLoc.T("ui.lang.englishMode"), DenEmoLoc.EnglishMode, GUILayout.Width(140));
            if (newLang != DenEmoLoc.EnglishMode)
            {
                DenEmoLoc.EnglishMode = newLang;
                window.Repaint();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }

        public static void DrawStatusBar(string statusMessage, int statusLevel)
        {
            GUILayout.FlexibleSpace();
            var rect = GUILayoutUtility.GetRect(1, 22, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                var bg = EditorGUIUtility.isProSkin ? new Color(0.15f, 0.15f, 0.15f) : new Color(0.90f, 0.90f, 0.90f);
                if (statusLevel == 1) // Success
                    bg = new Color(0.20f, 0.45f, 0.20f, 1f);
                else if (statusLevel == 2) // Warning
                    bg = new Color(0.55f, 0.45f, 0.15f, 1f);
                else if (statusLevel == 3) // Error
                    bg = new Color(0.55f, 0.20f, 0.20f, 1f);
                EditorGUI.DrawRect(rect, bg);
            }
            var style = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white },
                padding = new RectOffset(8, 8, 0, 0)
            };
            string text = string.IsNullOrEmpty(statusMessage) ? DenEmoLoc.T("status.ready") : statusMessage;
            GUI.Label(rect, text, style);
        }
    }
}
