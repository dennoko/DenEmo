using UnityEditor;
using UnityEngine;
using DenEmo.Models;

namespace DenEmo.UI
{
    public partial class AnimationTimelineUI
    {
        // ─── Keyframe tracks ──────────────────────────────────────────────────

        private void DrawKeyframeTracks(AnimationModeUI mode, ShapeKeyModel shapeModel, EditorWindow window)
        {
            var tracks = mode.ClipModel.Tracks;
            if (tracks.Count == 0)
            {
                GUILayout.Label(DenEmoLoc.T("ui.timeline.noKeys"), DenEmoTheme.CaptionStyle);
                return;
            }

            float rowsHeight = Mathf.Min(tracks.Count * (TRACK_ROW_HEIGHT + 1), MAX_TRACKS_HEIGHT);
            _tracksScroll = EditorGUILayout.BeginScrollView(_tracksScroll, GUILayout.Height(rowsHeight));

            // DeleteTrack するとキャッシュが再構築されリストが変わるため、削除されたら即座に抜ける
            for (int i = 0; i < tracks.Count; i++)
            {
                if (!DrawTrackRow(tracks[i], mode, shapeModel, window))
                    break;
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>1 トラック分の行を描画する。トラックが削除された場合は false を返す。</summary>
        private bool DrawTrackRow(AnimationTrack track, AnimationModeUI mode, ShapeKeyModel shapeModel, EditorWindow window)
        {
            var m = mode.ClipModel;
            string shapeName = track.ShapeName;

            Rect rowRect = GUILayoutUtility.GetRect(0, TRACK_ROW_HEIGHT, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rowRect, DenEmoTheme.Surface0);
                float cy = rowRect.y + rowRect.height * 0.5f;
                EditorGUI.DrawRect(new Rect(rowRect.x + _trackLabelWidth, cy, rowRect.width - _trackLabelWidth, 1), DenEmoTheme.Outline);
            }

            // レイアウト: [label][border(4px)][◆(25px)][gap(4px)][✕(20px)][gap(4px)]
            const float BORDER_W  = 4f;
            const float ADD_BTN_W = 25f;
            const float DEL_BTN_W = 20f;
            const float BTN_GAP   = 4f;
            const float LABEL_RIGHT_RESERVE = BORDER_W + ADD_BTN_W + BTN_GAP + DEL_BTN_W + BTN_GAP;

            float labelMaxW = _trackLabelWidth - LABEL_RIGHT_RESERVE - 8f;
            float borderX   = rowRect.x + _trackLabelWidth - LABEL_RIGHT_RESERVE;
            float addBtnX   = borderX + BORDER_W;
            float delBtnX   = addBtnX + ADD_BTN_W + BTN_GAP;
            float trackW    = _cachedTrackW > 0f ? _cachedTrackW : rowRect.width - _trackLabelWidth - RIGHT_PADDING;
            float trackX    = rowRect.x + _trackLabelWidth;

            GUI.Label(
                new Rect(rowRect.x + 8, rowRect.y + 4, labelMaxW, rowRect.height - 8),
                shapeName, DenEmoTheme.CaptionStyle);

            DrawLabelWidthSplitter(borderX, BORDER_W, rowRect, window);

            // ── ◆ キー追加・更新ボタン ─────────────────────────────────────────
            if (GUI.Button(
                new Rect(addBtnX, rowRect.y + 2, ADD_BTN_W, 20),
                new GUIContent("◆", DenEmoLoc.T("ui.timeline.addKey.tip")),
                DenEmoTheme.MiniButtonStyle))
            {
                var smr = shapeModel?.TargetSkinnedMesh;
                if (smr != null && smr.sharedMesh != null)
                {
                    int index = smr.sharedMesh.GetBlendShapeIndex(shapeName);
                    if (index >= 0)
                    {
                        mode.Editor.RecordKey(shapeName, m.CurrentTime, smr.GetBlendShapeWeight(index), mode.CurrentInterp);
                        mode.Preview.SampleAt(m.CurrentTime);
                        window.Repaint();
                    }
                }
            }

            // ── ✕ トラック削除ボタン ───────────────────────────────────────────
            if (GUI.Button(
                new Rect(delBtnX, rowRect.y + 2, DEL_BTN_W, 20),
                new GUIContent("✕", DenEmoLoc.T("ui.timeline.deleteTrack.tip")),
                DenEmoTheme.MiniButtonStyle))
            {
                if (EditorUtility.DisplayDialog(
                    DenEmoLoc.T("dlg.timeline.deleteTrack.title"),
                    DenEmoLoc.Tf("dlg.timeline.deleteTrack.msg", shapeName),
                    DenEmoLoc.T("dlg.yes"),
                    DenEmoLoc.T("dlg.no")))
                {
                    mode.Editor.DeleteTrack(shapeName);
                    window.Repaint();
                    return false; // トラックが消えたのでこの行の残り描画は行わない
                }
            }

            // ── 現在時刻ライン ─────────────────────────────────────────────────
            if (Event.current.type == EventType.Repaint)
            {
                float sx = TimeToPixel(m.CurrentTime, m.ClipLength, trackX, trackW);
                EditorGUI.DrawRect(new Rect(sx - 1, rowRect.y, 2, rowRect.height), new Color(1f, 1f, 1f, 0.4f));

                // キードラッグが他キーにブロックされたとき、ブロック元の位置を一瞬ハイライトする
                if (_blockFlashFrame >= 0 && EditorApplication.timeSinceStartup < _blockFlashUntil)
                {
                    float bx = TimeToPixel(m.FrameToTime(_blockFlashFrame), m.ClipLength, trackX, trackW);
                    if (bx >= trackX && bx <= trackX + trackW)
                        EditorGUI.DrawRect(new Rect(bx - 1, rowRect.y, 2, rowRect.height), DenEmoTheme.SemanticWarning);
                }
            }

            // ── キーフレームダイヤ ─────────────────────────────────────────────
            float tol = m.FrameTolerance;
            foreach (float kTime in track.KeyTimes)
            {
                float kx = TimeToPixel(kTime, m.ClipLength, trackX, trackW);
                if (kx < trackX - DIAMOND_SIZE || kx > trackX + trackW + DIAMOND_SIZE) continue;
                float ky = rowRect.y + rowRect.height * 0.5f;
                Rect hitR = new Rect(kx - DIAMOND_SIZE - 2, ky - DIAMOND_SIZE - 2, (DIAMOND_SIZE + 2) * 2, (DIAMOND_SIZE + 2) * 2);

                bool isCurrent = Mathf.Abs(kTime - m.CurrentTime) <= tol;

                if (Event.current.type == EventType.Repaint)
                {
                    GUI.Label(new Rect(kx - 8, ky - 8, 16, 16), "◆",
                        isCurrent ? DenEmoTheme.KeyDiamondActiveStyle : DenEmoTheme.KeyDiamondStyle);

                    if (hitR.Contains(Event.current.mousePosition))
                        DrawKeyHoverTip(track, kTime, kx, ky, trackX, trackW, m);
                }

                EditorGUIUtility.AddCursorRect(hitR, MouseCursor.SlideArrow);
                HandleKeyframeDrag(hitR, kTime, shapeName, mode, window, trackX, trackW, seekOnDown: true);

                if (Event.current.type == EventType.ContextClick && hitR.Contains(Event.current.mousePosition))
                {
                    ShowKeyContextMenu(mode, shapeName, kTime, window);
                    Event.current.Use();
                }
            }

            // ── トラック空白部の右クリック → ペーストメニュー ─────────────────
            Rect trackClickArea = new Rect(trackX, rowRect.y, trackW, rowRect.height);
            if (Event.current.type == EventType.ContextClick && trackClickArea.Contains(Event.current.mousePosition))
            {
                ShowTrackContextMenu(mode, window);
                Event.current.Use();
            }

            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.yMax - 1, rowRect.width, 1), DenEmoTheme.Surface2);

