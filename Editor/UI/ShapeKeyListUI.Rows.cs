using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using DenEmo.Models;

namespace DenEmo.UI
{
    public partial class ShapeKeyListUI
    {
        // ─── バインディング ───────────────────────────────────────────────────

        private class RowBinding
        {
            public ShapeKeyItem Left;
            public ShapeKeyItem Right; // Merged 行のみ非 null
            public Slider Slider;
            public Toggle Include;
            public Button Fav, Zero, Key;
            public Label  Name;
            public bool   Dragging;
            public bool   DragUndoDone;
        }

        private class GroupBinding
        {
            public int    Start, End;
            public string CollapseKey;
            public Button Fold;
            public Label  Count;
            public Toggle Toggle;
        }

        // ─── 再構築 ───────────────────────────────────────────────────────────

        private void RebuildRows()
        {
            _rowBindings.Clear();
            _groupBindings.Clear();
            _anyDragging = false;
            _rowsScroll.Clear();

            bool hasItems = _model.Items.Count > 0;
            bool anyRows  = _plan.Count > 0;
            _emptyLabel.style.display   = hasItems ? DisplayStyle.None : DisplayStyle.Flex;
            _noMatchLabel.style.display = (hasItems && !anyRows) ? DisplayStyle.Flex : DisplayStyle.None;

            if (_rowTemplate == null) return;

            foreach (var e in _plan)
            {
                switch (e.Kind)
                {
                    case RowKind.MeshHeader:
                        _rowsScroll.Add(MakeMeshHeader(e.Text));
                        break;
                    case RowKind.GroupHeader:
                        _rowsScroll.Add(MakeGroupHeader(e));
                        break;
                    case RowKind.Single:
                        _rowsScroll.Add(MakeRow(e.Item, null, e.Item.Name, e.Indent));
                        break;
                    case RowKind.Merged:
                        _rowsScroll.Add(MakeRow(e.Item, e.Right, e.Text + "_LR", e.Indent));
                        break;
                }
            }
        }

        private VisualElement MakeMeshHeader(string meshName)
        {
            var label = new Label(meshName);
            label.AddToClassList("dennoko-section-title");
            label.AddToClassList("dennoko-list-mesh-header");
            return label;
        }

        private VisualElement MakeGroupHeader(RowEntry entry)
        {
            var header = new VisualElement();
            header.AddToClassList("dennoko-list-group-header");

            var fold = new Button();
            fold.AddToClassList("dennoko-group-fold");
            var label = new Label(entry.Text);
            label.AddToClassList("dennoko-group-label");
            var count = new Label();
            count.AddToClassList("dennoko-group-count");
            var toggle = new Toggle();
            toggle.AddToClassList("dennoko-row-include");

            header.Add(fold);
            header.Add(label);
            header.Add(count);
            header.Add(toggle);

            var gb = new GroupBinding
            {
                Start = entry.SegStart, End = entry.SegEnd,
                CollapseKey = entry.CollapseKey,
                Fold = fold, Count = count, Toggle = toggle,
            };

            fold.clicked += () =>
            {
                if (!_collapsedGroups.Remove(gb.CollapseKey))
                    _collapsedGroups.Add(gb.CollapseKey);
                _structureDirty = true;
                TickStructureAndSync(); // クリックに即応して開閉する
            };

            toggle.RegisterValueChangedCallback(evt =>
            {
                if (gb.End > _model.Items.Count) return;
                for (int i = gb.Start; i < gb.End; i++)
                {
                    var item = _model.Items[i];
                    if (!item.IsVrcExcluded(_model.IsAnimationMode) && !item.IsLipSyncShape) item.IsIncluded = evt.newValue;
                }
                OnIncludeFlagsChanged?.Invoke();
                SyncDynamicState();
            });

            _groupBindings.Add(gb);
            return header;
        }

        // ─── 行生成 ───────────────────────────────────────────────────────────

