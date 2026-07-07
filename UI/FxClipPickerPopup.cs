using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using DenEmo.Core;
using DenEmo.Models;

namespace DenEmo.UI
{
    /// <summary>
    /// 差し替え先アニメーションのピッカー。保存フォルダ配下のシェイプキーアニメーションを列挙し、
    /// 行のマウスオーバーでシーンにループプレビュー、クリックで割当てて閉じる。
    /// UI は FxClipPickerPopup.uxml + 動的行生成（UI Toolkit）。
    /// </summary>
    internal class FxClipPickerPopup : PopupWindowContent
    {
        private readonly FxSetupModeUI     _owner;
        private readonly FxExpressionEntry _entry;
        private readonly EditorWindow      _parentWindow;

        private List<AnimationClip> _clips = new List<AnimationClip>();
        private string _search = string.Empty;

        private ScrollView _list;
        private Label      _folderLabel;
        private Label      _emptyLabel;

        internal FxClipPickerPopup(FxSetupModeUI owner, FxExpressionEntry entry, EditorWindow parentWindow)
        {
            _owner        = owner;
            _entry        = entry;
            _parentWindow = parentWindow;
        }

        public override Vector2 GetWindowSize() => new Vector2(300, 380);

        public override void OnOpen()
        {
            var root = editorWindow.rootVisualElement;
            DenEmoUiAssets.SetupRoot(root);
            root.style.paddingLeft   = 6;
            root.style.paddingRight  = 6;
            root.style.paddingTop    = 6;
            root.style.paddingBottom = 6;

            var tree = DenEmoUiAssets.LoadVisualTree(DenEmoUiAssets.FxClipPickerUxmlGuid);
            if (tree == null) return;
            tree.CloneTree(root);

            root.Q<Label>("picker-title").text = DenEmoLoc.T("ui.fx.picker.title");

            var folderButton = root.Q<Button>("picker-folder-button");
            folderButton.tooltip = DenEmoLoc.T("ui.fx.picker.folder.tip");
            folderButton.clicked += OnFolderButton;

            _folderLabel = root.Q<Label>("picker-folder-label");
            _folderLabel.text = _owner.PickerFolder;

            var searchField = root.Q<TextField>("picker-search");
            searchField.RegisterValueChangedCallback(evt =>
            {
                _search = evt.newValue ?? string.Empty;
                RebuildRows();
            });

            _emptyLabel = root.Q<Label>("picker-empty");
            _emptyLabel.text = DenEmoLoc.T("ui.fx.picker.empty");
            _list = root.Q<ScrollView>("picker-list");

            ReloadClips();
            RebuildRows();
        }

        // 描画は OnOpen() で構築した UI Toolkit 要素が行うため IMGUI は空
        public override void OnGUI(Rect rect) { }

        public override void OnClose()
        {
            _owner.SetPickerOpen(false);
            _owner.Hover.SetHover(null);
            _parentWindow?.Repaint();
        }

        private void OnFolderButton()
        {
            string abs = EditorUtility.OpenFolderPanel(DenEmoLoc.T("ui.fx.picker.folder.tip"),
                _owner.PickerFolder, "");
            if (string.IsNullOrEmpty(abs)) return;
            string projectRoot = Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length);
            if (!abs.StartsWith(projectRoot)) return;
            _owner.PickerFolder = abs.Substring(projectRoot.Length).Replace('\\', '/');
            _folderLabel.text = _owner.PickerFolder;
            ReloadClips();
            RebuildRows();
        }

        private void ReloadClips()
        {
            _clips = FxLayerScanner.ListCandidateClips(_owner.PickerFolder);
            // 差し替え元そのものは候補から除外（自己差し替え防止）
            _clips.Remove(_entry.Clip);
        }

        private void RebuildRows()
        {
            _list.Clear();

            // 「なし（割当て解除）」
            _list.Add(MakeRow(DenEmoLoc.T("ui.fx.picker.none"), null));

            var tokens = ShapeKeyModel.BuildSearchTokens(_search);
            int shown = 0;
            foreach (var clip in _clips)
            {
                if (clip == null) continue;
                if (tokens.Length > 0)
                {
                    bool match = true;
                    var nameLower = clip.name.ToLowerInvariant();
                    foreach (var t in tokens)
                        if (nameLower.IndexOf(t.ToLowerInvariant(), System.StringComparison.Ordinal) < 0) { match = false; break; }
                    if (!match) continue;
                }
                shown++;
                _list.Add(MakeRow(clip.name, clip));
            }

            _emptyLabel.style.display = shown == 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>1 行生成。クリックで割当てて閉じ、ホバーでシーンプレビュー。</summary>
        private VisualElement MakeRow(string label, AnimationClip clip)
        {
            var row = new Button(() =>
            {
                _owner.TryAssign(_entry, clip, _parentWindow);
                editorWindow.Close();
            }) { text = label };
            row.AddToClassList("dennoko-fx-picker-row");

            if (clip != null)
            {
                row.RegisterCallback<PointerEnterEvent>(_ => _owner.Hover.SetHover(clip));
                row.RegisterCallback<PointerLeaveEvent>(_ => _owner.Hover.SetHover(null));
            }
            return row;
        }
    }
}
