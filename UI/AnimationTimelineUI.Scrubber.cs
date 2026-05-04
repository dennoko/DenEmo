using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using DenEmo.Models;
using DenEmo.Core;

namespace DenEmo.UI
{
    public partial class AnimationTimelineUI
    {
        private const float SCROLLBAR_HEIGHT = 10f;

        // ─── Ruler & Scrubber ─────────────────────────────────────────────────

        private bool _showScrollbar;  // cached per-frame to keep Layout/Repaint GetRect calls consistent

        private void DrawRulerAndScrubber(
            AnimationClipModel clipModel, AnimationPreviewController preview, EditorWindow window)
        {
            Rect rulerRect = GUILayoutUtility.GetRect(0, RULER_HEIGHT,   GUILayout.ExpandWidth(true));
            Rect scrubRect = GUILayoutUtility.GetRect(0, SCRUBBER_HEIGHT, GUILayout.ExpandWidth(true));

            float trackW    = rulerRect.width - _trackLabelWidth - RIGHT_PADDING;
            float trackX    = rulerRect.x + _trackLabelWidth;
            Rect  trackArea = new Rect(trackX, rulerRect.y, trackW, rulerRect.height + scrubRect.height);

            // Capture before HandleTimelineZoom so Layout/Repaint always call GetRect the same times
            if (Event.current.type == EventType.Layout)
                _showScrollbar = ViewRange < 1f - 0.001f;

            HandleTimelineZoom(trackArea, clipModel.ClipLength, trackX, trackW);

            if (_showScrollbar)
                DrawTimelineScrollbar(clipModel.ClipLength, trackX, trackW, window);

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rulerRect, DenEmoTheme.Surface2);
                EditorGUI.DrawRect(scrubRect, DenEmoTheme.Surface0);

                float lw = _trackLabelWidth;
                EditorGUI.DrawRect(new Rect(rulerRect.x + lw, rulerRect.y, 1, rulerRect.height + scrubRect.height), DenEmoTheme.Outline);

                DrawRulerTicks(rulerRect, clipModel);
                DrawScrubberLine(scrubRect, clipModel);
            }

