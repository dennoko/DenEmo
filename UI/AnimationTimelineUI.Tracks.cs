using UnityEditor;
using UnityEngine;
using DenEmo.Models;
using DenEmo.Core;

namespace DenEmo.UI
{
    public partial class AnimationTimelineUI
    {
        // ─── Keyframe tracks ──────────────────────────────────────────────────

        private void DrawKeyframeTracks(
            AnimationClipModel clipModel, AnimationPreviewController preview,
            ShapeKeyModel shapeModel, string smrPath, InterpolationType currentInterp, EditorWindow window)
        {
            var shapes = clipModel.GetShapeNamesWithKeys(smrPath);
            if (shapes.Count == 0)
            {
                GUILayout.Label(
                    DenEmoLoc.EnglishMode
                        ? "No keyframes yet. Use 🔴 Auto-Key or the ◆ button to add keys."
                        : "キーフレームがまだありません。🔴 Auto-Key または ◆ ボタンでキーを追加してください。",
                    DenEmoTheme.CaptionStyle);
                return;
            }

            float rowsHeight = Mathf.Min(shapes.Count * (TRACK_ROW_HEIGHT + 1), MAX_TRACKS_HEIGHT);
            _tracksScroll = EditorGUILayout.BeginScrollView(_tracksScroll, GUILayout.Height(rowsHeight));

            foreach (string shapeName in shapes)
                DrawTrackRow(shapeName, clipModel, preview, shapeModel, smrPath, currentInterp, window);

            EditorGUILayout.EndScrollView();
        }

        private void DrawTrackRow(
            string shapeName, AnimationClipModel clipModel, AnimationPreviewController preview,
            ShapeKeyModel shapeModel, string smrPath, InterpolationType currentInterp, EditorWindow window)
        {
            Rect rowRect = GUILayoutUtility.GetRect(0, TRACK_ROW_HEIGHT, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rowRect, DenEmoTheme.Surface0);
                float cy = rowRect.y + rowRect.height * 0.5f;
                EditorGUI.DrawRect(new Rect(rowRect.x + TRACK_LABEL_WIDTH, cy, rowRect.width - TRACK_LABEL_WIDTH, 1), DenEmoTheme.Outline);
            }

            float trackW = rowRect.width - TRACK_LABEL_WIDTH;
            float trackX = rowRect.x + TRACK_LABEL_WIDTH;

            GUI.Label(
                new Rect(rowRect.x + 8, rowRect.y + 4, TRACK_LABEL_WIDTH - 44, rowRect.height - 8),
                shapeName, DenEmoTheme.CaptionStyle);

            if (GUI.Button(new Rect(rowRect.x + TRACK_LABEL_WIDTH - 40, rowRect.y + 4, 16, 16), "◆", DenEmoTheme.MiniButtonStyle))
            {
                var smr = shapeModel?.TargetSkinnedMesh;
                if (smr != null && smr.sharedMesh != null)
                {
                    int index = smr.sharedMesh.GetBlendShapeIndex(shapeName);
                    if (index >= 0)
                    {
                        float val = smr.GetBlendShapeWeight(index);
                        preview.RecordKeyframe(shapeName, smrPath, clipModel.CurrentTime, val, currentInterp);
                        preview.SampleAt(clipModel.CurrentTime);
                        window.Repaint();
                    }
                }
            }

            if (GUI.Button(
                new Rect(rowRect.x + TRACK_LABEL_WIDTH - 22, rowRect.y + 4, 16, 16),
                new GUIContent("✕", DenEmoLoc.EnglishMode ? "Delete this track" : "このトラックを削除"),
                DenEmoTheme.MiniButtonStyle))
            {
                if (EditorUtility.DisplayDialog(
                    DenEmoLoc.EnglishMode ? "Delete Track" : "トラックの削除",
                    DenEmoLoc.EnglishMode ? $"Delete all keyframes for '{shapeName}'?" : $"'{shapeName}'のすべてのキーフレームを削除しますか？",
                    DenEmoLoc.EnglishMode ? "Yes" : "はい",
                    DenEmoLoc.EnglishMode ? "No" : "いいえ"))
                {
                    preview.DeleteAllKeyframesForShape(shapeName, smrPath);
                    window.Repaint();
                }
            }

            if (Event.current.type == EventType.Repaint)
            {
                float norm = clipModel.ClipLength > 0f ? clipModel.CurrentTime / clipModel.ClipLength : 0f;
                EditorGUI.DrawRect(
                    new Rect(trackX + norm * trackW - 1, rowRect.y, 2, rowRect.height),
                    new Color(1f, 1f, 1f, 0.4f));
            }

            float[] keyTimes = clipModel.GetKeyTimesForShape(shapeName, smrPath);
            EnsureKfLabelStyle();
            foreach (float kTime in keyTimes)
            {
                float norm = clipModel.ClipLength > 0f ? kTime / clipModel.ClipLength : 0f;
                float kx = trackX + norm * trackW;
                float ky = rowRect.y + rowRect.height * 0.5f;
                Rect hitR = new Rect(kx - DIAMOND_SIZE - 2, ky - DIAMOND_SIZE - 2, (DIAMOND_SIZE + 2) * 2, (DIAMOND_SIZE + 2) * 2);

                bool isCurrent = Mathf.Abs(kTime - clipModel.CurrentTime) <= (clipModel.FPS > 0f ? 0.5f / clipModel.FPS : 0.01f);

                if (Event.current.type == EventType.Repaint)
                {
                    var style = new GUIStyle(_kfLabelStyle)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = isCurrent ? Color.white : DenEmoTheme.SemanticInfo },
                        fontSize = 10
                    };
                    GUI.Label(new Rect(kx - 8, ky - 8, 16, 16), "◆", style);
                }

                EditorGUIUtility.AddCursorRect(hitR, MouseCursor.SlideArrow);
                HandleKeyframeDrag(hitR, kTime, shapeName, clipModel, preview, smrPath, window, trackX, trackW, true);

                if (Event.current.type == EventType.ContextClick && hitR.Contains(Event.current.mousePosition))
                {
                    string sn = shapeName;
                    float kt = kTime;
                    var menu = new GenericMenu();
                    menu.AddItem(
                        new GUIContent(DenEmoLoc.EnglishMode ? "Delete" : "削除"),
                        false,
                        () => { preview.DeleteKeyframe(sn, smrPath, kt); window.Repaint(); });
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("Step"), false, () => { preview.ChangeInterpolation(sn, smrPath, kt, InterpolationType.Step); preview.SampleAt(clipModel.CurrentTime); window.Repaint(); });
                    menu.AddItem(new GUIContent("Linear"), false, () => { preview.ChangeInterpolation(sn, smrPath, kt, InterpolationType.Linear); preview.SampleAt(clipModel.CurrentTime); window.Repaint(); });
                    menu.AddItem(new GUIContent("Ease"), false, () => { preview.ChangeInterpolation(sn, smrPath, kt, InterpolationType.Ease); preview.SampleAt(clipModel.CurrentTime); window.Repaint(); });
                    menu.ShowAsContext();
                    Event.current.Use();
                }
            }

            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.yMax - 1, rowRect.width, 1), DenEmoTheme.Surface2);
        }
    }
}
