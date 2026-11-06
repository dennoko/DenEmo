using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using DenEmo.Models;

namespace DenEmo.UI
{
    /// <summary>
    /// マルチフレームタイムライン（UI Toolkit）。Animation モード内と別ウィンドウの両方で使う。
    /// 状態・ロジックは AnimationModeUI（ClipModel / Editor / Preview / Playback）を共有し、
    /// このクラスはビュー状態（ズーム・ドラッグ・クリップボード）のみを持つ。
    ///
    /// 描画方針:
    ///  - トランスポート・数値入力・トグルは UITK ネイティブコントロール
    ///  - ルーラー / スクラバー / トラックレーンのグラフィックは VisualElement.generateVisualContent
    ///    （Painter2D）で描画し、色は USS 変数（CustomStyleProperty）から解決する
    ///  - キーフレームのドラッグ・シーク・ズームは PointerEvent + ポインタキャプチャ
    /// </summary>
    public class TimelineUITKView
    {
        // ─── Layout constants（IMGUI 版と揃える） ────────────────────────────
        private const float RULER_HEIGHT     = 38f;
        private const float SCRUBBER_HEIGHT  = 14f;
        private const float SCROLLBAR_HEIGHT = 10f;
        private const float TRACK_ROW_HEIGHT = 24f;
        private const float RIGHT_PADDING    = 16f;
        private const float DIAMOND_SIZE     = 5f;
        private const float MAX_TRACKS_HEIGHT = 160f;
        private const float ACTION_ROW_HEIGHT = 14f;
        private const float ACTION_KEY_WIDTH = 20f;
        private const float MIN_VIEW_RANGE   = 1f / 50f;

        // ─── Context ──────────────────────────────────────────────────────────
        private AnimationModeUI _mode;
        private ShapeKeyModel   _model;
        private EditorWindow    _window;

        // ─── View state（0〜1 正規化） ────────────────────────────────────────
        private float _viewStart = 0f;
        private float _viewEnd   = 1f;
        private float _labelWidth = 150f;
        private float ViewRange => Mathf.Max(0.01f, _viewEnd - _viewStart);

        // ─── Interaction state ────────────────────────────────────────────────
        private bool   _draggingScrubber;
        private bool   _draggingViewScroll;
        private float  _viewScrollGrabOffset;
        private bool   _draggingLabelWidth;

        private bool   _draggingKey;
        private int    _dragFrame = -1;
        private string _dragShape;
        private bool   _dragUndoRecorded;
        private int    _blockFlashFrame = -1;
        private double _blockFlashUntil;

        private int _lastRevision = -1;
        private string _lastViewSig;
        private bool _active; // このタブが表示中か（Tick の無駄打ち防止）

        // ─── Clipboard（1 フレーム分） ───────────────────────────────────────
        private struct KeyClip { public string Shape; public float Value; public InterpolationType Interp; }
        private readonly List<KeyClip> _clipboard = new List<KeyClip>();

        // ─── Elements ─────────────────────────────────────────────────────────
        private VisualElement _root;
        private Label         _titleLabel;
        private ObjectField   _clipField;
        private Label         _noClipLabel;
        private VisualElement _body;

        private FloatField   _fpsField, _lenField, _speedField;
        private IntegerField _frameField;
        private Label        _fpsLabel, _lenLabel, _interpLabel, _frameLabel, _speedLabel;
        private Button       _interpStep, _interpLinear, _interpEase, _applyAllButton;
        private Button       _btnStart, _btnPrevKey, _btnPrevFrame, _btnPlay, _btnNextFrame, _btnNextKey, _btnEnd;
        private Toggle       _loopToggle;
        private Button       _recButton;

        private Label         _zoomLabel;
        private Button        _zoomReset;
        private VisualElement _rulerLane, _scrubLane, _scrollbarLane;
        private ScrollView    _tracksScroll;
        private Label         _noTracksLabel;

        // フレーム移動ハンドル行（トラック下部）
        private VisualElement _actionLane;
        private readonly List<ActionKeyWidget> _actionKeys = new List<ActionKeyWidget>();

        /// <summary>フレーム移動ハンドル行の 1 キー分（↔ ドラッグでフレーム内の全キーを移動）。</summary>
        private class ActionKeyWidget
        {
            public Label Handle;
            public float Time;
        }

        // ルーラーの目盛りラベルプール
        private readonly List<Label> _frameLabels = new List<Label>();
        private readonly List<Label> _secLabels   = new List<Label>();

        // 再描画対象レーン（現在時刻・ビュー変化時にまとめて dirty）
        private readonly List<VisualElement> _lanes = new List<VisualElement>();
        // トラック行の左パネル（ラベル幅スプリッタ操作時に一括更新）
        private readonly List<VisualElement> _leftPanels = new List<VisualElement>();

        // ─── 解決済みテーマ色（USS 変数から取得） ────────────────────────────
        private static readonly CustomStyleProperty<Color> OutlineProp = new CustomStyleProperty<Color>("--dennoko-outline");
        private static readonly CustomStyleProperty<Color> TextPrimaryProp = new CustomStyleProperty<Color>("--dennoko-text-primary");
        private static readonly CustomStyleProperty<Color> TextSecondaryProp = new CustomStyleProperty<Color>("--dennoko-text-secondary");
        private static readonly CustomStyleProperty<Color> Surface2Prop = new CustomStyleProperty<Color>("--dennoko-surface-2");
        private static readonly CustomStyleProperty<Color> WarningProp = new CustomStyleProperty<Color>("--dennoko-semantic-warning");

        private Color _cOutline    = new Color(0.23f, 0.23f, 0.23f);
        private Color _cTextPrim   = Color.white;
        private Color _cTextSecond = new Color(0.8f, 0.8f, 0.8f);
        private Color _cSurface2   = new Color(0.17f, 0.17f, 0.17f);
        private Color _cWarning    = new Color(1f, 0.72f, 0.3f);
        private static readonly Color ScrubberWhite = new Color(1f, 1f, 1f, 0.85f);
        private static readonly Color CurrentLine   = new Color(1f, 1f, 1f, 0.4f);

        // ショートカットを登録したパネルルート（ビューがツリーから外れたら解除する）
        private VisualElement _shortcutHost;

        private bool   _showClipField;
        private bool   _isSeparateWindow;
        private bool   _requireZoomModifier = true;
        private AnimationClip _lastClip;
        private Button _detachButton;

        public AnimationModeUI Mode => _mode;

        // ─── Build ────────────────────────────────────────────────────────────

        /// <param name="showClipField">クリップ選択フィールドを表示するか（Animation モードはクリップカードがあるため false）。</param>
        /// <param name="isSeparateWindow">別ウィンドウ（DenEmoTimelineWindow）として表示するか。</param>
        /// <param name="requireZoomModifier">ホイールズームに Ctrl/Cmd を要求するか（外側スクロールとの競合回避）。</param>
        public VisualElement Build(AnimationModeUI mode, ShapeKeyModel model, EditorWindow window,
            bool showClipField = true, bool isSeparateWindow = false, bool requireZoomModifier = true)
        {
            _mode = mode; _model = model; _window = window;
            _showClipField = showClipField;
            _isSeparateWindow = isSeparateWindow;
            _requireZoomModifier = requireZoomModifier;

            _root = new VisualElement();
            _root.AddToClassList("dennoko-card");
            _root.AddToClassList("dennoko-tl-root");

            // ── ヘッダ: タイトル + 別窓化/結合ボタン（メッシュ指定と同じ形式） ──
            var headerRow = new VisualElement();
            headerRow.AddToClassList("dennoko-card-header");
            headerRow.AddToClassList("dennoko-card-header--row");

            _titleLabel = new Label();
            _titleLabel.AddToClassList("dennoko-section-title");
            _titleLabel.AddToClassList("dennoko-card-header__title");
            headerRow.Add(_titleLabel);

            _detachButton = new Button(OnDetachAttach);
            _detachButton.AddToClassList("dennoko-icon-button");
            headerRow.Add(_detachButton);

            _root.Add(headerRow);

            // ── クリップ選択（任意） ──
            if (_showClipField)
            {
                var clipRow = Row();
                _clipField = new ObjectField { objectType = typeof(AnimationClip), allowSceneObjects = false };
                _clipField.AddToClassList("dennoko-tl-clip-field");
                _clipField.RegisterValueChangedCallback(evt =>
                {
                    _mode.ApplyClipSelection(evt.newValue as AnimationClip, _model, _window);
                    RefreshStructure();
                });
                clipRow.Add(_clipField);
                _root.Add(clipRow);
            }

            _noClipLabel = new Label();
            _noClipLabel.AddToClassList("dennoko-hint");
            _root.Add(_noClipLabel);

            // ── body（クリップがあるときのみ表示） ──
            _body = new VisualElement();
            _root.Add(_body);

            if (_isSeparateWindow)
            {
                _root.style.flexGrow = 1f;
                _root.style.flexShrink = 1f;
                _body.style.flexGrow = 1f;
                _body.style.flexShrink = 1f;
            }

            BuildTransportRow();
            BuildSettingsRow();
            BuildTimelineArea();

            _root.RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
            _root.RegisterCallback<AttachToPanelEvent>(evt => HookShortcuts(evt.destinationPanel));
            _root.RegisterCallback<DetachFromPanelEvent>(_ => UnhookShortcuts());
            // 現在時刻・再生の追従（表示中のみ）
            _root.schedule.Execute(Tick).Every(33);

            RefreshLabels();
            RefreshStructure();
            return _root;
        }