            return true;
        }

        // ─── Label width splitter ─────────────────────────────────────────────

        private void DrawLabelWidthSplitter(float borderX, float borderW, Rect rowRect, EditorWindow window)
        {
            Rect borderRect = new Rect(borderX, rowRect.y, borderW, rowRect.height);
            if (Event.current.type == EventType.Repaint)
            {
                float bCx = borderX + borderW * 0.5f;
                EditorGUI.DrawRect(new Rect(bCx - 0.5f, rowRect.y + 2, 1, rowRect.height - 4), DenEmoTheme.Outline);
            }
            EditorGUIUtility.AddCursorRect(borderRect, MouseCursor.ResizeHorizontal);

            Event ev = Event.current;
            if (ev.type == EventType.MouseDown && borderRect.Contains(ev.mousePosition) && ev.button == 0)
            {
                _isDraggingLabelWidth = true;
                ev.Use();
            }
            else if (ev.type == EventType.MouseDrag && _isDraggingLabelWidth)
            {
                _trackLabelWidth = Mathf.Clamp(_trackLabelWidth + ev.delta.x, 80f, rowRect.width * 0.8f);
                window.Repaint();
                ev.Use();
            }
            else if (ev.type == EventType.MouseUp && _isDraggingLabelWidth && ev.button == 0)
            {
                _isDraggingLabelWidth = false;
                ev.Use();
            }
        }

