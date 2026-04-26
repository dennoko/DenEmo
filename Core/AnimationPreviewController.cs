using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using DenEmo.Models;

namespace DenEmo.Core
{
    /// <summary>
    /// Provides editor-preview and keyframe-recording for blendshape animations
    /// without AnimationMode (which also resets bone transforms).
    /// Evaluates only blendShape.* curves and writes them to the SMR.
    /// Curves are cached per blendshape index and rebuilt only when the clip changes.
    /// </summary>
    public class AnimationPreviewController
    {
        private AnimationClipModel _clipModel;
        private ShapeKeyModel      _shapeModel;
        private bool               _isActive;
        private float[]            _savedWeights;
        private string             _smrPath;

        // ─── Curve cache ──────────────────────────────────────────────────────
        // Indexed by blendshape index; null entry = no curve for that shape.
        // Rebuilt lazily on the first SampleAt after any clip mutation.
        private AnimationCurve[] _curveCache;
        private bool             _cacheDirty = true;

        public bool IsActive => _isActive;

        // ─── Preview lifecycle ────────────────────────────────────────────────

        public void Start(AnimationClipModel clipModel, ShapeKeyModel shapeModel)
        {
            _clipModel  = clipModel;
            _shapeModel = shapeModel;

            var smr = shapeModel?.TargetSkinnedMesh;
            _smrPath = smr != null ? GetRelativePath(smr.transform, smr.transform.root) : "";

            _cacheDirty = true;
            SaveWeights();
            _isActive = true;
            SampleAt(clipModel.CurrentTime);
        }

        public void Stop()
        {
            if (!_isActive) return;
            RestoreWeights();
            _isActive   = false;
            _curveCache = null;
        }

        // ─── Sampling ─────────────────────────────────────────────────────────

        /// <summary>
        /// Evaluates blendShape curves at <paramref name="time"/> using the cached
        /// AnimationCurve array, then applies results to the SMR.
        /// Bone transforms are never touched.
        /// </summary>
        public void SampleAt(float time)
        {
            if (_clipModel?.Clip == null || _shapeModel?.TargetSkinnedMesh == null) return;
            if (!_isActive) return;

            if (_cacheDirty) RebuildCurveCache();
            if (_curveCache == null) return;

            var smr = _shapeModel.TargetSkinnedMesh;
            for (int i = 0; i < _curveCache.Length; i++)
            {
                if (_curveCache[i] == null) continue;
                smr.SetBlendShapeWeight(i, _curveCache[i].Evaluate(time));
            }

            _shapeModel.SyncValuesFromMesh();
            SceneView.RepaintAll();
        }

        // ─── Keyframe writing ─────────────────────────────────────────────────

        /// <summary>Records (or overwrites) a keyframe for the given blendshape.</summary>
        public void RecordKeyframe(string shapeName, string smrPath, float time, float value, InterpolationType interp)
        {
            if (_clipModel?.Clip == null) return;

            var binding = MakeBinding(shapeName, smrPath);
            Undo.RecordObject(_clipModel.Clip, "Record Keyframe");

            var curve = AnimationUtility.GetEditorCurve(_clipModel.Clip, binding) ?? new AnimationCurve();

            float tol      = _clipModel.FPS > 0f ? 0.5f / _clipModel.FPS : 0.01f;
            int   existing = FindKeyAtTime(curve, time, tol);
            if (existing >= 0) curve.RemoveKey(existing);

            int idx = curve.AddKey(new Keyframe(time, value));
            if (idx < 0)
            {
                idx = FindKeyAtTime(curve, time, 0f);
                if (idx < 0)
                {
                    AnimationUtility.SetEditorCurve(_clipModel.Clip, binding, curve);
                    EditorUtility.SetDirty(_clipModel.Clip);
                    _cacheDirty = true;
                    return;
                }
            }

            ApplyTangentMode(curve, idx, interp);
            AnimationUtility.SetEditorCurve(_clipModel.Clip, binding, curve);
            EditorUtility.SetDirty(_clipModel.Clip);
            _cacheDirty = true;
        }