        private static VisualElement Row()
        {
            var r = new VisualElement();
            r.AddToClassList("dennoko-hrow");
            return r;
        }

        private void BuildTransportRow()
        {
            var row = Row();
            row.AddToClassList("dennoko-tl-transport");

            // 再生・移動ボタン群
            var btnGroup = new VisualElement();
            btnGroup.AddToClassList("dennoko-hrow");
            btnGroup.AddToClassList("dennoko-tl-group");

            _btnStart     = Transport("|<",  () => SeekTo(0f));
            _btnPrevKey   = Transport("|◆",  GoToPrevKey);
            _btnPrevFrame = Transport("<",   () => StepFrame(-1));
            _btnPlay      = Transport("▶",   TogglePlay);
            _btnPlay.AddToClassList("dennoko-tl-play");
            _btnNextFrame = Transport(">",   () => StepFrame(+1));
            _btnNextKey   = Transport("◆|",  GoToNextKey);
            _btnEnd       = Transport(">|",  () => SeekTo(_mode.ClipModel.ClipLength));

            btnGroup.Add(_btnStart); btnGroup.Add(_btnPrevKey); btnGroup.Add(_btnPrevFrame);
            btnGroup.Add(_btnPlay);
            btnGroup.Add(_btnNextFrame); btnGroup.Add(_btnNextKey); btnGroup.Add(_btnEnd);
            row.Add(btnGroup);

            // フレーム & 速度
            var timeGroup = new VisualElement();
            timeGroup.AddToClassList("dennoko-hrow");
            timeGroup.AddToClassList("dennoko-tl-group");

            _frameLabel = Caption();
            _frameField = new IntegerField { isDelayed = true };
            _frameField.AddToClassList("dennoko-tl-num");
            _frameField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue != _mode.ClipModel.CurrentFrame)
                    SeekTo(_mode.ClipModel.FrameToTime(evt.newValue));
            });
            timeGroup.Add(_frameLabel); timeGroup.Add(_frameField);

            _speedLabel = Caption();
            _speedField = new FloatField { isDelayed = true };
            _speedField.AddToClassList("dennoko-tl-num");
            _speedField.RegisterValueChangedCallback(evt =>
            {
                if (!Mathf.Approximately(evt.newValue, _mode.Playback.Speed))
                    _mode.Playback.Speed = evt.newValue;
            });
            timeGroup.Add(_speedLabel); timeGroup.Add(_speedField);
            row.Add(timeGroup);

            // ループ & REC
            var recGroup = new VisualElement();
            recGroup.AddToClassList("dennoko-hrow");
            recGroup.AddToClassList("dennoko-tl-group");

            _loopToggle = new Toggle();
            _loopToggle.AddToClassList("dennoko-tl-loop-toggle");
            _loopToggle.RegisterValueChangedCallback(evt =>
            {
                _mode.ClipModel.SmoothLoopEnabled = evt.newValue;
                _mode.Preview.SampleAt(_mode.ClipModel.CurrentTime);
                _window.Repaint();
            });
            recGroup.Add(_loopToggle);

            _recButton = new Button(() =>
            {
                _mode.IsRecording = !_mode.IsRecording;
                if (_mode.IsRecording && !_mode.Preview.IsActive)
                    _mode.StartPreview(_model);
                RefreshValues();
                _window.Repaint();
            }) { text = "🔴 REC" };
            _recButton.AddToClassList("dennoko-chip");
            recGroup.Add(_recButton);
            row.Add(recGroup);

            _body.Add(row);
        }

        private void BuildSettingsRow()
        {
            var row = Row();
            row.AddToClassList("dennoko-tl-settings");

            // クリップ設定 (FPS / 長さ)
            var clipGroup = new VisualElement();
            clipGroup.AddToClassList("dennoko-hrow");
            clipGroup.AddToClassList("dennoko-tl-group");

            _fpsLabel = Caption();
            _fpsField = new FloatField { isDelayed = true };
            _fpsField.AddToClassList("dennoko-tl-num");
            _fpsField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue > 0f && !Mathf.Approximately(evt.newValue, _mode.ClipModel.FPS))
                {
                    _mode.Editor.SetFrameRate(evt.newValue);
                    RefreshStructure();
                }
            });
            clipGroup.Add(_fpsLabel); clipGroup.Add(_fpsField);

            _lenLabel = Caption();
            _lenField = new FloatField { isDelayed = true };
            _lenField.AddToClassList("dennoko-tl-num");
            _lenField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue > 0f && !Mathf.Approximately(evt.newValue, _mode.ClipModel.ClipLength))
                    ApplyLength(evt.newValue);
            });
            clipGroup.Add(_lenLabel); clipGroup.Add(_lenField);
            row.Add(clipGroup);

            // 補間設定
            var interpGroup = new VisualElement();
            interpGroup.AddToClassList("dennoko-hrow");
            interpGroup.AddToClassList("dennoko-tl-group");

            _interpLabel = Caption();
            _interpStep   = InterpChip(InterpolationType.Step,   "Step");
            _interpLinear = InterpChip(InterpolationType.Linear, "Linear");
            _interpEase   = InterpChip(InterpolationType.Ease,   "Ease");
            _applyAllButton = new Button(ApplyInterpAll);
            _applyAllButton.AddToClassList("dennoko-button-secondary");
            _applyAllButton.AddToClassList("dennoko-tl-apply-all");

            var interpSeg = new VisualElement();
            interpSeg.AddToClassList("dennoko-segmented");
            interpSeg.AddToClassList("dennoko-tl-interp");
            interpSeg.Add(_interpStep);
            interpSeg.Add(_interpLinear);
            interpSeg.Add(_interpEase);

            interpGroup.Add(_interpLabel);
            interpGroup.Add(interpSeg);
            interpGroup.Add(Spacer(4));
            interpGroup.Add(_applyAllButton);
            row.Add(interpGroup);

            _body.Add(row);
        }

        private Button InterpChip(InterpolationType type, string label)
        {
            var b = new Button(() => { _mode.CurrentInterp = type; RefreshValues(); }) { text = label };
            return b;
        }

        private Button Transport(string label, System.Action action)
        {
            var b = new Button(action) { text = label };
            b.AddToClassList("dennoko-icon-button");
            b.AddToClassList("dennoko-tl-transport-btn");
            return b;
        }

        private void BuildTimelineArea()
        {
            var area = new VisualElement();
            area.AddToClassList("dennoko-card-outer");
            area.AddToClassList("dennoko-tl-area");
            if (_isSeparateWindow)
            {
                area.style.flexGrow = 1f;
                area.style.flexShrink = 1f;
            }

            // ── ルーラー行 ──
            var rulerRow = Strip(out var rulerLeft);
            var zoomBox = new VisualElement(); zoomBox.AddToClassList("dennoko-tl-zoombox");
            _zoomLabel = new Label("100%"); _zoomLabel.AddToClassList("dennoko-tl-zoom-pct");
            _zoomReset = new Button(() => { _viewStart = 0f; _viewEnd = 1f; MarkAllDirty(); }) { };
            _zoomReset.AddToClassList("dennoko-icon-button");
            _zoomReset.AddToClassList("dennoko-tl-zoom-reset");
            zoomBox.Add(_zoomLabel); zoomBox.Add(_zoomReset);
            rulerLeft.Add(zoomBox);
            AddSplitter(rulerLeft);

            _rulerLane = Lane(RULER_HEIGHT, "dennoko-tl-ruler");
            _rulerLane.generateVisualContent += ctx => DrawRuler(ctx);
            _rulerLane.RegisterCallback<GeometryChangedEvent>(_ => LayoutRulerLabels());
            RegisterSeekAndZoom(_rulerLane);
            RegisterContextMenu(_rulerLane);
            rulerRow.Add(_rulerLane);
            area.Add(rulerRow);

            // ── スクラバー行 ──
            var scrubRow = Strip(out _);
            _scrubLane = Lane(SCRUBBER_HEIGHT, "dennoko-tl-scrub");
            _scrubLane.generateVisualContent += ctx => DrawScrubber(ctx);
            RegisterSeekAndZoom(_scrubLane);
            RegisterContextMenu(_scrubLane);
            scrubRow.Add(_scrubLane);
            area.Add(scrubRow);

            // ── スクロールバー行 ──
            var sbRow = Strip(out _);
            _scrollbarLane = Lane(SCROLLBAR_HEIGHT, "dennoko-tl-scrollbar");
            _scrollbarLane.generateVisualContent += ctx => DrawScrollbar(ctx);
            RegisterScrollbar(_scrollbarLane);
            sbRow.Add(_scrollbarLane);
            area.Add(sbRow);

            // ── トラック ──
            _tracksScroll = new ScrollView();
            _tracksScroll.AddToClassList("dennoko-tl-tracks");
            area.Add(_tracksScroll);

            _noTracksLabel = new Label();
            _noTracksLabel.AddToClassList("dennoko-hint");
            area.Add(_noTracksLabel);

            // ── フレーム移動ハンドル行（トラック下部） ──
            BuildActionRow(area);

            _body.Add(area);
        }

        /// <summary>左固定パネル + 右レーンの 1 行を作る。</summary>
        private VisualElement Strip(out VisualElement leftPanel)
        {
            var row = new VisualElement();
            row.AddToClassList("dennoko-tl-strip");
            leftPanel = new VisualElement();
            leftPanel.AddToClassList("dennoko-tl-left");
            leftPanel.style.width = _labelWidth;
            row.Add(leftPanel);
            _leftPanels.Add(leftPanel);
            return row;
        }

        private VisualElement Lane(float height, string cls)
        {
            var lane = new VisualElement();
            lane.AddToClassList("dennoko-tl-lane");
            if (!string.IsNullOrEmpty(cls)) lane.AddToClassList(cls);
            lane.style.height = height;
            _lanes.Add(lane);
            return lane;
        }

        private void AddSplitter(VisualElement leftPanel)
        {
            var sp = new VisualElement();
            sp.AddToClassList("dennoko-tl-splitter");
            sp.RegisterCallback<PointerDownEvent>(evt =>
            {
                _draggingLabelWidth = true;
                sp.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });
            sp.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!_draggingLabelWidth) return;
                _labelWidth = Mathf.Clamp(_labelWidth + evt.deltaPosition.x, 80f, 400f);
                foreach (var lp in _leftPanels) lp.style.width = _labelWidth;
                MarkAllDirty();
                evt.StopPropagation();
            });
            sp.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!_draggingLabelWidth) return;
                _draggingLabelWidth = false;
                sp.ReleasePointer(evt.pointerId);
                evt.StopPropagation();
            });
            leftPanel.Add(sp);
        }

        // ─── Localization / labels ────────────────────────────────────────────

        public void RefreshLabels()
        {
            if (_root == null) return;
            _titleLabel.text  = DenEmoLoc.T("ui.timeline.title");
            _noClipLabel.text = DenEmoLoc.T("ui.timeline.noClip");
            DennokoIcons.Apply(_detachButton,
                _isSeparateWindow ? "d_winbtn_win_restore|winbtn_win_restore" : DennokoIcons.Popout,
                DenEmoLoc.T(_isSeparateWindow ? "ui.timeline.attach" : "ui.timeline.detach"),
                DenEmoLoc.T(_isSeparateWindow ? "ui.timeline.attach.tip" : "ui.timeline.detach.tip"));
            _fpsLabel.text    = DenEmoLoc.T("ui.timeline.fps");
            _lenLabel.text    = DenEmoLoc.T("ui.timeline.length");
            _interpLabel.text    = DenEmoLoc.T("ui.timeline.interp");
            _interpLabel.tooltip = DenEmoLoc.T("ui.timeline.interp.tip");
            _applyAllButton.text = DenEmoLoc.T("ui.timeline.applyInterpAll");
            _applyAllButton.tooltip = DenEmoLoc.T("ui.timeline.applyInterpAll.tip");
            _frameLabel.text  = DenEmoLoc.T("ui.timeline.frame");
            _speedLabel.text  = DenEmoLoc.T("ui.timeline.speed");
            _loopToggle.text  = DenEmoLoc.T("ui.timeline.loop");
            _loopToggle.tooltip = DenEmoLoc.T("ui.timeline.loop.tip");
            _recButton.tooltip  = DenEmoLoc.T("ui.timeline.rec.tip");
            _zoomReset.text     = DenEmoLoc.T("ui.timeline.zoomReset");
            _zoomReset.tooltip  = DenEmoLoc.T("ui.timeline.zoomReset.tip");
            _noTracksLabel.text = DenEmoLoc.T("ui.timeline.noKeys");
            _btnStart.tooltip     = DenEmoLoc.T("ui.timeline.goStart.tip");
            _btnPrevKey.tooltip   = DenEmoLoc.T("ui.timeline.prevKey.tip");
            _btnPrevFrame.tooltip = DenEmoLoc.T("ui.timeline.prevFrame.tip");
            _btnNextFrame.tooltip = DenEmoLoc.T("ui.timeline.nextFrame.tip");
            _btnNextKey.tooltip   = DenEmoLoc.T("ui.timeline.nextKey.tip");
            _btnEnd.tooltip       = DenEmoLoc.T("ui.timeline.goEnd.tip");
            foreach (var w in _actionKeys)
                w.Handle.tooltip = DenEmoLoc.T("ui.timeline.moveFrame.tip");
        }

        // ─── State refresh ────────────────────────────────────────────────────

        /// <summary>クリップ有無・トラック集合が変わったときの再構築。</summary>
        public void RefreshStructure()
        {
            if (_root == null) return;

            _lastClip = _mode.ClipModel.Clip;
            if (_clipField != null && !ReferenceEquals(_clipField.value, _mode.ClipModel.Clip))
                _clipField.SetValueWithoutNotify(_mode.ClipModel.Clip);

            bool hasClip = _mode.ClipModel.Clip != null;
            _noClipLabel.style.display = hasClip ? DisplayStyle.None : DisplayStyle.Flex;
            _body.style.display        = hasClip ? DisplayStyle.Flex : DisplayStyle.None;
            if (!hasClip) return;

            RebuildTrackRows();
            RefreshValues();
            MarkAllDirty();
        }

        private void RebuildTrackRows()
        {
            _tracksScroll.Clear();
            _lanes.RemoveAll(l => l != _rulerLane && l != _scrubLane && l != _scrollbarLane);
            _leftPanels.RemoveAll(lp => lp.ClassListContains("dennoko-tl-track-left"));

            var tracks = _mode.ClipModel.Tracks;

            if (_isSeparateWindow)
            {
                _noTracksLabel.style.display = DisplayStyle.None;
                _tracksScroll.style.display  = DisplayStyle.Flex;
                _tracksScroll.style.maxHeight = StyleKeyword.Null;
                _tracksScroll.style.flexGrow = 1f;
                _tracksScroll.style.flexShrink = 1f;
            }
            else
            {
                _noTracksLabel.style.display = tracks.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
                _tracksScroll.style.display  = tracks.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
                float maxH = Mathf.Min(tracks.Count * (TRACK_ROW_HEIGHT + 1f), MAX_TRACKS_HEIGHT);
                _tracksScroll.style.maxHeight = maxH;
                _tracksScroll.style.flexGrow = 0f;
            }

            foreach (var track in tracks)
                _tracksScroll.Add(MakeTrackRow(track));

            if (_isSeparateWindow)
            {
                _tracksScroll.Add(MakeFillerRow(showEmptyNotice: tracks.Count == 0));
            }

            LayoutActionRow();
            _lastRevision = _mode.ClipModel.Revision;
        }

        private VisualElement MakeTrackRow(AnimationTrack track)
        {
            string shapeName = track.ShapeName;

            var row = new VisualElement();
            row.AddToClassList("dennoko-tl-strip");
            row.AddToClassList("dennoko-tl-track-row");

            var left = new VisualElement();
            left.AddToClassList("dennoko-tl-left");
            left.AddToClassList("dennoko-tl-track-left");
            left.style.width = _labelWidth;

            var nameLabel = new Label(shapeName);
            nameLabel.AddToClassList("dennoko-tl-track-name");
            left.Add(nameLabel);

            var addBtn = new Button(() => AddOrUpdateKey(shapeName)) { text = "◆", tooltip = DenEmoLoc.T("ui.timeline.addKey.tip") };
            addBtn.AddToClassList("dennoko-icon-button");
            addBtn.AddToClassList("dennoko-tl-key-btn");
            left.Add(addBtn);

            var delBtn = new Button(() => DeleteTrack(shapeName)) { text = "✕", tooltip = DenEmoLoc.T("ui.timeline.deleteTrack.tip") };
            delBtn.AddToClassList("dennoko-icon-button");
            left.Add(delBtn);

            row.Add(left);
            _leftPanels.Add(left);

            var lane = new VisualElement();
            lane.AddToClassList("dennoko-tl-lane");
            lane.AddToClassList("dennoko-tl-track-lane");
            lane.style.height = TRACK_ROW_HEIGHT;
            lane.generateVisualContent += ctx => DrawTrackLane(ctx, track);
            RegisterTrackLane(lane, track);
            _lanes.Add(lane);
            row.Add(lane);

            return row;
        }

        private VisualElement MakeFillerRow(bool showEmptyNotice = false)
        {
            var row = new VisualElement();
            row.AddToClassList("dennoko-tl-strip");
            row.AddToClassList("dennoko-tl-tracks-filler");

            var left = new VisualElement();
            left.AddToClassList("dennoko-tl-left");
            left.AddToClassList("dennoko-tl-track-left");
            left.style.width = _labelWidth;
            row.Add(left);
            _leftPanels.Add(left);

            var lane = new VisualElement();
            lane.AddToClassList("dennoko-tl-lane");
            lane.AddToClassList("dennoko-tl-track-lane");
            lane.AddToClassList("dennoko-tl-filler-lane");
            lane.generateVisualContent += ctx => DrawFillerLane(ctx);
            RegisterSeekAndZoom(lane);
            RegisterContextMenu(lane);
            _lanes.Add(lane);

            if (showEmptyNotice)
            {
                var lbl = new Label(DenEmoLoc.T("ui.timeline.noKeys"));
                lbl.AddToClassList("dennoko-hint");
                lbl.AddToClassList("dennoko-tl-lane-notice");
                lane.Add(lbl);
            }

            row.Add(lane);
            return row;
        }

        /// <summary>数値フィールド・ボタン状態を現在の状態に同期する。</summary>
        private void RefreshValues()
        {
            if (_mode.ClipModel.Clip == null) return;
            var m = _mode.ClipModel;

            SetIfChanged(_fpsField, m.FPS);
            SetIfChanged(_lenField, m.ClipLength);
            SetIfChanged(_speedField, _mode.Playback.Speed);
            if (_frameField.value != m.CurrentFrame && _frameField.focusController?.focusedElement != _frameField)
                _frameField.SetValueWithoutNotify(m.CurrentFrame);
            if (_loopToggle.value != m.SmoothLoopEnabled)
                _loopToggle.SetValueWithoutNotify(m.SmoothLoopEnabled);

            _btnPlay.text = _mode.Playback.IsPlaying ? "■" : "▶";
            _btnPlay.tooltip = _mode.Playback.IsPlaying ? DenEmoLoc.T("ui.timeline.stop.tip") : DenEmoLoc.T("ui.timeline.play.tip");

            SetChip(_interpStep,   _mode.CurrentInterp == InterpolationType.Step);
            SetChip(_interpLinear, _mode.CurrentInterp == InterpolationType.Linear);
            SetChip(_interpEase,   _mode.CurrentInterp == InterpolationType.Ease);
            SetChip(_recButton,    _mode.IsRecording);

            _zoomLabel.text = Mathf.RoundToInt(1f / ViewRange * 100f) + "%";
            _zoomReset.style.display = ViewRange < 1f - 0.001f ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static void SetIfChanged(FloatField f, float v)
        {
            if (f.focusController?.focusedElement == f) return; // 入力中は上書きしない
            if (!Mathf.Approximately(f.value, v)) f.SetValueWithoutNotify(v);
        }

        private static void SetChip(Button b, bool on)
        {
            b.EnableInClassList("dennoko-button-active", on);
        }

        /// <summary>タブ/ウィンドウの表示切替。非表示時は Tick を止め、表示時に一度再構築する。</summary>
        public void SetActive(bool active)
        {
            _active = active;
            if (active) RefreshStructure();
        }

        // 別窓化（埋め込み時）/ 結合（別ウィンドウ時）
        private void OnDetachAttach()
        {
            if (_isSeparateWindow) _window.Close();
            else DenEmoTimelineWindow.ShowWindow();
        }

        /// <summary>毎フレーム相当の追従（表示中のみ）。現在時刻・再生の反映と再描画。</summary>
        public void Tick()
        {
            if (!_active || _root == null) return;

            // クリップが外部（クリップカード等）で差し替わったら作り直す
            if (!ReferenceEquals(_mode.ClipModel.Clip, _lastClip))
            {
                RefreshStructure();
                return;
            }
            if (_mode.ClipModel.Clip == null) return;

            if (_mode.ClipModel.Revision != _lastRevision)
            {
                RebuildTrackRows();
            }

            RefreshValues();

            string sig = _viewStart.ToString("F4") + "|" + _viewEnd.ToString("F4") + "|" + _mode.ClipModel.CurrentTime.ToString("F4");
            bool blockFlash = _blockFlashFrame >= 0 && EditorApplication.timeSinceStartup < _blockFlashUntil;
            if (sig != _lastViewSig || blockFlash || _draggingKey || _mode.Playback.IsPlaying)
            {
                _lastViewSig = sig;
                MarkAllDirty();
            }
        }

        private void MarkAllDirty()
        {
            foreach (var lane in _lanes) lane.MarkDirtyRepaint();
            LayoutRulerLabels();
            LayoutActionRow();
        }

        // ─── Coordinate helpers ───────────────────────────────────────────────

        private float ClipLen => _mode.ClipModel.ClipLength;

        private float TimeToX(float time, float laneW)
        {
            if (ClipLen <= 0f) return 0f;
            return (((time / ClipLen) - _viewStart) / ViewRange) * laneW;
        }

        private float XToTime(float x, float laneW)
        {
            if (laneW <= 0f || ClipLen <= 0f) return 0f;
            float norm = _viewStart + (x / laneW) * ViewRange;
            return Mathf.Clamp01(norm) * ClipLen;
        }

        // ─── Drawing ──────────────────────────────────────────────────────────

        private static void Line(Painter2D p, float x0, float y0, float x1, float y1, Color c, float w = 1f)
        {
            p.strokeColor = c; p.lineWidth = w;
            p.BeginPath(); p.MoveTo(new Vector2(x0, y0)); p.LineTo(new Vector2(x1, y1)); p.Stroke();
        }

        private static void FillRect(Painter2D p, float x, float y, float w, float h, Color c)
        {
            p.fillColor = c;
            p.BeginPath();
            p.MoveTo(new Vector2(x, y)); p.LineTo(new Vector2(x + w, y));
            p.LineTo(new Vector2(x + w, y + h)); p.LineTo(new Vector2(x, y + h));
            p.ClosePath(); p.Fill();
        }

        // ルーラーの目盛り線のみを描画する（ラベルは LayoutRulerLabels で配置）。
        private void DrawRuler(MeshGenerationContext ctx)
        {
            var lane = ctx.visualElement;
            float w = lane.contentRect.width, h = lane.contentRect.height;
            if (w <= 0f) return;
            var p = ctx.painter2D;
            var m = _mode.ClipModel;
            int total = m.TotalFrames;
            if (total <= 0) return;

            float sepY = h * 0.5f;
            Line(p, 0, sepY, w, sepY, _cOutline);

            int step = CalcRulerStep(total, w / ViewRange);
            int fStart = Mathf.FloorToInt(_viewStart * total);
            int fEnd   = Mathf.CeilToInt(_viewEnd * total);
            fStart -= fStart % step;
            for (int f = fStart; f <= fEnd; f += step)
            {
                if (f < 0) continue;
                float x = TimeToX(m.FrameToTime(f), w);
                if (x < 0 || x > w) continue;
                Line(p, x, sepY - 5f, x, sepY, _cOutline);
            }

            const float SEC = 0.1f;
            float visStartSec = _viewStart * m.ClipLength;
            float visEndSec   = _viewEnd * m.ClipLength;
            int startIdx = Mathf.Max(0, Mathf.FloorToInt(visStartSec / SEC) - 1);
            int endIdx   = Mathf.CeilToInt(visEndSec / SEC) + 1;
            for (int i = startIdx; i <= endIdx; i++)
            {
                float t = i * SEC;
                if (t > m.ClipLength + 0.001f) break;
                float x = TimeToX(t, w);
                if (x < 0 || x > w) continue;
                Line(p, x, h - 5f, x, h, _cOutline);
            }
        }

        // ルーラーの数値ラベルをプールで配置する（描画外＝ツリー変更が許される文脈で呼ぶ）。
        private void LayoutRulerLabels()
        {
            if (_rulerLane == null || _mode?.ClipModel?.Clip == null) return;
            float w = _rulerLane.contentRect.width;
            float h = _rulerLane.contentRect.height;
            var m = _mode.ClipModel;
            int total = m.TotalFrames;
            if (w <= 0f || total <= 0) { HideExtra(_frameLabels, 0); HideExtra(_secLabels, 0); return; }

            float sepY = h * 0.5f;

            int step = CalcRulerStep(total, w / ViewRange);
            int fStart = Mathf.FloorToInt(_viewStart * total);
            int fEnd   = Mathf.CeilToInt(_viewEnd * total);
            fStart -= fStart % step;
            int fi = 0;
            for (int f = fStart; f <= fEnd; f += step)
            {
                if (f < 0) continue;
                float x = TimeToX(m.FrameToTime(f), w);
                if (x < 0 || x + 24 > w) continue;
                PlaceLabel(_frameLabels, fi++, _rulerLane, f.ToString(), x + 2, 1, false);
            }
            HideExtra(_frameLabels, fi);

            const float SEC = 0.1f;
            float pxPer01 = (w * SEC) / (m.ClipLength * ViewRange);
            int labelSkip = Mathf.Max(1, Mathf.CeilToInt(26f / Mathf.Max(0.001f, pxPer01)));
            float visStartSec = _viewStart * m.ClipLength;
            float visEndSec   = _viewEnd * m.ClipLength;
            int startIdx = Mathf.Max(0, Mathf.FloorToInt(visStartSec / SEC) - 1);
            int endIdx   = Mathf.CeilToInt(visEndSec / SEC) + 1;
            int si = 0;
            for (int i = startIdx; i <= endIdx; i++)
            {
                float t = i * SEC;
                if (t > m.ClipLength + 0.001f) break;
                float x = TimeToX(t, w);
                if (x < 0 || x + 30 > w) continue;
                if (i % labelSkip == 0)
                    PlaceLabel(_secLabels, si++, _rulerLane, t.ToString("0.0"), x + 2, sepY + 1, true);
            }
            HideExtra(_secLabels, si);
        }

        private void DrawScrubber(MeshGenerationContext ctx)
        {
            var lane = ctx.visualElement;
            float w = lane.contentRect.width, h = lane.contentRect.height;
            if (w <= 0f) return;
            var p = ctx.painter2D;
            var m = _mode.ClipModel;
            float sx = TimeToX(m.CurrentTime, w);
            FillRect(p, sx - 5f, 0, 10f, h, _cTextPrim);
            Line(p, sx, 0, sx, h, ScrubberWhite, 2f);
        }

        private void DrawScrollbar(MeshGenerationContext ctx)
        {
            var lane = ctx.visualElement;
            float w = lane.contentRect.width, h = lane.contentRect.height;
            if (w <= 0f) return;
            if (ViewRange >= 1f - 0.001f) return; // ズームなし時は非表示
            var p = ctx.painter2D;
            float thumbX = _viewStart * w;
            float thumbW = Mathf.Max(12f, ViewRange * w);
            FillRect(p, thumbX, 1, thumbW, h - 2, _cSurface2);
            Line(p, thumbX, 1, thumbX + thumbW, 1, _cOutline);
            Line(p, thumbX, h - 1, thumbX + thumbW, h - 1, _cOutline);
        }

        private void DrawTrackLane(MeshGenerationContext ctx, AnimationTrack track)
        {
            var lane = ctx.visualElement;
            float w = lane.contentRect.width, h = lane.contentRect.height;
            if (w <= 0f) return;
            var p = ctx.painter2D;
            var m = _mode.ClipModel;

            float cy = h * 0.5f;
            Line(p, 0, cy, w, cy, _cOutline);

            // 現在時刻ライン
            float sx = TimeToX(m.CurrentTime, w);
            if (sx >= 0 && sx <= w) Line(p, sx, 0, sx, h, CurrentLine, 2f);

            // ブロックフラッシュ
            if (_blockFlashFrame >= 0 && EditorApplication.timeSinceStartup < _blockFlashUntil)
            {
                float bx = TimeToX(m.FrameToTime(_blockFlashFrame), w);
                if (bx >= 0 && bx <= w) Line(p, bx, 0, bx, h, _cWarning, 2f);
            }

            // キーフレームダイヤ
            float tol = m.FrameTolerance;
            foreach (float kt in track.KeyTimes)
            {
                float kx = TimeToX(kt, w);
                if (kx < -DIAMOND_SIZE || kx > w + DIAMOND_SIZE) continue;
                bool current = Mathf.Abs(kt - m.CurrentTime) <= tol;
                Diamond(p, kx, cy, DIAMOND_SIZE + (current ? 1f : 0f), current ? _cTextPrim : _cTextSecond);
            }
        }

        private void DrawFillerLane(MeshGenerationContext ctx)
        {
            var lane = ctx.visualElement;
            float w = lane.contentRect.width, h = lane.contentRect.height;
            if (w <= 0f || h <= 0f || _mode?.ClipModel?.Clip == null) return;
            var p = ctx.painter2D;
            var m = _mode.ClipModel;

            // 現在時刻ライン
            float sx = TimeToX(m.CurrentTime, w);
            if (sx >= 0 && sx <= w) Line(p, sx, 0, sx, h, CurrentLine, 2f);

            // ブロックフラッシュ
            if (_blockFlashFrame >= 0 && EditorApplication.timeSinceStartup < _blockFlashUntil)
            {
                float bx = TimeToX(m.FrameToTime(_blockFlashFrame), w);
                if (bx >= 0 && bx <= w) Line(p, bx, 0, bx, h, _cWarning, 2f);
            }
        }

        private static void Diamond(Painter2D p, float cx, float cy, float d, Color c)
        {
            p.fillColor = c;
            p.BeginPath();
            p.MoveTo(new Vector2(cx, cy - d));
            p.LineTo(new Vector2(cx + d, cy));
            p.LineTo(new Vector2(cx, cy + d));
            p.LineTo(new Vector2(cx - d, cy));
            p.ClosePath(); p.Fill();
        }

        // ルーラーラベルのプール配置
        private void PlaceLabel(List<Label> pool, int idx, VisualElement parent, string text, float x, float y, bool tiny)
        {
            Label lbl;
            if (idx < pool.Count) lbl = pool[idx];
            else
            {
                lbl = new Label();
                lbl.AddToClassList(tiny ? "dennoko-tl-tick-tiny" : "dennoko-tl-tick");
                lbl.pickingMode = PickingMode.Ignore;
                parent.Add(lbl);
                pool.Add(lbl);
            }
            lbl.style.display = DisplayStyle.Flex;
            lbl.text = text;
            // ラベルはレーン基準の絶対座標（レーンは左パネルの右にあるため親はレーン）
            lbl.style.left = x;
            lbl.style.top  = y;
        }

        private static void HideExtra(List<Label> pool, int usedCount)
        {
            for (int i = usedCount; i < pool.Count; i++)
                pool[i].style.display = DisplayStyle.None;
        }

        private static int CalcRulerStep(int totalFrames, float pixelWidth)
        {
            if (totalFrames <= 0 || pixelWidth <= 0) return 1;
            float pxPerFrame = pixelWidth / totalFrames;
            int step = Mathf.Max(1, Mathf.RoundToInt(40f / pxPerFrame));
            int[] nice = { 1, 2, 5, 10, 15, 20, 30, 60, 120 };
            foreach (int n in nice) if (n >= step) return n;
            return step;
        }

        // ─── Pointer: seek / zoom ─────────────────────────────────────────────

        private void RegisterSeekAndZoom(VisualElement lane)
        {
            lane.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                _draggingScrubber = true;
                lane.CapturePointer(evt.pointerId);
                SeekFromX(evt.localPosition.x, lane.contentRect.width);
                evt.StopPropagation();
            });
            lane.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!_draggingScrubber) return;
                SeekFromX(evt.localPosition.x, lane.contentRect.width);
                evt.StopPropagation();
            });
            lane.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!_draggingScrubber) return;
                _draggingScrubber = false;
                lane.ReleasePointer(evt.pointerId);
                evt.StopPropagation();
            });
            lane.RegisterCallback<WheelEvent>(evt =>
            {
                // 外側 ScrollView との競合を避けるため、埋め込み時は Ctrl/Cmd 必須
                if (_requireZoomModifier && !evt.ctrlKey && !evt.commandKey) return;
                ZoomAt(evt.localMousePosition.x, lane.contentRect.width, evt.delta.y);
                evt.StopPropagation();
            });
        }

        private void SeekFromX(float x, float laneW)
        {
            var m = _mode.ClipModel;
            float t = XToTime(x, laneW);
            SeekTo(m.SnapToFrame(t));
        }

        private void ZoomAt(float x, float laneW, float deltaY)
        {
            if (laneW <= 0f) return;
            float mouseNorm = Mathf.Clamp01(_viewStart + (x / laneW) * ViewRange);
            float zoom = deltaY > 0f ? 1.15f : (1f / 1.15f);
            float newRange = Mathf.Clamp(ViewRange * zoom, MIN_VIEW_RANGE, 1f);
            _viewStart = mouseNorm - (mouseNorm - _viewStart) * (newRange / ViewRange);
            _viewEnd = _viewStart + newRange;
            if (_viewStart < 0f) { _viewEnd -= _viewStart; _viewStart = 0f; }
            if (_viewEnd > 1f) { _viewStart -= (_viewEnd - 1f); _viewEnd = 1f; }
            _viewStart = Mathf.Clamp01(_viewStart);
            _viewEnd = Mathf.Clamp01(_viewEnd);
            MarkAllDirty();
            RefreshValues();
        }

        private void RegisterScrollbar(VisualElement lane)
        {
            lane.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0 || ViewRange >= 1f - 0.001f) return;
                float w = lane.contentRect.width;
                float thumbX = _viewStart * w;
                float thumbW = Mathf.Max(12f, ViewRange * w);
                _draggingViewScroll = true;
                _viewScrollGrabOffset = (evt.localPosition.x >= thumbX && evt.localPosition.x <= thumbX + thumbW)
                    ? evt.localPosition.x - thumbX : thumbW * 0.5f;
                lane.CapturePointer(evt.pointerId);
                MoveViewScroll(evt.localPosition.x, w);
                evt.StopPropagation();
            });
            lane.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!_draggingViewScroll) return;
                MoveViewScroll(evt.localPosition.x, lane.contentRect.width);
                evt.StopPropagation();
            });
            lane.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!_draggingViewScroll) return;
                _draggingViewScroll = false;
                lane.ReleasePointer(evt.pointerId);
                evt.StopPropagation();
            });
        }

        private void MoveViewScroll(float mouseX, float w)
        {
            if (w <= 0f) return;
            float range = ViewRange;
            float newStart = (mouseX - _viewScrollGrabOffset) / w;
            _viewStart = Mathf.Clamp(newStart, 0f, 1f - range);
            _viewEnd = _viewStart + range;
            MarkAllDirty();
            RefreshValues();
        }

        // ─── Pointer: keyframe drag / context ─────────────────────────────────

        private void RegisterTrackLane(VisualElement lane, AnimationTrack track)
        {
            lane.RegisterCallback<PointerDownEvent>(evt =>
            {
                float w = lane.contentRect.width;
                var m = _mode.ClipModel;
                int hit = HitKey(track, evt.localPosition.x, w);
                if (evt.button == 1)
                {
                    if (hit >= 0) ShowKeyContextMenu(track.ShapeName, track.KeyTimes[hit]);
                    else ShowTimelineContextMenu();
                    evt.StopPropagation();
                    return;
                }
                if (evt.button != 0 || hit < 0) return;
                float kt = track.KeyTimes[hit];
                _draggingKey = true;
                _dragFrame = m.TimeToFrame(kt);
                _dragShape = track.ShapeName;
                _dragUndoRecorded = false;
                lane.CapturePointer(evt.pointerId);
                // seekOnDown
                _mode.Playback.Stop();
                m.CurrentTime = kt;
                _mode.Preview.SampleAt(kt);
                RefreshValues();
                MarkAllDirty();
                _window.Repaint();
                evt.StopPropagation();
            });
            lane.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!_draggingKey || _dragShape != track.ShapeName) { UpdateHoverTip(lane, track, evt.localPosition.x); return; }
                MoveDraggedKeys(evt.localPosition.x, lane.contentRect.width, track.ShapeName);
                evt.StopPropagation();
            });
            lane.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!_draggingKey) return;
                _draggingKey = false;
                lane.ReleasePointer(evt.pointerId);
                evt.StopPropagation();
            });
        }

        /// <summary>
        /// ドラッグ中のキーをレーン内 X 座標が指すフレームへ移動する。
        /// shapeName = null でそのフレームの全トラックのキーをまとめて移動する。
        /// Undo スナップショットはジェスチャ内で 1 回だけ取る（recordUndo: !_dragUndoRecorded）。
        /// </summary>
        private void MoveDraggedKeys(float localX, float laneW, string shapeName)
        {
            var m = _mode.ClipModel;
            float t = XToTime(localX, laneW);
            int targetFrame = Mathf.RoundToInt(Mathf.Clamp(t * m.FPS, 0f, m.TotalFrames));
            if (targetFrame == _dragFrame) return;

            int reached = _mode.Editor.MoveKeys(_dragFrame, targetFrame, shapeName, recordUndo: !_dragUndoRecorded);
            if (reached != _dragFrame)
            {
                _dragUndoRecorded = true;
                _dragFrame = reached;
                m.CurrentTime = m.FrameToTime(reached);
                _mode.Preview.SampleAt(m.CurrentTime);
                _window.Repaint();
            }
            if (reached != targetFrame)
            {
                // 他キーにブロックされた: 停止位置の隣にあるブロック元キーを一瞬ハイライトする
                _blockFlashFrame = reached + (targetFrame > reached ? 1 : -1);
                _blockFlashUntil = EditorApplication.timeSinceStartup + 0.4;
            }
            RefreshValues();
            MarkAllDirty();
        }

        private int HitKey(AnimationTrack track, float x, float laneW)
        {
            var times = track.KeyTimes;
            for (int i = 0; i < times.Length; i++)
            {
                float kx = TimeToX(times[i], laneW);
                if (Mathf.Abs(kx - x) <= DIAMOND_SIZE + 3f) return i;
            }
            return -1;
        }

        private void UpdateHoverTip(VisualElement lane, AnimationTrack track, float x)
        {
            int hit = HitKey(track, x, lane.contentRect.width);
            if (hit < 0) { if (!string.IsNullOrEmpty(lane.tooltip)) lane.tooltip = string.Empty; return; }
            float kt = track.KeyTimes[hit];
            lane.tooltip = $"F:{_mode.ClipModel.TimeToFrame(kt)}  V:{track.Curve.Evaluate(kt):F1}";
        }

        // ─── Transport / edit operations（ロジックへ接続） ────────────────────

        private void SeekTo(float time)
        {
            var m = _mode.ClipModel;
            _mode.NotifyBeforeSeek();
            _mode.Playback.Stop();
            m.CurrentTime = Mathf.Clamp(time, 0f, m.ClipLength);
            _mode.Preview.SampleAt(m.CurrentTime);
            EnsureScrubberVisible();
            RefreshValues();
            MarkAllDirty();
            _window.Repaint();
        }

        private void StepFrame(int dir)
        {
            var m = _mode.ClipModel;
            SeekTo(m.CurrentTime + dir / Mathf.Max(1f, m.FPS));
        }

        private void GoToPrevKey()
        {
            float prev = _mode.ClipModel.PrevKeyTime(_mode.ClipModel.CurrentTime);
            if (prev >= 0f) SeekTo(prev);
        }

        private void GoToNextKey()
        {
            float next = _mode.ClipModel.NextKeyTime(_mode.ClipModel.CurrentTime);
            if (next >= 0f) SeekTo(next);
        }

        private void TogglePlay()
        {
            _mode.Playback.Toggle();
            RefreshValues();
            _window.Repaint();
        }

        private void EnsureScrubberVisible()
        {
            var m = _mode.ClipModel;
            if (m.ClipLength <= 0f) return;
            float norm = m.CurrentTime / m.ClipLength;
            if (norm < _viewStart || norm > _viewEnd)
            {
                float half = ViewRange * 0.5f;
                _viewStart = Mathf.Clamp01(norm - half);
                _viewEnd = Mathf.Clamp01(_viewStart + ViewRange);
            }
        }

        private void AddOrUpdateKey(string shapeName)
        {
            var smr = _model?.TargetSkinnedMesh;
            if (smr == null || smr.sharedMesh == null) return;
            int index = smr.sharedMesh.GetBlendShapeIndex(shapeName);
            if (index < 0) return;
            _mode.Editor.RecordKey(shapeName, _mode.ClipModel.CurrentTime, smr.GetBlendShapeWeight(index), _mode.CurrentInterp);
            _mode.Preview.SampleAt(_mode.ClipModel.CurrentTime);
            RefreshStructure();
            _window.Repaint();
        }

        private void DeleteTrack(string shapeName)
        {
            if (!EditorUtility.DisplayDialog(
                DenEmoLoc.T("dlg.timeline.deleteTrack.title"),
                DenEmoLoc.Tf("dlg.timeline.deleteTrack.msg", shapeName),
                DenEmoLoc.T("dlg.yes"), DenEmoLoc.T("dlg.no"))) return;
            _mode.Editor.DeleteTrack(shapeName);
            RefreshStructure();
            _window.Repaint();
        }

        private void ApplyLength(float newLen)
        {
            var m = _mode.ClipModel;
            int outside = 0;
            foreach (float t in m.AllKeyTimes)
                if (t > newLen + m.FrameTolerance) outside++;
            if (outside > 0)
            {
                int choice = EditorUtility.DisplayDialogComplex(
                    DenEmoLoc.T("dlg.timeline.shorten.title"),
                    DenEmoLoc.Tf("dlg.timeline.shorten.msg", outside),
                    DenEmoLoc.T("dlg.timeline.shorten.keep"),
                    DenEmoLoc.T("dlg.cancel"),
                    DenEmoLoc.T("dlg.timeline.shorten.delete"));
                if (choice == 1) { RefreshValues(); return; }
                if (choice == 2) _mode.Editor.DeleteKeysAfter(newLen);
            }
            m.ClipLength = newLen;
            if (m.CurrentTime > newLen) m.CurrentTime = newLen;
            _mode.Preview.SampleAt(m.CurrentTime);
            RefreshStructure();
            _window.Repaint();
        }

        private void ApplyInterpAll()
        {
            string interpName = new[] { "Step", "Linear", "Ease" }[(int)_mode.CurrentInterp];
            if (!EditorUtility.DisplayDialog(
                DenEmoLoc.T("dlg.timeline.applyInterpAll.title"),
                DenEmoLoc.Tf("dlg.timeline.applyInterpAll.msg", interpName),
                DenEmoLoc.T("dlg.yes"), DenEmoLoc.T("dlg.no"))) return;
            _mode.Editor.SetAllKeysInterpolation(_mode.CurrentInterp);
            _mode.Preview.SampleAt(_mode.ClipModel.CurrentTime);
            RefreshStructure();
            _window.Repaint();
        }

        // ─── Action row（フレーム単位のドラッグ移動ハンドル） ─────────────────

        /// <summary>
        /// トラック下部のフレーム移動ハンドル行を作る。キーのある各時刻に ↔ ハンドルを置き、
        /// ドラッグでそのフレームの全トラックのキーをまとめて移動する。
        /// キーの一括追加・削除はレーンの右クリックメニュー（ShowTimelineContextMenu）から行う。
        /// </summary>
        private void BuildActionRow(VisualElement area)
        {
            var row = Strip(out _);

            _actionLane = new VisualElement();
            _actionLane.AddToClassList("dennoko-tl-lane");
            _actionLane.AddToClassList("dennoko-tl-action-lane");
            _actionLane.style.height = ACTION_ROW_HEIGHT;
            _actionLane.RegisterCallback<GeometryChangedEvent>(_ => LayoutActionRow());
            RegisterContextMenu(_actionLane);
            row.Add(_actionLane);

            area.Add(row);
        }

        /// <summary>
        /// ハンドル行のキー単位ウィジェットを、現在のキー時刻とビュー範囲に合わせて配置する。
        /// ルーラーラベルと同じくプール再利用で、描画外（ツリー変更が許される文脈）から呼ぶ。
        /// </summary>
        private void LayoutActionRow()
        {
            if (_actionLane == null || _mode?.ClipModel?.Clip == null) return;
            var m = _mode.ClipModel;
            float w = _actionLane.contentRect.width;
            if (w <= 0f || m.ClipLength <= 0f) { HideExtraActionKeys(0); return; }

            int used = 0;
            foreach (float kTime in m.AllKeyTimes)
            {
                float kx = TimeToX(kTime, w);
                if (kx < -ACTION_KEY_WIDTH || kx > w + ACTION_KEY_WIDTH) continue;
                var widget = GetActionKey(used++);
                widget.Time = kTime;
                widget.Handle.style.display = DisplayStyle.Flex;
                widget.Handle.style.left = kx - ACTION_KEY_WIDTH * 0.5f;
            }
            HideExtraActionKeys(used);
        }

        private ActionKeyWidget GetActionKey(int idx)
        {
            if (idx < _actionKeys.Count) return _actionKeys[idx];

            var widget = new ActionKeyWidget();
            widget.Handle = new Label("↔") { tooltip = DenEmoLoc.T("ui.timeline.moveFrame.tip") };
            widget.Handle.AddToClassList("dennoko-tl-action-handle");
            RegisterFrameDrag(widget);

            _actionLane.Add(widget.Handle);
            _actionKeys.Add(widget);
            return widget;
        }

        private void HideExtraActionKeys(int usedCount)
        {
            for (int i = usedCount; i < _actionKeys.Count; i++)
                _actionKeys[i].Handle.style.display = DisplayStyle.None;
        }

        /// <summary>↔ ハンドル: そのフレームにある全トラックのキーをまとめて移動する（shapeName = null）。</summary>
        private void RegisterFrameDrag(ActionKeyWidget widget)
        {
            var handle = widget.Handle;
            handle.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                _draggingKey = true;
                _dragFrame = _mode.ClipModel.TimeToFrame(widget.Time);
                _dragShape = null;
                _dragUndoRecorded = false;
                handle.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });
            handle.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!_draggingKey || _dragShape != null) return;
                float w = _actionLane.contentRect.width;
                float x = _actionLane.WorldToLocal(new Vector2(evt.position.x, evt.position.y)).x;
                MoveDraggedKeys(x, w, null);
                evt.StopPropagation();
            });
            handle.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!_draggingKey) return;
                _draggingKey = false;
                handle.ReleasePointer(evt.pointerId);
                evt.StopPropagation();
            });
        }

        /// <summary>
        /// リスト表示中（検索・フィルター後）の全シェイプのキーを、現在時刻へ一括挿入する。
        /// Undo は AnimationClipEditor.RecordKeys が 1 スナップショットにまとめる。
        /// </summary>
        private void InsertKeysAtCurrentFrame()
        {
            if (_model == null || _mode.ClipModel.Clip == null) return;
            var m = _mode.ClipModel;

            var entries = new List<(string, float)>();
            foreach (var item in _model.Items)
            {
                if (!IsInsertTarget(item)) continue;
                var smr = item.OwnerSmr != null ? item.OwnerSmr : _model.TargetSkinnedMesh;
                if (smr == null || smr.sharedMesh == null) continue;
                if (item.Index < 0 || item.Index >= smr.sharedMesh.blendShapeCount) continue;
                entries.Add((item.Name, smr.GetBlendShapeWeight(item.Index)));
            }
            if (entries.Count == 0) return;

            _mode.Editor.RecordKeys(entries, m.CurrentTime, _mode.CurrentInterp);
            _mode.ClearUnrecordedTweaks();
            _mode.Preview.SampleAt(m.CurrentTime);
            RefreshStructure();
            _window.Repaint();
        }

        private static bool IsInsertTarget(ShapeKeyItem item)
            => item.IsVisible && !item.IsVrcShape && !item.IsLipSyncShape;

        private int CountInsertTargets()
        {
            if (_model == null) return 0;
            int n = 0;
            foreach (var item in _model.Items)
                if (IsInsertTarget(item)) n++;
            return n;
        }

        /// <summary>
        /// 指定時刻にある全トラックのキーを削除する（Undo は 1 スナップショット）。
        /// confirm=false はショートカット用（ダイアログを出さず Undo で戻す前提）。
        /// </summary>
        private void DeleteFrameKeys(float kTime, bool confirm = true)
        {
            if (!_mode.ClipModel.HasAnyKeyframeAt(kTime)) return;
            if (confirm && !EditorUtility.DisplayDialog(
                DenEmoLoc.T("dlg.timeline.deleteFrame.title"),
                DenEmoLoc.Tf("dlg.timeline.deleteFrame.msg", kTime.ToString("F2")),
                DenEmoLoc.T("dlg.yes"), DenEmoLoc.T("dlg.no"))) return;
            _mode.Editor.DeleteKeysAtTime(kTime);
            _mode.Preview.SampleAt(_mode.ClipModel.CurrentTime);
            RefreshStructure();
            _window.Repaint();
        }

        // ─── Clipboard / context menus ────────────────────────────────────────

        private void CopyFrame(float time)
        {
            var m = _mode.ClipModel;
            _clipboard.Clear();
            foreach (var track in m.Tracks)
            {
                if (AnimationClipModel.FindKeyIndex(track.Curve, time, m.FrameTolerance) < 0) continue;
                _clipboard.Add(new KeyClip
                {
                    Shape = track.ShapeName,
                    Value = track.Curve.Evaluate(time),
                    Interp = m.GetKeyInterpolation(track.ShapeName, time),
                });
            }
        }

        private void PasteAtCurrent()
        {
            if (_clipboard.Count == 0) return;
            var m = _mode.ClipModel;
            float pasteTime = m.SnapToFrame(m.CurrentTime);
            bool first = true;
            foreach (var e in _clipboard)
            {
                _mode.Editor.RecordKey(e.Shape, pasteTime, e.Value, e.Interp, recordUndo: first);
                first = false;
            }
            _mode.Preview.SampleAt(m.CurrentTime);
            RefreshStructure();
            _window.Repaint();
        }

        private void ShowKeyContextMenu(string shapeName, float kTime)
        {
            var m = _mode.ClipModel;
            var menu = new GenericMenu();
            AddCurrentFrameItems(menu);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent(DenEmoLoc.T("ui.timeline.menu.delete")), false, () =>
            {
                _mode.Editor.DeleteKey(shapeName, kTime);
                _mode.Preview.SampleAt(m.CurrentTime);
                RefreshStructure();
                _window.Repaint();
            });
            menu.AddSeparator("");
            menu.AddItem(new GUIContent(DenEmoLoc.T("ui.timeline.menu.copyFrame")), false, () => CopyFrame(kTime));
            AddPasteItem(menu);
            menu.AddSeparator("");
            void AddInterp(string label, InterpolationType interp)
            {
                bool cur = m.GetKeyInterpolation(shapeName, kTime) == interp;
                menu.AddItem(new GUIContent(label), cur, () =>
                {
                    _mode.Editor.SetKeyInterpolation(shapeName, kTime, interp);
                    _mode.Preview.SampleAt(m.CurrentTime);
                    RefreshStructure();
                    _window.Repaint();
                });
            }
            AddInterp("Step", InterpolationType.Step);
            AddInterp("Linear", InterpolationType.Linear);
            AddInterp("Ease", InterpolationType.Ease);
            menu.ShowAsContext();
        }

        /// <summary>ダイヤ以外（ルーラー / スクラバー / ハンドル行 / トラックの余白）の右クリックメニュー。</summary>
        private void ShowTimelineContextMenu()
        {
            if (_mode.ClipModel.Clip == null) return;
            var menu = new GenericMenu();
            AddCurrentFrameItems(menu);
            menu.AddSeparator("");
            AddPasteItem(menu);
            menu.ShowAsContext();
        }

        /// <summary>現在フレームへのキー一括追加・一括削除。すべての右クリックメニューの先頭に置く。</summary>
        private void AddCurrentFrameItems(GenericMenu menu)
        {
            var m = _mode.ClipModel;
            menu.AddItem(new GUIContent(DenEmoLoc.Tf("ui.timeline.menu.insertAllN", CountInsertTargets())),
                false, InsertKeysAtCurrentFrame);

            var deleteLabel = new GUIContent(DenEmoLoc.T("ui.timeline.menu.deleteFrame"));
            if (m.HasAnyKeyframeAt(m.CurrentTime))
                menu.AddItem(deleteLabel, false, () => DeleteFrameKeys(m.SnapToFrame(m.CurrentTime)));
            else
                menu.AddDisabledItem(deleteLabel);
        }

        private void AddPasteItem(GenericMenu menu)
        {
            var paste = new GUIContent(DenEmoLoc.T("ui.timeline.menu.paste"));
            if (_clipboard.Count > 0) menu.AddItem(paste, false, PasteAtCurrent);
            else                      menu.AddDisabledItem(paste);
        }

        /// <summary>レーンの右クリックで共通メニューを開く。</summary>
        private void RegisterContextMenu(VisualElement lane)
        {
            lane.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 1) return;
                ShowTimelineContextMenu();
                evt.StopPropagation();
            });
        }

        // ─── Keyboard shortcuts ───────────────────────────────────────────────
        // Space: 再生/停止  ← / →: 1 フレーム移動  , / .: 前後のキーへ
        // Delete / Backspace: 現在フレームの全キー削除（確認なし・Undo 可）
        // Ctrl+C / Ctrl+V: 現在フレームのコピー / ペースト

        /// <summary>
        /// パネルのルートに TrickleDown で登録し、フォーカス中の子コントロールより先にキーを受け取る。
        /// フォーカスが無いときのキーもパネルルートへ届くため、ウィンドウ全体で効く。
        /// </summary>
        private void HookShortcuts(IPanel panel)
        {
            UnhookShortcuts();
            _shortcutHost = panel?.visualTree;
            _shortcutHost?.RegisterCallback<KeyDownEvent>(OnShortcutKeyDown, TrickleDown.TrickleDown);
        }

        private void UnhookShortcuts()
        {
            if (_shortcutHost == null) return;
            _shortcutHost.UnregisterCallback<KeyDownEvent>(OnShortcutKeyDown, TrickleDown.TrickleDown);
            _shortcutHost = null;
        }

        private void OnShortcutKeyDown(KeyDownEvent evt)
        {
            // 非表示のタブ／クリップ未設定のときは何もしない（_active はホスト側が更新する）
            if (!_active || _root == null || _mode?.ClipModel?.Clip == null) return;
            if (evt.altKey || evt.shiftKey) return;
            if (IsTextInputFocused()) return;

            var m = _mode.ClipModel;
            bool ctrl = evt.ctrlKey || evt.commandKey;
            bool handled = true;

            switch (evt.keyCode)
            {
                case KeyCode.Space  when !ctrl: TogglePlay();  break;
                case KeyCode.Comma  when !ctrl: GoToPrevKey(); break;
                case KeyCode.Period when !ctrl: GoToNextKey(); break;

                // 矢印はフォーカス中のスライダーの値調整を優先する（該当時は default へ落ちて素通し）
                case KeyCode.LeftArrow  when !ctrl && !IsSliderFocused(): StepFrame(-1); break;
                case KeyCode.RightArrow when !ctrl && !IsSliderFocused(): StepFrame(+1); break;

                case KeyCode.Delete    when !ctrl:
                case KeyCode.Backspace when !ctrl:
                    // ショートカットからの削除は確認ダイアログを出さない（Undo で戻せる）
                    handled = m.HasAnyKeyframeAt(m.CurrentTime);
                    if (handled) DeleteFrameKeys(m.SnapToFrame(m.CurrentTime), confirm: false);
                    break;

                case KeyCode.C when ctrl:
                    CopyFrame(m.SnapToFrame(m.CurrentTime));
                    break;

                case KeyCode.V when ctrl:
                    handled = _clipboard.Count > 0;
                    if (handled) PasteAtCurrent();
                    break;

                default: handled = false; break;
            }

            if (!handled) return;
            evt.StopPropagation();
            evt.PreventDefault();
            _window.Repaint();
        }

        /// <summary>テキスト入力（検索欄・フレーム番号・FPS / 長さ / 速度）にフォーカスがあるか。</summary>
        private bool IsTextInputFocused()
        {
            for (var e = FocusedElement(); e != null; e = e.parent)
                if (e is TextField || e is IntegerField || e is FloatField ||
                    e is DoubleField || e is LongField) return true;
            return false;
        }

        /// <summary>シェイプキーのスライダーにフォーカスがあるか（矢印キーは値調整に譲る）。</summary>
        private bool IsSliderFocused()
        {
            for (var e = FocusedElement(); e != null; e = e.parent)
                if (e is Slider || e is SliderInt || e is MinMaxSlider) return true;
            return false;
        }

        private VisualElement FocusedElement()
            => _root.panel?.focusController?.focusedElement as VisualElement;

        // ─── Theme color resolution ───────────────────────────────────────────

        private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
        {
            var cs = evt.customStyle;
            if (cs.TryGetValue(OutlineProp, out var o)) _cOutline = o;
            if (cs.TryGetValue(TextPrimaryProp, out var tp)) _cTextPrim = tp;
            if (cs.TryGetValue(TextSecondaryProp, out var ts)) _cTextSecond = ts;
            if (cs.TryGetValue(Surface2Prop, out var s2)) _cSurface2 = s2;
            if (cs.TryGetValue(WarningProp, out var wn)) _cWarning = wn;
            MarkAllDirty();
        }

        // ─── small builders ───────────────────────────────────────────────────

        private static Label Caption()
        {
            var l = new Label();
            l.AddToClassList("dennoko-text-tertiary");
            l.AddToClassList("dennoko-tl-caption");
            return l;
        }

        private static VisualElement Spacer(float w)
        {
            var s = new VisualElement();
            s.style.width = w;
            s.style.flexShrink = 0;
            return s;
        }
    }
}
