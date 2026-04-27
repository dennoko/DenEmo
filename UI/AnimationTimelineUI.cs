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
    public partial class AnimationTimelineUI
    {
        // ─── Layout constants ─────────────────────────────────────────────────
        private const float RULER_HEIGHT      = 24f;
        private const float SCRUBBER_HEIGHT   = 14f;
        private const float CONTROLS_HEIGHT   = 36f;
        private const float TRACK_ROW_HEIGHT  = 24f;
        private const float TRACK_LABEL_WIDTH = 150f;
        private const float DIAMOND_SIZE      = 5f;
        private const float MAX_TRACKS_HEIGHT = 160f;
        private const float SMALL_BUTTON_WIDTH = 28f;
        private const float BUTTON_HEIGHT = 24f;
        private const float PLAY_BUTTON_WIDTH = 44f;

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
