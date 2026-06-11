using System.Linq;
using UnityEditor;
using UnityEngine;
using DenEmo.Models;

namespace DenEmo.UI
{
    public partial class AnimationTimelineUI
    {
        // ─── Ruler & Scrubber ─────────────────────────────────────────────────

        private bool _showScrollbar; // Layout/Repaint 間で GetRect 回数を一致させるためフレーム単位でキャッシュ

        private void DrawRulerAndScrubber(AnimationModeUI mode, EditorWindow window)
        {
            var m = mode.ClipModel;

            Rect rulerRect = GUILayoutUtility.GetRect(0, RULER_HEIGHT,    GUILayout.ExpandWidth(true));
            Rect scrubRect = GUILayoutUtility.GetRect(0, SCRUBBER_HEIGHT, GUILayout.ExpandWidth(true));

            float trackW    = rulerRect.width - _trackLabelWidth - RIGHT_PADDING;
            float trackX    = rulerRect.x + _trackLabelWidth;
            Rect  trackArea = new Rect(trackX, rulerRect.y, trackW, rulerRect.height + scrubRect.height);

            _cachedTrackW = trackW;

            // HandleTimelineZoom より前に判定し、Layout と Repaint の GetRect 呼び出し回数を揃える
            if (Event.current.type == EventType.Layout)
                _showScrollbar = ViewRange < 1f - 0.001f;

            // メインウィンドウ埋め込み時は外側のスクロールビューとの競合を避けるため Ctrl 必須。
            // 分離ウィンドウではホイール単独でもズームできる。
            HandleTimelineZoom(trackArea, trackX, trackW, requireModifier: !(window is DenEmoTimelineWindow));

            if (_showScrollbar)
                DrawTimelineScrollbar(trackX, trackW, window);

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rulerRect, DenEmoTheme.Surface2);
                EditorGUI.DrawRect(scrubRect, DenEmoTheme.Surface0);
                EditorGUI.DrawRect(new Rect(trackX, rulerRect.y, 1, rulerRect.height + scrubRect.height), DenEmoTheme.Outline);

                DrawRulerTicks(rulerRect, m);
                DrawScrubberLine(scrubRect, m);
            }

