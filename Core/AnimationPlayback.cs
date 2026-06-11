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

        public bool IsPlaying { get; private set; }

        public float Speed
        {
            get => _speed;
            set => _speed = Mathf.Clamp(value, MinSpeed, MaxSpeed);
        }

        public void Play()
        {
            IsPlaying     = true;
            _lastTickTime = EditorApplication.timeSinceStartup;
        }

        public void Stop() => IsPlaying = false;

        public void Toggle()
        {
            if (IsPlaying) Stop();
            else           Play();
        }

        /// <summary>EditorApplication.update から呼ぶ。再生中なら時刻を進めてプレビューを更新する。</summary>
        public void Tick(AnimationClipModel model, AnimationPreviewController preview, EditorWindow window)
        {
            if (!IsPlaying || model?.Clip == null || !preview.IsActive) return;

            double now = EditorApplication.timeSinceStartup;
            float  dt  = (float)(now - _lastTickTime);
            _lastTickTime = now;

            float t = model.CurrentTime + dt * _speed;
            if (model.ClipLength > 0f && t >= model.ClipLength)
                t = Mathf.Repeat(t, model.ClipLength); // ループ：超過分を次周へ持ち越す

            model.CurrentTime = t;
            preview.SampleAt(t);
            window.Repaint();
        }
    }
}
