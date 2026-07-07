using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DenEmo.Models
{
    public enum InterpolationType { Step, Linear, Ease }

    /// <summary>
    /// クリップ内の 1 ブレンドシェイプ分のトラック情報（キャッシュエントリ）。
    /// Curve はエディタカーブのコピー、PreviewCurve はスムーズループ有効時に
    /// 末尾キーを先頭値へ揃えた評価用カーブ（無効時は Curve と同一参照）。
    /// </summary>
    public class AnimationTrack
    {
        public string             ShapeName;
        public EditorCurveBinding Binding;
        public AnimationCurve     Curve;
        public AnimationCurve     PreviewCurve;
        public float[]            KeyTimes;   // 昇順
    }

    /// <summary>
    /// マルチフレームモードのクリップ状態。
    /// AnimationClip を唯一のデータソースとしつつ、UI クエリ用のトラックキャッシュを保持する。
    /// クリップを変更した側は必ず MarkDirty() を呼ぶこと（AnimationClipEditor 経由なら自動）。
    /// キャッシュは次のアクセス時に一度だけ再構築される。
    /// </summary>
    public class AnimationClipModel
    {
        public AnimationClip Clip { get; private set; }
        public float CurrentTime  { get; set; }

        private float  _clipLength = 1f;
        private float  _fps        = 60f;
        private bool   _smoothLoop;
        private string _smrPath = "";

        public float ClipLength
        {
            get => _clipLength;
            set { if (!Mathf.Approximately(_clipLength, value)) { _clipLength = value; MarkDirty(); } }
        }

        public float FPS
        {
            get => _fps;
            set { if (!Mathf.Approximately(_fps, value)) { _fps = value; MarkDirty(); } }
        }

        /// <summary>ループ対応（末尾値を先頭値に揃える）。プレビューと保存時にのみ反映され、編集中のクリップ自体は変更しない。</summary>
        public bool SmoothLoopEnabled
        {
            get => _smoothLoop;
            set { if (_smoothLoop != value) { _smoothLoop = value; MarkDirty(); } }
        }

        /// <summary>編集対象 SMR のルートからの相対パス。トラックキャッシュのフィルタに使う。</summary>
        public string SmrPath
        {
            get => _smrPath;
            set { value = value ?? ""; if (_smrPath != value) { _smrPath = value; MarkDirty(); } }
        }

        public int   CurrentFrame   => Mathf.RoundToInt(CurrentTime * _fps);
        public int   TotalFrames    => Mathf.RoundToInt(_clipLength * _fps);
        public float FrameTolerance => _fps > 0f ? 0.5f / _fps : 0.01f;

        public float SnapToFrame(float time)
        {
            float t = _fps > 0f ? Mathf.Round(time * _fps) / _fps : time;
            return Mathf.Clamp(t, 0f, _clipLength);
        }

        public float FrameToTime(int frame) => _fps > 0f ? frame / _fps : 0f;
        public int   TimeToFrame(float time) => Mathf.RoundToInt(time * _fps);

        // ─── Clip management ─────────────────────────────────────────────────

        public void SetClip(AnimationClip clip)
        {
            Clip        = clip;
            CurrentTime = 0f;
            if (clip != null)
            {
                _clipLength = clip.length    > 0f ? clip.length    : 1f;
                _fps        = clip.frameRate > 0f ? clip.frameRate : 60f;
            }
            MarkDirty();
        }

        // ─── Track cache ─────────────────────────────────────────────────────

        private int _revision;
        private int _cachedRevision = -1;

        private readonly List<AnimationTrack>               _tracks      = new List<AnimationTrack>();
        private readonly Dictionary<string, AnimationTrack> _trackByName = new Dictionary<string, AnimationTrack>();
        private float[] _allKeyTimes = Array.Empty<float>();
        private bool    _allKeyTimesDirty;

        public int Revision => _revision;

        /// <summary>クリップ・長さ・SMR パス等が変わった後に呼ぶ。キャッシュは次回アクセス時に再構築される。</summary>
        public void MarkDirty() => _revision++;

        /// <summary>
        /// 既存トラック 1 本のカーブ変更をキャッシュへ直接反映する（全トラック再構築を避ける高速パス）。
        /// キャッシュが古い・トラックが存在しない・カーブが空になった場合は MarkDirty() にフォールバックする。
        /// Revision は変更しないため、トラック集合に依存するキャッシュ（サンプル対象マップ等）はそのまま有効。
        /// </summary>
        public void UpdateTrackCurve(string shapeName, AnimationCurve curve)
        {
            if (_cachedRevision != _revision ||
                curve == null || curve.length == 0 ||
                !_trackByName.TryGetValue(shapeName, out var track))
            {
                MarkDirty();
                return;
            }

            ApplyTrackCurve(track, curve);
            _allKeyTimesDirty = true;
        }

        /// <summary>
        /// 複数トラックのカーブ変更をまとめてキャッシュへ反映する（キー移動の全トラック同時移動用）。
        /// AllKeyTimes の再構築は末尾で 1 回だけ遅延させる。いずれかのトラックが未知・空カーブなら
        /// false を返す（呼び出し側は MarkDirty() にフォールバックすること）。
        /// </summary>
        public bool UpdateTrackCurvesBatch(IEnumerable<(string shapeName, AnimationCurve curve)> entries)
        {
            if (_cachedRevision != _revision) return false;

            foreach (var (shapeName, curve) in entries)
            {
                if (curve == null || curve.length == 0 ||
                    !_trackByName.TryGetValue(shapeName, out var track))
                    return false;
                ApplyTrackCurve(track, curve);
            }
            _allKeyTimesDirty = true;
            return true;
        }

        private void ApplyTrackCurve(AnimationTrack track, AnimationCurve curve)
        {
            track.Curve        = curve;
            track.PreviewCurve = _smoothLoop ? BuildLoopCurve(curve) : curve;

            var keys  = curve.keys;
            var times = new float[keys.Length];
            for (int i = 0; i < keys.Length; i++) times[i] = keys[i].time;
            track.KeyTimes = times;
        }

        private void RebuildAllKeyTimes()
        {
            var timeSet = new SortedSet<float>();
            foreach (var track in _tracks)
                foreach (var t in track.KeyTimes)
                    timeSet.Add(t);
            _allKeyTimes = new float[timeSet.Count];
            timeSet.CopyTo(_allKeyTimes);
            _allKeyTimesDirty = false;
        }

        /// <summary>現在の SMR パスに属する全ブレンドシェイプトラック（キャッシュ済み）。</summary>
        public IReadOnlyList<AnimationTrack> Tracks
        {
            get { EnsureCache(); return _tracks; }
        }

        /// <summary>全トラックのキー時刻の和集合（昇順・重複なし）。実際に読まれた時に一度だけ再構築される。</summary>
        public float[] AllKeyTimes
        {
            get
            {
                EnsureCache();
                if (_allKeyTimesDirty) RebuildAllKeyTimes();
                return _allKeyTimes;
            }
        }

        public bool TryGetTrack(string shapeName, out AnimationTrack track)
        {
            EnsureCache();
            return _trackByName.TryGetValue(shapeName, out track);
        }

        private void EnsureCache()
        {
            if (_cachedRevision == _revision) return;
            _cachedRevision = _revision;

            _tracks.Clear();
            _trackByName.Clear();
            _allKeyTimesDirty = false;

            if (Clip == null)
            {
                _allKeyTimes = Array.Empty<float>();
                return;
            }

            var timeSet = new SortedSet<float>();
            foreach (var binding in AnimationUtility.GetCurveBindings(Clip))
            {
                if (binding.type != typeof(SkinnedMeshRenderer)) continue;
                if (!binding.propertyName.StartsWith("blendShape.")) continue;
                if (binding.path != _smrPath) continue;

                var curve = AnimationUtility.GetEditorCurve(Clip, binding);
                if (curve == null || curve.length == 0) continue;

                var keys = curve.keys;
                var times = new float[keys.Length];
                for (int i = 0; i < keys.Length; i++)
                {
                    times[i] = keys[i].time;
                    timeSet.Add(keys[i].time);
                }

                var track = new AnimationTrack
                {
                    ShapeName    = binding.propertyName.Substring("blendShape.".Length),
                    Binding      = binding,
                    Curve        = curve,
                    PreviewCurve = _smoothLoop ? BuildLoopCurve(curve) : curve,
                    KeyTimes     = times,
                };
                _tracks.Add(track);
                _trackByName[track.ShapeName] = track;
            }

            _allKeyTimes = new float[timeSet.Count];
            timeSet.CopyTo(_allKeyTimes);
        }

        /// <summary>末尾（ClipLength）のキー値を先頭（0 秒）の評価値に揃えたカーブを生成する。</summary>
        public AnimationCurve BuildLoopCurve(AnimationCurve source)
        {
            var loop = new AnimationCurve(source.keys);
            if (loop.keys.Length == 0 || _clipLength <= 0f) return loop;

            float valZero = loop.Evaluate(0f);
            float tol     = FrameTolerance;

            int endIdx = FindKeyIndex(loop, _clipLength, tol);
            if (endIdx >= 0) loop.RemoveKey(endIdx);

            int newIdx = loop.AddKey(new Keyframe(_clipLength, valZero));
            if (newIdx < 0)
            {
                // 既存キーと時刻が重なった場合（浮動小数点誤差）は MoveKey で上書き
                int existingIdx = FindKeyIndex(loop, _clipLength, 0f);
                if (existingIdx >= 0)
                {
                    loop.MoveKey(existingIdx, new Keyframe(_clipLength, valZero));
                    newIdx = existingIdx;
                }
            }

            int startIdx = FindKeyIndex(loop, 0f, tol);
            if (startIdx >= 0 && newIdx >= 0)
            {
                AnimationUtility.SetKeyLeftTangentMode(loop,  newIdx, AnimationUtility.GetKeyLeftTangentMode(loop,  startIdx));
                AnimationUtility.SetKeyRightTangentMode(loop, newIdx, AnimationUtility.GetKeyRightTangentMode(loop, startIdx));
            }
            return loop;
        }

        // ─── Keyframe queries（すべてキャッシュ経由） ────────────────────────

        public bool HasKeyframeAt(string shapeName, float time)
        {
            if (!TryGetTrack(shapeName, out var track)) return false;
            return FindTimeIndex(track.KeyTimes, time, FrameTolerance) >= 0;
        }

        public bool HasAnyKeyframeAt(float time)
        {
            return FindTimeIndex(AllKeyTimes, time, FrameTolerance) >= 0;
        }

        /// <summary>現在時刻のカーブ値（トラックがなければ 0）。</summary>
        public float GetValueAt(string shapeName, float time)
        {
            return TryGetTrack(shapeName, out var track) ? track.Curve.Evaluate(time) : 0f;
        }

        public float[] GetKeyTimes(string shapeName)
        {
            return TryGetTrack(shapeName, out var track) ? track.KeyTimes : Array.Empty<float>();
        }

        /// <summary>現在時刻より前の直近キー時刻。なければ負値。</summary>
        public float PrevKeyTime(float time)
        {
            var all = AllKeyTimes;
            float tol = FrameTolerance;
            float prev = -1f;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] < time - tol) prev = all[i];
                else break;
            }
            return prev;
        }

        /// <summary>現在時刻より後の直近キー時刻。なければ負値。</summary>
        public float NextKeyTime(float time)
        {
            var all = AllKeyTimes;
            float tol = FrameTolerance;
            for (int i = 0; i < all.Length; i++)
                if (all[i] > time + tol) return all[i];
            return -1f;
        }

        /// <summary>指定シェイプ・時刻のキーの補間タイプ。見つからない場合は Ease。</summary>
        public InterpolationType GetKeyInterpolation(string shapeName, float time)
        {
            if (!TryGetTrack(shapeName, out var track)) return InterpolationType.Ease;
            // KeyTimes（キャッシュ済み float[]）のインデックスは curve のキーインデックスと一致するため、
            // curve.keys のフルコピーを避けてタングェントモードを引ける（150ms ポーリングで呼ばれる）。
            int idx = FindTimeIndex(track.KeyTimes, time, FrameTolerance);
            if (idx < 0) return InterpolationType.Ease;

            var lm = AnimationUtility.GetKeyLeftTangentMode(track.Curve, idx);
            if (lm == AnimationUtility.TangentMode.Constant) return InterpolationType.Step;
            if (lm == AnimationUtility.TangentMode.Linear)   return InterpolationType.Linear;
            return InterpolationType.Ease;
        }

        // ─── Static helpers ──────────────────────────────────────────────────

        public static int FindKeyIndex(AnimationCurve curve, float time, float tol)
        {
            var keys = curve.keys;
            for (int i = 0; i < keys.Length; i++)
                if (Mathf.Abs(keys[i].time - time) <= tol) return i;
            return -1;
        }

        private static int FindTimeIndex(float[] sortedTimes, float time, float tol)
        {
            for (int i = 0; i < sortedTimes.Length; i++)
            {
                if (Mathf.Abs(sortedTimes[i] - time) <= tol) return i;
                if (sortedTimes[i] > time + tol) return -1;
            }
            return -1;
        }
    }
}
