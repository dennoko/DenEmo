using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using DenEmo.Models;
using DenEmo.Core;

namespace DenEmo.UI
{
    // ─── Animation draw context ───────────────────────────────────────────────

    /// <summary>
    /// Passed to ShapeKeyListUI when in Animation mode to intercept value changes
    /// and show keyframe indicators per shape.
    /// </summary>
    public class AnimationDrawContext
    {
        public bool IsRecording;
        /// <summary>Returns true when the given shape has a keyframe at the current playhead position.</summary>
        public Func<string, bool> HasKeyframeAtCurrentTime;
        /// <summary>Called when the user moves a slider; routing depends on record state.</summary>
        public Action<ShapeKeyItem, ShapeKeyModel, float> OnValueChanged;
        /// <summary>Called when the user clicks the ◆/◇ keyframe toggle button.</summary>
        public Action<ShapeKeyItem, ShapeKeyModel> OnKeyframeToggle;
        /// <summary>When non-null, only shapes in this set are shown in the list.</summary>
        public HashSet<string> TrackShapeNames;
        /// <summary>True when the track-only filter is active.</summary>
        public bool TrackFilterEnabled;
        /// <summary>Toggles the track-only filter.</summary>
        public Action OnToggleTrackFilter;
    }

    // ─── AnimationModeUI ─────────────────────────────────────────────────────

    /// <summary>
    /// Orchestrates the animation-mode tab: clip settings, timeline, and recording.
    /// </summary>
    public class AnimationModeUI
    {
        // ─── Sub-objects ──────────────────────────────────────────────────────

        public AnimationClipModel         ClipModel  { get; } = new AnimationClipModel();
        public AnimationPreviewController Preview    { get; } = new AnimationPreviewController();
        public AnimationTimelineUI        TimelineUI { get; } = new AnimationTimelineUI();

        // ─── State ────────────────────────────────────────────────────────────

        public bool               IsRecording  { get; set; }
        public InterpolationType  CurrentInterp { get; set; } = InterpolationType.Ease;

        private bool   _isPlaying;
        private double _playStartRealTime;
        private float  _playStartClipTime;
        private string _smrPath = "";
        private bool   _trackFilterEnabled;
        private float  _playbackSpeed = 1f;

        public float PlaybackSpeed
        {
            get => _playbackSpeed;
            set => _playbackSpeed = Mathf.Clamp(value, 0.1f, 4f);
        }

        public bool TrackFilterEnabled
        {
            get => _trackFilterEnabled;
            set => _trackFilterEnabled = value;
        }

        // Cached styles
        private GUIStyle _recOnStyle;
        private GUIStyle _recOffStyle;

        // ─── Lifecycle ────────────────────────────────────────────────────────

        public void OnEnable(ShapeKeyModel shapeModel)
        {
            if (ClipModel.Clip != null && shapeModel.TargetSkinnedMesh != null)
                StartPreview(shapeModel);
        }

        public void OnDisable()
        {
            _isPlaying  = false;
            IsRecording = false;
            Preview.Stop();
        }

        /// <summary>Call from EditorApplication.update to advance playback.</summary>
        public void OnUpdate(EditorWindow window)
        {
            if (!_isPlaying || ClipModel.Clip == null) return;
            if (!Preview.IsActive) return;

            double elapsed = EditorApplication.timeSinceStartup - _playStartRealTime;
            float  t       = _playStartClipTime + (float)elapsed * _playbackSpeed;

            if (t >= ClipModel.ClipLength)
            {
                // Loop: carry the overshoot into the next cycle so no time is lost
                float overshoot    = t - ClipModel.ClipLength;
                _playStartRealTime = EditorApplication.timeSinceStartup;
                _playStartClipTime = overshoot;
                t                  = overshoot;
            }

            ClipModel.CurrentTime = t;
            Preview.SampleAt(t);
            window.Repaint();
        }

        // ─── Preview management ───────────────────────────────────────────────

