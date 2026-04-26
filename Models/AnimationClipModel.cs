using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DenEmo.Models
{
    public enum InterpolationType { Step, Linear, Ease }

    public class AnimationClipModel
    {
        public AnimationClip Clip { get; private set; }
        public float CurrentTime  { get; set; }
        public float ClipLength   { get; set; } = 1f;
        public float FPS          { get; set; } = 60f;

        public int CurrentFrame => Mathf.RoundToInt(CurrentTime * FPS);
        public int TotalFrames  => Mathf.RoundToInt(ClipLength  * FPS);

        // ─── Clip management ─────────────────────────────────────────────────

        public void SetClip(AnimationClip clip)
        {
            Clip = clip;
            if (clip != null)
            {
                ClipLength = clip.length > 0f ? clip.length : 1f;
                FPS        = clip.frameRate > 0f ? clip.frameRate : 60f;
            }
        }

        // ─── Keyframe queries ─────────────────────────────────────────────────

        /// <summary>Returns the curve value at the given time for a blendshape (ignores SMR path).</summary>
        public float GetShapeKeyValue(string shapeName, float time)
        {
            if (Clip == null) return 0f;
            string propName = "blendShape." + shapeName;
            foreach (var b in AnimationUtility.GetCurveBindings(Clip))
            {
                if (b.type != typeof(SkinnedMeshRenderer) || b.propertyName != propName) continue;
                var curve = AnimationUtility.GetEditorCurve(Clip, b);
                if (curve != null) return curve.Evaluate(time);
            }
            return 0f;
        }

        /// <summary>Returns true when a keyframe exists within half-a-frame tolerance.</summary>
        public bool HasKeyframeAt(string shapeName, float time, string smrPath = null)
        {
            if (Clip == null) return false;
            float tol = FPS > 0f ? 0.5f / FPS : 0.01f;
            string propName = "blendShape." + shapeName;
            foreach (var b in AnimationUtility.GetCurveBindings(Clip))
            {
                if (b.type != typeof(SkinnedMeshRenderer) || b.propertyName != propName) continue;
                if (smrPath != null && b.path != smrPath) continue;
                var curve = AnimationUtility.GetEditorCurve(Clip, b);
                if (curve == null) continue;
                foreach (var key in curve.keys)
                    if (Mathf.Abs(key.time - time) <= tol) return true;
            }
            return false;
        }

        /// <summary>Returns all shape names that have at least one keyframe in the clip.</summary>
        public List<string> GetShapeNamesWithKeys(string smrPath = null)
        {
            var result = new List<string>();
            if (Clip == null) return result;
            foreach (var b in AnimationUtility.GetCurveBindings(Clip))
            {
                if (b.type != typeof(SkinnedMeshRenderer)) continue;
                if (!b.propertyName.StartsWith("blendShape.")) continue;
                if (smrPath != null && b.path != smrPath) continue;
                result.Add(b.propertyName.Substring("blendShape.".Length));
            }
            return result;
        }

        /// <summary>Returns all keyframe times for a blendshape.</summary>
        public float[] GetKeyTimesForShape(string shapeName, string smrPath = null)
        {
            if (Clip == null) return new float[0];
            string propName = "blendShape." + shapeName;
            foreach (var b in AnimationUtility.GetCurveBindings(Clip))
            {
                if (b.type != typeof(SkinnedMeshRenderer) || b.propertyName != propName) continue;
                if (smrPath != null && b.path != smrPath) continue;
                var curve = AnimationUtility.GetEditorCurve(Clip, b);
                if (curve == null) continue;
                var times = new float[curve.keys.Length];
                for (int i = 0; i < curve.keys.Length; i++) times[i] = curve.keys[i].time;
                return times;
            }
            return new float[0];
        }
    }
}
