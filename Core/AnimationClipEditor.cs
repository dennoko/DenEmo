using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using DenEmo.Models;

namespace DenEmo.Core
{
    /// <summary>
    /// クリップへのすべてのキーフレーム変更操作を一手に引き受けるクラス。
    /// 各操作は「Undo.RecordObject → カーブ変更 → SetEditorCurve → SetDirty → model.MarkDirty()」
    /// を必ずこの順で行い、UI 側のトラックキャッシュとプレビューを自動的に追従させる。
    /// </summary>
    public class AnimationClipEditor
    {
        private readonly AnimationClipModel _model;

        public AnimationClipEditor(AnimationClipModel model)
        {
            _model = model;
        }

        private AnimationClip Clip => _model.Clip;

        // ─── Clip settings ───────────────────────────────────────────────────

        public void SetFrameRate(float fps)
        {
            if (Clip == null || fps <= 0f) return;
            Undo.RecordObject(Clip, "Change FPS");
            Clip.frameRate = fps;
            _model.FPS = fps;
            Commit();
        }

        // ─── Keyframe write ──────────────────────────────────────────────────

        /// <summary>指定時刻にキーを記録（既存キーは上書き）。</summary>
        public void RecordKey(string shapeName, float time, float value, InterpolationType interp)
        {
            if (Clip == null) return;

            var binding = MakeBinding(shapeName);
            Undo.RecordObject(Clip, "Record Keyframe");

            var curve = AnimationUtility.GetEditorCurve(Clip, binding) ?? new AnimationCurve();
            WriteKey(curve, time, value, interp, _model.FrameTolerance);

            AnimationUtility.SetEditorCurve(Clip, binding, curve);
            Commit();
        }

        /// <summary>複数シェイプのキーを 1 回の Undo 操作でまとめて記録する。</summary>
        public void RecordKeys(IEnumerable<(string shapeName, float value)> entries, float time, InterpolationType interp)
        {
            if (Clip == null) return;

            Undo.RecordObject(Clip, "Insert Keyframe (All)");
            float tol = _model.FrameTolerance;
            bool changed = false;

            foreach (var (shapeName, value) in entries)
            {
                var binding = MakeBinding(shapeName);
                var curve = AnimationUtility.GetEditorCurve(Clip, binding) ?? new AnimationCurve();
                WriteKey(curve, time, value, interp, tol);
                AnimationUtility.SetEditorCurve(Clip, binding, curve);
                changed = true;
            }

            if (changed) Commit();
        }

        // ─── Keyframe delete ─────────────────────────────────────────────────

        /// <summary>指定時刻に最も近いキーを 1 つ削除する。</summary>
        public void DeleteKey(string shapeName, float time)
        {
            if (Clip == null) return;

            var binding = MakeBinding(shapeName);
            var curve   = AnimationUtility.GetEditorCurve(Clip, binding);
            if (curve == null) return;

            int idx = AnimationClipModel.FindKeyIndex(curve, time, _model.FrameTolerance);
            if (idx < 0) return;

            Undo.RecordObject(Clip, "Delete Keyframe");
            curve.RemoveKey(idx);
            AnimationUtility.SetEditorCurve(Clip, binding, curve.keys.Length > 0 ? curve : null);
            Commit();
        }

        /// <summary>指定時刻にある全トラックのキーを 1 回の Undo 操作で削除する。</summary>
        public void DeleteKeysAtTime(float time)
        {
            if (Clip == null) return;

            float tol = _model.FrameTolerance;
            Undo.RecordObject(Clip, "Delete Frame Keys");
            bool changed = false;

            foreach (var track in _model.Tracks)
            {
                int idx = AnimationClipModel.FindKeyIndex(track.Curve, time, tol);
                if (idx < 0) continue;
                track.Curve.RemoveKey(idx);
                AnimationUtility.SetEditorCurve(Clip, track.Binding, track.Curve.keys.Length > 0 ? track.Curve : null);
                changed = true;
            }

            if (changed) Commit();
        }

        /// <summary>指定シェイプの全キー（トラックごと）を削除する。</summary>
        public void DeleteTrack(string shapeName)
        {
            if (Clip == null) return;
            if (!_model.TryGetTrack(shapeName, out var track)) return;

            Undo.RecordObject(Clip, "Delete Track");
            AnimationUtility.SetEditorCurve(Clip, track.Binding, null);
            Commit();
        }

        // ─── Keyframe move ───────────────────────────────────────────────────