        private VisualElement MakeRow(ShapeKeyItem left, ShapeKeyItem right, string displayName, bool indent)
        {
            var container = _rowTemplate.CloneTree();
            var row = container.Q<VisualElement>("shape-row");

            var b = new RowBinding
            {
                Left    = left,
                Right   = right,
                Fav     = row.Q<Button>("row-fav"),
                Include = row.Q<Toggle>("row-include"),
                Name    = row.Q<Label>("row-name"),
                Zero    = row.Q<Button>("row-zero"),
                Slider  = row.Q<Slider>("row-slider"),
                Key     = row.Q<Button>("row-key"),
            };

            row.Q<VisualElement>("row-indent").style.display = indent ? DisplayStyle.Flex : DisplayStyle.None;

            b.Name.text    = displayName;
            b.Name.tooltip = displayName;

            b.Fav.tooltip = DenEmoLoc.T(left.IsFavorite ? "ui.fav.remove" : "ui.fav.add");
            b.Fav.clicked += () =>
            {
                b.Left.IsFavorite = !b.Left.IsFavorite;
                OnFavoriteChanged?.Invoke(b.Left.Name, b.Left.IsFavorite);
                b.Fav.tooltip = DenEmoLoc.T(b.Left.IsFavorite ? "ui.fav.remove" : "ui.fav.add");
                SyncDynamicState();
            };

            b.Include.RegisterValueChangedCallback(evt =>
            {
                b.Left.IsIncluded = evt.newValue;
                if (b.Right != null) b.Right.IsIncluded = evt.newValue;
                OnIncludeFlagsChanged?.Invoke();
                SyncDynamicState();
            });

            b.Zero.tooltip = DenEmoLoc.T(right != null ? "ui.row.zeroLR.tip" : "ui.row.zero.tip");
            b.Zero.clicked += () => OnZeroClicked(b);

            b.Slider.showInputField = true;
            b.Slider.SetValueWithoutNotify(left.Value);
            b.Slider.RegisterValueChangedCallback(evt => OnRowSliderChanged(b, evt.newValue));

            // ドラッグジェスチャの検出（Undo を 1 回に抑え、SMR 反映をスロットルする）
            var dragArea = b.Slider.Q(className: "unity-base-slider__drag-container") ?? (VisualElement)b.Slider;
            dragArea.RegisterCallback<PointerDownEvent>(_ => BeginRowDrag(b), TrickleDown.TrickleDown);
            b.Slider.RegisterCallback<PointerUpEvent>(_ => EndRowDrag(b), TrickleDown.TrickleDown);
            b.Slider.RegisterCallback<PointerCaptureOutEvent>(_ => EndRowDrag(b), TrickleDown.TrickleDown);

            b.Key.clicked += () => OnKeyframeClicked(b);

            _rowBindings.Add(b);
            return container;
        }

        // ─── 行操作ハンドラ ───────────────────────────────────────────────────

        private void OnZeroClicked(RowBinding b)
        {
            var smr = b.Left.OwnerSmr ?? _model.TargetSkinnedMesh;

            if (_animContext != null)
            {
                _animContext.OnValueChanged?.Invoke(b.Left, _model, 0f);
                if (b.Right != null) _animContext.OnValueChanged?.Invoke(b.Right, _model, 0f);
            }
            else if (b.Right != null)
            {
                if (smr != null) Undo.RecordObject(smr, "Reset Shape Key");
                b.Left.Value = 0f;
                b.Right.Value = 0f;
                if (smr != null)
                {
                    smr.SetBlendShapeWeight(b.Left.Index, 0f);
                    smr.SetBlendShapeWeight(b.Right.Index, 0f);
                }
            }
            else if (b.Left.Value != 0f)
            {
                if (smr != null) Undo.RecordObject(smr, "Reset Shape Key");
                b.Left.Value = 0f;
                if (smr != null) smr.SetBlendShapeWeight(b.Left.Index, 0f);
            }

            b.Slider.SetValueWithoutNotify(b.Left.Value);
        }

        private void BeginRowDrag(RowBinding b)
        {
            b.Dragging     = true;
            b.DragUndoDone = false;
            _anyDragging   = true;
            _animContext?.OnSliderDragStateChanged?.Invoke(true);
        }

        private void EndRowDrag(RowBinding b)
        {
            if (!b.Dragging) return;
            b.Dragging = false;

            _anyDragging = false;
            foreach (var rb in _rowBindings)
                if (rb.Dragging) { _anyDragging = true; break; }

            if (_animContext != null)
            {
                _animContext.OnSliderDragStateChanged?.Invoke(false);
                return;
            }

            // ドラッグ終了時にスロットルを破棄し、最終値を確定反映する
            StopThrottle();
            var smr = b.Left.OwnerSmr ?? _model.TargetSkinnedMesh;
            if (smr != null)
            {
                smr.SetBlendShapeWeight(b.Left.Index, b.Left.Value);
                if (b.Right != null) smr.SetBlendShapeWeight(b.Right.Index, b.Right.Value);
            }
        }