        public void StartPreview(ShapeKeyModel shapeModel)
        {
            if (shapeModel?.TargetSkinnedMesh == null) return;
            _smrPath = GetRelativePath(
                shapeModel.TargetSkinnedMesh.transform,
                shapeModel.TargetSkinnedMesh.transform.root);
            Preview.Start(ClipModel, shapeModel);
        }

        public void StopPreview()
        {
            _isPlaying = false;
            Preview.Stop();
        }

        public void OnTargetChanged(ShapeKeyModel shapeModel)
        {
            Preview.Stop();
            if (ClipModel.Clip != null && shapeModel?.TargetSkinnedMesh != null)
                StartPreview(shapeModel);
        }

        // ─── Draw: Clip settings ──────────────────────────────────────────────

        public void DrawAnimationClipSection(ShapeKeyModel shapeModel, string saveFolder, EditorWindow window)
        {
            DenEmoTheme.BeginSection(DenEmoLoc.EnglishMode ? "ANIMATION CLIP" : "アニメーションクリップ");

            // ── Clip field + New button ──────────────────────────────────────
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(
                DenEmoLoc.T("ui.animMode.clip.label"),
                DenEmoTheme.CaptionStyle, GUILayout.Width(46));

            EditorGUI.BeginChangeCheck();
            var newClip = EditorGUILayout.ObjectField(ClipModel.Clip, typeof(AnimationClip), false) as AnimationClip;
            if (EditorGUI.EndChangeCheck())
            {
                StopPreview();
                ClipModel.SetClip(newClip);
                if (newClip != null && shapeModel.TargetSkinnedMesh != null)
                    StartPreview(shapeModel);
                window.Repaint();
            }

            if (GUILayout.Button(DenEmoLoc.T("ui.animMode.clip.new"), DenEmoTheme.MiniButtonStyle, GUILayout.Width(48)))
                CreateNewClip(shapeModel, saveFolder, window);

            EditorGUILayout.EndHorizontal();

            if (ClipModel.Clip == null)
            {
                GUILayout.Space(2);
                GUILayout.Label(DenEmoLoc.T("ui.animMode.clip.hint"), DenEmoTheme.CaptionStyle);
                DenEmoTheme.EndSection();
                return;
            }

            DenEmoTheme.EndSection();
        }

        // ─── Draw: Timeline ───────────────────────────────────────────────────

        public void DrawTimeline(ShapeKeyModel shapeModel, EditorWindow window)
        {
            if (ClipModel.Clip == null) return;
            bool isRec = IsRecording;
            InterpolationType interp = CurrentInterp;
            float speed = _playbackSpeed;
            TimelineUI.Draw(
                ClipModel, Preview, shapeModel, _smrPath,
                ref isRec, ref interp, ref speed, window,
                ref _isPlaying, ref _playStartRealTime, ref _playStartClipTime,
                () => {
                    if (isRec && !Preview.IsActive && shapeModel.TargetSkinnedMesh != null)
                        StartPreview(shapeModel);
                });
            IsRecording = isRec;
            CurrentInterp = interp;
            _playbackSpeed = Mathf.Clamp(speed, 0.1f, 4f);
        }

        // ─── Animation draw context ───────────────────────────────────────────

