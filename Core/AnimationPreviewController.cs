using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using DenEmo.Models;

namespace DenEmo.Core
{
    /// <summary>
    /// AnimationMode を使わないブレンドシェイプ専用のエディタプレビュー。
    /// （AnimationMode.StartAnimationMode はボーントランスフォームまでリセットするため使わない）
    /// blendShape.* カーブのみを評価して SMR にウェイトを直接書き込む。
    /// カーブは AnimationClipModel のトラックキャッシュを参照し、Revision の変化で
    /// インデックスマップだけを再構築する。
    /// </summary>
    public class AnimationPreviewController
    {
        private AnimationClipModel _clipModel;
        private ShapeKeyModel      _shapeModel;
        private bool               _isActive;
        private float[]            _savedWeights;

        // ブレンドシェイプインデックス → 評価対象トラック のマップ
        private readonly List<(int blendShapeIndex, AnimationTrack track)> _sampleTargets
            = new List<(int, AnimationTrack)>();
        private int _mappedRevision = -1;
        private Mesh _mappedMesh;

        public bool IsActive => _isActive;

        // ─── Preview lifecycle ────────────────────────────────────────────────

        public void Start(AnimationClipModel clipModel, ShapeKeyModel shapeModel)
        {
            _clipModel  = clipModel;
            _shapeModel = shapeModel;
            _mappedRevision = -1;

            SaveWeights();
            _isActive = true;
            SampleAt(clipModel.CurrentTime);
        }

        public void Stop()
        {
            if (!_isActive) return;
            RestoreWeights();
            _isActive = false;
            _sampleTargets.Clear();
            _mappedRevision = -1;
        }

        // ─── Sampling ─────────────────────────────────────────────────────────

        /// <summary>
        /// 指定時刻のカーブ値を SMR のウェイトに反映する。ボーンには一切触れない。
        /// </summary>
        public void SampleAt(float time)
        {
            if (!_isActive) return;
            if (_clipModel?.Clip == null) return;

            var smr = _shapeModel?.TargetSkinnedMesh;
            if (smr == null || smr.sharedMesh == null) return;

            EnsureSampleTargets(smr);

            foreach (var (index, track) in _sampleTargets)
                smr.SetBlendShapeWeight(index, track.PreviewCurve.Evaluate(time));

            _shapeModel.SyncValuesFromMesh();
            SceneView.RepaintAll();
        }

        private void EnsureSampleTargets(SkinnedMeshRenderer smr)
        {
            var mesh = smr.sharedMesh;
            if (_mappedRevision == _clipModel.Revision && _mappedMesh == mesh) return;

            // Revision を確定させてからマップを作る（Tracks アクセスでキャッシュが再構築される）
            var tracks = _clipModel.Tracks;
            _mappedRevision = _clipModel.Revision;
            _mappedMesh     = mesh;

            _sampleTargets.Clear();
            foreach (var track in tracks)
            {
                int index = mesh.GetBlendShapeIndex(track.ShapeName);
                if (index >= 0) _sampleTargets.Add((index, track));
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
            if (smr == null || smr.sharedMesh == null || _savedWeights == null) { _savedWeights = null; return; }
            int count = Mathf.Min(_savedWeights.Length, smr.sharedMesh.blendShapeCount);
            for (int i = 0; i < count; i++)
                smr.SetBlendShapeWeight(i, _savedWeights[i]);
            _savedWeights = null;
        }
    }
}
