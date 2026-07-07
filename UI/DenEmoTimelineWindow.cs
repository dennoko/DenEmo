using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

namespace DenEmo.UI
{
    /// <summary>
    /// タイムラインを別ウィンドウとして表示する。
    /// 外枠・タイムライン本体ともに UI Toolkit（TimelineUITKView）。
    /// 状態は DenEmo 本体の AnimationModeUI を共有し、本体が閉じている間は通知を表示する。
    /// </summary>
    public class DenEmoTimelineWindow : EditorWindow
    {
        /// <summary>再生中の再描画用に AnimationModeUI が参照する。</summary>
        public static DenEmoTimelineWindow Instance { get; private set; }

        private VisualElement _noticeBox;
        private Label         _noticeLabel;
        private Button        _openMainButton;
        private ScrollView    _scroll;
        private VisualElement _host;

        private TimelineUITKView _view;
        private AnimationModeUI  _boundMode; // _view を構築したときの本体 AnimationModeUI

        public static void ShowWindow()
        {
            var w = GetWindow<DenEmoTimelineWindow>("Timeline");
            w.minSize = new Vector2(760, 200);
            w.Show();
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            DenEmoUiAssets.SetupRoot(root);

            var tree = DenEmoUiAssets.LoadVisualTree(DenEmoUiAssets.TimelineWindowUxmlGuid);
            if (tree == null) return;
            tree.CloneTree(root);

            _noticeBox      = root.Q<VisualElement>("tlwin-notice");
            _noticeLabel    = root.Q<Label>("tlwin-notice-label");
            _openMainButton = root.Q<Button>("tlwin-open-main");
            _scroll         = root.Q<ScrollView>("tlwin-scroll");
            _host           = root.Q<VisualElement>("tlwin-host");

            _openMainButton.clicked += () => DenEmoWindow.ShowWindow();

            // 本体の開閉・言語切替・クリップ差し替えはこのウィンドウの外で起きるためポーリングで追従する
            root.schedule.Execute(UpdateChromeState).Every(250);
            UpdateChromeState();
        }

        private void UpdateChromeState()
        {
            if (_noticeBox == null) return;
            var main = DenEmoWindow.Instance;
            // プレビュー整合のため、本体が開いていて Animation モードのときのみタイムラインを表示する
            bool available = main != null && main.IsAnimationModeActive;

            _noticeBox.style.display = available ? DisplayStyle.None : DisplayStyle.Flex;
            _scroll.style.display    = available ? DisplayStyle.Flex : DisplayStyle.None;
            _noticeLabel.text        = DenEmoLoc.T("ui.timeline.window.notOpen");
            _openMainButton.text     = DenEmoLoc.T("ui.timeline.window.open");

            if (!available)
            {
                _view?.SetActive(false);
                return;
            }

            // 本体が新しく開き直された場合（AnimationModeUI が別インスタンス）はビューを作り直す
            if (_view == null || !ReferenceEquals(_boundMode, main.SeparateTimelineMode))
            {
                _host.Clear();
                _boundMode = main.SeparateTimelineMode;
                _view = new TimelineUITKView();
                _host.Add(_view.Build(
                    _boundMode, main.SeparateTimelineModel, this,
                    showClipField: false, isSeparateWindow: true, requireZoomModifier: false));
            }
            _view.RefreshLabels();
            _view.SetActive(true);
        }

        // 常時 EditorApplication.update += Repaint で再描画し続けるとアイドル時もフル稼働するため購読しない。
        // 再生中の再描画は AnimationModeUI.OnUpdate がサンプル実行時のみ Instance.Repaint() を呼ぶ。
        private void OnEnable()
        {
            Instance = this;
        }

        private void OnDisable()
        {
            if (Instance == this) Instance = null;
        }
    }
}
