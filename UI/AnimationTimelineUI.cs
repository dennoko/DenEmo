using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using DenEmo.Models;
using DenEmo.Core;

namespace DenEmo.UI
{
    /// <summary>
    /// ミニタイムライン（ルーラー・スクラバー・再生コントロール・キーフレームトラック）を描画する。
    /// 状態は AnimationModeUI（ClipModel / Editor / Preview / Playback）が所有し、
    /// このクラスはビュー状態（ズーム・ドラッグ・クリップボード）のみを持つ。
    /// キーフレームのクエリはすべて ClipModel のトラックキャッシュ経由で行い、
    /// 描画中に AnimationUtility を直接呼ばない。
    /// </summary>
    public partial class AnimationTimelineUI
    {
        // ─── Layout constants ─────────────────────────────────────────────────
        private const float RULER_HEIGHT      = 38f;
        private const float SCRUBBER_HEIGHT   = 14f;
        private const float TRACK_ROW_HEIGHT  = 24f;
        private const float ACTION_ROW_HEIGHT = 36f;
        private const float RIGHT_PADDING     = 16f;
        private const float DIAMOND_SIZE      = 5f;
        private const float MAX_TRACKS_HEIGHT = 160f;
        private const float SCROLLBAR_HEIGHT  = 10f;
        private const float MIN_VIEW_RANGE    = 1f / 50f; // 最大ズーム 5000%

        // ─── View state（ズーム・スクロール、0〜1 正規化） ────────────────────
        private float _viewStart = 0f;
        private float _viewEnd   = 1f;
        private float ViewRange => Mathf.Max(0.01f, _viewEnd - _viewStart);

        // ─── Interaction state ────────────────────────────────────────────────
        private float   _trackLabelWidth = 150f;
        private bool    _isDraggingLabelWidth;
        private bool    _isDraggingScrubber;
        private bool    _isDraggingViewScroll;
        private float   _viewScrollGrabOffset;
        private Vector2 _tracksScroll;

        // Keyframe dragging state
        private bool   _isDraggingKeyframe;
        private int    _draggingFrame = -1;
        private string _draggingShapeName; // null = 全トラック

        // Cached track geometry (DrawRulerAndScrubber が設定し、スクロールビュー内の行が使う)
        private float _cachedTrackW;

        // ─── Copy/paste clipboard（1 フレーム分のキー） ───────────────────────
        private struct KeyClipboardEntry
        {
            public string            ShapeName;
            public float             Value;
            public InterpolationType Interp;
        }

        private readonly List<KeyClipboardEntry> _keyClipboard = new List<KeyClipboardEntry>();
        private bool HasClipboardData => _keyClipboard.Count > 0;

        // ─── Cached styles（ドメインリロード後に null チェックで再構築） ─────
        private GUIStyle _recOnStyle;
        private GUIStyle _recOffStyle;
        private GUIStyle _playBtnStyle;
        private GUIStyle _timelineLabelStyle;

        private void EnsureTimelineStyles()
        {
            if (_playBtnStyle == null || _playBtnStyle.normal.background == null)
            {
                _playBtnStyle = new GUIStyle(DenEmoTheme.ActionButtonStyle) { fixedHeight = 0 };

                _timelineLabelStyle = new GUIStyle(DenEmoTheme.CaptionStyle)
                {
                    alignment = TextAnchor.MiddleLeft,
                };

                _recOnStyle = new GUIStyle(DenEmoTheme.SecondaryButtonStyle)
                {
                    fontStyle   = FontStyle.Bold,
                    fixedHeight = 0,
                };
                _recOnStyle.normal.background = DenEmoTheme.MakeBorderedTex(DenEmoTheme.Surface1, DenEmoTheme.RecordingRed);
                _recOnStyle.hover.background  = DenEmoTheme.MakeBorderedTex(DenEmoTheme.Surface2, DenEmoTheme.RecordingRed);
                _recOnStyle.active.background = DenEmoTheme.MakeBorderedTex(Color.Lerp(DenEmoTheme.Surface1, Color.white, 0.10f), DenEmoTheme.RecordingRed);
                DenEmoTheme.FixAllTextColors(_recOnStyle, DenEmoTheme.RecordingRed);
                _recOnStyle.hover.textColor = new Color(1f, 0.4f, 0.4f);

                _recOffStyle = new GUIStyle(DenEmoTheme.SecondaryButtonStyle) { fixedHeight = 0 };
                _recOffStyle.normal.background = DenEmoTheme.MakeBorderedTex(DenEmoTheme.Surface1, new Color(0.8f, 0.2f, 0.2f, 0.8f));
                _recOffStyle.hover.background  = DenEmoTheme.MakeBorderedTex(DenEmoTheme.Surface2, DenEmoTheme.RecordingRed);
                _recOffStyle.active.background = DenEmoTheme.MakeBorderedTex(Color.Lerp(DenEmoTheme.Surface1, Color.white, 0.10f), DenEmoTheme.RecordingRed);
            }
        }

        // ─── Main Draw entry ──────────────────────────────────────────────────

