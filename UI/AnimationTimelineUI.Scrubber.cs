using UnityEditor;
using UnityEngine;
using DenEmo.Models;
using DenEmo.Core;

namespace DenEmo.UI
{
    public partial class AnimationTimelineUI
    {
        // ─── Ruler & Scrubber ─────────────────────────────────────────────────

        private void DrawRulerAndScrubber(
            AnimationClipModel clipModel, AnimationPreviewController preview, EditorWindow window)
        {
            Rect rulerRect   = GUILayoutUtility.GetRect(0, RULER_HEIGHT, GUILayout.ExpandWidth(true));
            Rect scrubRect   = GUILayoutUtility.GetRect(0, SCRUBBER_HEIGHT, GUILayout.ExpandWidth(true));

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rulerRect, DenEmoTheme.Surface2);
                EditorGUI.DrawRect(scrubRect, DenEmoTheme.Surface0);

                float lw = TRACK_LABEL_WIDTH;
                EditorGUI.DrawRect(new Rect(rulerRect.x + lw, rulerRect.y, 1, rulerRect.height + scrubRect.height), DenEmoTheme.Outline);

                DrawRulerTicks(rulerRect, clipModel);
                DrawScrubberLine(scrubRect, clipModel);
            }

            HandleScrubberInput(scrubRect, clipModel, preview, window);
        }

        private void DrawRulerTicks(Rect rect, AnimationClipModel clipModel)
        {
            float trackW = rect.width - TRACK_LABEL_WIDTH;
            float trackX = rect.x + TRACK_LABEL_WIDTH;
            int total = clipModel.TotalFrames;
            if (total <= 0 || trackW <= 0) return;

            int step = CalcRulerStep(total, trackW);

            EnsureKfLabelStyle();
            var style = new GUIStyle(_kfLabelStyle) { alignment = TextAnchor.UpperLeft };

            for (int f = 0; f <= total; f += step)
            {
                float x = trackX + ((float)f / total) * trackW;
                EditorGUI.DrawRect(new Rect(x, rect.yMax - 6, 1, 6), DenEmoTheme.Outline);
                if (x + 24 < trackX + trackW)
                    GUI.Label(new Rect(x + 2, rect.y + 2, 28, 14), f.ToString(), style);
            }
        }

        private void DrawScrubberLine(Rect rect, AnimationClipModel clipModel)
        {
            float trackW = rect.width - TRACK_LABEL_WIDTH;
            float trackX = rect.x + TRACK_LABEL_WIDTH;
            float norm = clipModel.ClipLength > 0f ? clipModel.CurrentTime / clipModel.ClipLength : 0f;
            norm = Mathf.Clamp01(norm);
            float sx = trackX + norm * trackW;

            EditorGUI.DrawRect(new Rect(sx - 1, rect.y, 2, rect.height), new Color(1f, 1f, 1f, 0.8f));
            EditorGUI.DrawRect(new Rect(sx - 5, rect.y, 10, rect.height), DenEmoTheme.TextPrimary);
        }

        private void HandleScrubberInput(
            Rect scrubRect, AnimationClipModel clipModel, AnimationPreviewController preview, EditorWindow window)
        {
            float trackW = scrubRect.width - TRACK_LABEL_WIDTH;
            float trackX = scrubRect.x + TRACK_LABEL_WIDTH;
            Rect trackR = new Rect(trackX, scrubRect.y, trackW, scrubRect.height + RULER_HEIGHT);

            Event e = Event.current;

            if (e.type == EventType.MouseDown && trackR.Contains(e.mousePosition))
            {
                GUI.FocusControl(null);
                _isDraggingScrubber = true;
                SeekFromMouseX(e.mousePosition.x, trackX, trackW, clipModel, preview);
                window.Repaint();
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && _isDraggingScrubber)
            {
                SeekFromMouseX(e.mousePosition.x, trackX, trackW, clipModel, preview);
                window.Repaint();
                e.Use();
            }
            else if (e.type == EventType.MouseUp && _isDraggingScrubber)
            {
                _isDraggingScrubber = false;
                e.Use();
            }
        }

        private static void SeekFromMouseX(
            float mouseX, float trackX, float trackW,
            AnimationClipModel clipModel, AnimationPreviewController preview)
        {
            float t = Mathf.Clamp01((mouseX - trackX) / trackW) * clipModel.ClipLength;
            t = Mathf.Round(t * clipModel.FPS) / clipModel.FPS;
            clipModel.CurrentTime = Mathf.Clamp(t, 0f, clipModel.ClipLength);
            preview.SampleAt(clipModel.CurrentTime);
        }
    }
}
