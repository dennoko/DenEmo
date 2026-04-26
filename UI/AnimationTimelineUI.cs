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
        private const float CONTROLS_HEIGHT   = 36f;
        private const float TRACK_ROW_HEIGHT  = 24f;
        private const float TRACK_LABEL_WIDTH = 150f;
        private const float DIAMOND_SIZE      = 5f;
        private const float MAX_TRACKS_HEIGHT = 160f;

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
            ref bool                   isRecording,
            ref InterpolationType      currentInterp,
            ref float                  playbackSpeed,
            EditorWindow               window,
            ref bool                   isPlaying,
            ref double                 playStartRealTime,
            ref float                  playStartClipTime,
            System.Action              startPreview)
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

            DrawPlaybackControls(
                clipModel, preview, smrPath, ref isRecording, ref currentInterp, ref playbackSpeed, window,
                ref isPlaying, ref playStartRealTime, ref playStartClipTime, startPreview);

            GUILayout.Space(8);

            // Timeline Area
            EditorGUILayout.BeginVertical("box");

            DrawRulerAndScrubber(clipModel, preview, window);

            if (!_tracksCollapsed)
            {
                DrawKeyframeTracks(clipModel, preview, smrPath, window);
            }

            EditorGUILayout.EndVertical();

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
                EditorGUI.DrawRect(scrubRect,  DenEmoTheme.Surface0);

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
            EditorGUI.DrawRect(new Rect(sx - 1, rect.y, 2, rect.height), new Color(1f, 1f, 1f, 0.8f));
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

        // ─── Playback controls ────────────────────────────────────────────────

        private void DrawPlaybackControls(
            AnimationClipModel clipModel, AnimationPreviewController preview, string smrPath,
            ref bool isRecording, ref InterpolationType currentInterp, ref float playbackSpeed,
            EditorWindow window,
            ref bool isPlaying, ref double playStartRealTime, ref float playStartClipTime, System.Action startPreview)
        {
            GUILayout.Space(4);

            // ─── Row 1: Essential Controls ───
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            // Playback buttons in a cohesive toolbar container
            EditorGUILayout.BeginHorizontal(DenEmoTheme.ToolbarStyle);

            if (GUILayout.Button("|<", DenEmoTheme.MiniButtonStyle, GUILayout.Width(28), GUILayout.Height(24)))
            {
                GUI.FocusControl(null);
                isPlaying = false;
                clipModel.CurrentTime = 0f;
                preview.SampleAt(0f);
                window.Repaint();
            }
            if (GUILayout.Button("|◆", DenEmoTheme.MiniButtonStyle, GUILayout.Width(28), GUILayout.Height(24)))
            {
                GUI.FocusControl(null);
                isPlaying = false;
                float[] allKeys = clipModel.GetAllKeyTimes(smrPath);
                float tol = clipModel.FPS > 0f ? 0.5f / clipModel.FPS : 0.01f;
                float prev = -1f;
                foreach (float kt in allKeys)
                    if (kt < clipModel.CurrentTime - tol) prev = kt;
                if (prev >= 0f) { clipModel.CurrentTime = prev; preview.SampleAt(prev); }
                window.Repaint();
            }
            if (GUILayout.Button("<", DenEmoTheme.MiniButtonStyle, GUILayout.Width(28), GUILayout.Height(24)))
            {
                GUI.FocusControl(null);
                isPlaying = false;
                clipModel.CurrentTime = Mathf.Max(0f, clipModel.CurrentTime - 1f / clipModel.FPS);
                preview.SampleAt(clipModel.CurrentTime);
                window.Repaint();
            }

            // Play / Stop with matching height
            string playLabel = isPlaying ? "■" : "▶";
            if (GUILayout.Button(playLabel, DenEmoTheme.ActionButtonStyle, GUILayout.Width(44), GUILayout.Height(24)))
            {
                GUI.FocusControl(null);
                isPlaying = !isPlaying;
                if (isPlaying)
                {
                    playStartRealTime = EditorApplication.timeSinceStartup;
                    playStartClipTime = clipModel.CurrentTime;
                }
                window.Repaint();
            }

            if (GUILayout.Button(">", DenEmoTheme.MiniButtonStyle, GUILayout.Width(28), GUILayout.Height(24)))
            {
                GUI.FocusControl(null);
                isPlaying = false;
                clipModel.CurrentTime = Mathf.Min(clipModel.ClipLength, clipModel.CurrentTime + 1f / clipModel.FPS);
                preview.SampleAt(clipModel.CurrentTime);
                window.Repaint();
            }
            if (GUILayout.Button("◆|", DenEmoTheme.MiniButtonStyle, GUILayout.Width(28), GUILayout.Height(24)))
            {
                GUI.FocusControl(null);
                isPlaying = false;
                float[] allKeys = clipModel.GetAllKeyTimes(smrPath);
                float tol = clipModel.FPS > 0f ? 0.5f / clipModel.FPS : 0.01f;
                float next = -1f;
                foreach (float kt in allKeys)
                    if (kt > clipModel.CurrentTime + tol) { next = kt; break; }
                if (next >= 0f) { clipModel.CurrentTime = next; preview.SampleAt(next); }
                window.Repaint();
            }
            if (GUILayout.Button(">|", DenEmoTheme.MiniButtonStyle, GUILayout.Width(28), GUILayout.Height(24)))
            {
                GUI.FocusControl(null);
                isPlaying = false;
                clipModel.CurrentTime = clipModel.ClipLength;
                preview.SampleAt(clipModel.ClipLength);
                window.Repaint();
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(16);

            // Current Frame & REC toggle
            EditorGUILayout.BeginVertical();
            GUILayout.Space(2); // Center align with buttons
            EditorGUILayout.BeginHorizontal();

            GUILayout.Label(DenEmoLoc.EnglishMode ? "Frame:" : "フレーム:", DenEmoTheme.CaptionStyle, GUILayout.Width(50));
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

            GUILayout.Space(16);

            EnsureRecStyles();
            if (GUILayout.Button(isRecording ? "● REC" : "○ REC",
                isRecording ? _recOnStyle : _recOffStyle, GUILayout.Height(22), GUILayout.Width(64)))
            {
                isRecording = !isRecording;
                startPreview?.Invoke();
                window.Repaint();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(8);

            // ─── Row 2: Secondary Options ───
            EditorGUILayout.BeginHorizontal(DenEmoTheme.CardOuterStyle);
            GUILayout.Space(8);

            // Left side: Clip Settings
            EditorGUILayout.BeginVertical();
            GUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();

            GUILayout.Label("FPS:", DenEmoTheme.CaptionStyle, GUILayout.Width(28));
            float newFps = EditorGUILayout.FloatField(clipModel.FPS, GUILayout.Width(40));
            if (!Mathf.Approximately(newFps, clipModel.FPS) && newFps > 0f)
            {
                Undo.RecordObject(clipModel.Clip, "Change FPS");
                clipModel.FPS = newFps;
                clipModel.Clip.frameRate = newFps;
                EditorUtility.SetDirty(clipModel.Clip);
            }

            GUILayout.Space(8);

            GUILayout.Label(DenEmoLoc.EnglishMode ? "Len:" : "長さ:", DenEmoTheme.CaptionStyle, GUILayout.Width(30));
            float newLen = EditorGUILayout.FloatField(clipModel.ClipLength, GUILayout.Width(40));
            if (!Mathf.Approximately(newLen, clipModel.ClipLength) && newLen > 0f)
                clipModel.ClipLength = newLen;

            GUILayout.Space(8);

            GUILayout.Label(DenEmoLoc.EnglishMode ? "Speed:" : "速度:", DenEmoTheme.CaptionStyle, GUILayout.Width(32));
            float newSpeed = EditorGUILayout.FloatField(playbackSpeed, GUILayout.Width(40));
            if (!Mathf.Approximately(newSpeed, playbackSpeed))
                playbackSpeed = Mathf.Clamp(newSpeed, 0.1f, 4f);

            GUILayout.Space(8);

            GUILayout.Label(DenEmoLoc.EnglishMode ? "Interp:" : "補完:", DenEmoTheme.CaptionStyle, GUILayout.Width(32));
            InterpolationType newInterp = (InterpolationType)GUILayout.Toolbar(
                (int)currentInterp,
                new[] { "Step", "Linear", "Ease" },
                DenEmoTheme.MiniButtonStyle,
                GUILayout.Height(20),
                GUILayout.ExpandWidth(false));

            if (newInterp != currentInterp)
            {
                currentInterp = newInterp;
                preview.ChangeAllKeyframesInterpolation(smrPath, currentInterp);
                preview.SampleAt(clipModel.CurrentTime);
                window.Repaint();
            }

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(6);
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            // Right side: Track Actions
            EditorGUILayout.BeginVertical();
            GUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(DenEmoLoc.EnglishMode ? "✕ Clear Frame" : "✕ フレーム削除", DenEmoTheme.SecondaryButtonStyle, GUILayout.Height(22)))
            {
                preview.DeleteAllKeyframesAtTime(smrPath, clipModel.CurrentTime);
                preview.SampleAt(clipModel.CurrentTime);
                window.Repaint();
            }

            GUILayout.Space(8);

            if (GUILayout.Button(DenEmoLoc.EnglishMode ? "↺ Loop" : "↺ ループ", DenEmoTheme.SecondaryButtonStyle, GUILayout.Height(22), GUILayout.Width(62)))
            {
                preview.AddLoopKey(smrPath, clipModel.ClipLength, currentInterp);
                preview.SampleAt(clipModel.CurrentTime);
                window.Repaint();
            }

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4);
            EditorGUILayout.EndVertical();

            GUILayout.Space(8);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(8);
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
                EditorGUI.DrawRect(rowRect, DenEmoTheme.Surface0);
                // Center line
                float cy = rowRect.y + rowRect.height * 0.5f;
                EditorGUI.DrawRect(new Rect(rowRect.x + TRACK_LABEL_WIDTH, cy, rowRect.width - TRACK_LABEL_WIDTH, 1), DenEmoTheme.Outline);
            }

            float trackW = rowRect.width - TRACK_LABEL_WIDTH;
            float trackX = rowRect.x + TRACK_LABEL_WIDTH;

            // Shape name label
            GUI.Label(
                new Rect(rowRect.x + 8, rowRect.y + 4, TRACK_LABEL_WIDTH - 32, rowRect.height - 8),
                shapeName, DenEmoTheme.CaptionStyle);

            if (GUI.Button(new Rect(rowRect.x + TRACK_LABEL_WIDTH - 22, rowRect.y + 4, 16, 16), "✕", DenEmoTheme.MiniButtonStyle))
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
                    GUI.FocusControl(null);
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
                    menu.AddItem(new GUIContent("Step"),   false, () => { preview.ChangeInterpolation(sn, smrPath, kt, InterpolationType.Step);   preview.SampleAt(clipModel.CurrentTime); window.Repaint(); });
                    menu.AddItem(new GUIContent("Linear"), false, () => { preview.ChangeInterpolation(sn, smrPath, kt, InterpolationType.Linear); preview.SampleAt(clipModel.CurrentTime); window.Repaint(); });
                    menu.AddItem(new GUIContent("Ease"),   false, () => { preview.ChangeInterpolation(sn, smrPath, kt, InterpolationType.Ease);   preview.SampleAt(clipModel.CurrentTime); window.Repaint(); });
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
                _recOnStyle = new GUIStyle(DenEmoTheme.SecondaryButtonStyle)
                {
                    fontStyle = FontStyle.Bold
                };
                _recOnStyle.normal.textColor = Color.red;
                _recOnStyle.hover.textColor = new Color(1f, 0.4f, 0.4f);
                _recOnStyle.active.textColor = Color.red;
            }
            if (_recOffStyle == null)
            {
                _recOffStyle = new GUIStyle(DenEmoTheme.SecondaryButtonStyle);
            }
        }
    }
}
