using UnityEditor;
using UnityEngine;
using DenEmo.Models;
using DenEmo.Core;

namespace DenEmo.UI
{
    public partial class AnimationTimelineUI
    {
        // ─── Keyframe tracks ──────────────────────────────────────────────────

        private void DrawKeyframeTracks(
            AnimationClipModel clipModel, AnimationPreviewController preview,
            ShapeKeyModel shapeModel, string smrPath, InterpolationType currentInterp, EditorWindow window)
        {
            var shapes = clipModel.GetShapeNamesWithKeys(smrPath);
            if (shapes.Count == 0)
            {
                GUILayout.Label(
                    DenEmoLoc.EnglishMode
                        ? "No keyframes yet. Use 🔴 Auto-Key or the ◆ button to add keys."
                        : "キーフレームがまだありません。🔴 Auto-Key または ◆ ボタンでキーを追加してください。",
                    DenEmoTheme.CaptionStyle);
                return;
            }

            float rowsHeight = Mathf.Min(shapes.Count * (TRACK_ROW_HEIGHT + 1), MAX_TRACKS_HEIGHT);
            _tracksScroll = EditorGUILayout.BeginScrollView(_tracksScroll, GUILayout.Height(rowsHeight));

            foreach (string shapeName in shapes)
                DrawTrackRow(shapeName, clipModel, preview, shapeModel, smrPath, currentInterp, window);

            EditorGUILayout.EndScrollView();
        }

        private void DrawTrackRow(
            string shapeName, AnimationClipModel clipModel, AnimationPreviewController preview,
            ShapeKeyModel shapeModel, string smrPath, InterpolationType currentInterp, EditorWindow window)
        {
            Rect rowRect = GUILayoutUtility.GetRect(0, TRACK_ROW_HEIGHT, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rowRect, DenEmoTheme.Surface0);
                float cy = rowRect.y + rowRect.height * 0.5f;
                EditorGUI.DrawRect(new Rect(rowRect.x + _trackLabelWidth, cy, rowRect.width - _trackLabelWidth, 1), DenEmoTheme.Outline);
            }

            // Layout: [label][border(4px)][◆(25px)][gap(4px)][✕(20px)][gap(4px)] → last 53px reserved for buttons+border
            const float BORDER_W       = 4f;
            const float ADD_BTN_W      = 25f;
            const float DEL_BTN_W      = 20f;
            const float BTN_GAP        = 4f;
            // Total right-of-label: BORDER_W + ADD_BTN_W + BTN_GAP + DEL_BTN_W + BTN_GAP = 57
            const float LABEL_RIGHT_RESERVE = BORDER_W + ADD_BTN_W + BTN_GAP + DEL_BTN_W + BTN_GAP;

            float labelMaxW  = _trackLabelWidth - LABEL_RIGHT_RESERVE - 8f;
            float borderX    = rowRect.x + _trackLabelWidth - LABEL_RIGHT_RESERVE;
            float addBtnX    = borderX + BORDER_W;
            float delBtnX    = addBtnX + ADD_BTN_W + BTN_GAP;
            float trackW     = rowRect.width - _trackLabelWidth - RIGHT_PADDING;
            float trackX     = rowRect.x + _trackLabelWidth;

            GUI.Label(
                new Rect(rowRect.x + 8, rowRect.y + 4, labelMaxW, rowRect.height - 8),
                shapeName, DenEmoTheme.CaptionStyle);

            // ── Splitter border between label and buttons ──────────────────────
            Rect borderRect = new Rect(borderX, rowRect.y, BORDER_W, rowRect.height);
            if (Event.current.type == EventType.Repaint)
            {
                float bCx = borderX + BORDER_W * 0.5f;
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
                _trackLabelWidth += ev.delta.x;
                _trackLabelWidth = Mathf.Clamp(_trackLabelWidth, 80f, rowRect.width * 0.8f);
                window.Repaint();
                ev.Use();
            }
            else if (ev.type == EventType.MouseUp && _isDraggingLabelWidth && ev.button == 0)
            {
                _isDraggingLabelWidth = false;
                ev.Use();
            }

