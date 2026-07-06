using UnityEditor;
using UnityEngine;
using DenEmo.Models;

namespace DenEmo.Core
{
    /// <summary>
    /// マルチフレームモードの再生状態（再生中フラグ・速度・時刻送り）を管理する。
    /// EditorApplication.update から Tick() を呼ぶことでループ再生する。
    /// </summary>
    public class AnimationPlayback
    {
        public const float MinSpeed = 0.1f;
        public const float MaxSpeed = 4f;

        private float  _speed = 1f;
        private double _lastTickTime;
        private double _lastSampleTime;

        public bool IsPlaying { get; private set; }

        public float Speed
        {
            get => _speed;
            set => _speed = Mathf.Clamp(value, MinSpeed, MaxSpeed);
        }

        public void Play()
        {
            IsPlaying       = true;
            _lastTickTime   = EditorApplication.timeSinceStartup;
            _lastSampleTime = 0; // 再生開始直後のフレームは必ずサンプルする
        }

        public void Stop() => IsPlaying = false;

        public void Toggle()
        {
            if (IsPlaying) Stop();
            else           Play();
        }

        /// <summary>
        /// EditorApplication.update から呼ぶ。再生中なら時刻を進めてプレビューを更新する。
        /// エディタ更新はクリップ FPS を大きく超える頻度で呼ばれるため、サンプリングと再描画は
        /// クリップ FPS を上限にスロットルする（時刻は実時間で進め続ける）。
        /// サンプルを実行したフレームで true を返す。
        /// </summary>
        public bool Tick(AnimationClipModel model, AnimationPreviewController preview, EditorWindow window)
        {
            if (!IsPlaying || model?.Clip == null || !preview.IsActive) return false;

            double now = EditorApplication.timeSinceStartup;
            float  dt  = (float)(now - _lastTickTime);
            _lastTickTime = now;

            float t = model.CurrentTime + dt * _speed;
            if (model.ClipLength > 0f && t >= model.ClipLength)
                t = Mathf.Repeat(t, model.ClipLength); // ループ：超過分を次周へ持ち越す

            model.CurrentTime = t;

            double sampleInterval = model.FPS > 0f ? 1.0 / model.FPS : 0.0;
            if (now - _lastSampleTime < sampleInterval) return false;
            _lastSampleTime = now;

            preview.SampleAt(t);
            window.Repaint();
            return true;
        }
    }
}