            DrawZoomControls(rulerRect, window);
            // ルーラー全域＋スクラバー帯をシーク対象にする（トラックラベル列・ズームボタン領域は trackX より左で対象外）
            HandleScrubberInput(trackArea, mode, window);
        }

        // ─── Zoom（マウスホイール、カーソル位置中心） ─────────────────────────

        private void HandleTimelineZoom(Rect trackRect, float trackX, float trackW, bool requireModifier)
        {
            Event e = Event.current;
            if (e.type != EventType.ScrollWheel) return;
            if (!trackRect.Contains(e.mousePosition)) return;
            if (requireModifier && !e.control && !e.command) return; // 素のホイールは外側スクロールへ流す

            float mouseNorm = _viewStart + ((e.mousePosition.x - trackX) / trackW) * ViewRange;
            mouseNorm = Mathf.Clamp01(mouseNorm);

            float zoomFactor = e.delta.y > 0f ? 1.15f : (1f / 1.15f);
            float newRange   = Mathf.Clamp(ViewRange * zoomFactor, MIN_VIEW_RANGE, 1f);

            _viewStart = mouseNorm - (mouseNorm - _viewStart) * (newRange / ViewRange);
            _viewEnd   = _viewStart + newRange;

            if (_viewStart < 0f) { _viewEnd -= _viewStart; _viewStart = 0f; }
            if (_viewEnd   > 1f) { _viewStart -= (_viewEnd - 1f); _viewEnd = 1f; }
            _viewStart = Mathf.Clamp01(_viewStart);
            _viewEnd   = Mathf.Clamp01(_viewEnd);

            e.Use();
        }

        // ─── Scrollbar（テーマ描画のカスタムスクロールバー） ─────────────────

        private void DrawTimelineScrollbar(float trackX, float trackW, EditorWindow window)
        {
            Rect sbRect    = GUILayoutUtility.GetRect(0, SCROLLBAR_HEIGHT, GUILayout.ExpandWidth(true));
            Rect barRect   = new Rect(trackX, sbRect.y, trackW, SCROLLBAR_HEIGHT);
            Rect thumbRect = new Rect(
                trackX + _viewStart * trackW, sbRect.y + 1,
                Mathf.Max(12f, ViewRange * trackW), SCROLLBAR_HEIGHT - 2);

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(barRect, DenEmoTheme.Surface0);
                EditorGUI.DrawRect(thumbRect, DenEmoTheme.Surface2);
                EditorGUI.DrawRect(new Rect(thumbRect.x, thumbRect.y, thumbRect.width, 1), DenEmoTheme.Outline);
                EditorGUI.DrawRect(new Rect(thumbRect.x, thumbRect.yMax - 1, thumbRect.width, 1), DenEmoTheme.Outline);
            }

            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && barRect.Contains(e.mousePosition))
            {
                _isDraggingViewScroll = true;
                // つまみの外をクリックした場合はつまみ中央をマウス位置へジャンプ
                _viewScrollGrabOffset = thumbRect.Contains(e.mousePosition)
                    ? e.mousePosition.x - thumbRect.x
                    : thumbRect.width * 0.5f;
                MoveViewScroll(e.mousePosition.x, trackX, trackW, window);
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && _isDraggingViewScroll)
            {
                MoveViewScroll(e.mousePosition.x, trackX, trackW, window);
                e.Use();
            }
            else if (e.type == EventType.MouseUp && _isDraggingViewScroll)
            {
                _isDraggingViewScroll = false;
                e.Use();
            }
        }

        private void MoveViewScroll(float mouseX, float trackX, float trackW, EditorWindow window)
        {
            if (trackW <= 0f) return;
            float range    = ViewRange;
            float newStart = (mouseX - _viewScrollGrabOffset - trackX) / trackW;
            _viewStart = Mathf.Clamp(newStart, 0f, 1f - range);
            _viewEnd   = _viewStart + range;
            window.Repaint();
        }

        // ─── Zoom % display + reset ───────────────────────────────────────────

        private void DrawZoomControls(Rect rulerRect, EditorWindow window)
        {
            float separatorY = rulerRect.y + rulerRect.height * 0.5f;
            int   zoomPct    = Mathf.RoundToInt(1f / ViewRange * 100f);
            float innerW     = _trackLabelWidth - 8f;

            // 上段：ズーム率表示
            if (Event.current.type == EventType.Repaint)
                GUI.Label(new Rect(rulerRect.x + 4, rulerRect.y + 4, innerW, 14), zoomPct + "%", DenEmoTheme.SmallLabelStyle);

            // 下段：リセットボタン（ズーム中のみ）
            if (ViewRange < 1f - 0.001f)
            {
                if (GUI.Button(
                    new Rect(rulerRect.x + 4, separatorY + 2, innerW, 14),
                    new GUIContent(DenEmoLoc.T("ui.timeline.zoomReset"), DenEmoLoc.T("ui.timeline.zoomReset.tip")),
                    DenEmoTheme.MiniButtonStyle))
                {
                    _viewStart = 0f;
                    _viewEnd   = 1f;
                    window.Repaint();
                }
            }
        }

        // ─── Ruler ticks（上段：フレーム番号 / 下段：秒） ─────────────────────

        private void DrawRulerTicks(Rect rect, AnimationClipModel m)
        {
            float trackW = rect.width - _trackLabelWidth - RIGHT_PADDING;
            float trackX = rect.x + _trackLabelWidth;
            int total = m.TotalFrames;
            if (total <= 0 || trackW <= 0) return;

            float separatorY = rect.y + rect.height * 0.5f;

            // フレーム行と秒行の区切り線
            EditorGUI.DrawRect(new Rect(trackX, separatorY, trackW, 1), DenEmoTheme.Outline);

            // ── Frame numbers (top row) ──────────────────────────────────────
            int step = CalcRulerStep(total, trackW / ViewRange);
            int frameStart = Mathf.FloorToInt(_viewStart * total);
            int frameEnd   = Mathf.CeilToInt(_viewEnd   * total);
            frameStart -= frameStart % step; // 目盛りをステップ境界に揃える

            for (int f = frameStart; f <= frameEnd; f += step)
            {
                if (f < 0) continue;
                float x = TimeToPixel(m.FrameToTime(f), m.ClipLength, trackX, trackW);
                if (x < trackX || x > trackX + trackW) continue;
                EditorGUI.DrawRect(new Rect(x, separatorY - 5f, 1, 5), DenEmoTheme.Outline);
                if (x + 24 < trackX + trackW)
                    GUI.Label(new Rect(x + 2, rect.y + 2, 32, 14), f.ToString(), DenEmoTheme.SmallLabelStyle);
            }

            // ── Seconds (bottom row, 0.1s intervals) ─────────────────────────
            const float SEC_INTERVAL = 0.1f;
            float pxPer01 = (trackW * SEC_INTERVAL) / (m.ClipLength * ViewRange);
            int labelSkip = Mathf.Max(1, Mathf.CeilToInt(26f / Mathf.Max(0.001f, pxPer01)));

            float visStartSec = _viewStart * m.ClipLength;
            float visEndSec   = _viewEnd   * m.ClipLength;
            int startIdx = Mathf.Max(0, Mathf.FloorToInt(visStartSec / SEC_INTERVAL) - 1);
            int endIdx   = Mathf.CeilToInt(visEndSec / SEC_INTERVAL) + 1;

            for (int i = startIdx; i <= endIdx; i++)
            {
                float t = i * SEC_INTERVAL;
                if (t > m.ClipLength + 0.001f) break;

                float x = TimeToPixel(t, m.ClipLength, trackX, trackW);
                if (x < trackX || x > trackX + trackW) continue;

                EditorGUI.DrawRect(new Rect(x, rect.yMax - 5f, 1, 5), DenEmoTheme.Outline);

                if (i % labelSkip == 0 && x + 30 < trackX + trackW)
                    GUI.Label(new Rect(x + 2, separatorY + 2, 34, 12), t.ToString("0.0"), DenEmoTheme.TinyLabelStyle);
            }
        }

        // ─── Scrubber line ────────────────────────────────────────────────────

        private void DrawScrubberLine(Rect rect, AnimationClipModel m)
        {
            float trackW = rect.width - _trackLabelWidth - RIGHT_PADDING;
            float trackX = rect.x + _trackLabelWidth;
            float sx = TimeToPixel(m.CurrentTime, m.ClipLength, trackX, trackW);

            EditorGUI.DrawRect(new Rect(sx - 1, rect.y, 2, rect.height), new Color(1f, 1f, 1f, 0.8f));
            EditorGUI.DrawRect(new Rect(sx - 5, rect.y, 10, rect.height), DenEmoTheme.TextPrimary);

            // キードラッグ中は現在フレーム番号をスクラバー上に表示する
            if (_isDraggingKeyframe)
            {
                string frameText = m.CurrentFrame.ToString();
                Vector2 fSize = DenEmoTheme.HoverTipStyle.CalcSize(new GUIContent(frameText));
                float labelX  = sx - fSize.x * 0.5f;
                float labelY  = rect.y - fSize.y - 2;

                EditorGUI.DrawRect(new Rect(labelX - 2, labelY - 1, fSize.x + 4, fSize.y + 2), new Color(0.2f, 0.5f, 1f, 0.9f));
                GUI.Label(new Rect(labelX, labelY, fSize.x, fSize.y), frameText, DenEmoTheme.HoverTipStyle);
            }
        }

        // ─── Scrubber input ───────────────────────────────────────────────────
        // seekArea はルーラー＋スクラバー帯のトラック領域（ラベル列除外済み）。

        private void HandleScrubberInput(Rect seekArea, AnimationModeUI mode, EditorWindow window)
        {
            float trackX = seekArea.x;
            float trackW = seekArea.width;
            Rect trackR = seekArea;

            Event e = Event.current;

            if (e.type == EventType.MouseDown && trackR.Contains(e.mousePosition))
            {
                GUI.FocusControl(null);
                _isDraggingScrubber = true;
                SeekFromMouseX(e.mousePosition.x, trackX, trackW, mode, window);
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && _isDraggingScrubber)
            {
                SeekFromMouseX(e.mousePosition.x, trackX, trackW, mode, window);
                e.Use();
            }
            else if (e.type == EventType.MouseUp && _isDraggingScrubber)
            {
                _isDraggingScrubber = false;
                e.Use();
            }
        }

        private void SeekFromMouseX(float mouseX, float trackX, float trackW, AnimationModeUI mode, EditorWindow window)
        {
            var m = mode.ClipModel;
            float t = PixelToTime(mouseX, m.ClipLength, trackX, trackW);
            SeekTo(mode, m.SnapToFrame(t), window);
        }

        // ─── Action row（◆+ 全シェイプキー挿入 / フレーム単位の移動・削除） ──

        private void DrawKeyframeActionRow(AnimationModeUI mode, ShapeKeyModel shapeModel, EditorWindow window)
        {
            var m = mode.ClipModel;
            float[] allKeys = m.AllKeyTimes;

            Rect rowRect = GUILayoutUtility.GetRect(0, ACTION_ROW_HEIGHT, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rowRect, DenEmoTheme.Surface0);
                EditorGUI.DrawRect(new Rect(rowRect.x + _trackLabelWidth, rowRect.y, rowRect.width - _trackLabelWidth, 1), DenEmoTheme.Outline);
            }

            // ── ◆+ 表示中の全シェイプにキーを挿入 ─────────────────────────────
            // フィルター状態で対象が変わるため、ツールチップに対象件数を明示する
            int insertCount = 0;
            foreach (var it in shapeModel.Items)
                if (it.IsVisible && !it.IsVrcShape && !it.IsLipSyncShape) insertCount++;

            Rect insertRect = new Rect(rowRect.x + 4, rowRect.y + 8, _trackLabelWidth - 12, 20);
            if (GUI.Button(insertRect, new GUIContent("◆+", DenEmoLoc.Tf("ui.timeline.insertAll.tipN", insertCount)), DenEmoTheme.MiniButtonStyle))
            {
                var smr = shapeModel.TargetSkinnedMesh;
                if (smr != null && smr.sharedMesh != null)
                {
                    var entries = shapeModel.Items
                        .Where(i => i.IsVisible && !i.IsVrcShape && !i.IsLipSyncShape)
                        .Select(i => (i.Name, smr.GetBlendShapeWeight(i.Index)));
                    mode.Editor.RecordKeys(entries, m.CurrentTime, mode.CurrentInterp);
                    mode.ClearUnrecordedTweaks();
                    mode.Preview.SampleAt(m.CurrentTime);
                    window.Repaint();
                }
            }

            if (allKeys.Length == 0) return;

            float trackW = rowRect.width - _trackLabelWidth - RIGHT_PADDING;
            float trackX = rowRect.x + _trackLabelWidth;

            foreach (float kTime in allKeys)
            {
                float kx = TimeToPixel(kTime, m.ClipLength, trackX, trackW);
                if (kx < trackX - 10 || kx > trackX + trackW + 10) continue;

                // ↔ ドラッグハンドル（フレーム上の全キーを移動）
                Rect dragRect = new Rect(kx - 10, rowRect.y + 2, 20, 12);
                EditorGUIUtility.AddCursorRect(dragRect, MouseCursor.SlideArrow);
                GUI.Label(dragRect, new GUIContent("↔", DenEmoLoc.T("ui.timeline.moveFrame.tip")), DenEmoTheme.CenterCaptionStyle);
                HandleKeyframeDrag(dragRect, kTime, null, mode, window, trackX, trackW);

                // ✕ フレーム上の全キーを削除
                Rect btnRect = new Rect(kx - 10, rowRect.y + 16, 20, 20);
                if (GUI.Button(btnRect, new GUIContent("✕", DenEmoLoc.T("ui.timeline.frameDelete.tip")), DenEmoTheme.MiniButtonStyle))
                {
                    if (EditorUtility.DisplayDialog(
                        DenEmoLoc.T("dlg.timeline.deleteFrame.title"),
                        DenEmoLoc.Tf("dlg.timeline.deleteFrame.msg", kTime.ToString("F2")),
                        DenEmoLoc.T("dlg.yes"),
                        DenEmoLoc.T("dlg.no")))
                    {
                        mode.Editor.DeleteKeysAtTime(kTime);
                        mode.Preview.SampleAt(m.CurrentTime);
                        window.Repaint();
                    }
                }
            }
        }

        // ─── Keyframe drag（共通処理） ────────────────────────────────────────
        // shapeName == null で全トラック移動。他キーに衝突する場合は手前で停止する。

        private void HandleKeyframeDrag(
            Rect hitRect, float kTime, string shapeName,
            AnimationModeUI mode, EditorWindow window,
            float trackX, float trackW, bool seekOnDown = false)
        {
            Event e = Event.current;
            var m = mode.ClipModel;
            int frame = m.TimeToFrame(kTime);

            if (e.type == EventType.MouseDown && hitRect.Contains(e.mousePosition) && e.button == 0)
            {
                _isDraggingKeyframe  = true;
                _draggingFrame       = frame;
                _draggingShapeName   = shapeName;
                _dragKeyUndoRecorded = false;
                GUI.FocusControl(null);
                if (seekOnDown)
                {
                    mode.Playback.Stop();
                    m.CurrentTime = kTime;
                    mode.Preview.SampleAt(kTime);
                    window.Repaint();
                }
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && _isDraggingKeyframe
                     && _draggingFrame == frame && _draggingShapeName == shapeName)
            {
                float t = PixelToTime(e.mousePosition.x, m.ClipLength, trackX, trackW);
                int targetFrame = Mathf.RoundToInt(Mathf.Clamp(t * m.FPS, 0f, m.TotalFrames));

                if (targetFrame != _draggingFrame)
                {
                    int reached = mode.Editor.MoveKeys(
                        _draggingFrame, targetFrame, shapeName,
                        recordUndo: !_dragKeyUndoRecorded);
                    if (reached != _draggingFrame)
                    {
                        _dragKeyUndoRecorded = true;
                        _draggingFrame = reached;
                        m.CurrentTime  = m.FrameToTime(reached);
                        mode.Preview.SampleAt(m.CurrentTime);
                        window.Repaint();
                    }
                    if (reached != targetFrame)
                    {
                        // 他キーにブロックされた：停止位置の隣にあるブロック元キーを一瞬ハイライトする
                        _blockFlashFrame = reached + (targetFrame > reached ? 1 : -1);
                        _blockFlashUntil = EditorApplication.timeSinceStartup + 0.4;
                        window.Repaint();
                    }
                }
                e.Use();
            }
            else if (e.type == EventType.MouseUp && _isDraggingKeyframe && e.button == 0)
            {
                if (_draggingFrame == frame && _draggingShapeName == shapeName)
                {
                    _isDraggingKeyframe = false;
                    e.Use();
                }
            }
        }
    }
}
