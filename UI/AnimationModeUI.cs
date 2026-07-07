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
    // ─── Animation draw context ───────────────────────────────────────────────

    /// <summary>
    /// Animation モード時に ShapeKeyListUI へ渡すコンテキスト。
    /// 値変更の横取りとキーフレームインジケータ表示を提供する。
    /// AnimationModeUI が単一インスタンスを使い回し、毎フレームの割り当てを避ける。
    /// </summary>
    public class AnimationDrawContext
    {
        public bool IsRecording;
        /// <summary>現在の再生ヘッド位置にキーフレームがあるか。</summary>
        public Func<string, bool> HasKeyframeAtCurrentTime;
        /// <summary>スライダー操作時に呼ばれる。録画状態によってルーティングされる。</summary>
        public Action<ShapeKeyItem, ShapeKeyModel, float> OnValueChanged;
        /// <summary>◆/◇ キーフレームトグルボタンのクリック時に呼ばれる。</summary>
        public Action<ShapeKeyItem, ShapeKeyModel> OnKeyframeToggle;
        /// <summary>非 null のとき、このセットに含まれるシェイプのみリスト表示する。</summary>
        public HashSet<string> TrackShapeNames;
        /// <summary>トラックのみ表示フィルターが有効か。</summary>
        public bool TrackFilterEnabled;
        /// <summary>トラックのみ表示フィルターを切り替える。</summary>
        public Action OnToggleTrackFilter;
        /// <summary>
        /// UI Toolkit スライダーのドラッグ開始(true)/終了(false)を通知する。
        /// UITK スライダーは GUIUtility.hotControl を使わないため、キー記録フラッシュの
        /// ジェスチャ検知をこの通知で補完する。
        /// </summary>
        public Action<bool> OnSliderDragStateChanged;
    }

    // ─── AnimationModeUI ─────────────────────────────────────────────────────

    /// <summary>
    /// マルチフレーム（Animation）モードのオーケストレータ。
    /// ClipModel（状態+キャッシュ）・ClipEditor（変更）・Preview（表示）・Playback（再生）を所有し、
    /// DenEmoWindow からの描画呼び出しを各 UI に振り分ける。
    /// </summary>
    public class AnimationModeUI
    {
        // ─── Sub-objects ──────────────────────────────────────────────────────

        public AnimationClipModel         ClipModel    { get; } = new AnimationClipModel();
        public AnimationClipEditor        Editor       { get; }
        public AnimationPreviewController Preview      { get; } = new AnimationPreviewController();
        public AnimationPlayback          Playback     { get; } = new AnimationPlayback();
        public AnimationClipCorrectionUI  CorrectionUI { get; } = new AnimationClipCorrectionUI();

        public AnimationModeUI()
        {
            Editor = new AnimationClipEditor(ClipModel);
        }

        // ─── State ────────────────────────────────────────────────────────────

        public bool              IsRecording        { get; set; }
        public InterpolationType CurrentInterp      { get; set; } = InterpolationType.Ease;
        public bool              TrackFilterEnabled { get; set; }

        /// <summary>ステータスバーへの通知先（DenEmoWindow が配線する）。</summary>
        public Action<string, int> StatusSink { get; set; }

        // 再利用する描画コンテキストとトラック名セット
        private AnimationDrawContext _drawContext;
        private readonly HashSet<string> _trackNames = new HashSet<string>();
        private int _trackNamesRevision = -1;

        // ── スライダードラッグ中のキー記録スロットル（A-1 対策） ──────────────
        // ドラッグイベント毎に Undo.RecordObject + SetEditorCurve を呼ぶとクリップ全体の
        // シリアライズ/再構築が滞留しフリーズ級になるため、SMR ウェイトへの即時反映と
        // キー記録のフラッシュを分離する。
        private const double RECORD_FLUSH_INTERVAL_SEC = 0.05;
        private readonly Dictionary<string, float> _pendingRecords = new Dictionary<string, float>();
        private double _lastRecordFlush;
        private bool   _dragUndoRecorded; // 同一ドラッグジェスチャ内で Undo スナップショットを 1 回に抑える
        private bool   _externalSliderDrag; // UI Toolkit スライダーのドラッグ中フラグ（hotControl の代替）

        // REC オフ・キーなしで動かした（クリップに記録されない）シェイプ。シーク時に警告を出す。
        private readonly HashSet<string> _unrecordedTweaks = new HashSet<string>();

        // ── UI Toolkit: クリップ設定カードの要素（BindClipSectionUI で配線） ──
        private ObjectField   _clipField;
        private Button        _clipNewButton;
        private Label         _clipSectionTitle;
        private Label         _clipFieldLabel;
        private VisualElement _clipGuide;
        private Label         _guide1, _guide2, _guide3;
        private Label         _conflictWarn;

        // ─── Lifecycle ────────────────────────────────────────────────────────

        public void OnEnable(ShapeKeyModel shapeModel)
        {
            if (ClipModel.Clip != null && shapeModel.TargetSkinnedMesh != null)
                StartPreview(shapeModel);
        }

        public void OnDisable()
        {
            FlushPendingRecords(force: true);
            Playback.Stop();
            IsRecording = false;
            Preview.Stop();
        }

        /// <summary>EditorApplication.update から呼ぶ。再生を進め、ドラッグ終了時の記録フラッシュを行う。</summary>
        public void OnUpdate(EditorWindow window)
        {
            bool sampled = Playback.Tick(ClipModel, Preview, window);

            // 分離タイムラインウィンドウは常時 Repaint 購読をやめたため、再生中はここから再描画する
            if (sampled && DenEmoTimelineWindow.Instance != null &&
                !ReferenceEquals(window, DenEmoTimelineWindow.Instance))
            {
                DenEmoTimelineWindow.Instance.Repaint();
            }

            // ドラッグジェスチャ終了（hotControl 解放）を検知して未フラッシュのキー記録を確定する
            if (GUIUtility.hotControl == 0 && !_externalSliderDrag)
            {
                if (_pendingRecords.Count > 0)
                {
                    FlushPendingRecords(force: true);
                    window.Repaint();
                }
                _dragUndoRecorded = false;
            }
        }

        /// <summary>Undo/Redo 後に呼ぶ。キャッシュを無効化してビューポートを同期する。</summary>
        public void OnUndoRedo()
        {
            ClipModel.MarkDirty();
            if (Preview.IsActive)
                Preview.SampleAt(ClipModel.CurrentTime);
        }

        // ─── Preview management ───────────────────────────────────────────────

        public void StartPreview(ShapeKeyModel shapeModel)
        {
            if (shapeModel?.TargetSkinnedMesh == null) return;
            ClipModel.SmrPath = ShapeKeyModel.ComputeSmrPath(shapeModel.TargetSkinnedMesh);
            Preview.Start(ClipModel, shapeModel);
        }

        public void StopPreview()
        {
            FlushPendingRecords(force: true);
            Playback.Stop();
            Preview.Stop();
        }

        // ─── Pending record throttle ──────────────────────────────────────────

        /// <summary>
        /// スロットル中のキー記録をクリップへ確定する。force=false の場合は前回フラッシュから
        /// 一定時間経過しているときのみ実行する。Undo スナップショットはジェスチャ内で 1 回だけ取る。
        /// </summary>
        private void FlushPendingRecords(bool force)
        {
            if (_pendingRecords.Count == 0 || ClipModel.Clip == null) return;

            double now = EditorApplication.timeSinceStartup;
            if (!force && now - _lastRecordFlush < RECORD_FLUSH_INTERVAL_SEC) return;
            _lastRecordFlush = now;

            bool recordUndo = !_dragUndoRecorded;
            // 保留中の全シェイプを 1 回の SetEditorCurves コミットにまとめる（LR 同期や複数スライダー操作で
            // シェイプごとの逐次コミットによるクリップ内部再構築を避ける）。
            var entries = new List<(string, float)>(_pendingRecords.Count);
            foreach (var kv in _pendingRecords)
            {
                entries.Add((kv.Key, kv.Value));
                _unrecordedTweaks.Remove(kv.Key);
            }
            Editor.RecordKeysThrottled(entries, ClipModel.CurrentTime, CurrentInterp, recordUndo);
            _dragUndoRecorded = true;
            _pendingRecords.Clear();

            Preview.SampleAt(ClipModel.CurrentTime);
        }

        /// <summary>
        /// シーク直前に呼ぶ。未フラッシュのキー記録を現在時刻で確定し、
        /// クリップに記録されないまま破棄されるスライダー変更があれば警告する。
        /// </summary>
        public void NotifyBeforeSeek()
        {
            FlushPendingRecords(force: true);

            if (_unrecordedTweaks.Count > 0)
            {
                StatusSink?.Invoke(DenEmoLoc.Tf("status.anim.tweaksDiscarded", _unrecordedTweaks.Count), 2);
                _unrecordedTweaks.Clear();
            }
        }

        /// <summary>◆+（全シェイプ挿入）などでまとめてキーが記録されたときに呼ぶ。</summary>
        public void ClearUnrecordedTweaks() => _unrecordedTweaks.Clear();

        /// <summary>
        /// UI Toolkit スライダーのドラッグ状態変化を受け取る。終了時は未フラッシュの
        /// キー記録を確定し、次のジェスチャで新しい Undo スナップショットを取れるようにする。
        /// </summary>
        private void NotifySliderDrag(bool dragging)
        {
            if (_externalSliderDrag == dragging) return;
            _externalSliderDrag = dragging;
            if (!dragging)
            {
                if (_pendingRecords.Count > 0)
                    FlushPendingRecords(force: true);
                _dragUndoRecorded = false;
            }
        }

        public void OnTargetChanged(ShapeKeyModel shapeModel)
        {
            FlushPendingRecords(force: true);
            Preview.Stop();
            if (shapeModel?.TargetSkinnedMesh != null)
            {
                ClipModel.SmrPath = ShapeKeyModel.ComputeSmrPath(shapeModel.TargetSkinnedMesh);
                if (ClipModel.Clip != null)
                    StartPreview(shapeModel);
            }
        }

        // ─── UI Toolkit: Clip settings ────────────────────────────────────────

        /// <summary>
        /// クリップ設定カード（DenEmoWindow.uxml の anim-clip-card）を配線する。
        /// CreateGUI から一度だけ呼ぶ。
        /// </summary>
        public void BindClipSectionUI(
            VisualElement card, ShapeKeyModel shapeModel, Func<string> getSaveFolder, EditorWindow window)
        {
            _clipSectionTitle = card.Q<Label>("anim-clip-title");
            _clipFieldLabel   = card.Q<Label>("anim-clip-label");
            _clipField        = card.Q<ObjectField>("anim-clip-field");
            _clipNewButton    = card.Q<Button>("anim-clip-new");
            _clipGuide        = card.Q<VisualElement>("anim-clip-guide");
            _guide1           = card.Q<Label>("anim-guide-1");
            _guide2           = card.Q<Label>("anim-guide-2");
            _guide3           = card.Q<Label>("anim-guide-3");
            _conflictWarn     = card.Q<Label>("anim-conflict-warn");

            _clipField.objectType = typeof(AnimationClip);
            _clipField.allowSceneObjects = false;
            _clipField.RegisterValueChangedCallback(evt =>
                ApplyClipSelection(evt.newValue as AnimationClip, shapeModel, window));

            _clipNewButton.clicked += () => CreateNewClip(shapeModel, getSaveFolder(), window);

            // クリップの外部からの差し替えや Animation ウィンドウのプレビュー状態を反映する
            card.schedule.Execute(RefreshClipSectionState).Every(250);

            RefreshClipSectionLabels();
            RefreshClipSectionState();
        }

        /// <summary>
        /// クリップの差し替え（クリップ設定カードと実験タイムラインタブで共有）。
        /// プレビューの停止 → 差し替え → 再開までを一括で行う。
        /// </summary>
        public void ApplyClipSelection(AnimationClip newClip, ShapeKeyModel shapeModel, EditorWindow window)
        {
            if (ReferenceEquals(newClip, ClipModel.Clip)) return;
            StopPreview();
            ClipModel.SmoothLoopEnabled = false;
            ClipModel.SetClip(newClip);
            if (newClip != null && shapeModel.TargetSkinnedMesh != null)
                StartPreview(shapeModel);
            window.Repaint();
        }

        /// <summary>言語切替時に呼ぶ。クリップ設定カードと補正カードのラベルを更新する。</summary>
        public void RefreshClipSectionLabels()
        {
            if (_clipField == null) return;
            _clipSectionTitle.text = DenEmoLoc.T("ui.animMode.clip.section");
            _clipFieldLabel.text   = DenEmoLoc.T("ui.animMode.clip.label");
            _clipNewButton.text    = DenEmoLoc.T("ui.animMode.clip.new");
            _guide1.text           = DenEmoLoc.T("ui.animMode.guide.step1");
            _guide2.text           = DenEmoLoc.T("ui.animMode.guide.step2");
            _guide3.text           = DenEmoLoc.T("ui.animMode.guide.step3");
            _conflictWarn.text     = DenEmoLoc.T("ui.animMode.animWindowConflict");
            CorrectionUI.RefreshLabels();
        }

        private void RefreshClipSectionState()
        {
            if (_clipField == null) return;

            if (!ReferenceEquals(_clipField.value, ClipModel.Clip))
                _clipField.SetValueWithoutNotify(ClipModel.Clip);

            bool noClip = ClipModel.Clip == null;
            _clipGuide.style.display = noClip ? DisplayStyle.Flex : DisplayStyle.None;

            // Unity 標準 Animation ウィンドウのプレビューと SMR 書き込みが競合する（B-2 対策）
            bool conflict = !noClip && UnityEditor.AnimationMode.InAnimationMode();
            _conflictWarn.style.display = conflict ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ─── Animation draw context ───────────────────────────────────────────

        public AnimationDrawContext BuildDrawContext(ShapeKeyModel shapeModel)
        {
            if (_drawContext == null)
            {
                _drawContext = new AnimationDrawContext
                {
                    OnToggleTrackFilter = () => TrackFilterEnabled = !TrackFilterEnabled,

                    OnSliderDragStateChanged = NotifySliderDrag,

                    HasKeyframeAtCurrentTime = shapeName =>
                        ClipModel.HasKeyframeAt(shapeName, ClipModel.CurrentTime),

                    OnValueChanged = (item, model, newValue) =>
                    {
                        item.Value = newValue;
                        bool writesToClip = ClipModel.Clip != null &&
                            (IsRecording ||
                             _pendingRecords.ContainsKey(item.Name) ||
                             ClipModel.HasKeyframeAt(item.Name, ClipModel.CurrentTime));

                        // 視覚フィードバックとしてウェイトは常に即時反映する
                        if (model.TargetSkinnedMesh != null)
                            model.TargetSkinnedMesh.SetBlendShapeWeight(item.Index, newValue);

                        if (writesToClip)
                        {
                            // 録画中、または既存キー上でのスライダー操作 → キー記録（スロットル付き）
                            _pendingRecords[item.Name] = newValue;
                            FlushPendingRecords(force: false);
                        }
                        else if (ClipModel.Clip != null)
                        {
                            // クリップに記録されない一時変更。シーク時に破棄される旨を警告するため記憶する
                            _unrecordedTweaks.Add(item.Name);
                        }
                    },

                    OnKeyframeToggle = (item, model) =>
                    {
                        if (ClipModel.Clip == null) return;
                        FlushPendingRecords(force: true);
                        if (ClipModel.HasKeyframeAt(item.Name, ClipModel.CurrentTime))
                        {
                            Editor.DeleteKey(item.Name, ClipModel.CurrentTime);
                        }
                        else
                        {
                            Editor.RecordKey(item.Name, ClipModel.CurrentTime, item.Value, CurrentInterp);
                            _unrecordedTweaks.Remove(item.Name);
                            Preview.SampleAt(ClipModel.CurrentTime);
                        }
                    },
                };
            }

            _drawContext.IsRecording        = IsRecording;
            _drawContext.TrackFilterEnabled = TrackFilterEnabled;
            _drawContext.TrackShapeNames    = TrackFilterEnabled ? GetTrackNameSet() : null;
            return _drawContext;
        }

        private HashSet<string> GetTrackNameSet()
        {
            if (_trackNamesRevision != ClipModel.Revision)
            {
                _trackNames.Clear();
                foreach (var track in ClipModel.Tracks)
                    _trackNames.Add(track.ShapeName);
                _trackNamesRevision = ClipModel.Revision;
            }
            return _trackNames;
        }

        // ─── Save ─────────────────────────────────────────────────────────────

        public void SaveClip(string saveFolder, ShapeKeyModel shapeModel, Action<string, int> setStatus, bool saveAsNew = false)
        {
            FlushPendingRecords(force: true);
            if (ClipModel.Clip == null)
            {
                setStatus(DenEmoLoc.T("dlg.apply.noClip"), 3);
                return;
            }

            string existingPath = AssetDatabase.GetAssetPath(ClipModel.Clip);

            string path;
            if (saveAsNew || string.IsNullOrEmpty(existingPath))
            {
                string defaultFolder = string.IsNullOrEmpty(existingPath)
                    ? saveFolder
                    : System.IO.Path.GetDirectoryName(existingPath);
                string defaultName = string.IsNullOrEmpty(existingPath)
                    ? (shapeModel.TargetObject ? shapeModel.TargetObject.name + "_anim" : "blendshape_anim")
                    : System.IO.Path.GetFileNameWithoutExtension(existingPath);

                path = EditorUtility.SaveFilePanelInProject(
                    DenEmoLoc.T("save.panel.title"), defaultName + ".anim", "anim",
                    DenEmoLoc.T("save.panel.hint"), defaultFolder);
                if (string.IsNullOrEmpty(path)) return;
            }
            else
            {
                path = existingPath;
            }

            string err = AnimationExporter.SaveMultiFrameClip(ClipModel, path);
            if (err != null) setStatus(err, 3);
            else             setStatus(DenEmoLoc.Tf("dlg.save.done.msg", path), 1);
        }

        // ─── Private helpers ──────────────────────────────────────────────────

        private void CreateNewClip(ShapeKeyModel shapeModel, string saveFolder, EditorWindow window)
        {
            string defaultName = shapeModel.TargetObject
                ? shapeModel.TargetObject.name + "_anim"
                : "blendshape_anim";
            string path = EditorUtility.SaveFilePanelInProject(
                DenEmoLoc.T("save.panel.title"), defaultName + ".anim", "anim",
                DenEmoLoc.T("save.panel.hint"), saveFolder);
            if (string.IsNullOrEmpty(path)) return;

            var clip = new AnimationClip { frameRate = ClipModel.FPS };
            AssetDatabase.CreateAsset(clip, path);
            AssetDatabase.SaveAssets();

            StopPreview();
            ClipModel.SetClip(clip);
            if (shapeModel.TargetSkinnedMesh != null)
                StartPreview(shapeModel);
            window.Repaint();
        }

    }
}
