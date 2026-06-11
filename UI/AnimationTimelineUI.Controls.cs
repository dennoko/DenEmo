using UnityEditor;
using UnityEngine;
using DenEmo.Models;

namespace DenEmo.UI
{
    public partial class AnimationTimelineUI
    {
        // ─── Keyboard shortcuts ───────────────────────────────────────────────
        // Space: 再生/停止  ←/→: 1フレーム移動  ,/.: 前後のキーへ
        // Delete/Backspace: 現在フレームの全キー削除  Ctrl+C/V: フレームのコピー/ペースト

        private void HandleKeyboardInput(AnimationModeUI mode, EditorWindow window)
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown) return;
            if (!string.IsNullOrEmpty(GUI.GetNameOfFocusedControl())) return;

            var m = mode.ClipModel;

            switch (e.keyCode)
            {
                case KeyCode.Space:
                    TogglePlay(mode, window);
                    e.Use();
                    break;

                case KeyCode.LeftArrow:
                    StepFrame(mode, -1, window);
                    e.Use();
                    break;

                case KeyCode.RightArrow:
                    StepFrame(mode, +1, window);
                    e.Use();
                    break;

                case KeyCode.Comma:
                    GoToPrevKey(mode, window);
                    e.Use();
                    break;

                case KeyCode.Period:
                    GoToNextKey(mode, window);
                    e.Use();
                    break;

                case KeyCode.Delete:
                case KeyCode.Backspace:
                    if (m.HasAnyKeyframeAt(m.CurrentTime))
                    {
                        mode.Editor.DeleteKeysAtTime(m.CurrentTime);
                        mode.Preview.SampleAt(m.CurrentTime);
                        e.Use();
                        window.Repaint();
                    }
                    break;

                case KeyCode.C when e.control:
                    CopyFrame(mode, m.CurrentTime);
                    e.Use();
                    break;

                case KeyCode.V when e.control:
                    PasteAtCurrentTime(mode, window);
                    e.Use();
                    break;
            }
        }

        // ─── Playback controls ────────────────────────────────────────────────

        private void DrawPlaybackControls(AnimationModeUI mode, ShapeKeyModel shapeModel, EditorWindow window)
        {
            var m = mode.ClipModel;
            GUILayout.Space(4);

            // ── Row 1: Global settings (FPS / Length / All-keys interpolation) ──
            EditorGUILayout.BeginHorizontal(DenEmoTheme.CardOuterStyle);
            GUILayout.Space(8);
            EditorGUILayout.BeginVertical();
            GUILayout.Space(6);
            EditorGUILayout.BeginHorizontal(GUILayout.Height(20));

            GUILayout.FlexibleSpace();

            GUILayout.Label(DenEmoLoc.T("ui.timeline.fps"), _timelineLabelStyle, GUILayout.Width(28), GUILayout.Height(20));
            float newFps = EditorGUILayout.FloatField(m.FPS, GUILayout.Width(40), GUILayout.Height(20));
            if (!Mathf.Approximately(newFps, m.FPS) && newFps > 0f)
                mode.Editor.SetFrameRate(newFps);

            GUILayout.Space(16);

            GUILayout.Label(DenEmoLoc.T("ui.timeline.length"), _timelineLabelStyle, GUILayout.Width(50), GUILayout.Height(20));
            float newLen = EditorGUILayout.FloatField(m.ClipLength, GUILayout.Width(40), GUILayout.Height(20));
            if (!Mathf.Approximately(newLen, m.ClipLength) && newLen > 0f)
                m.ClipLength = newLen;

            GUILayout.Space(16);

            GUILayout.Label(DenEmoLoc.T("ui.timeline.allInterp"), _timelineLabelStyle, GUILayout.Width(85), GUILayout.Height(20));
            InterpolationType newInterp = (InterpolationType)GUILayout.Toolbar(
                (int)mode.CurrentInterp,
                new[] { "Step", "Linear", "Ease" },
                DenEmoTheme.MiniButtonStyle,
                GUILayout.Height(20),
                GUILayout.ExpandWidth(false));

            if (newInterp != mode.CurrentInterp)
            {
                mode.CurrentInterp = newInterp;
                mode.Editor.SetAllKeysInterpolation(newInterp);
                mode.Preview.SampleAt(m.CurrentTime);
                window.Repaint();
            }

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(6);
            EditorGUILayout.EndVertical();
            GUILayout.Space(8);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(12);

            // ── Row 2 (& 3): Transport / current state ───────────────────────
            bool isSeparate = window is DenEmoTimelineWindow;

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginHorizontal(DenEmoTheme.ToolbarStyle, GUILayout.ExpandWidth(false));
            DrawTransportButtons(mode, window);
            EditorGUILayout.EndHorizontal();

            if (!isSeparate)
            {
                // メインウィンドウでは 2 行に分けて表示する
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(8);

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
            }
            else
            {
                GUILayout.Space(16);
            }

            EditorGUILayout.BeginHorizontal(DenEmoTheme.ToolbarStyle, GUILayout.ExpandWidth(false));
            DrawStateControls(mode, shapeModel, window);
            EditorGUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(8);
        }

        private void DrawTransportButtons(AnimationModeUI mode, EditorWindow window)
        {
            var m = mode.ClipModel;

            if (TransportButton("|<", DenEmoLoc.T("ui.timeline.goStart.tip")))
                SeekTo(mode, 0f, window);

            if (TransportButton("|◆", DenEmoLoc.T("ui.timeline.prevKey.tip")))
                GoToPrevKey(mode, window);

            if (TransportButton("<", DenEmoLoc.T("ui.timeline.prevFrame.tip")))
                StepFrame(mode, -1, window);

            bool isPlaying = mode.Playback.IsPlaying;
            string playLabel = isPlaying ? "■" : "▶";
            string playTip   = isPlaying ? DenEmoLoc.T("ui.timeline.stop.tip") : DenEmoLoc.T("ui.timeline.play.tip");
            if (GUILayout.Button(new GUIContent(playLabel, playTip), _playBtnStyle, GUILayout.Width(40), GUILayout.Height(22)))
            {
                GUI.FocusControl(null);
                TogglePlay(mode, window);
            }

            if (TransportButton(">", DenEmoLoc.T("ui.timeline.nextFrame.tip")))
                StepFrame(mode, +1, window);

            if (TransportButton("◆|", DenEmoLoc.T("ui.timeline.nextKey.tip")))
                GoToNextKey(mode, window);

            if (TransportButton(">|", DenEmoLoc.T("ui.timeline.goEnd.tip")))
                SeekTo(mode, m.ClipLength, window);
        }

        private static bool TransportButton(string label, string tip)
        {
            bool pressed = GUILayout.Button(
                new GUIContent(label, tip),
                DenEmoTheme.MiniButtonStyle, GUILayout.Width(28), GUILayout.Height(22));
            if (pressed) GUI.FocusControl(null);
            return pressed;
        }

        private void DrawStateControls(AnimationModeUI mode, ShapeKeyModel shapeModel, EditorWindow window)
        {
            var m = mode.ClipModel;

            // ── Frame field ──────────────────────────────────────────────────
            GUILayout.Label(DenEmoLoc.T("ui.timeline.frame"), _timelineLabelStyle, GUILayout.Width(50), GUILayout.Height(22));
            int curFrame = m.CurrentFrame;
            int newFrame = EditorGUILayout.IntField(curFrame, GUILayout.Width(44), GUILayout.Height(22));
            if (newFrame != curFrame)
                SeekTo(mode, m.FrameToTime(newFrame), window);

            GUILayout.Space(16);

            // ── Playback speed ───────────────────────────────────────────────
            GUILayout.Label(DenEmoLoc.T("ui.timeline.speed"), _timelineLabelStyle, GUILayout.Width(32), GUILayout.Height(22));
            float newSpeed = EditorGUILayout.FloatField(mode.Playback.Speed, GUILayout.Width(40), GUILayout.Height(22));
            if (!Mathf.Approximately(newSpeed, mode.Playback.Speed))
                mode.Playback.Speed = newSpeed;

            GUILayout.Space(16);

            // ── Smooth loop toggle ───────────────────────────────────────────
            // ループ補正はプレビューと保存時にのみ反映され、編集中のクリップは変更しない。
            bool newSmoothLoop = GUILayout.Toggle(
                m.SmoothLoopEnabled,
                new GUIContent(DenEmoLoc.T("ui.timeline.loop"), DenEmoLoc.T("ui.timeline.loop.tip")),
                GUILayout.Height(22));
            if (newSmoothLoop != m.SmoothLoopEnabled)
            {
                m.SmoothLoopEnabled = newSmoothLoop;
                mode.Preview.SampleAt(m.CurrentTime);
                window.Repaint();
            }

            GUILayout.Space(16);

            // ── REC toggle ───────────────────────────────────────────────────
            if (GUILayout.Button(
                new GUIContent("🔴 REC", DenEmoLoc.T("ui.timeline.rec.tip")),
                mode.IsRecording ? _recOnStyle : _recOffStyle,
                GUILayout.Width(64), GUILayout.Height(22)))
            {
                mode.IsRecording = !mode.IsRecording;
                if (mode.IsRecording && !mode.Preview.IsActive)
                    mode.StartPreview(shapeModel);
                window.Repaint();
            }
        }
    }
}
