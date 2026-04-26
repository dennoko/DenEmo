using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using DenEmo.Models;
using DenEmo.Core;

namespace DenEmo.UI
{
    /// <summary>
    /// Draws the mini-timeline strip (ruler, scrubber, playback controls, keyframe tracks).
    /// All playback state is owned by the caller (AnimationModeUI) and passed via ref.
    /// </summary>
    public class AnimationTimelineUI
    {
        // ─── Layout constants ─────────────────────────────────────────────────
        private const float RULER_HEIGHT      = 24f;
        private const float SCRUBBER_HEIGHT   = 14f;
        private const float CONTROLS_HEIGHT   = 26f;
        private const float TRACK_ROW_HEIGHT  = 20f;
        private const float TRACK_LABEL_WIDTH = 130f;
        private const float DIAMOND_SIZE      = 5f;
        private const float MAX_TRACKS_HEIGHT = 110f;

        // ─── Internal state ───────────────────────────────────────────────────
        private bool    _isDraggingScrubber;
        private Vector2 _tracksScroll;
        private bool    _tracksCollapsed;

        // Cached styles (re-created when null after domain reload)
        private GUIStyle _kfLabelStyle;
        private GUIStyle _recOnStyle;
        private GUIStyle _recOffStyle;

        // ─── Main Draw entry ──────────────────────────────────────────────────

        public void Draw(
            AnimationClipModel         clipModel,
            AnimationPreviewController preview,
            ShapeKeyModel              shapeModel,
            string                     smrPath,
            bool                       isRecording,
            InterpolationType          currentInterp,
            EditorWindow               window,
            ref bool                   isPlaying,
            ref double                 playStartRealTime,
            ref float                  playStartClipTime)
        {
            if (clipModel?.Clip == null) return;
            DenEmoTheme.Initialize();

            GUILayout.BeginVertical(DenEmoTheme.CardStyle);

            // Section header + track collapse toggle
            GUILayout.BeginHorizontal();
            GUILayout.Label(
                DenEmoLoc.EnglishMode ? "TIMELINE" : "タイムライン",
                DenEmoTheme.SectionHeaderStyle);
            GUILayout.FlexibleSpace();
            string colLabel = _tracksCollapsed
                ? (DenEmoLoc.EnglishMode ? "▶ Tracks" : "▶ トラック")
                : (DenEmoLoc.EnglishMode ? "▼ Tracks" : "▼ トラック");
            if (GUILayout.Button(colLabel, DenEmoTheme.MiniButtonStyle))
                _tracksCollapsed = !_tracksCollapsed;
            GUILayout.EndHorizontal();

            DenEmoTheme.DrawSeparator(2);

            DrawRulerAndScrubber(clipModel, preview, window);
            GUILayout.Space(2);
            DrawPlaybackControls(
                clipModel, preview, isRecording,
                ref isPlaying, ref playStartRealTime, ref playStartClipTime, window);

            if (!_tracksCollapsed)
            {
                GUILayout.Space(4);
                DrawKeyframeTracks(clipModel, preview, smrPath, window);
            }

            GUILayout.EndVertical();
        }

        // ─── Ruler & Scrubber ─────────────────────────────────────────────────

        private void DrawRulerAndScrubber(
            AnimationClipModel clipModel, AnimationPreviewController preview, EditorWindow window)
        {
            Rect rulerRect   = GUILayoutUtility.GetRect(0, RULER_HEIGHT,   GUILayout.ExpandWidth(true));
            Rect scrubRect   = GUILayoutUtility.GetRect(0, SCRUBBER_HEIGHT, GUILayout.ExpandWidth(true));

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rulerRect,  DenEmoTheme.Surface2);
                EditorGUI.DrawRect(scrubRect,  DenEmoTheme.Surface1);

                // Label column separator
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
            int   total  = clipModel.TotalFrames;
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
            float norm   = clipModel.ClipLength > 0f ? clipModel.CurrentTime / clipModel.ClipLength : 0f;
            norm = Mathf.Clamp01(norm);
            float sx     = trackX + norm * trackW;

            // Vertical line
            EditorGUI.DrawRect(new Rect(sx - 1, rect.y, 2, rect.height + SCRUBBER_HEIGHT), new Color(1f, 1f, 1f, 0.8f));
            // Handle nub
            EditorGUI.DrawRect(new Rect(sx - 5, rect.y, 10, rect.height), DenEmoTheme.TextPrimary);
        }

