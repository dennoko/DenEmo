using UnityEditor;
using UnityEngine;
using DenEmo.Models;

namespace DenEmo.Core
{
    /// <summary>
    /// Wraps UnityEditor.AnimationMode to provide editor-preview and keyframe-recording
    /// for blendshape animations without entering Play Mode.
    /// </summary>
    public class AnimationPreviewController
    {
        private AnimationClipModel _clipModel;
        private ShapeKeyModel      _shapeModel;

        public bool IsActive => AnimationMode.InAnimationMode();

        // ─── Preview lifecycle ────────────────────────────────────────────────

        public void Start(AnimationClipModel clipModel, ShapeKeyModel shapeModel)
        {
            _clipModel  = clipModel;
            _shapeModel = shapeModel;
            if (!AnimationMode.InAnimationMode())
                AnimationMode.StartAnimationMode();
            SampleAt(clipModel.CurrentTime);
        }

        public void Stop()
        {
            if (AnimationMode.InAnimationMode())
                AnimationMode.StopAnimationMode();
        }

        // ─── Sampling ─────────────────────────────────────────────────────────

        /// <summary>Applies the clip at the given time to the mesh in the scene.</summary>
        public void SampleAt(float time)
        {
            if (_clipModel?.Clip == null || _shapeModel?.TargetSkinnedMesh == null) return;
            if (!AnimationMode.InAnimationMode()) return;

            var root = _shapeModel.TargetSkinnedMesh.transform.root.gameObject;

            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(root, _clipModel.Clip, time);
            AnimationMode.EndSampling();

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

            // Remove existing key at the same time, if any
            float tol = _clipModel.FPS > 0f ? 0.5f / _clipModel.FPS : 0.01f;
            int existing = FindKeyAtTime(curve, time, tol);
            if (existing >= 0) curve.RemoveKey(existing);

            // Add the new key
            var newKey = new Keyframe(time, value);
            int idx = curve.AddKey(newKey);
            if (idx < 0) idx = 0; // AddKey may return -1 on duplicates; reuse 0

            ApplyTangentMode(curve, idx, interp);

            AnimationUtility.SetEditorCurve(_clipModel.Clip, binding, curve);
            EditorUtility.SetDirty(_clipModel.Clip);
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
            int idx = FindKeyAtTime(curve, time, tol);
            if (idx < 0) return;

            curve.RemoveKey(idx);
            AnimationUtility.SetEditorCurve(_clipModel.Clip, binding, curve.keys.Length > 0 ? curve : null);
            EditorUtility.SetDirty(_clipModel.Clip);
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
            int idx = FindKeyAtTime(curve, time, tol);
            if (idx < 0) return;

            ApplyTangentMode(curve, idx, interp);
            AnimationUtility.SetEditorCurve(_clipModel.Clip, binding, curve);
            EditorUtility.SetDirty(_clipModel.Clip);
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

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
                    AnimationUtility.SetKeyLeftTangentMode(curve,  idx, AnimationUtility.TangentMode.Auto);
                    AnimationUtility.SetKeyRightTangentMode(curve, idx, AnimationUtility.TangentMode.Auto);
                    break;
            }
        }

        private static int FindKeyAtTime(AnimationCurve curve, float time, float tol)
        {
            for (int i = 0; i < curve.keys.Length; i++)
                if (Mathf.Abs(curve.keys[i].time - time) <= tol) return i;
            return -1;
        }
    }
}