        // ─── Key hover tooltip ────────────────────────────────────────────────

        private void DrawKeyHoverTip(AnimationTrack track, float kTime, float kx, float ky, float trackX, float trackW, AnimationClipModel m)
        {
            int    frameNum = m.TimeToFrame(kTime);
            float  val      = track.Curve.Evaluate(kTime);
            string tipText  = $"F:{frameNum}  V:{val:F1}";

            Vector2 size   = DenEmoTheme.HoverTipStyle.CalcSize(new GUIContent(tipText));
            float   labelX = (kx + 10 + size.x < trackX + trackW) ? kx + 10 : kx - 10 - size.x;
            float   labelY = ky - size.y * 0.5f;

            EditorGUI.DrawRect(new Rect(labelX - 3, labelY - 2, size.x + 6, size.y + 4), new Color(0.1f, 0.1f, 0.1f, 0.85f));
            GUI.Label(new Rect(labelX, labelY, size.x, size.y), tipText, DenEmoTheme.HoverTipStyle);
        }

        // ─── Context menus ────────────────────────────────────────────────────

        private void ShowKeyContextMenu(AnimationModeUI mode, string shapeName, float kTime, EditorWindow window)
        {
            var m = mode.ClipModel;
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent(DenEmoLoc.T("ui.timeline.menu.delete")), false, () =>
            {
                mode.Editor.DeleteKey(shapeName, kTime);
                mode.Preview.SampleAt(m.CurrentTime);
                window.Repaint();
            });

            menu.AddSeparator("");

            menu.AddItem(new GUIContent(DenEmoLoc.T("ui.timeline.menu.copyFrame")), false, () =>
            {
                CopyFrame(mode, kTime);
            });

            if (HasClipboardData)
                menu.AddItem(new GUIContent(DenEmoLoc.T("ui.timeline.menu.paste")), false, () =>
                {
                    PasteAtCurrentTime(mode, window);
                });
            else
                menu.AddDisabledItem(new GUIContent(DenEmoLoc.T("ui.timeline.menu.paste")));

            menu.AddSeparator("");

            void AddInterpItem(string label, InterpolationType interp)
            {
                bool isCurrent = m.GetKeyInterpolation(shapeName, kTime) == interp;
                menu.AddItem(new GUIContent(label), isCurrent, () =>
                {
                    mode.Editor.SetKeyInterpolation(shapeName, kTime, interp);
                    mode.Preview.SampleAt(m.CurrentTime);
                    window.Repaint();
                });
            }

            AddInterpItem("Step",   InterpolationType.Step);
            AddInterpItem("Linear", InterpolationType.Linear);
            AddInterpItem("Ease",   InterpolationType.Ease);

            menu.ShowAsContext();
        }

        private void ShowTrackContextMenu(AnimationModeUI mode, EditorWindow window)
        {
            var menu = new GenericMenu();
            if (HasClipboardData)
                menu.AddItem(new GUIContent(DenEmoLoc.T("ui.timeline.menu.paste")), false, () =>
                {
                    PasteAtCurrentTime(mode, window);
                });
            else
                menu.AddDisabledItem(new GUIContent(DenEmoLoc.T("ui.timeline.menu.paste")));
            menu.ShowAsContext();
        }
    }
}