        private void HandleScrubberInput(
            Rect scrubRect, AnimationClipModel clipModel, AnimationPreviewController preview, EditorWindow window)
        {
            float trackW = scrubRect.width - TRACK_LABEL_WIDTH;
            float trackX = scrubRect.x + TRACK_LABEL_WIDTH;
            Rect  trackR = new Rect(trackX, scrubRect.y, trackW, scrubRect.height + RULER_HEIGHT);

            Event e = Event.current;

            if (e.type == EventType.MouseDown && trackR.Contains(e.mousePosition))
            {
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

        // ─── Playback controls ────────────────────────────────────────────────

        private void DrawPlaybackControls(
            AnimationClipModel clipModel, AnimationPreviewController preview, bool isRecording,
            ref bool isPlaying, ref double playStartRealTime, ref float playStartClipTime,
            EditorWindow window)
        {
            EditorGUILayout.BeginHorizontal();

            // |< (start)
            if (GUILayout.Button("|<", DenEmoTheme.MiniButtonStyle, GUILayout.Width(24)))
            {
                isPlaying = false;
                clipModel.CurrentTime = 0f;
                preview.SampleAt(0f);
                window.Repaint();
            }
            // < (prev frame)
            if (GUILayout.Button("<", DenEmoTheme.MiniButtonStyle, GUILayout.Width(20)))
            {
                isPlaying = false;
                clipModel.CurrentTime = Mathf.Max(0f, clipModel.CurrentTime - 1f / clipModel.FPS);
                preview.SampleAt(clipModel.CurrentTime);
                window.Repaint();
            }
            // Play / Stop
            string playLabel = isPlaying ? "■" : "▶";
            if (GUILayout.Button(playLabel, DenEmoTheme.MiniButtonStyle, GUILayout.Width(28)))
            {
                isPlaying = !isPlaying;
                if (isPlaying)
                {
                    playStartRealTime = EditorApplication.timeSinceStartup;
                    playStartClipTime = clipModel.CurrentTime;
                    if (!preview.IsActive) preview.Start(clipModel, null);
                }
                window.Repaint();
            }
            // > (next frame)
            if (GUILayout.Button(">", DenEmoTheme.MiniButtonStyle, GUILayout.Width(20)))
            {
                isPlaying = false;
                clipModel.CurrentTime = Mathf.Min(clipModel.ClipLength, clipModel.CurrentTime + 1f / clipModel.FPS);
                preview.SampleAt(clipModel.CurrentTime);
                window.Repaint();
            }
            // >| (end)
            if (GUILayout.Button(">|", DenEmoTheme.MiniButtonStyle, GUILayout.Width(24)))
            {
                isPlaying = false;
                clipModel.CurrentTime = clipModel.ClipLength;
                preview.SampleAt(clipModel.ClipLength);
                window.Repaint();
            }

            GUILayout.Space(6);

            // Frame number field
            GUILayout.Label(
                DenEmoLoc.EnglishMode ? "f:" : "f:",
                DenEmoTheme.CaptionStyle, GUILayout.Width(14));
            int curFrame = clipModel.CurrentFrame;
            int newFrame = EditorGUILayout.IntField(curFrame, GUILayout.Width(44));
            if (newFrame != curFrame)
            {
                isPlaying = false;
                float t = Mathf.Clamp(newFrame / clipModel.FPS, 0f, clipModel.ClipLength);
                clipModel.CurrentTime = t;
                preview.SampleAt(t);
                window.Repaint();
            }

            GUILayout.FlexibleSpace();

            // REC indicator (toggle handled in AnimationModeUI)
            EnsureRecStyles();
            GUILayout.Label(
                isRecording ? "● REC" : "○ REC",
                isRecording ? _recOnStyle : _recOffStyle,
                GUILayout.ExpandWidth(false));

            EditorGUILayout.EndHorizontal();
        }

        // ─── Keyframe tracks ──────────────────────────────────────────────────

        private void DrawKeyframeTracks(
            AnimationClipModel clipModel, AnimationPreviewController preview,
            string smrPath, EditorWindow window)
        {
            var shapes = clipModel.GetShapeNamesWithKeys(smrPath);
            if (shapes.Count == 0)
            {
                GUILayout.Label(
                    DenEmoLoc.EnglishMode
                        ? "No keyframes yet. Use ● REC or the ◆ button to add keys."
                        : "キーフレームがまだありません。● REC または ◆ ボタンでキーを追加してください。",
                    DenEmoTheme.CaptionStyle);
                return;
            }

            float rowsHeight = Mathf.Min(shapes.Count * (TRACK_ROW_HEIGHT + 1), MAX_TRACKS_HEIGHT);
            _tracksScroll = EditorGUILayout.BeginScrollView(_tracksScroll, GUILayout.Height(rowsHeight));

            foreach (string shapeName in shapes)
                DrawTrackRow(shapeName, clipModel, preview, smrPath, window);

            EditorGUILayout.EndScrollView();
        }

        private void DrawTrackRow(
            string shapeName, AnimationClipModel clipModel, AnimationPreviewController preview,
            string smrPath, EditorWindow window)
        {
            Rect rowRect = GUILayoutUtility.GetRect(0, TRACK_ROW_HEIGHT, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rowRect, DenEmoTheme.Surface1);
                // Center line
                float cy = rowRect.y + rowRect.height * 0.5f;
                EditorGUI.DrawRect(new Rect(rowRect.x + TRACK_LABEL_WIDTH, cy, rowRect.width - TRACK_LABEL_WIDTH, 1), DenEmoTheme.Outline);
            }

            float trackW = rowRect.width - TRACK_LABEL_WIDTH;
            float trackX = rowRect.x + TRACK_LABEL_WIDTH;

            // Shape name label
            GUI.Label(
                new Rect(rowRect.x + 4, rowRect.y + 2, TRACK_LABEL_WIDTH - 8, rowRect.height - 4),
                shapeName, DenEmoTheme.CaptionStyle);

            // Current-time cursor line
            if (Event.current.type == EventType.Repaint)
            {
                float norm = clipModel.ClipLength > 0f ? clipModel.CurrentTime / clipModel.ClipLength : 0f;
                EditorGUI.DrawRect(
                    new Rect(trackX + norm * trackW - 1, rowRect.y, 2, rowRect.height),
                    new Color(1f, 1f, 1f, 0.4f));
            }

            // Keyframe diamonds
            float[] keyTimes = clipModel.GetKeyTimesForShape(shapeName, smrPath);
            EnsureKfLabelStyle();
            foreach (float kTime in keyTimes)
            {
                float norm = clipModel.ClipLength > 0f ? kTime / clipModel.ClipLength : 0f;
                float kx   = trackX + norm * trackW;
                float ky   = rowRect.y + rowRect.height * 0.5f;
                Rect  hitR = new Rect(kx - DIAMOND_SIZE - 2, ky - DIAMOND_SIZE - 2, (DIAMOND_SIZE + 2) * 2, (DIAMOND_SIZE + 2) * 2);

                bool isCurrent = Mathf.Abs(kTime - clipModel.CurrentTime) <= (clipModel.FPS > 0f ? 0.5f / clipModel.FPS : 0.01f);

                if (Event.current.type == EventType.Repaint)
                {
                    var style = new GUIStyle(_kfLabelStyle)
                    {
                        alignment          = TextAnchor.MiddleCenter,
                        normal             = { textColor = isCurrent ? Color.white : DenEmoTheme.SemanticInfo },
                        fontSize           = 10
                    };
                    GUI.Label(new Rect(kx - 8, ky - 8, 16, 16), "◆", style);
                }

                // Click → seek
                if (Event.current.type == EventType.MouseDown && hitR.Contains(Event.current.mousePosition))
                {
                    clipModel.CurrentTime = kTime;
                    preview.SampleAt(kTime);
                    window.Repaint();
                    Event.current.Use();
                }

                // Right-click context menu
                if (Event.current.type == EventType.ContextClick && hitR.Contains(Event.current.mousePosition))
                {
                    string sn = shapeName;
                    float  kt = kTime;
                    var menu = new GenericMenu();
                    menu.AddItem(
                        new GUIContent(DenEmoLoc.EnglishMode ? "Delete" : "削除"),
                        false,
                        () => { preview.DeleteKeyframe(sn, smrPath, kt); window.Repaint(); });
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("Step"),   false, () => { preview.ChangeInterpolation(sn, smrPath, kt, InterpolationType.Step);   });
                    menu.AddItem(new GUIContent("Linear"), false, () => { preview.ChangeInterpolation(sn, smrPath, kt, InterpolationType.Linear); });
                    menu.AddItem(new GUIContent("Ease"),   false, () => { preview.ChangeInterpolation(sn, smrPath, kt, InterpolationType.Ease);   });
                    menu.ShowAsContext();
                    Event.current.Use();
                }
            }

            // Row bottom border
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.yMax - 1, rowRect.width, 1), DenEmoTheme.Surface2);
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private static int CalcRulerStep(int totalFrames, float pixelWidth)
        {
            if (totalFrames <= 0 || pixelWidth <= 0) return 1;
            float pxPerFrame = pixelWidth / totalFrames;
            int   step       = Mathf.Max(1, Mathf.RoundToInt(40f / pxPerFrame));
            int[] nice       = { 1, 2, 5, 10, 15, 20, 30, 60, 120 };
            foreach (int n in nice) if (n >= step) return n;
            return step;
        }

        private void EnsureKfLabelStyle()
        {
            if (_kfLabelStyle == null || _kfLabelStyle.normal.background == null)
            {
                _kfLabelStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = DenEmoTheme.TextTertiary },
                    fontSize = 9
                };
            }
        }

        private void EnsureRecStyles()
        {
            if (_recOnStyle == null)
            {
                _recOnStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal    = { textColor = Color.red },
                    fontStyle = FontStyle.Bold
                };
            }
            if (_recOffStyle == null)
            {
                _recOffStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = DenEmoTheme.TextTertiary }
                };
            }
        }
    }
}