            // ── ◆ Add key button ───────────────────────────────────────────────
            if (GUI.Button(
                new Rect(addBtnX, rowRect.y + 2, ADD_BTN_W, 20),
                new GUIContent("◆", DenEmoLoc.EnglishMode ? "Add / Update Key" : "キーの追加・更新"),
                DenEmoTheme.MiniButtonStyle))
            {
                var smr = shapeModel?.TargetSkinnedMesh;
                if (smr != null && smr.sharedMesh != null)
                {
                    int index = smr.sharedMesh.GetBlendShapeIndex(shapeName);
                    if (index >= 0)
                    {
                        float val = smr.GetBlendShapeWeight(index);
                        preview.RecordKeyframe(shapeName, smrPath, clipModel.CurrentTime, val, currentInterp);
                        preview.SampleAt(clipModel.CurrentTime);
                        window.Repaint();
                    }
                }
            }

            // ── ✕ Delete track button ──────────────────────────────────────────
            if (GUI.Button(
                new Rect(delBtnX, rowRect.y + 2, DEL_BTN_W, 20),
                new GUIContent("✕", DenEmoLoc.EnglishMode ? "Delete this track" : "このトラックを削除"),
                DenEmoTheme.MiniButtonStyle))
            {
                if (EditorUtility.DisplayDialog(
                    DenEmoLoc.EnglishMode ? "Delete Track" : "トラックの削除",
                    DenEmoLoc.EnglishMode ? $"Delete all keyframes for '{shapeName}'?" : $"'{shapeName}'のすべてのキーフレームを削除しますか？",
                    DenEmoLoc.EnglishMode ? "Yes" : "はい",
                    DenEmoLoc.EnglishMode ? "No" : "いいえ"))
                {
                    preview.DeleteAllKeyframesForShape(shapeName, smrPath);
                    window.Repaint();
                }
            }

            if (Event.current.type == EventType.Repaint)
            {
                float sx = TimeToPixel(clipModel.CurrentTime, clipModel.ClipLength, trackX, trackW);
                EditorGUI.DrawRect(
                    new Rect(sx - 1, rowRect.y, 2, rowRect.height),
                    new Color(1f, 1f, 1f, 0.4f));
            }

            float[] keyTimes = clipModel.GetKeyTimesForShape(shapeName, smrPath);
            EnsureKfLabelStyle();
            foreach (float kTime in keyTimes)
            {
                float kx = TimeToPixel(kTime, clipModel.ClipLength, trackX, trackW);
                if (kx < trackX - DIAMOND_SIZE || kx > trackX + trackW + DIAMOND_SIZE) continue;
                float ky = rowRect.y + rowRect.height * 0.5f;
                Rect hitR = new Rect(kx - DIAMOND_SIZE - 2, ky - DIAMOND_SIZE - 2, (DIAMOND_SIZE + 2) * 2, (DIAMOND_SIZE + 2) * 2);

                bool isCurrent = Mathf.Abs(kTime - clipModel.CurrentTime) <= (clipModel.FPS > 0f ? 0.5f / clipModel.FPS : 0.01f);

                if (Event.current.type == EventType.Repaint)
                {
                    var style = new GUIStyle(_kfLabelStyle)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = isCurrent ? Color.white : DenEmoTheme.SemanticInfo },
                        fontSize = 10
                    };
                    GUI.Label(new Rect(kx - 8, ky - 8, 16, 16), "◆", style);

                    bool isHovered = hitR.Contains(Event.current.mousePosition);
                    if (isHovered)
                    {
                        EnsureHoverLabelStyle();
                        int   frameNum = Mathf.RoundToInt(kTime * clipModel.FPS);
                        float val      = clipModel.GetShapeKeyValue(shapeName, kTime);
                        string tipText = $"F:{frameNum}  V:{val:F1}";

                        Vector2 size   = _hoverLabelStyle.CalcSize(new GUIContent(tipText));
                        float   labelX = (kx + 10 + size.x < trackX + trackW)
                            ? kx + 10
                            : kx - 10 - size.x;
                        float labelY = ky - size.y * 0.5f;

                        Rect bgRect = new Rect(labelX - 3, labelY - 2, size.x + 6, size.y + 4);
                        EditorGUI.DrawRect(bgRect, new Color(0.1f, 0.1f, 0.1f, 0.85f));
                        GUI.Label(new Rect(labelX, labelY, size.x, size.y), tipText, _hoverLabelStyle);
                    }
                }

                EditorGUIUtility.AddCursorRect(hitR, MouseCursor.SlideArrow);
                HandleKeyframeDrag(hitR, kTime, shapeName, clipModel, preview, smrPath, window, trackX, trackW, true);

