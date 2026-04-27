using UnityEditor;
using UnityEngine;
using DenEmo.Models;
using DenEmo.Core;

namespace DenEmo.UI
{
    public partial class AnimationTimelineUI
    {
        // ─── Playback controls ────────────────────────────────────────────────

        private void DrawPlaybackControls(
            AnimationClipModel clipModel, AnimationPreviewController preview, string smrPath,
            ref bool isRecording, ref InterpolationType currentInterp, ref float playbackSpeed,
            EditorWindow window,
            ref bool isPlaying, ref double playStartRealTime, ref float playStartClipTime, System.Action startPreview)
        {
            GUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginHorizontal(DenEmoTheme.ToolbarStyle);

            if (GUILayout.Button(
                new GUIContent("|<", DenEmoLoc.EnglishMode ? "Go to start frame" : "先頭フレームへ移動"),
                DenEmoTheme.MiniButtonStyle,
                GUILayout.Width(SMALL_BUTTON_WIDTH), GUILayout.Height(BUTTON_HEIGHT)))
            {
                GUI.FocusControl(null);
                isPlaying = false;
                clipModel.CurrentTime = 0f;
                preview.SampleAt(0f);
                window.Repaint();
            }

            if (GUILayout.Button(
                new GUIContent("|◆", DenEmoLoc.EnglishMode ? "Go to previous keyframe" : "前のキーフレームへ移動"),
                DenEmoTheme.MiniButtonStyle,
                GUILayout.Width(SMALL_BUTTON_WIDTH), GUILayout.Height(BUTTON_HEIGHT)))
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

            if (GUILayout.Button(
                new GUIContent("<", DenEmoLoc.EnglishMode ? "Step one frame backward" : "1フレーム戻る"),
                DenEmoTheme.MiniButtonStyle,
                GUILayout.Width(SMALL_BUTTON_WIDTH), GUILayout.Height(BUTTON_HEIGHT)))
            {
                GUI.FocusControl(null);
                isPlaying = false;
                clipModel.CurrentTime = Mathf.Max(0f, clipModel.CurrentTime - 1f / clipModel.FPS);
                preview.SampleAt(clipModel.CurrentTime);
                window.Repaint();
            }

            string playLabel = isPlaying ? "■" : "▶";
            string playTip = isPlaying
                ? (DenEmoLoc.EnglishMode ? "Stop playback" : "再生停止")
                : (DenEmoLoc.EnglishMode ? "Start playback" : "再生開始");
            if (GUILayout.Button(
                new GUIContent(playLabel, playTip),
                DenEmoTheme.ActionButtonStyle,
                GUILayout.Width(PLAY_BUTTON_WIDTH), GUILayout.Height(BUTTON_HEIGHT)))
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

            if (GUILayout.Button(
                new GUIContent(">", DenEmoLoc.EnglishMode ? "Step one frame forward" : "1フレーム進む"),
                DenEmoTheme.MiniButtonStyle,
                GUILayout.Width(SMALL_BUTTON_WIDTH), GUILayout.Height(BUTTON_HEIGHT)))
            {
                GUI.FocusControl(null);
                isPlaying = false;
                clipModel.CurrentTime = Mathf.Min(clipModel.ClipLength, clipModel.CurrentTime + 1f / clipModel.FPS);
                preview.SampleAt(clipModel.CurrentTime);
                window.Repaint();
            }

            if (GUILayout.Button(
                new GUIContent("◆|", DenEmoLoc.EnglishMode ? "Go to next keyframe" : "次のキーフレームへ移動"),
                DenEmoTheme.MiniButtonStyle,
                GUILayout.Width(SMALL_BUTTON_WIDTH), GUILayout.Height(BUTTON_HEIGHT)))
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

            if (GUILayout.Button(
                new GUIContent(">|", DenEmoLoc.EnglishMode ? "Go to end frame" : "末尾フレームへ移動"),
                DenEmoTheme.MiniButtonStyle,
                GUILayout.Width(SMALL_BUTTON_WIDTH), GUILayout.Height(BUTTON_HEIGHT)))
            {
                GUI.FocusControl(null);
                isPlaying = false;
                clipModel.CurrentTime = clipModel.ClipLength;
                preview.SampleAt(clipModel.ClipLength);
                window.Repaint();
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(16);

            EditorGUILayout.BeginVertical();
            GUILayout.Space(2);
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
            if (GUILayout.Button(
                new GUIContent(isRecording ? "● REC" : "○ REC", DenEmoLoc.EnglishMode ? "Toggle recording mode" : "録画モード切替"),
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

            EditorGUILayout.BeginHorizontal(DenEmoTheme.CardOuterStyle);
            GUILayout.Space(8);

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

            GUILayout.Label(DenEmoLoc.EnglishMode ? "Interp:" : "補間:", DenEmoTheme.CaptionStyle, GUILayout.Width(32));
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

            EditorGUILayout.BeginVertical();
            GUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(
                new GUIContent(DenEmoLoc.EnglishMode ? "✕ Clear Frame" : "✕ フレーム削除", DenEmoLoc.EnglishMode ? "Delete all keys at current frame" : "現在フレームのキーを全削除"),
                DenEmoTheme.SecondaryButtonStyle, GUILayout.Height(22)))
            {
                preview.DeleteAllKeyframesAtTime(smrPath, clipModel.CurrentTime);
                preview.SampleAt(clipModel.CurrentTime);
                window.Repaint();
            }

            GUILayout.Space(8);

            if (GUILayout.Button(
                new GUIContent(DenEmoLoc.EnglishMode ? "↺ Loop" : "↺ ループ", DenEmoLoc.EnglishMode ? "Copy first key to end frame" : "先頭キーを末尾フレームへ複製"),
                DenEmoTheme.SecondaryButtonStyle, GUILayout.Height(22), GUILayout.Width(62)))
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
    }
}
