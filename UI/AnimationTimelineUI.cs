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
        private const float RIGHT_PADDING     = 16f;
        private const float DIAMOND_SIZE      = 5f;
        private const float MAX_TRACKS_HEIGHT = 160f;
        private const float SMALL_BUTTON_WIDTH = 28f;
        private const float BUTTON_HEIGHT = 24f;
        private const float PLAY_BUTTON_WIDTH = 44f;

        // ─── Internal state ───────────────────────────────────────────────────
        private float   _trackLabelWidth = 150f;
        private bool    _isDraggingLabelWidth;
        private bool    _isDraggingScrubber;
        private Vector2 _tracksScroll;
        private bool    _tracksCollapsed;

        // Keyframe dragging state
        private bool   _isDraggingKeyframe;
        private int    _draggingOldFrame = -1;
        private string _draggingShapeName; // null means "All Tracks"


        // Cached styles (re-created when null after domain reload)
        private GUIStyle _kfLabelStyle;
        private GUIStyle _recOnStyle;
        private GUIStyle _recOffStyle;
        private GUIStyle _playBtnStyle;
        private GUIStyle _clearBtnStyle;
        private GUIStyle _timelineLabelStyle;

        private void EnsureTimelineStyles()
        {
            EnsureRecStyles();
            if (_playBtnStyle == null || _playBtnStyle.normal.background == null)
            {
                _playBtnStyle = new GUIStyle(DenEmoTheme.ActionButtonStyle);
                _playBtnStyle.fixedHeight = 0;

                _clearBtnStyle = new GUIStyle(DenEmoTheme.SecondaryButtonStyle);
                _clearBtnStyle.fixedHeight = 0;

                _timelineLabelStyle = new GUIStyle(DenEmoTheme.CaptionStyle);
                _timelineLabelStyle.alignment = TextAnchor.MiddleLeft;
            }
        }

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

            // Section header + detach/attach button
            GUILayout.BeginHorizontal();
            GUILayout.Label(
                DenEmoLoc.EnglishMode ? "TIMELINE" : "タイムライン",
                DenEmoTheme.SectionHeaderStyle);
            GUILayout.FlexibleSpace();
            
            bool isSeparate = window.GetType().Name == "DenEmoTimelineWindow";
            if (isSeparate)
            {
                if (GUILayout.Button(new GUIContent(DenEmoLoc.EnglishMode ? "↘ Attach" : "↘ 結合", DenEmoLoc.EnglishMode ? "Return timeline to main window" : "メインウィンドウに戻す"), DenEmoTheme.MiniButtonStyle))
                {
                    window.Close();
                }
            }
            else
            {
                if (GUILayout.Button(new GUIContent(DenEmoLoc.EnglishMode ? "↗ Detach" : "↗ 別窓化", DenEmoLoc.EnglishMode ? "Open timeline in a separate window" : "別ウィンドウでタイムラインを開く"), DenEmoTheme.MiniButtonStyle))
                {
                    DenEmoTimelineWindow.ShowWindow();
                }
            }
            GUILayout.EndHorizontal();

            DenEmoTheme.DrawSeparator(2);

            // Global Settings and Transport
            DrawPlaybackControls(
                clipModel, preview, smrPath, ref isRecording, ref currentInterp, ref playbackSpeed, window,
                ref isPlaying, ref playStartRealTime, ref playStartClipTime, startPreview);

            GUILayout.Space(8);

            // Timeline Area
            EditorGUILayout.BeginVertical("box");

            DrawRulerAndScrubber(clipModel, preview, window);

            DrawKeyframeTracks(clipModel, preview, shapeModel, smrPath, currentInterp, window);

            DrawKeyframeDeleteButtons(clipModel, preview, smrPath, window);

            EditorGUILayout.EndVertical(); // end "box"

            GUILayout.EndVertical(); // end CardStyle
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
            if (_recOnStyle == null || _recOnStyle.normal.background == null)
            {
                _recOnStyle = new GUIStyle(DenEmoTheme.SecondaryButtonStyle)
                {
                    fontStyle = FontStyle.Bold,
                    fixedHeight = 0
                };
                _recOnStyle.normal.background = DenEmoTheme.MakeBorderedTex(DenEmoTheme.Surface1, Color.red);
                _recOnStyle.hover.background = DenEmoTheme.MakeBorderedTex(DenEmoTheme.Surface2, Color.red);
                _recOnStyle.active.background = DenEmoTheme.MakeBorderedTex(Color.Lerp(DenEmoTheme.Surface1, Color.white, 0.10f), Color.red);
                _recOnStyle.normal.textColor = Color.red;
                _recOnStyle.hover.textColor = new Color(1f, 0.4f, 0.4f);
                _recOnStyle.active.textColor = Color.red;
            }
            if (_recOffStyle == null || _recOffStyle.normal.background == null)
            {
                _recOffStyle = new GUIStyle(DenEmoTheme.SecondaryButtonStyle);
                _recOffStyle.fixedHeight = 0;
                _recOffStyle.normal.background = DenEmoTheme.MakeBorderedTex(DenEmoTheme.Surface1, Color.red);
                _recOffStyle.hover.background = DenEmoTheme.MakeBorderedTex(DenEmoTheme.Surface2, Color.red);
                _recOffStyle.active.background = DenEmoTheme.MakeBorderedTex(Color.Lerp(DenEmoTheme.Surface1, Color.white, 0.10f), Color.red);
            }
        }
    }
}