                if (Event.current.type == EventType.ContextClick && hitR.Contains(Event.current.mousePosition))
                {
                    string sn = shapeName;
                    float kt = kTime;
                    var menu = new GenericMenu();
                    menu.AddItem(
                        new GUIContent(DenEmoLoc.EnglishMode ? "Delete" : "削除"),
                        false,
                        () => { preview.DeleteKeyframe(sn, smrPath, kt); window.Repaint(); });
                    menu.AddSeparator("");
                    menu.AddItem(
                        new GUIContent(DenEmoLoc.EnglishMode ? "Copy frame" : "フレームをコピー"),
                        false,
                        () =>
                        {
                            _keyClipboard.Clear();
                            float tol2 = clipModel.FPS > 0f ? 0.5f / clipModel.FPS : 0.01f;
                            foreach (string sh in clipModel.GetShapeNamesWithKeys(smrPath))
                            {
                                foreach (float t2 in clipModel.GetKeyTimesForShape(sh, smrPath))
                                {
                                    if (Mathf.Abs(t2 - kt) > tol2) continue;
                                    _keyClipboard.Add(new KeyClipboardEntry
                                    {
                                        ShapeName    = sh,
                                        RelativeTime = 0f,
                                        Value        = clipModel.GetShapeKeyValue(sh, t2),
                                        Interp       = clipModel.GetKeyInterpolationType(sh, t2, smrPath),
                                    });
                                }
                            }
                        });
                    if (_hasClipboardData)
                        menu.AddItem(
                            new GUIContent(DenEmoLoc.EnglishMode ? "Paste at current time" : "現在時刻にペースト"),
                            false,
                            () =>
                            {
                                foreach (var entry in _keyClipboard)
                                {
                                    float pt = Mathf.Clamp(clipModel.CurrentTime + entry.RelativeTime, 0f, clipModel.ClipLength);
                                    pt = Mathf.Round(pt * clipModel.FPS) / clipModel.FPS;
                                    preview.RecordKeyframe(entry.ShapeName, smrPath, pt, entry.Value, entry.Interp);
                                }
                                preview.SampleAt(clipModel.CurrentTime);
                                window.Repaint();
                            });
                    else
                        menu.AddDisabledItem(new GUIContent(DenEmoLoc.EnglishMode ? "Paste at current time" : "現在時刻にペースト"));
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("Step"), false, () => { preview.ChangeInterpolation(sn, smrPath, kt, InterpolationType.Step); preview.SampleAt(clipModel.CurrentTime); window.Repaint(); });
                    menu.AddItem(new GUIContent("Linear"), false, () => { preview.ChangeInterpolation(sn, smrPath, kt, InterpolationType.Linear); preview.SampleAt(clipModel.CurrentTime); window.Repaint(); });
                    menu.AddItem(new GUIContent("Ease"), false, () => { preview.ChangeInterpolation(sn, smrPath, kt, InterpolationType.Ease); preview.SampleAt(clipModel.CurrentTime); window.Repaint(); });
                    menu.ShowAsContext();
                    Event.current.Use();
                }
            }

            // Right-click on empty track area (no keyframe consumed the event) → paste menu
            Rect trackClickArea = new Rect(trackX, rowRect.y, trackW, rowRect.height);
            if (Event.current.type == EventType.ContextClick && trackClickArea.Contains(Event.current.mousePosition))
            {
                var menu = new GenericMenu();
                if (_hasClipboardData)
                    menu.AddItem(
                        new GUIContent(DenEmoLoc.EnglishMode ? "Paste at current time" : "現在時刻にペースト"),
                        false,
                        () =>
                        {
                            foreach (var entry in _keyClipboard)
                            {
                                float pt = Mathf.Clamp(clipModel.CurrentTime + entry.RelativeTime, 0f, clipModel.ClipLength);
                                pt = Mathf.Round(pt * clipModel.FPS) / clipModel.FPS;
                                preview.RecordKeyframe(entry.ShapeName, smrPath, pt, entry.Value, entry.Interp);
                            }
                            preview.SampleAt(clipModel.CurrentTime);
                            window.Repaint();
                        });
                else
                    menu.AddDisabledItem(new GUIContent(DenEmoLoc.EnglishMode ? "Paste at current time" : "現在時刻にペースト"));
                menu.ShowAsContext();
                Event.current.Use();
            }

            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.yMax - 1, rowRect.width, 1), DenEmoTheme.Surface2);
        }
    }
}
