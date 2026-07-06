using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DenEmo.Core
{
    /// <summary>
    /// 任意の AnimationClip の blendShape.* カーブのみをアバター配下の SMR に直接書き込む軽量サンプラ。
    /// AnimationMode は使わない（ボーン非干渉、AnimationPreviewController と同方針）。
    /// 初回 Apply 時に対象 SMR のウェイトを退避し、Restore() で完全に復元する。
    /// </summary>
    public class FxClipSampler
    {
        private Transform _avatarRoot;
        private readonly Dictionary<SkinnedMeshRenderer, float[]> _savedWeights
            = new Dictionary<SkinnedMeshRenderer, float[]>();

        // クリップごとの評価ターゲットキャッシュ
        private AnimationClip _cachedClip;
        private readonly List<(SkinnedMeshRenderer smr, int shapeIndex, AnimationCurve curve)> _targets
            = new List<(SkinnedMeshRenderer, int, AnimationCurve)>();

        public bool HasCapture => _savedWeights.Count > 0;

        public void SetRoot(Transform avatarRoot)
        {
            if (_avatarRoot == avatarRoot) return;
            Restore();
            _avatarRoot = avatarRoot;
            InvalidateCache();
        }

        /// <summary>指定時刻のカーブ値を SMR に書き込む。対象 SMR は初回書き込み前にウェイトを退避する。</summary>
        public void Apply(AnimationClip clip, float time)
        {
            if (clip == null || _avatarRoot == null) return;

            EnsureTargets(clip);

            foreach (var (smr, shapeIndex, curve) in _targets)
            {
                if (smr == null || smr.sharedMesh == null) continue;
                CaptureIfNeeded(smr);
                smr.SetBlendShapeWeight(shapeIndex, curve.Evaluate(time));
            }
        }

        /// <summary>退避済みウェイトをすべて書き戻し、退避をクリアする。</summary>
        public void Restore()
        {
            foreach (var kvp in _savedWeights)
            {
                var smr = kvp.Key;
                if (smr == null || smr.sharedMesh == null) continue;
                int count = Mathf.Min(kvp.Value.Length, smr.sharedMesh.blendShapeCount);
                for (int i = 0; i < count; i++)
                    smr.SetBlendShapeWeight(i, kvp.Value[i]);
            }
            _savedWeights.Clear();
        }

        public void InvalidateCache()
        {
            _cachedClip = null;
            _targets.Clear();
        }

        private void CaptureIfNeeded(SkinnedMeshRenderer smr)
        {
            if (_savedWeights.ContainsKey(smr)) return;
            int count = smr.sharedMesh.blendShapeCount;
            var weights = new float[count];
            for (int i = 0; i < count; i++)
                weights[i] = smr.GetBlendShapeWeight(i);
            _savedWeights.Add(smr, weights);
        }

        private void EnsureTargets(AnimationClip clip)
        {
            if (_cachedClip == clip) return;
            _cachedClip = clip;
            _targets.Clear();

            foreach (var b in AnimationUtility.GetCurveBindings(clip))
            {
                if (b.type != typeof(SkinnedMeshRenderer)) continue;
                if (!b.propertyName.StartsWith("blendShape.")) continue;

                var tr = string.IsNullOrEmpty(b.path) ? _avatarRoot : _avatarRoot.Find(b.path);
                if (tr == null) continue;
                var smr = tr.GetComponent<SkinnedMeshRenderer>();
                if (smr == null || smr.sharedMesh == null) continue;

                string shapeName = b.propertyName.Substring("blendShape.".Length);
                int index = smr.sharedMesh.GetBlendShapeIndex(shapeName);
                if (index < 0) continue;

                var curve = AnimationUtility.GetEditorCurve(clip, b);
                if (curve == null) continue;

                _targets.Add((smr, index, curve));
            }
        }
    }
}
