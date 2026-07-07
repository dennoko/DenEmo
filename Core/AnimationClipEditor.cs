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

        /// <summary>
        /// 指定時刻にキーを記録（既存キーは上書き）。
        /// recordUndo=false でドラッグジェスチャ中の 2 回目以降の Undo スナップショットを省略できる
        /// （Undo.RecordObject はクリップ全体をシリアライズするため、同一 Undo グループ内では 1 回で十分）。
        /// </summary>
        public void RecordKey(string shapeName, float time, float value, InterpolationType interp, bool recordUndo = true)
        {
            if (Clip == null) return;

            var binding = MakeBinding(shapeName);
            if (recordUndo) Undo.RecordObject(Clip, "Record Keyframe");

            var curve = AnimationUtility.GetEditorCurve(Clip, binding) ?? new AnimationCurve();
            WriteKey(curve, time, value, interp, _model.FrameTolerance);

            AnimationUtility.SetEditorCurve(Clip, binding, curve);
            CommitTrack(shapeName, curve);
        }

        /// <summary>複数シェイプのキーを 1 回の Undo 操作でまとめて記録する。</summary>
        public void RecordKeys(IEnumerable<(string shapeName, float value)> entries, float time, InterpolationType interp)
        {
            if (Clip == null) return;

            Undo.RecordObject(Clip, "Insert Keyframe (All)");
            float tol = _model.FrameTolerance;
            var bindings = new List<EditorCurveBinding>();
            var curves   = new List<AnimationCurve>();

            foreach (var (shapeName, value) in entries)
            {
                var binding = MakeBinding(shapeName);
                var curve = AnimationUtility.GetEditorCurve(Clip, binding) ?? new AnimationCurve();
                WriteKey(curve, time, value, interp, tol);
                bindings.Add(binding);
                curves.Add(curve);
            }

            if (bindings.Count > 0)
            {
                SetEditorCurvesBatch(bindings, curves);
                Commit();
            }
        }

        /// <summary>
        /// REC スロットルフラッシュ用: 保留中の複数シェイプを 1 回の SetEditorCurves コミットでまとめて記録する。
        /// キャッシュは全無効化（MarkDirty）せず該当トラックのみ増分更新するため、プレビューのサンプル対象マップ
        /// （Revision + Mesh キャッシュ）が毎フラッシュ再構築されない。recordUndo=false でジェスチャ内の
        /// 2 回目以降の Undo スナップショットを省略する。未知シェイプ（新規トラック）が含まれる場合のみ全再構築へフォールバック。
        /// </summary>
        public void RecordKeysThrottled(IReadOnlyList<(string shapeName, float value)> entries, float time, InterpolationType interp, bool recordUndo)
        {
            if (Clip == null || entries.Count == 0) return;

            if (recordUndo) Undo.RecordObject(Clip, "Record Keyframe");
            float tol = _model.FrameTolerance;

            var bindings = new List<EditorCurveBinding>(entries.Count);
            var curves   = new List<AnimationCurve>(entries.Count);
            var updates  = new List<(string, AnimationCurve)>(entries.Count);

            foreach (var (shapeName, value) in entries)
            {
                var binding = MakeBinding(shapeName);
                var curve = AnimationUtility.GetEditorCurve(Clip, binding) ?? new AnimationCurve();
                WriteKey(curve, time, value, interp, tol);
                bindings.Add(binding);
                curves.Add(curve);
                updates.Add((shapeName, curve));
            }

            SetEditorCurvesBatch(bindings, curves);
            EditorUtility.SetDirty(Clip);
            if (!_model.UpdateTrackCurvesBatch(updates))
                _model.MarkDirty();
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
            bool hasKeys = curve.length > 0;
            AnimationUtility.SetEditorCurve(Clip, binding, hasKeys ? curve : null);
            if (hasKeys) CommitTrack(shapeName, curve);
            else         Commit();
        }

        /// <summary>指定時刻にある全トラックのキーを 1 回の Undo 操作で削除する。</summary>
        public void DeleteKeysAtTime(float time)
        {
            if (Clip == null) return;

            float tol = _model.FrameTolerance;
            Undo.RecordObject(Clip, "Delete Frame Keys");
            var bindings = new List<EditorCurveBinding>();
            var curves   = new List<AnimationCurve>();

            foreach (var track in _model.Tracks)
            {
                int idx = AnimationClipModel.FindKeyIndex(track.Curve, time, tol);
                if (idx < 0) continue;
                track.Curve.RemoveKey(idx);
                bindings.Add(track.Binding);
                curves.Add(track.Curve.length > 0 ? track.Curve : null);
            }

            if (bindings.Count > 0)
            {
                SetEditorCurvesBatch(bindings, curves);
                Commit();
            }
        }

        /// <summary>
        /// 指定時刻より後ろにある全キーを削除する（クリップ長短縮時の整理用）。
        /// 削除したキー数を返す。
        /// </summary>
        public int DeleteKeysAfter(float time)
        {
            if (Clip == null) return 0;

            float tol = _model.FrameTolerance;
            Undo.RecordObject(Clip, "Delete Keys Beyond Length");
            var bindings = new List<EditorCurveBinding>();
            var curves   = new List<AnimationCurve>();
            int deleted  = 0;

            foreach (var track in _model.Tracks)
            {
                var keys = track.Curve.keys;
                int removeCount = 0;
                for (int i = keys.Length - 1; i >= 0; i--)
                {
                    if (keys[i].time <= time + tol) break;
                    track.Curve.RemoveKey(i);
                    removeCount++;
                }
                if (removeCount == 0) continue;

                deleted += removeCount;
                bindings.Add(track.Binding);
                curves.Add(track.Curve.length > 0 ? track.Curve : null);
            }

            if (deleted > 0)
            {
                SetEditorCurvesBatch(bindings, curves);
                Commit();
            }
            return deleted;
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
        /// recordUndo はドラッグジェスチャの最初の移動でのみ true を渡せば十分。
        /// </summary>
        public int MoveKeys(int oldFrame, int newFrame, string shapeName = null, bool recordUndo = true)
        {
            if (Clip == null || oldFrame == newFrame) return oldFrame;

            float fps = _model.FPS > 0f ? _model.FPS : 60f;
            newFrame = Mathf.Clamp(newFrame, 0, _model.TotalFrames);
            int dir = newFrame > oldFrame ? 1 : -1;

            // 移動対象（oldFrame にキーを持つトラック）と、各トラックの到達限界を求める。
            // 限界計算はキャッシュ済み KeyTimes（float[]）だけで足りるため、ドラッグ中の毎イベントで
            // curve.keys（配列フルコピー）を触らない。
            var moving = new List<(AnimationTrack track, int keyIndex)>();
            int reached = newFrame;

            foreach (var track in _model.Tracks)
            {
                if (shapeName != null && track.ShapeName != shapeName) continue;

                int keyIndex = -1;
                int limit    = newFrame;
                var times    = track.KeyTimes;
                for (int i = 0; i < times.Length; i++)
                {
                    int f = Mathf.RoundToInt(times[i] * fps);
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

            if (recordUndo)
                Undo.RecordObject(Clip, shapeName == null ? "Move All Keyframes" : "Move Keyframe");
            float newTime = reached / fps;

            // 全トラックの変更を集めて 1 回でコミットする（T 回のクリップ内部再構築を回避）。
            var bindings = new List<EditorCurveBinding>(moving.Count);
            var curves   = new List<AnimationCurve>(moving.Count);
            var updates  = new List<(string, AnimationCurve)>(moving.Count);
            foreach (var (track, keyIndex) in moving)
            {
                var k = track.Curve[keyIndex];   // インデクサは配列全体をコピーしない
                k.time = newTime;
                track.Curve.MoveKey(keyIndex, k);
                bindings.Add(track.Binding);
                curves.Add(track.Curve);
                updates.Add((track.ShapeName, track.Curve));
            }

            SetEditorCurvesBatch(bindings, curves);
            EditorUtility.SetDirty(Clip);
            // キャッシュは全再構築せず、移動したトラックのみ更新する（AllKeyTimes は 1 回だけ遅延再構築）。
            if (!_model.UpdateTrackCurvesBatch(updates))
                _model.MarkDirty();

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
            CommitTrack(shapeName, track.Curve);
        }

        /// <summary>全トラックの全キーの補間タイプを一括変更する。</summary>
        public void SetAllKeysInterpolation(InterpolationType interp)
        {
            if (Clip == null) return;

            Undo.RecordObject(Clip, "Change All Interpolation");
            var bindings = new List<EditorCurveBinding>();
            var curves   = new List<AnimationCurve>();

            foreach (var track in _model.Tracks)
            {
                int keyCount = track.Curve.length;   // ループ条件で毎回 keys 配列をコピーしない
                for (int i = 0; i < keyCount; i++)
                    ApplyTangentMode(track.Curve, i, interp);
                bindings.Add(track.Binding);
                curves.Add(track.Curve);
            }

            if (bindings.Count > 0)
            {
                SetEditorCurvesBatch(bindings, curves);
                Commit();
            }
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
            var bindings = new List<EditorCurveBinding>();
            var curves   = new List<AnimationCurve>();

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
                bindings.Add(track.Binding);
                curves.Add(track.Curve);
            }

            if (bindings.Count > 0)
            {
                SetEditorCurvesBatch(bindings, curves);
                Commit();
                return true;
            }
            return false;
        }

        // ─── Private helpers ─────────────────────────────────────────────────

        private void Commit()
        {
            EditorUtility.SetDirty(Clip);
            _model.MarkDirty();
        }

        /// <summary>単一トラック変更用のコミット。キャッシュ全体を無効化せず該当トラックのみ更新する。</summary>
        private void CommitTrack(string shapeName, AnimationCurve curve)
        {
            EditorUtility.SetDirty(Clip);
            _model.UpdateTrackCurve(shapeName, curve);
        }

        /// <summary>
        /// 複数カーブの一括書き込み。SetEditorCurve はクリップ全体の再構築を伴うため、
        /// 対応バージョンでは複数形 API でまとめてコミットする（A-4 対策）。
        /// curves の要素に null を渡すとそのバインディングのカーブを削除する。
        /// </summary>
        private void SetEditorCurvesBatch(List<EditorCurveBinding> bindings, List<AnimationCurve> curves)
        {
#if UNITY_2022_1_OR_NEWER
            AnimationUtility.SetEditorCurves(Clip, bindings.ToArray(), curves.ToArray());
#else
            for (int i = 0; i < bindings.Count; i++)
                AnimationUtility.SetEditorCurve(Clip, bindings[i], curves[i]);
#endif
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
