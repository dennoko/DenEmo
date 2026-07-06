using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using DenEmo.Models;
using DenEmo.Core;

namespace DenEmo.UI
{
    /// <summary>
    /// クリップ内の各シェイプキー値レンジを再マップする折りたたみセクション（UI Toolkit）。
    ///
    /// 各キーフレーム値 v に対して: new_v = v * (max - min) / 100 + min
    ///
    /// max=100 / min=0（既定値）のシェイプは変更されない。
    /// max=80 にすると全値が [0, 80] にスケールされる。
    /// min=20 にすると全値が [20, 100] にスケールされる。
    /// </summary>
    public class AnimationClipCorrectionUI
    {
        private bool _expanded = false;
        private readonly Dictionary<string, float> _minValues = new Dictionary<string, float>();
        private readonly Dictionary<string, float> _maxValues = new Dictionary<string, float>();

        // Bind で配線される参照
        private AnimationClipModel         _clipModel;
        private AnimationClipEditor        _editor;
        private AnimationPreviewController _preview;
        private Func<bool>                 _hasTarget;
        private Action<string, int>        _setStatus;
        private EditorWindow               _window;

        // UI 要素
        private VisualElement _card;
        private VisualElement _content;
        private Button        _header;
        private Button        _apply;
        private Label         _desc, _empty, _colShape, _colMin, _colMax;
        private ScrollView    _rows;

        // 行リストの再構築判定
        private int           _rowsRevision = -1;
        private AnimationClip _rowsClip;

        // ─── Bind ─────────────────────────────────────────────────────────────

        public void Bind(
            VisualElement card,
            AnimationClipModel clipModel,
            AnimationClipEditor editor,
            AnimationPreviewController preview,
            Func<bool> hasTarget,
            Action<string, int> setStatus,
            EditorWindow window)
        {
            _card      = card;
            _clipModel = clipModel;
            _editor    = editor;
            _preview   = preview;
            _hasTarget = hasTarget;
            _setStatus = setStatus;
            _window    = window;

            _header   = card.Q<Button>("correction-header");
            _content  = card.Q<VisualElement>("correction-content");
            _desc     = card.Q<Label>("correction-desc");
            _empty    = card.Q<Label>("correction-empty");
            _colShape = card.Q<Label>("correction-col-shape");
            _colMin   = card.Q<Label>("correction-col-min");
            _colMax   = card.Q<Label>("correction-col-max");
            _rows     = card.Q<ScrollView>("correction-rows");
            _apply    = card.Q<Button>("correction-apply");

            _header.clicked += () =>
            {
                _expanded = !_expanded;
                Refresh();
            };
            _apply.clicked += ApplyCorrection;

            // クリップの差し替え・キーフレーム編集・ターゲット変更を反映する
            card.schedule.Execute(Refresh).Every(250);

            RefreshLabels();
            Refresh();
        }

        /// <summary>言語切替時に呼ぶ。</summary>
        public void RefreshLabels()
        {
            if (_header == null) return;
            _header.text     = FoldHeaderText();
            _desc.text       = DenEmoLoc.T("ui.correction.desc");
            _empty.text      = DenEmoLoc.T("ui.correction.noTracks");
            _colShape.text   = DenEmoLoc.T("ui.correction.col.shape");
            _colMin.text     = DenEmoLoc.T("ui.correction.col.min");
            _colMax.text     = DenEmoLoc.T("ui.correction.col.max");
            _colMin.tooltip  = DenEmoLoc.T("ui.correction.col.min.tip");
            _colMax.tooltip  = DenEmoLoc.T("ui.correction.col.max.tip");
            _apply.text      = DenEmoLoc.T("ui.correction.apply");
        }

        private string FoldHeaderText()
            => (_expanded ? "▼  " : "▶  ") + DenEmoLoc.T("ui.correction.title");

        // ─── Refresh ──────────────────────────────────────────────────────────

        private void Refresh()
        {
            if (_card == null) return;

            bool visible = _clipModel.Clip != null && (_hasTarget == null || _hasTarget());
            _card.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible) return;

            _header.text = FoldHeaderText();
            _content.style.display = _expanded ? DisplayStyle.Flex : DisplayStyle.None;
            if (!_expanded) return;

            if (_rowsRevision != _clipModel.Revision || !ReferenceEquals(_rowsClip, _clipModel.Clip))
                RebuildRows();
        }

        private void RebuildRows()
        {
            _rowsRevision = _clipModel.Revision;
            _rowsClip     = _clipModel.Clip;
            _rows.Clear();

            var tracks = _clipModel.Tracks;
            bool hasTracks = tracks.Count > 0;
            _empty.style.display = hasTracks ? DisplayStyle.None : DisplayStyle.Flex;
            _rows.style.display  = hasTracks ? DisplayStyle.Flex : DisplayStyle.None;
            _apply.style.display = hasTracks ? DisplayStyle.Flex : DisplayStyle.None;
            if (!hasTracks) return;

            foreach (var track in tracks)
            {
                string name = track.ShapeName;
                if (!_minValues.TryGetValue(name, out float min)) min = 0f;
                if (!_maxValues.TryGetValue(name, out float max)) max = 100f;

                var row = new VisualElement();
                row.AddToClassList("dennoko-correction-row");

                var label = new Label(name);
                label.AddToClassList("dennoko-col-grow");
                row.Add(label);

                row.Add(MakeRangeField(name, min, _minValues));
                row.Add(MakeRangeField(name, max, _maxValues));

                _rows.Add(row);
            }
        }

        private FloatField MakeRangeField(string shapeName, float initial, Dictionary<string, float> store)
        {
            var field = new FloatField { value = initial };
            field.RegisterValueChangedCallback(evt =>
            {
                float clamped = Mathf.Clamp(evt.newValue, 0f, 100f);
                store[shapeName] = clamped;
                if (!Mathf.Approximately(clamped, evt.newValue))
                    field.SetValueWithoutNotify(clamped);
            });
            return field;
        }

        // ─── Apply ────────────────────────────────────────────────────────────

        private void ApplyCorrection()
        {
            if (_clipModel.Clip == null) return;

            bool changed = _editor.ApplyValueCorrection(_minValues, _maxValues);
            if (changed)
            {
                if (_preview.IsActive)
                    _preview.SampleAt(_clipModel.CurrentTime);
                _setStatus?.Invoke(DenEmoLoc.T("status.correction.applied"), 1);
            }
            else
            {
                _setStatus?.Invoke(DenEmoLoc.T("status.correction.none"), 0);
            }
            _window?.Repaint();
        }
    }
}
