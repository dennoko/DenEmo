using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DenEmo.UI
{
    /// <summary>
    /// タイムラインを別ウィンドウとして表示する（描画は DenEmoWindow に委譲）。
    /// 外枠は UI Toolkit、タイムライン本体は IMGUIContainer でカプセル化する。
    /// </summary>
    public class DenEmoTimelineWindow : EditorWindow
    {
        /// <summary>再生中の再描画用に AnimationModeUI が参照する。</summary>
        public static DenEmoTimelineWindow Instance { get; private set; }

        private VisualElement _noticeBox;
        private Label         _noticeLabel;
        private Button        _openMainButton;
        private ScrollView    _scroll;

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
            var timelineGui = root.Q<IMGUIContainer>("tlwin-imgui");

            timelineGui.onGUIHandler = OnTimelineGUI;
            _openMainButton.clicked += () => DenEmoWindow.ShowWindow();

            // DenEmo 本体の開閉と言語切替はこのウィンドウの外で起きるため、ポーリングで追従する
            root.schedule.Execute(UpdateChromeState).Every(250);
            UpdateChromeState();
        }

        private void OnTimelineGUI()
        {
            if (DenEmoWindow.Instance == null) return;
            DenEmoTheme.Initialize();
            DenEmoTheme.PushEditorTheme();
            try
            {
                DenEmoWindow.Instance.DrawTimelineForSeparateWindow(this);
            }
            finally
            {
                DenEmoTheme.PopEditorTheme();
            }
        }

        private void UpdateChromeState()
        {
            if (_noticeBox == null) return;
            bool hasMain = DenEmoWindow.Instance != null;
            _noticeBox.style.display = hasMain ? DisplayStyle.None : DisplayStyle.Flex;
            _scroll.style.display    = hasMain ? DisplayStyle.Flex : DisplayStyle.None;
            _noticeLabel.text        = DenEmoLoc.T("ui.timeline.window.notOpen");
            _openMainButton.text     = DenEmoLoc.T("ui.timeline.window.open");
        }

        // 常時 EditorApplication.update += Repaint で再描画し続けるとエディタが
        // アイドル時もフル稼働するため購読しない。再生中の再描画は
        // AnimationModeUI.OnUpdate がサンプル実行時のみ Instance.Repaint() を呼ぶ。
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