            HandleScrubberInput(scrubRect, clipModel, preview, window);
        }

        private void HandleTimelineZoom(Rect trackRect, float clipLen, float trackX, float trackW)
        {
            Event e = Event.current;
            if (e.type != EventType.ScrollWheel) return;
            if (!trackRect.Contains(e.mousePosition)) return;

            float mouseNorm = _viewStart + ((e.mousePosition.x - trackX) / trackW) * ViewRange;
            mouseNorm = Mathf.Clamp01(mouseNorm);

            float zoomFactor = e.delta.y > 0f ? 1.15f : (1f / 1.15f);
            float newRange   = Mathf.Clamp(ViewRange * zoomFactor, 1f / 50f, 1f);

            _viewStart = mouseNorm - (mouseNorm - _viewStart) * (newRange / ViewRange);
            _viewEnd   = _viewStart + newRange;

            if (_viewStart < 0f) { _viewEnd -= _viewStart; _viewStart = 0f; }
            if (_viewEnd   > 1f) { _viewStart -= (_viewEnd - 1f); _viewEnd = 1f; }
            _viewStart = Mathf.Clamp01(_viewStart);
            _viewEnd   = Mathf.Clamp01(_viewEnd);

            e.Use();
        }

        private void DrawTimelineScrollbar(float clipLen, float trackX, float trackW, EditorWindow window)
        {
            Rect sbRect      = GUILayoutUtility.GetRect(0, SCROLLBAR_HEIGHT, GUILayout.ExpandWidth(true));
            Rect trackSbRect = new Rect(trackX, sbRect.y, trackW, SCROLLBAR_HEIGHT);

            float newStart = GUI.HorizontalScrollbar(trackSbRect, _viewStart, ViewRange, 0f, 1f);
            if (!Mathf.Approximately(newStart, _viewStart))
            {
                float range = ViewRange;
                _viewStart  = Mathf.Clamp01(newStart);
                _viewEnd    = Mathf.Clamp01(_viewStart + range);
                window.Repaint();
            }
        }

        private void DrawRulerTicks(Rect rect, AnimationClipModel clipModel)
        {
            float trackW = rect.width - _trackLabelWidth - RIGHT_PADDING;
            float trackX = rect.x + _trackLabelWidth;
            int total = clipModel.TotalFrames;
            if (total <= 0 || trackW <= 0) return;

            int step = CalcRulerStep(total, trackW);

            EnsureKfLabelStyle();
            var style = new GUIStyle(_kfLabelStyle) { alignment = TextAnchor.UpperLeft };

            int frameStart = Mathf.FloorToInt(_viewStart * total);
            int frameEnd   = Mathf.CeilToInt(_viewEnd   * total);
            for (int f = frameStart; f <= frameEnd; f += step)
            {
                float x = TimeToPixel((float)f / clipModel.FPS, clipModel.ClipLength, trackX, trackW);
                if (x < trackX || x > trackX + trackW) continue;
                EditorGUI.DrawRect(new Rect(x, rect.yMax - 6, 1, 6), DenEmoTheme.Outline);
                if (x + 24 < trackX + trackW)
                    GUI.Label(new Rect(x + 2, rect.y + 2, 28, 14), f.ToString(), style);
            }
        }

        private void DrawScrubberLine(Rect rect, AnimationClipModel clipModel)
        {
            float trackW = rect.width - _trackLabelWidth - RIGHT_PADDING;
            float trackX = rect.x + _trackLabelWidth;
            float sx = TimeToPixel(clipModel.CurrentTime, clipModel.ClipLength, trackX, trackW);

            EditorGUI.DrawRect(new Rect(sx - 1, rect.y, 2, rect.height), new Color(1f, 1f, 1f, 0.8f));
            EditorGUI.DrawRect(new Rect(sx - 5, rect.y, 10, rect.height), DenEmoTheme.TextPrimary);

            if (_isDraggingKeyframe)
            {
                EnsureHoverLabelStyle();
                string frameText = clipModel.CurrentFrame.ToString();
                Vector2 fSize = _hoverLabelStyle.CalcSize(new GUIContent(frameText));
                float labelX  = sx - fSize.x * 0.5f;
                float labelY  = rect.y - fSize.y - 2;

                Rect bgRect = new Rect(labelX - 2, labelY - 1, fSize.x + 4, fSize.y + 2);
                EditorGUI.DrawRect(bgRect, new Color(0.2f, 0.5f, 1f, 0.9f));
                GUI.Label(new Rect(labelX, labelY, fSize.x, fSize.y), frameText, _hoverLabelStyle);
            }
        }

        private void HandleScrubberInput(
            Rect scrubRect, AnimationClipModel clipModel, AnimationPreviewController preview, EditorWindow window)
        {
            float trackW = scrubRect.width - _trackLabelWidth - RIGHT_PADDING;
            float trackX = scrubRect.x + _trackLabelWidth;
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

        private void SeekFromMouseX(
            float mouseX, float trackX, float trackW,
            AnimationClipModel clipModel, AnimationPreviewController preview)
        {
            float t = PixelToTime(mouseX, clipModel.ClipLength, trackX, trackW);
            t = Mathf.Round(t * clipModel.FPS) / clipModel.FPS;
            clipModel.CurrentTime = Mathf.Clamp(t, 0f, clipModel.ClipLength);
            preview.SampleAt(clipModel.CurrentTime);
        }

        private void DrawKeyframeDeleteButtons(
            AnimationClipModel clipModel, AnimationPreviewController preview, string smrPath,
            ShapeKeyModel shapeModel, InterpolationType currentInterp, EditorWindow window)
        {
            float[] allKeys = clipModel.GetAllKeyTimes(smrPath);

            Rect rowRect = GUILayoutUtility.GetRect(0, 36, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rowRect, DenEmoTheme.Surface0);
                float cy = rowRect.y;
                EditorGUI.DrawRect(new Rect(rowRect.x + _trackLabelWidth, cy, rowRect.width - _trackLabelWidth, 1), DenEmoTheme.Outline);
            }

            // ── ◆+ Insert-all button ──────────────────────────────────────────
            Rect insertRect = new Rect(rowRect.x + 4, rowRect.y + 8, _trackLabelWidth - 12, 20);
            string insertTip = DenEmoLoc.EnglishMode
                ? "Insert keyframe at current time for all visible shapes"
                : "現在フレームに表示中の全シェイプのキーを追加";
            if (GUI.Button(insertRect, new GUIContent("◆+", insertTip), DenEmoTheme.MiniButtonStyle))
            {
                var visibleItems = shapeModel.Items
                    .Where(i => i.IsVisible && !i.IsVrcShape && !i.IsLipSyncShape)
                    .ToList();
                preview.RecordAllKeyframes(visibleItems, smrPath, clipModel.CurrentTime, currentInterp);
                preview.SampleAt(clipModel.CurrentTime);
                window.Repaint();
            }

            if (allKeys == null || allKeys.Length == 0) return;

            float trackW = rowRect.width - _trackLabelWidth - RIGHT_PADDING;
            float trackX = rowRect.x + _trackLabelWidth;

            EnsureKfLabelStyle();

            foreach (float kTime in allKeys)
            {
                float kx = TimeToPixel(kTime, clipModel.ClipLength, trackX, trackW);
                
                Rect dragRect = new Rect(kx - 10, rowRect.y + 2, 20, 12);
                EditorGUIUtility.AddCursorRect(dragRect, MouseCursor.SlideArrow);
                
                var style = new GUIStyle(DenEmoTheme.CaptionStyle)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 10,
                    normal = { textColor = DenEmoTheme.TextSecondary }
                };
                GUI.Label(dragRect, "＝", style);

                HandleKeyframeDrag(dragRect, kTime, null, clipModel, preview, smrPath, window, trackX, trackW);

                Rect btnRect = new Rect(kx - 10, rowRect.y + 16, 20, 20);
                
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
                float t = PixelToTime(mouseX, clipModel.ClipLength, trackX, trackW);
                int targetFrame = Mathf.RoundToInt(Mathf.Clamp(t * clipModel.FPS, 0f, clipModel.TotalFrames));

                if (targetFrame != _draggingOldFrame)
                {
                    int reachedFrame = _draggingOldFrame;
                    if (shapeName == null)
                    {
                        reachedFrame = preview.MoveAllTracksKeyframes(smrPath, _draggingOldFrame, targetFrame, clipModel.TotalFrames);
                    }
                    else
                    {
                        reachedFrame = preview.MoveSingleTrackKeyframes(shapeName, smrPath, _draggingOldFrame, targetFrame, clipModel.TotalFrames);
                    }

                    if (reachedFrame != _draggingOldFrame)
                    {
                        _draggingOldFrame = reachedFrame;
                        clipModel.CurrentTime = (float)reachedFrame / clipModel.FPS;
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
