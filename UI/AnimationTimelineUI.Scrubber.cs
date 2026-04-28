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

        private void DrawKeyframeDeleteButtons(
            AnimationClipModel clipModel, AnimationPreviewController preview, string smrPath, EditorWindow window)
        {
            float[] allKeys = clipModel.GetAllKeyTimes(smrPath);
            if (allKeys == null || allKeys.Length == 0) return;

            Rect rowRect = GUILayoutUtility.GetRect(0, 32, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rowRect, DenEmoTheme.Surface0);
                float cy = rowRect.y;
                EditorGUI.DrawRect(new Rect(rowRect.x + TRACK_LABEL_WIDTH, cy, rowRect.width - TRACK_LABEL_WIDTH, 1), DenEmoTheme.Outline);
            }

            float trackW = rowRect.width - TRACK_LABEL_WIDTH;
            float trackX = rowRect.x + TRACK_LABEL_WIDTH;

            EnsureKfLabelStyle();
            
            foreach (float kTime in allKeys)
            {
                float norm = clipModel.ClipLength > 0f ? kTime / clipModel.ClipLength : 0f;
                float kx = trackX + norm * trackW;
                
                Rect dragRect = new Rect(kx - 8, rowRect.y + 2, 16, 12);
                EditorGUIUtility.AddCursorRect(dragRect, MouseCursor.SlideArrow);
                
                var style = new GUIStyle(DenEmoTheme.CaptionStyle)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 10,
                    normal = { textColor = DenEmoTheme.TextSecondary }
                };
                GUI.Label(dragRect, "＝", style);

                HandleKeyframeDrag(dragRect, kTime, null, clipModel, preview, smrPath, window, trackX, trackW);

                Rect btnRect = new Rect(kx - 8, rowRect.y + 14, 16, 16);
                
                if (GUI.Button(btnRect, new GUIContent("✕", DenEmoLoc.EnglishMode ? "Delete all keys at this frame" : "このフレームの全キーを削除"), DenEmoTheme.MiniButtonStyle))
                {
                    if (EditorUtility.DisplayDialog(
                        DenEmoLoc.EnglishMode ? "Delete Frame Keys" : "フレームキーの削除",
                        DenEmoLoc.EnglishMode ? $"Delete all keyframes at {kTime:F2}s?" : $"{kTime:F2}秒のすべてのキーフレームを削除しますか？",
                        DenEmoLoc.EnglishMode ? "Yes" : "はい",
                        DenEmoLoc.EnglishMode ? "No" : "いいえ"))
                    {
                        preview.DeleteAllKeyframesAtTime(smrPath, kTime);
                        preview.SampleAt(clipModel.CurrentTime);
                        window.Repaint();
                    }
                }
            }
        }

        private void HandleKeyframeDrag(
            Rect hitRect, float kTime, string shapeName, AnimationClipModel clipModel, 
            AnimationPreviewController preview, string smrPath, EditorWindow window, 
            float trackX, float trackW, bool seekOnDown = false)
        {
            Event e = Event.current;
            int frame = Mathf.RoundToInt(kTime * clipModel.FPS);

            if (e.type == EventType.MouseDown && hitRect.Contains(e.mousePosition) && e.button == 0)
            {
                _isDraggingKeyframe = true;
                _draggingOldFrame = frame;
                _draggingShapeName = shapeName;
                GUI.FocusControl(null);
                if (seekOnDown)
                {
                    clipModel.CurrentTime = kTime;
                    preview.SampleAt(kTime);
                    window.Repaint();
                }
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && _isDraggingKeyframe && _draggingOldFrame == frame && _draggingShapeName == shapeName)
            {
                float mouseX = e.mousePosition.x;
                float norm = Mathf.Clamp01((mouseX - trackX) / trackW);
                int targetFrame = Mathf.RoundToInt(norm * clipModel.ClipLength * clipModel.FPS);

                if (targetFrame != _draggingOldFrame)
                {
                    bool moved = false;
                    if (shapeName == null)
                    {
                        moved = preview.MoveAllTracksKeyframes(smrPath, _draggingOldFrame, targetFrame, clipModel.TotalFrames);
                    }
                    else
                    {
                        moved = preview.MoveSingleTrackKeyframes(shapeName, smrPath, _draggingOldFrame, targetFrame, clipModel.TotalFrames);
                    }

                    if (moved)
                    {
                        _draggingOldFrame = targetFrame;
                        clipModel.CurrentTime = (float)targetFrame / clipModel.FPS;
                        preview.SampleAt(clipModel.CurrentTime);
                        window.Repaint();
                    }
                }
                e.Use();
            }
            else if (e.type == EventType.MouseUp && _isDraggingKeyframe && e.button == 0)
            {
                if (_draggingOldFrame == frame && _draggingShapeName == shapeName)
                {
                    _isDraggingKeyframe = false;
                    e.Use();
                }
            }
        }
    }
}