        public void Draw(AnimationModeUI mode, ShapeKeyModel shapeModel, EditorWindow window)
        {
            if (mode?.ClipModel?.Clip == null) return;
            DenEmoTheme.Initialize();
            EnsureTimelineStyles();

            HandleKeyboardInput(mode, window);

            GUILayout.BeginVertical(DenEmoTheme.CardStyle);

            // ── Section header + detach/attach button ────────────────────────
            GUILayout.BeginHorizontal();
            GUILayout.Label(DenEmoLoc.T("ui.timeline.title"), DenEmoTheme.SectionHeaderStyle);
            GUILayout.FlexibleSpace();

            if (window is DenEmoTimelineWindow)
            {
                if (GUILayout.Button(
                    new GUIContent(DenEmoLoc.T("ui.timeline.attach"), DenEmoLoc.T("ui.timeline.attach.tip")),
                    DenEmoTheme.MiniButtonStyle))
                {
                    window.Close();
                }
            }
            else
            {
                if (GUILayout.Button(
                    new GUIContent(DenEmoLoc.T("ui.timeline.detach"), DenEmoLoc.T("ui.timeline.detach.tip")),
                    DenEmoTheme.MiniButtonStyle))
                {
                    DenEmoTimelineWindow.ShowWindow();
                }
            }
            GUILayout.EndHorizontal();

            DenEmoTheme.DrawSeparator(2);

            // ── Global settings & transport ──────────────────────────────────
            DrawPlaybackControls(mode, shapeModel, window);

            GUILayout.Space(8);

            // ── Timeline area ─────────────────────────────────────────────────
            EditorGUILayout.BeginVertical(DenEmoTheme.CardOuterStyle);

            EnsureScrubberVisible(mode.ClipModel, window);
            DrawRulerAndScrubber(mode, window);
            DrawKeyframeTracks(mode, shapeModel, window);
            DrawKeyframeActionRow(mode, shapeModel, window);

            EditorGUILayout.EndVertical();

            GUILayout.EndVertical(); // end CardStyle
        }

        // ─── Coordinate helpers ───────────────────────────────────────────────

        private float TimeToPixel(float time, float clipLen, float trackX, float trackW)
        {
            if (clipLen <= 0f) return trackX;
            float norm    = time / clipLen;
            float visible = (norm - _viewStart) / ViewRange;
            return trackX + visible * trackW;
        }

        private float PixelToTime(float pixelX, float clipLen, float trackX, float trackW)
        {
            if (trackW <= 0f || clipLen <= 0f) return 0f;
            float visible = (pixelX - trackX) / trackW;
            float norm    = _viewStart + visible * ViewRange;
            return Mathf.Clamp(norm, 0f, 1f) * clipLen;
        }

        private void EnsureScrubberVisible(AnimationClipModel clipModel, EditorWindow window)
        {
            if (clipModel.ClipLength <= 0f) return;
            float norm = clipModel.CurrentTime / clipModel.ClipLength;
            if (norm < _viewStart || norm > _viewEnd)
            {
                float halfRange = ViewRange * 0.5f;
                _viewStart = Mathf.Clamp01(norm - halfRange);
                _viewEnd   = Mathf.Clamp01(_viewStart + ViewRange);
                window.Repaint();
            }
        }

        private static int CalcRulerStep(int totalFrames, float pixelWidth)
        {
            if (totalFrames <= 0 || pixelWidth <= 0) return 1;
            float pxPerFrame = pixelWidth / totalFrames;
            int   step       = Mathf.Max(1, Mathf.RoundToInt(40f / pxPerFrame));
            int[] nice       = { 1, 2, 5, 10, 15, 20, 30, 60, 120 };
            foreach (int n in nice) if (n >= step) return n;
            return step;
        }

        // ─── Shared seek / transport operations ──────────────────────────────
        // キーボードショートカットとトランスポートボタンの両方から呼ばれる。

        private void SeekTo(AnimationModeUI mode, float time, EditorWindow window)
        {
            mode.Playback.Stop();
            mode.ClipModel.CurrentTime = Mathf.Clamp(time, 0f, mode.ClipModel.ClipLength);
            mode.Preview.SampleAt(mode.ClipModel.CurrentTime);
            window.Repaint();
        }

        private void StepFrame(AnimationModeUI mode, int direction, EditorWindow window)
        {
            var m = mode.ClipModel;
            SeekTo(mode, m.CurrentTime + direction / Mathf.Max(1f, m.FPS), window);
        }

        private void GoToPrevKey(AnimationModeUI mode, EditorWindow window)
        {
            float prev = mode.ClipModel.PrevKeyTime(mode.ClipModel.CurrentTime);
            if (prev >= 0f) SeekTo(mode, prev, window);
        }

        private void GoToNextKey(AnimationModeUI mode, EditorWindow window)
        {
            float next = mode.ClipModel.NextKeyTime(mode.ClipModel.CurrentTime);
            if (next >= 0f) SeekTo(mode, next, window);
        }

        private void TogglePlay(AnimationModeUI mode, EditorWindow window)
        {
            mode.Playback.Toggle();
            window.Repaint();
        }

        // ─── Clipboard operations ─────────────────────────────────────────────

        /// <summary>指定時刻にキーを持つ全シェイプの値と補間タイプをクリップボードへコピーする。</summary>
        private void CopyFrame(AnimationModeUI mode, float time)
        {
            var m = mode.ClipModel;
            _keyClipboard.Clear();
            foreach (var track in m.Tracks)
            {
                if (AnimationClipModel.FindKeyIndex(track.Curve, time, m.FrameTolerance) < 0) continue;
                _keyClipboard.Add(new KeyClipboardEntry
                {
                    ShapeName = track.ShapeName,
                    Value     = track.Curve.Evaluate(time),
                    Interp    = m.GetKeyInterpolation(track.ShapeName, time),
                });
            }
        }

        /// <summary>クリップボードの内容を現在時刻（フレームスナップ）にペーストする。</summary>
        private void PasteAtCurrentTime(AnimationModeUI mode, EditorWindow window)
        {
            if (!HasClipboardData) return;
            var m = mode.ClipModel;
            float pasteTime = m.SnapToFrame(m.CurrentTime);
            foreach (var entry in _keyClipboard)
                mode.Editor.RecordKey(entry.ShapeName, pasteTime, entry.Value, entry.Interp);
            mode.Preview.SampleAt(m.CurrentTime);
            window.Repaint();
        }
    }
}