        /// <summary>Deletes the keyframe closest to the given time for the given blendshape.</summary>
        public void DeleteKeyframe(string shapeName, string smrPath, float time)
        {
            if (_clipModel?.Clip == null) return;

            var binding = MakeBinding(shapeName, smrPath);
            var curve   = AnimationUtility.GetEditorCurve(_clipModel.Clip, binding);
            if (curve == null) return;

            Undo.RecordObject(_clipModel.Clip, "Delete Keyframe");

            float tol = _clipModel.FPS > 0f ? 0.5f / _clipModel.FPS : 0.01f;
            int   idx = FindKeyAtTime(curve, time, tol);
            if (idx < 0) return;

            curve.RemoveKey(idx);
            AnimationUtility.SetEditorCurve(_clipModel.Clip, binding, curve.keys.Length > 0 ? curve : null);
            EditorUtility.SetDirty(_clipModel.Clip);
            _cacheDirty = true;
        }

        /// <summary>Deletes all keyframes for the given blendshape.</summary>
        public void DeleteAllKeyframesForShape(string shapeName, string smrPath)
        {
            if (_clipModel?.Clip == null) return;

            var binding = MakeBinding(shapeName, smrPath);
            var curve   = AnimationUtility.GetEditorCurve(_clipModel.Clip, binding);
            if (curve == null) return;

            Undo.RecordObject(_clipModel.Clip, "Delete All Keyframes");

            AnimationUtility.SetEditorCurve(_clipModel.Clip, binding, null);
            EditorUtility.SetDirty(_clipModel.Clip);
            _cacheDirty = true;
        }

        /// <summary>Deletes keyframes at the given time across all tracks in one operation.</summary>
        public void DeleteAllKeyframesAtTime(string smrPath, float time)
        {
            if (_clipModel?.Clip == null) return;

            float tol = _clipModel.FPS > 0f ? 0.5f / _clipModel.FPS : 0.01f;
            var shapes = new List<string>();
            foreach (var b in AnimationUtility.GetCurveBindings(_clipModel.Clip))
            {
                if (b.type != typeof(SkinnedMeshRenderer)) continue;
                if (!b.propertyName.StartsWith("blendShape.")) continue;
                if (b.path != (smrPath ?? "")) continue;
                var curve = AnimationUtility.GetEditorCurve(_clipModel.Clip, b);
                if (curve == null) continue;
                foreach (var key in curve.keys)
                {
                    if (Mathf.Abs(key.time - time) <= tol)
                    {
                        shapes.Add(b.propertyName.Substring("blendShape.".Length));
                        break;
                    }
                }
            }

            foreach (var shapeName in shapes)
                DeleteKeyframe(shapeName, smrPath, time);
        }

        /// <summary>
        /// Copies the first keyframe value of every track to clipLength, creating a seamless loop.
        /// Collects shapes before writing to avoid mutating the binding list mid-iteration.
        /// </summary>
        public void AddLoopKey(string smrPath, float clipLength, InterpolationType interp)
        {
            if (_clipModel?.Clip == null) return;

            var shapes = new List<(string name, float value)>();
            foreach (var b in AnimationUtility.GetCurveBindings(_clipModel.Clip))
            {
                if (b.type != typeof(SkinnedMeshRenderer)) continue;
                if (!b.propertyName.StartsWith("blendShape.")) continue;
                if (b.path != (smrPath ?? "")) continue;
                var curve = AnimationUtility.GetEditorCurve(_clipModel.Clip, b);
                if (curve == null || curve.keys.Length == 0) continue;
                shapes.Add((b.propertyName.Substring("blendShape.".Length), curve.keys[0].value));
            }

            foreach (var (name, value) in shapes)
                RecordKeyframe(name, smrPath, clipLength, value, interp);
        }

        /// <summary>Changes the interpolation mode of the keyframe closest to the given time.</summary>
        public void ChangeInterpolation(string shapeName, string smrPath, float time, InterpolationType interp)
        {
            if (_clipModel?.Clip == null) return;

            var binding = MakeBinding(shapeName, smrPath);
            var curve   = AnimationUtility.GetEditorCurve(_clipModel.Clip, binding);
            if (curve == null) return;

            Undo.RecordObject(_clipModel.Clip, "Change Interpolation");

            float tol = _clipModel.FPS > 0f ? 0.5f / _clipModel.FPS : 0.01f;
            int   idx = FindKeyAtTime(curve, time, tol);
            if (idx < 0) return;

            ApplyTangentMode(curve, idx, interp);
            AnimationUtility.SetEditorCurve(_clipModel.Clip, binding, curve);
            EditorUtility.SetDirty(_clipModel.Clip);
            _cacheDirty = true;
        }

