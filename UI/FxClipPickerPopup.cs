using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using DenEmo.Core;
using DenEmo.Models;

namespace DenEmo.UI
{
    /// <summary>
    /// 差し替え先アニメーションのピッカー。保存フォルダ配下のシェイプキーアニメーションを列挙し、
    /// 行のマウスオーバーでシーンにループプレビュー、クリックで割当てて閉じる。
    /// </summary>
    internal class FxClipPickerPopup : PopupWindowContent
    {
        private readonly FxSetupModeUI     _owner;
        private readonly FxExpressionEntry _entry;
        private readonly EditorWindow      _parentWindow;

        private List<AnimationClip> _clips = new List<AnimationClip>();
        private string  _search = string.Empty;
        private Vector2 _scroll;

        internal FxClipPickerPopup(FxSetupModeUI owner, FxExpressionEntry entry, EditorWindow parentWindow)
        {
            _owner        = owner;
            _entry        = entry;
            _parentWindow = parentWindow;
        }

        public override Vector2 GetWindowSize() => new Vector2(300, 380);

        public override void OnOpen()
        {
            editorWindow.wantsMouseMove = true;
            ReloadClips();
        }

        public override void OnClose()
        {
            _owner.SetPickerOpen(false);
            _owner.Hover.SetHover(null);
            _parentWindow?.Repaint();
        }

        private void ReloadClips()
        {
            _clips = FxLayerScanner.ListCandidateClips(_owner.PickerFolder);
            // 差し替え元そのものは候補から除外（自己差し替え防止）
            _clips.Remove(_entry.Clip);
        }

        public override void OnGUI(Rect rect)
        {
            DenEmoTheme.Initialize();
            DenEmoTheme.PushEditorTheme();
            try
            {
                EditorGUI.DrawRect(rect, DenEmoTheme.Surface1);

                var e = Event.current;
                if (e.type == EventType.MouseMove) editorWindow.Repaint();

                AnimationClip hoveredClip = null;

                GUILayout.Space(6);

                // ヘッダ: タイトル + フォルダ変更
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(6);
                GUILayout.Label(DenEmoLoc.T("ui.fx.picker.title"), DenEmoTheme.GroupLabelStyle);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("…", DenEmoLoc.T("ui.fx.picker.folder.tip")),
                        DenEmoTheme.MiniButtonStyle, GUILayout.Width(26), GUILayout.Height(18)))
                {
                    string abs = EditorUtility.OpenFolderPanel(DenEmoLoc.T("ui.fx.picker.folder.tip"),
                        _owner.PickerFolder, "");
                    if (!string.IsNullOrEmpty(abs))
                    {
                        string projectRoot = Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length);
                        if (abs.StartsWith(projectRoot))
                        {
                            _owner.PickerFolder = abs.Substring(projectRoot.Length).Replace('\\', '/');
                            ReloadClips();
                        }
                    }
                }
                GUILayout.Space(6);
                EditorGUILayout.EndHorizontal();

                GUILayout.Label(_owner.PickerFolder, DenEmoTheme.CaptionStyle);
                GUILayout.Space(2);

                // 検索
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(6);
                _search = EditorGUILayout.TextField(_search, DenEmoTheme.SearchTextFieldStyle);
                GUILayout.Space(6);
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(2);

                _scroll = EditorGUILayout.BeginScrollView(_scroll);

                // 「なし（割当て解除）」
                if (DrawRow(DenEmoLoc.T("ui.fx.picker.none"), null, ref hoveredClip))
                {
                    _owner.TryAssign(_entry, null, _parentWindow);
                    editorWindow.Close();
                }

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

                    if (DrawRow(clip.name, clip, ref hoveredClip))
                    {
                        _owner.TryAssign(_entry, clip, _parentWindow);
                        editorWindow.Close();
                    }
                }

                if (shown == 0)
                    GUILayout.Label(DenEmoLoc.T("ui.fx.picker.empty"), DenEmoTheme.CaptionStyle);

                EditorGUILayout.EndScrollView();

                // ポップアップ表示中はこちらがホバーの権限を持つ
                if (e.type == EventType.Repaint)
                    _owner.Hover.SetHover(hoveredClip);
                if (e.type == EventType.MouseLeaveWindow)
                    _owner.Hover.SetHover(null);
            }
            finally
            {
                DenEmoTheme.PopEditorTheme();
            }
        }

        /// <summary>1 行描画。クリックされたら true。ホバー中のクリップを hoveredClip へ返す。</summary>
        private bool DrawRow(string label, AnimationClip clip, ref AnimationClip hoveredClip)
        {
            bool hovered = _owner.Hover.ActiveClip != null && clip == _owner.Hover.ActiveClip;
            EditorGUILayout.BeginHorizontal(hovered ? DenEmoTheme.RowHoverStyle : DenEmoTheme.RowStyle);
            bool clicked = GUILayout.Button(label, DenEmoTheme.SecondaryTextStyle, GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();

            if (clip != null && Event.current.type == EventType.Repaint &&
                GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                hoveredClip = clip;

            return clicked;
        }
    }
}