        public AnimationDrawContext BuildDrawContext(ShapeKeyModel shapeModel)
        {
            var trackNames = _trackFilterEnabled
                ? new HashSet<string>(ClipModel.GetShapeNamesWithKeys(_smrPath))
                : null;

            return new AnimationDrawContext
            {
                IsRecording       = IsRecording,
                TrackFilterEnabled = _trackFilterEnabled,
                TrackShapeNames   = trackNames,
                OnToggleTrackFilter = () => _trackFilterEnabled = !_trackFilterEnabled,

                HasKeyframeAtCurrentTime = (shapeName) =>
                    ClipModel.HasKeyframeAt(shapeName, ClipModel.CurrentTime, _smrPath),

                OnValueChanged = (item, model, newValue) =>
                {
                    item.Value = newValue;
                    if (ClipModel.Clip != null &&
                        (IsRecording || ClipModel.HasKeyframeAt(item.Name, ClipModel.CurrentTime, _smrPath)))
                    {
                        // Recording mode or slider moved while at an existing keyframe → update the key directly
                        Preview.RecordKeyframe(item.Name, _smrPath, ClipModel.CurrentTime, newValue, CurrentInterp);
                        Preview.SampleAt(ClipModel.CurrentTime);
                    }
                    else
                    {
                        if (model.TargetSkinnedMesh != null)
                            model.TargetSkinnedMesh.SetBlendShapeWeight(item.Index, newValue);
                    }
                },

                OnKeyframeToggle = (item, model) =>
                {
                    if (ClipModel.Clip == null) return;
                    bool hasKey = ClipModel.HasKeyframeAt(item.Name, ClipModel.CurrentTime, _smrPath);
                    if (hasKey)
                    {
                        Preview.DeleteKeyframe(item.Name, _smrPath, ClipModel.CurrentTime);
                    }
                    else
                    {
                        Preview.RecordKeyframe(item.Name, _smrPath, ClipModel.CurrentTime, item.Value, CurrentInterp);
                        Preview.SampleAt(ClipModel.CurrentTime);
                    }
                }
            };
        }

        // ─── Save ─────────────────────────────────────────────────────────────

        public void SaveClip(string saveFolder, ShapeKeyModel shapeModel, Action<string, int> setStatus)
        {
            if (ClipModel.Clip == null)
            {
                setStatus(DenEmoLoc.T("dlg.apply.noClip"), 3);
                return;
            }

            string path = AssetDatabase.GetAssetPath(ClipModel.Clip);
            if (string.IsNullOrEmpty(path))
            {
                string defaultName = shapeModel.TargetObject
                    ? shapeModel.TargetObject.name + "_anim"
                    : "blendshape_anim";
                path = EditorUtility.SaveFilePanelInProject(
                    DenEmoLoc.T("save.panel.title"), defaultName + ".anim", "anim",
                    DenEmoLoc.T("save.panel.hint"), saveFolder);
                if (string.IsNullOrEmpty(path)) return;
            }

            string err = AnimationExporter.SaveMultiFrameClip(ClipModel, path);
            if (err != null) setStatus(err, 3);
            else             setStatus(DenEmoLoc.Tf("dlg.save.done.msg", path), 1);
        }

        // ─── Private helpers ──────────────────────────────────────────────────

        private void CreateNewClip(ShapeKeyModel shapeModel, string saveFolder, EditorWindow window)
        {
            string defaultName = shapeModel.TargetObject
                ? shapeModel.TargetObject.name + "_anim"
                : "blendshape_anim";
            string path = EditorUtility.SaveFilePanelInProject(
                DenEmoLoc.T("save.panel.title"), defaultName + ".anim", "anim",
                DenEmoLoc.T("save.panel.hint"), saveFolder);
            if (string.IsNullOrEmpty(path)) return;

            var clip = new AnimationClip { frameRate = ClipModel.FPS };
            AssetDatabase.CreateAsset(clip, path);
            AssetDatabase.SaveAssets();

            StopPreview();
            ClipModel.SetClip(clip);
            if (shapeModel.TargetSkinnedMesh != null)
                StartPreview(shapeModel);
            window.Repaint();
        }

        private static string GetRelativePath(Transform target, Transform root)
        {
            if (target == root) return "";
            var parts = new List<string>();
            var t = target;
            while (t != null && t != root) { parts.Add(t.name); t = t.parent; }
            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }

        private void EnsureRecStyles()
        {
            if (_recOnStyle == null)
            {
                _recOnStyle = new GUIStyle(EditorStyles.miniButton)
                {
                    fontStyle = FontStyle.Bold,
                    normal    = { textColor = Color.red },
                    hover     = { textColor = Color.red },
                    active    = { textColor = Color.red }
                };
            }
            if (_recOffStyle == null)
            {
                _recOffStyle = new GUIStyle(EditorStyles.miniButton)
                {
                    normal = { textColor = DenEmoTheme.TextTertiary }
                };
            }
        }
    }
}