        /// <summary>Changes the interpolation mode of all keyframes across all tracks for the given SMR.</summary>
        public void ChangeAllKeyframesInterpolation(string smrPath, InterpolationType interp)
        {
            if (_clipModel?.Clip == null) return;

            Undo.RecordObject(_clipModel.Clip, "Change All Interpolation");
            bool changed = false;

            foreach (var binding in AnimationUtility.GetCurveBindings(_clipModel.Clip))
            {
                if (binding.type != typeof(SkinnedMeshRenderer)) continue;
                if (!binding.propertyName.StartsWith("blendShape.")) continue;
                if (binding.path != (smrPath ?? "")) continue;

                var curve = AnimationUtility.GetEditorCurve(_clipModel.Clip, binding);
                if (curve == null) continue;

                for (int i = 0; i < curve.keys.Length; i++)
                {
                    ApplyTangentMode(curve, i, interp);
                }
                AnimationUtility.SetEditorCurve(_clipModel.Clip, binding, curve);
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(_clipModel.Clip);
                _cacheDirty = true;
            }
        }

        // ─── Cache management ─────────────────────────────────────────────────

        private void RebuildCurveCache()
        {
            _cacheDirty = false;
            var smr  = _shapeModel?.TargetSkinnedMesh;
            var clip = _clipModel?.Clip;
            if (smr == null || clip == null || smr.sharedMesh == null)
            {
                _curveCache = null;
                return;
            }

            int count   = smr.sharedMesh.blendShapeCount;
            _curveCache = new AnimationCurve[count];

            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.type != typeof(SkinnedMeshRenderer)) continue;
                if (!binding.propertyName.StartsWith("blendShape.")) continue;
                if (binding.path != _smrPath) continue;

                string shapeName = binding.propertyName.Substring("blendShape.".Length);
                int    index     = smr.sharedMesh.GetBlendShapeIndex(shapeName);
                if (index < 0 || index >= count) continue;

                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve != null) _curveCache[index] = curve;
            }
        }

        // ─── Weight save / restore ────────────────────────────────────────────

        private void SaveWeights()
        {
            var smr = _shapeModel?.TargetSkinnedMesh;
            if (smr == null || smr.sharedMesh == null) { _savedWeights = null; return; }
            int count = smr.sharedMesh.blendShapeCount;
            _savedWeights = new float[count];
            for (int i = 0; i < count; i++)
                _savedWeights[i] = smr.GetBlendShapeWeight(i);
        }

        private void RestoreWeights()
        {
            var smr = _shapeModel?.TargetSkinnedMesh;
            if (smr == null || _savedWeights == null) return;
            int count = Mathf.Min(_savedWeights.Length, smr.sharedMesh.blendShapeCount);
            for (int i = 0; i < count; i++)
                smr.SetBlendShapeWeight(i, _savedWeights[i]);
            _savedWeights = null;
        }

        // ─── Static helpers ───────────────────────────────────────────────────

        private static EditorCurveBinding MakeBinding(string shapeName, string smrPath)
        {
            return new EditorCurveBinding
            {
                type         = typeof(SkinnedMeshRenderer),
                path         = smrPath ?? "",
                propertyName = "blendShape." + shapeName
            };
        }

        private static void ApplyTangentMode(AnimationCurve curve, int idx, InterpolationType interp)
        {
            switch (interp)
            {
                case InterpolationType.Step:
                    AnimationUtility.SetKeyLeftTangentMode(curve,  idx, AnimationUtility.TangentMode.Constant);
                    AnimationUtility.SetKeyRightTangentMode(curve, idx, AnimationUtility.TangentMode.Constant);
                    break;
                case InterpolationType.Linear:
                    AnimationUtility.SetKeyLeftTangentMode(curve,  idx, AnimationUtility.TangentMode.Linear);
                    AnimationUtility.SetKeyRightTangentMode(curve, idx, AnimationUtility.TangentMode.Linear);
                    break;
                case InterpolationType.Ease:
                    AnimationUtility.SetKeyLeftTangentMode(curve,  idx, AnimationUtility.TangentMode.ClampedAuto);
                    AnimationUtility.SetKeyRightTangentMode(curve, idx, AnimationUtility.TangentMode.ClampedAuto);
                    break;
            }
        }

        private static int FindKeyAtTime(AnimationCurve curve, float time, float tol)
        {
            for (int i = 0; i < curve.keys.Length; i++)
                if (Mathf.Abs(curve.keys[i].time - time) <= tol) return i;
            return -1;
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
    }
}
