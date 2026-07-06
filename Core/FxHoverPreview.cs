using UnityEditor;
using UnityEngine;

namespace DenEmo.Core
{
    /// <summary>
    /// 一覧行のマウスオーバーで対象クリップをシーンにループ再生するプレビュー。
    /// マウスが行を横切っただけで表情が暴れないよう、再生開始にデバウンスを挟む。
    /// 時間の進行は DenEmoWindow.OnEditorUpdate から Tick() で駆動する。
    /// </summary>
    public class FxHoverPreview
    {
        private const double HoverDelaySec = 0.2;
        // 1 フレーム相当以下のクリップは静止表示（Pose モード保存クリップは長さ 0.0001s）
        private const float MinLoopLength = 0.05f;

        private readonly FxClipSampler _sampler = new FxClipSampler();

        private AnimationClip _pendingClip;   // デバウンス待ち
        private double        _pendingSince;
        private AnimationClip _activeClip;    // 再生中
        private double        _lastTickTime;
        private float         _playTime;

        public AnimationClip ActiveClip => _activeClip;
        public bool IsActive => _activeClip != null;

        public void SetRoot(Transform avatarRoot) => _sampler.SetRoot(avatarRoot);

        /// <summary>
        /// 現在ホバー中のクリップを毎リペイントで通知する（null = どの行にも乗っていない）。
        /// </summary>
        public void SetHover(AnimationClip clip)
        {
            if (clip == null)
            {
                _pendingClip = null;
                StopActive();
                return;
            }
            if (clip == _activeClip)
            {
                _pendingClip = null;
                return;
            }
            if (_pendingClip != clip)
            {
                _pendingClip  = clip;
                _pendingSince = EditorApplication.timeSinceStartup;
            }
        }

        /// <summary>デバウンス消化とループ時間の進行。EditorApplication.update 系から呼ぶ。</summary>
        public void Tick()
        {
            double now = EditorApplication.timeSinceStartup;

            if (_pendingClip != null && now - _pendingSince >= HoverDelaySec)
            {
                if (_activeClip != null) _sampler.Restore();
                _activeClip  = _pendingClip;
                _pendingClip = null;
                _playTime    = 0f;
                _sampler.Apply(_activeClip, 0f);
                SceneView.RepaintAll();
            }

            if (_activeClip != null)
            {
                if (_activeClip.length > MinLoopLength)
                {
                    _playTime += (float)(now - _lastTickTime);
                    if (_playTime > _activeClip.length) _playTime %= _activeClip.length;
                    _sampler.Apply(_activeClip, _playTime);
                    var sceneView = SceneView.lastActiveSceneView;
                    if (sceneView != null) sceneView.Repaint();
                }
            }
            _lastTickTime = now;
        }

        /// <summary>プレビューを止めてウェイトを復元する。タブ離脱・ターゲット変更時にも呼ぶこと。</summary>
        public void Stop()
        {
            _pendingClip = null;
            StopActive();
        }

        private void StopActive()
        {
            if (_activeClip == null) return;
            _activeClip = null;
            _sampler.Restore();
            _sampler.InvalidateCache();
            SceneView.RepaintAll();
        }
    }
}