        /// <summary>
        /// oldFrame にあるキーを newFrame 方向へ移動する。途中に他のキーがある場合は
        /// その手前で停止する（ブロッキング）。shapeName が null なら全トラックを同時移動。
        /// 実際に到達したフレームを返す。
        /// </summary>
        public int MoveKeys(int oldFrame, int newFrame, string shapeName = null)
        {
            if (Clip == null || oldFrame == newFrame) return oldFrame;

            float fps = _model.FPS > 0f ? _model.FPS : 60f;
            newFrame = Mathf.Clamp(newFrame, 0, _model.TotalFrames);
            int dir = newFrame > oldFrame ? 1 : -1;

            // 移動対象（oldFrame にキーを持つトラック）と、各トラックの到達限界を求める
            var moving = new List<(AnimationTrack track, int keyIndex)>();
            int reached = newFrame;

            foreach (var track in _model.Tracks)
            {
                if (shapeName != null && track.ShapeName != shapeName) continue;

                int keyIndex = -1;
                int limit    = newFrame;
                var keys     = track.Curve.keys;
                for (int i = 0; i < keys.Length; i++)
                {
                    int f = Mathf.RoundToInt(keys[i].time * fps);
                    if (f == oldFrame) { keyIndex = i; continue; }
                    // 移動方向にある最初の他キーの 1 フレーム手前まで
                    if (dir > 0 && f > oldFrame && f - 1 < limit) limit = f - 1;
                    if (dir < 0 && f < oldFrame && f + 1 > limit) limit = f + 1;
                }

                if (keyIndex < 0) continue;
                moving.Add((track, keyIndex));
                if (dir > 0) reached = Mathf.Min(reached, limit);
                else         reached = Mathf.Max(reached, limit);
            }

            if (moving.Count == 0 || reached == oldFrame) return oldFrame;

            Undo.RecordObject(Clip, shapeName == null ? "Move All Keyframes" : "Move Keyframe");
            float newTime = reached / fps;

            foreach (var (track, keyIndex) in moving)
            {
                var k = track.Curve.keys[keyIndex];
                k.time = newTime;
                track.Curve.MoveKey(keyIndex, k);
                AnimationUtility.SetEditorCurve(Clip, track.Binding, track.Curve);
            }

            Commit();
            return reached;
        }

        // ─── Interpolation ───────────────────────────────────────────────────

        /// <summary>指定時刻に最も近いキーの補間タイプを変更する。</summary>
        public void SetKeyInterpolation(string shapeName, float time, InterpolationType interp)
        {
            if (Clip == null) return;
            if (!_model.TryGetTrack(shapeName, out var track)) return;

            int idx = AnimationClipModel.FindKeyIndex(track.Curve, time, _model.FrameTolerance);
            if (idx < 0) return;

            Undo.RecordObject(Clip, "Change Interpolation");
            ApplyTangentMode(track.Curve, idx, interp);
            AnimationUtility.SetEditorCurve(Clip, track.Binding, track.Curve);
            Commit();
        }

        /// <summary>全トラックの全キーの補間タイプを一括変更する。</summary>
        public void SetAllKeysInterpolation(InterpolationType interp)
        {
            if (Clip == null) return;

            Undo.RecordObject(Clip, "Change All Interpolation");
            bool changed = false;

            foreach (var track in _model.Tracks)
            {
                for (int i = 0; i < track.Curve.keys.Length; i++)
                    ApplyTangentMode(track.Curve, i, interp);
                AnimationUtility.SetEditorCurve(Clip, track.Binding, track.Curve);
                changed = true;
            }

            if (changed) Commit();
        }

        // ─── Value correction ────────────────────────────────────────────────

        /// <summary>
        /// 各シェイプのキー値を new_v = v * (max - min) / 100 + min で再スケールする。
        /// min=0 / max=100（既定値）のシェイプはスキップ。変更があれば true。
        /// </summary>
        public bool ApplyValueCorrection(
            IReadOnlyDictionary<string, float> minValues,
            IReadOnlyDictionary<string, float> maxValues)
        {
            if (Clip == null) return false;

            Undo.RecordObject(Clip, "Apply Shape Key Correction");
            bool changed = false;

            foreach (var track in _model.Tracks)
            {
                float min = minValues.TryGetValue(track.ShapeName, out var storedMin) ? storedMin : 0f;
                float max = maxValues.TryGetValue(track.ShapeName, out var storedMax) ? storedMax : 100f;
                if (Mathf.Approximately(min, 0f) && Mathf.Approximately(max, 100f)) continue;

                float scale  = (max - min) / 100f;
                float offset = min;

                var keys = track.Curve.keys;
                for (int i = 0; i < keys.Length; i++)
                {
                    var k = keys[i];
                    var newKey = new Keyframe(
                        k.time,
                        Mathf.Clamp(k.value * scale + offset, 0f, 100f),
                        k.inTangent  * scale,
                        k.outTangent * scale)
                    {
                        weightedMode = k.weightedMode,
                        inWeight     = k.inWeight,
                        outWeight    = k.outWeight,
                    };
                    keys[i] = newKey;
                }

                track.Curve.keys = keys;
                AnimationUtility.SetEditorCurve(Clip, track.Binding, track.Curve);
                changed = true;
            }

            if (changed) Commit();
            return changed;
        }

        // ─── Private helpers ─────────────────────────────────────────────────

        private void Commit()
        {
            EditorUtility.SetDirty(Clip);
            _model.MarkDirty();
        }

        private EditorCurveBinding MakeBinding(string shapeName)
        {
            return new EditorCurveBinding
            {
                type         = typeof(SkinnedMeshRenderer),
                path         = _model.SmrPath ?? "",
                propertyName = "blendShape." + shapeName,
            };
        }

        private static void WriteKey(AnimationCurve curve, float time, float value, InterpolationType interp, float tol)
        {
            int existing = AnimationClipModel.FindKeyIndex(curve, time, tol);
            if (existing >= 0) curve.RemoveKey(existing);

            int idx = curve.AddKey(new Keyframe(time, value));
            if (idx < 0)
            {
                idx = AnimationClipModel.FindKeyIndex(curve, time, 0f);
                if (idx < 0) return;
            }
            ApplyTangentMode(curve, idx, interp);
        }

        internal static void ApplyTangentMode(AnimationCurve curve, int idx, InterpolationType interp)
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
    }
}