        private void OnRowSliderChanged(RowBinding b, float newValue)
        {
            if (_animContext != null)
            {
                _animContext.OnValueChanged?.Invoke(b.Left, _model, newValue);
                if (b.Right != null) _animContext.OnValueChanged?.Invoke(b.Right, _model, newValue);
                return;
            }

            var smr = b.Left.OwnerSmr ?? _model.TargetSkinnedMesh;

            if (b.Dragging)
            {
                if (!b.DragUndoDone)
                {
                    if (smr != null) Undo.RecordObject(smr, "Change Shape Key");
                    b.DragUndoDone = true;
                }
                b.Left.Value = newValue;
                QueuePendingApply(b.Left, newValue);
                if (b.Right != null)
                {
                    b.Right.Value = newValue;
                    QueuePendingApply(b.Right, newValue);
                }
            }
            else
            {
                // 入力欄への直接入力など、ドラッグ外の変更
                if (smr != null) Undo.RecordObject(smr, "Change Shape Key");
                b.Left.Value = newValue;
                if (smr != null) smr.SetBlendShapeWeight(b.Left.Index, newValue);
                if (b.Right != null)
                {
                    b.Right.Value = newValue;
                    if (smr != null) smr.SetBlendShapeWeight(b.Right.Index, newValue);
                }
            }
        }

        private void OnKeyframeClicked(RowBinding b)
        {
            var ctx = _animContext;
            if (ctx == null) return;

            if (b.Right != null)
            {
                float rightVal = b.Right.Value;
                ctx.OnKeyframeToggle?.Invoke(b.Left, _model);
                b.Right.Value = rightVal;
                ctx.OnKeyframeToggle?.Invoke(b.Right, _model);
            }
            else
            {
                ctx.OnKeyframeToggle?.Invoke(b.Left, _model);
            }
            SyncDynamicState();
        }

        // ─── 動的状態の同期 ───────────────────────────────────────────────────

        private void SyncDynamicState()
        {
            var ctx = _animContext;
            bool showKey = ctx != null;

            foreach (var b in _rowBindings)
            {
                if (!b.Dragging)
                {
                    float v = b.Left.Value;
                    if (!Mathf.Approximately(b.Slider.value, v))
                        b.Slider.SetValueWithoutNotify(v);
                }

                bool inc = b.Right != null ? (b.Left.IsIncluded && b.Right.IsIncluded) : b.Left.IsIncluded;
                if (b.Include.value != inc) b.Include.SetValueWithoutNotify(inc);
                b.Name.EnableInClassList("dennoko-row-name--dim", !inc);

                bool fav = b.Left.IsFavorite;
                string favIcon = fav ? "★" : "☆";
                if (b.Fav.text != favIcon) b.Fav.text = favIcon;
                b.Fav.EnableInClassList("dennoko-row-icon-button--on", fav);

                b.Key.style.display = showKey ? DisplayStyle.Flex : DisplayStyle.None;
                if (showKey)
                {
                    bool hasKey = ctx.HasKeyframeAtCurrentTime?.Invoke(b.Left.Name) ?? false;
                    string icon = hasKey ? "◆" : "◇";
                    if (b.Key.text != icon)
                    {
                        b.Key.text    = icon;
                        b.Key.tooltip = DenEmoLoc.T(hasKey ? "ui.key.remove" : "ui.key.add");
                    }
                    b.Key.EnableInClassList("dennoko-row-icon-button--on", hasKey);
                }
            }

            var trackNames = ctx?.TrackShapeNames;
            foreach (var g in _groupBindings)
            {
                if (g.End > _model.Items.Count) continue;

                int enabled = 0, visible = 0;
                for (int i = g.Start; i < g.End; i++)
                {
                    var it = _model.Items[i];
                    if (!it.IsVisible || it.IsLipSyncShape) continue;
                    if (trackNames != null && !trackNames.Contains(it.Name)) continue;
                    visible++;
                    if (it.IsIncluded) enabled++;
                }

                bool allOn  = visible > 0 && enabled == visible;
                bool allOff = enabled == 0;
                string suffix = allOn ? DenEmoLoc.T("ui.group.all")
                    : allOff ? DenEmoLoc.T("ui.group.none")
                    : DenEmoLoc.T("ui.group.some");
                string text = $"{enabled}/{visible}  {suffix}";
                if (g.Count.text != text) g.Count.text = text;
                if (g.Toggle.value != allOn) g.Toggle.SetValueWithoutNotify(allOn);

                string foldIcon = _collapsedGroups.Contains(g.CollapseKey) ? "▶" : "▼";
                if (g.Fold.text != foldIcon) g.Fold.text = foldIcon;
            }
        }
    }
}
