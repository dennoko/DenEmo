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
            EnsureTimelineStyles();

            // ─── Row 1: Global Settings ───
            EditorGUILayout.BeginHorizontal(DenEmoTheme.CardOuterStyle);
            GUILayout.Space(8);
            EditorGUILayout.BeginVertical();
            GUILayout.Space(6);
            EditorGUILayout.BeginHorizontal(GUILayout.Height(20));

            GUILayout.FlexibleSpace();

            GUILayout.Label("FPS:", _timelineLabelStyle, GUILayout.Width(28), GUILayout.Height(20));
            float newFps = EditorGUILayout.FloatField(clipModel.FPS, GUILayout.Width(40), GUILayout.Height(20));
            if (!Mathf.Approximately(newFps, clipModel.FPS) && newFps > 0f)
            {
                Undo.RecordObject(clipModel.Clip, "Change FPS");
                clipModel.FPS = newFps;
                clipModel.Clip.frameRate = newFps;
                EditorUtility.SetDirty(clipModel.Clip);
            }

            GUILayout.Space(16);

            GUILayout.Label(DenEmoLoc.EnglishMode ? "Len (s):" : "長さ (秒):", _timelineLabelStyle, GUILayout.Width(50), GUILayout.Height(20));
            float newLen = EditorGUILayout.FloatField(clipModel.ClipLength, GUILayout.Width(40), GUILayout.Height(20));
            if (!Mathf.Approximately(newLen, clipModel.ClipLength) && newLen > 0f)
                clipModel.ClipLength = newLen;

            GUILayout.Space(16);

            GUILayout.Label(DenEmoLoc.EnglishMode ? "All Keys Interp:" : "全キー一括補完:", _timelineLabelStyle, GUILayout.Width(85), GUILayout.Height(20));
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

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(6);
            EditorGUILayout.EndVertical();
            GUILayout.Space(8);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(12);

            // ─── Row 2: Transport ───
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            // Playback buttons in a cohesive toolbar container
            EditorGUILayout.BeginHorizontal(DenEmoTheme.ToolbarStyle, GUILayout.ExpandWidth(false));

            if (GUILayout.Button(
                new GUIContent("|<", DenEmoLoc.EnglishMode ? "Go to start frame" : "先頭フレームへ移動"),
                DenEmoTheme.MiniButtonStyle, GUILayout.Width(28), GUILayout.Height(22)))
            {
                GUI.FocusControl(null);
                isPlaying = false;
                clipModel.CurrentTime = 0f;
                preview.SampleAt(0f);
                window.Repaint();
            }

            if (GUILayout.Button(
                new GUIContent("|◆", DenEmoLoc.EnglishMode ? "Go to previous keyframe" : "前のキーフレームへ移動"),
                DenEmoTheme.MiniButtonStyle, GUILayout.Width(28), GUILayout.Height(22)))
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
                DenEmoTheme.MiniButtonStyle, GUILayout.Width(28), GUILayout.Height(22)))
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
                _playBtnStyle, GUILayout.Width(40), GUILayout.Height(22)))
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
                DenEmoTheme.MiniButtonStyle, GUILayout.Width(28), GUILayout.Height(22)))
            {
                GUI.FocusControl(null);
                isPlaying = false;
                clipModel.CurrentTime = Mathf.Min(clipModel.ClipLength, clipModel.CurrentTime + 1f / clipModel.FPS);
                preview.SampleAt(clipModel.CurrentTime);
                window.Repaint();
            }

            if (GUILayout.Button(
                new GUIContent("◆|", DenEmoLoc.EnglishMode ? "Go to next keyframe" : "次のキーフレームへ移動"),
                DenEmoTheme.MiniButtonStyle, GUILayout.Width(28), GUILayout.Height(22)))
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
                DenEmoTheme.MiniButtonStyle, GUILayout.Width(28), GUILayout.Height(22)))
            {
                GUI.FocusControl(null);
                isPlaying = false;
                clipModel.CurrentTime = clipModel.ClipLength;
                preview.SampleAt(clipModel.ClipLength);
                window.Repaint();
            }

            EditorGUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(8);

            // ─── Row 3: Current State & Options ───
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginHorizontal(DenEmoTheme.ToolbarStyle, GUILayout.ExpandWidth(false));

            GUILayout.Label(DenEmoLoc.EnglishMode ? "Frame:" : "フレーム:", _timelineLabelStyle, GUILayout.Width(50), GUILayout.Height(22));
            int curFrame = clipModel.CurrentFrame;
            int newFrame = EditorGUILayout.IntField(curFrame, GUILayout.Width(44), GUILayout.Height(22));
            if (newFrame != curFrame)
            {
                isPlaying = false;
                float t = Mathf.Clamp(newFrame / clipModel.FPS, 0f, clipModel.ClipLength);
                clipModel.CurrentTime = t;
                preview.SampleAt(t);
                window.Repaint();
            }

            GUILayout.Space(16);

            GUILayout.Label(DenEmoLoc.EnglishMode ? "Speed:" : "速度:", _timelineLabelStyle, GUILayout.Width(32), GUILayout.Height(22));
            float newSpeed = EditorGUILayout.FloatField(playbackSpeed, GUILayout.Width(40), GUILayout.Height(22));
            if (!Mathf.Approximately(newSpeed, playbackSpeed))
                playbackSpeed = Mathf.Clamp(newSpeed, 0.1f, 4f);

            GUILayout.Space(16);

            bool newSmoothLoop = GUILayout.Toggle(
                clipModel.SmoothLoopEnabled,
                new GUIContent(
                    DenEmoLoc.EnglishMode ? " Loop Support" : " ループ対応",
                    DenEmoLoc.EnglishMode ? "Aligns the last value to the first to connect smoothly" : "なめらかに繋げるために最後の値を最初に揃える機能"),
                GUILayout.Height(22));
            if (newSmoothLoop != clipModel.SmoothLoopEnabled)
            {
                clipModel.SmoothLoopEnabled = newSmoothLoop;
                if (newSmoothLoop)
                    preview.ApplyLoopKeysToSourceClip(smrPath);
                else
                    preview.RemoveLoopKeysFromSourceClip(smrPath);
                preview.SetCacheDirty();
                preview.SampleAt(clipModel.CurrentTime);
                window.Repaint();
            }

            GUILayout.Space(16);

            var prevColor = GUI.backgroundColor;
            if (isRecording) GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            
            Rect recBtnRect = GUILayoutUtility.GetRect(new GUIContent("🔴 REC"), isRecording ? _recOnStyle : _recOffStyle, GUILayout.Height(22), GUILayout.Width(64));
            
            if (Event.current.type == EventType.Repaint && !isRecording)
            {
                Color borderColor = new Color(0.8f, 0.2f, 0.2f, 0.8f);
                EditorGUI.DrawRect(new Rect(recBtnRect.x, recBtnRect.y, recBtnRect.width, 1), borderColor);
                EditorGUI.DrawRect(new Rect(recBtnRect.x, recBtnRect.yMax - 1, recBtnRect.width, 1), borderColor);
                EditorGUI.DrawRect(new Rect(recBtnRect.x, recBtnRect.y, 1, recBtnRect.height), borderColor);
                EditorGUI.DrawRect(new Rect(recBtnRect.xMax - 1, recBtnRect.y, 1, recBtnRect.height), borderColor);
            }
            
            if (GUI.Button(recBtnRect,
                new GUIContent("🔴 REC", DenEmoLoc.EnglishMode ? "Toggle recording mode" : "録画モード切替"),
                isRecording ? _recOnStyle : _recOffStyle))
            {
                isRecording = !isRecording;
                startPreview?.Invoke();
                window.Repaint();
            }
            
            GUI.backgroundColor = prevColor;

            EditorGUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(8);

        }
    }
}
