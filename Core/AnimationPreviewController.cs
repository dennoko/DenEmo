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
        // SyncValuesFromMesh の対象を評価対象シェイプのみに絞るための名前集合
        private readonly HashSet<string> _sampleNames = new HashSet<string>();
        // 上記名前に対応する ShapeKeyItem 参照（全 Items 走査を避けるための直接リスト）
        private readonly List<ShapeKeyItem> _sampleItems = new List<ShapeKeyItem>();
        private int _sampleItemsGeneration = -1;
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
            _sampleNames.Clear();
            _sampleItems.Clear();
            _sampleItemsGeneration = -1;
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

            // 高頻度で呼ばれるため、同期はトラック対象アイテムのみを直接ループ（全 Items 走査を回避）。
            // Items が作り直された（世代変化）ときだけ参照リストを再収集する。
            if (_sampleItemsGeneration != _shapeModel.ItemsGeneration)
            {
                _shapeModel.CollectItemsByName(_sampleNames, _sampleItems);
                _sampleItemsGeneration = _shapeModel.ItemsGeneration;
            }
            _shapeModel.SyncValues(_sampleItems);
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null) sceneView.Repaint();
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
            _sampleNames.Clear();
            foreach (var track in tracks)
            {
                int index = mesh.GetBlendShapeIndex(track.ShapeName);
                if (index >= 0)
                {
                    _sampleTargets.Add((index, track));
                    _sampleNames.Add(track.ShapeName);
                }
            }
            // 対象名が変わったので、次回 SampleAt でアイテム参照リストを作り直させる。
            _sampleItemsGeneration = -1;
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
