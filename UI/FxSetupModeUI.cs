using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using DenEmo.Core;
using DenEmo.Models;

namespace DenEmo.UI
{
    /// <summary>
    /// 「アバターへ適用」モードのオーケストレータ。
    /// アバター/FX レイヤーの自動検出 → 表情アニメーション一覧 → 差し替え先の割当て（マッピング）→
    /// 一括差し替え、までを 1 画面で行う。マッピングはセッション限定で永続化しない。
    /// </summary>
    public class FxSetupModeUI
    {
        public System.Action<string, int> StatusSink;

        // ─── 検出状態 ─────────────────────────────────────────────────────────
        private Component          _descriptor;
        private AnimatorController _controller;        // 実際にスキャン/書き換えする対象
        private AnimatorController _manualController;  // Descriptor 不在時の手動指定
        private bool _fxMissing;            // Descriptor はあるが FX が未設定
        private bool _fxReadFailed;         // baseAnimationLayers を読めなかった（SDK 差異）
        private bool _fxOverrideUnsupported;
        private Transform _avatarRoot;
        private readonly HashSet<string> _targetSmrPaths = new HashSet<string>();

        // ─── 一覧・マッピング ─────────────────────────────────────────────────
        private List<FxExpressionEntry> _entries = new List<FxExpressionEntry>();
        private readonly Dictionary<AnimationClip, FxMapping> _mappings = new Dictionary<AnimationClip, FxMapping>();
        private readonly HashSet<AnimationClip> _expanded = new HashSet<AnimationClip>();
        private string  _search = string.Empty;
        private bool    _showOnlyAssigned;
        private bool    _showAllClips;      // メッシュパスフィルタ解除（0件時の救済）
        private Vector2 _listScroll;

        // ─── プレビュー / 適用 ────────────────────────────────────────────────
        private readonly FxHoverPreview _hover = new FxHoverPreview();
        private AnimationClip   _uiHoveredClip;   // 前回 Repaint でホバーしていたクリップ（行ハイライト用）
        private bool            _pickerOpen;      // ピッカー表示中はメイン側のホバー判定を止める
        private bool            _applyDirect;     // false = 複製モード（推奨）
        private FxReplaceResult _lastResult;
        private string          _pickerFolder;

        private GUIStyle _warnCaptionStyle;
        private GUIStyle _successCaptionStyle;
        private GUIStyle _infoBandStyle;

        public FxHoverPreview Hover => _hover;
        public string PickerFolder
        {
            get => _pickerFolder;
            set => _pickerFolder = value;
        }
        public void SetPickerOpen(bool open) => _pickerOpen = open;

        // ─── ライフサイクル ───────────────────────────────────────────────────

        public void OnEnter(ShapeKeyModel model)
        {
            _applyDirect = DenEmoProjectPrefs.GetBool("DenEmo_Fx_ApplyMode_Direct", false);
            _lastResult  = null;
            Refresh(model);
        }

        public void OnExit()
        {
            _hover.Stop();
            _uiHoveredClip = null;
            DenEmoProjectPrefs.SetBool("DenEmo_Fx_ApplyMode_Direct", _applyDirect);
        }

        public void OnDisable() => OnExit();

        public void Tick(EditorWindow window) => _hover.Tick();

        public void OnUndoRedo(ShapeKeyModel model) => Refresh(model);

        public void OnTargetChanged(ShapeKeyModel model)
        {
            _hover.Stop();
            _mappings.Clear();
            _lastResult = null;
            Refresh(model);
        }

        /// <summary>アバター/FX の再検出と一覧の再スキャン。</summary>
        public void Refresh(ShapeKeyModel model)
        {
            _descriptor = null;
            _controller = null;
            _fxMissing = false;
            _fxReadFailed = false;
            _fxOverrideUnsupported = false;
            _avatarRoot = null;
            _entries = new List<FxExpressionEntry>();
            _targetSmrPaths.Clear();

            var target = model?.TargetSkinnedMesh;
            if (target == null)
            {
                _hover.SetRoot(null);
                return;
            }

            _descriptor = VrcAvatarReflection.FindDescriptor(target.transform);
            _avatarRoot = _descriptor != null ? _descriptor.transform : target.transform.root;
            _hover.SetRoot(_avatarRoot);

            // FX クリップのバインディングパスと突き合わせる対象メッシュのパス集合
            var meshes = (model.ActiveMeshes != null && model.ActiveMeshes.Count > 0)
                ? model.ActiveMeshes
                : new List<SkinnedMeshRenderer> { target };
            foreach (var smr in meshes)
            {
                if (smr == null) continue;
                _targetSmrPaths.Add(ComputePathFrom(_avatarRoot, smr.transform));
            }

            if (_descriptor != null)
            {
                if (!VrcAvatarReflection.TryGetFxController(_descriptor, out var rc, out bool isDefault))
                {
                    _fxReadFailed = true;
                }
                else if (rc == null || isDefault)
                {
                    _fxMissing = rc == null;
                    _controller = rc as AnimatorController;
                    if (rc != null && _controller == null) _fxOverrideUnsupported = true;
                }
                else
                {
                    _controller = rc as AnimatorController;
                    if (_controller == null) _fxOverrideUnsupported = true;
                }
            }

            // Descriptor なし / 読み取り失敗時は手動指定コントローラーで代替
            if (_controller == null && _manualController != null)
                _controller = _manualController;

            if (_controller != null)
                _entries = FxLayerScanner.Scan(_controller, _targetSmrPaths);

            // 存在しなくなったマッピングを掃除
            var alive = new HashSet<AnimationClip>();
            foreach (var e in _entries) alive.Add(e.Clip);
            var stale = new List<AnimationClip>();
            foreach (var kvp in _mappings)
                if (!alive.Contains(kvp.Key)) stale.Add(kvp.Key);
            foreach (var c in stale) _mappings.Remove(c);
        }

        private static string ComputePathFrom(Transform root, Transform t)
        {
            var parts = new List<string>();
            while (t != null && t != root) { parts.Add(t.name); t = t.parent; }
            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }

        // ─── 描画 ─────────────────────────────────────────────────────────────

        public void Draw(ShapeKeyModel model, string saveFolder, EditorWindow window)
        {
            EnsureStyles();
            if (string.IsNullOrEmpty(_pickerFolder)) _pickerFolder = saveFolder;

            var e = Event.current;
            if (e.type == EventType.MouseMove) window.Repaint();
            if (e.type == EventType.MouseLeaveWindow && !_pickerOpen)
            {
                _hover.SetHover(null);
                _uiHoveredClip = null;
                window.Repaint();
            }

            AnimationClip hoveredClip = null;

            DrawAvatarSection(model, window);

            if (_controller != null)
            {
                DrawMappingSection(model, window, ref hoveredClip);
                DrawApplySection(model, window);
            }

            // Repaint 時のみ rect が確定しているため、ここでまとめてホバー先を通知する
            if (e.type == EventType.Repaint && !_pickerOpen)
            {
                _hover.SetHover(hoveredClip);
                _uiHoveredClip = hoveredClip;
            }
        }

        // ─── アバター / FX セクション ─────────────────────────────────────────

        private void DrawAvatarSection(ShapeKeyModel model, EditorWindow window)
        {
            DenEmoTheme.BeginSection(DenEmoLoc.T("ui.fx.section.avatar"));

            if (_descriptor == null)
            {
                GUILayout.Label(DenEmoLoc.T("ui.fx.avatar.notFound"), _warnCaptionStyle);
                GUILayout.Space(4);

                EditorGUI.BeginChangeCheck();
                var newCtrl = EditorGUILayout.ObjectField(
                    new GUIContent(DenEmoLoc.T("ui.fx.avatar.manualController")),
                    _manualController, typeof(AnimatorController), false) as AnimatorController;
                if (EditorGUI.EndChangeCheck())
                {
                    _manualController = newCtrl;
                    _mappings.Clear();
                    _lastResult = null;
                    Refresh(model);
                }
            }
            else
            {
                GUILayout.Label(DenEmoLoc.Tf("ui.fx.avatar.label", _avatarRoot != null ? _avatarRoot.name : _descriptor.name) + " ✓",
                    _successCaptionStyle);

                if (_fxReadFailed)
                {
                    GUILayout.Label(DenEmoLoc.T("ui.fx.fx.readFailed"), _warnCaptionStyle);
                    GUILayout.Space(4);
                    EditorGUI.BeginChangeCheck();
                    var newCtrl = EditorGUILayout.ObjectField(
                        new GUIContent(DenEmoLoc.T("ui.fx.avatar.manualController")),
                        _manualController, typeof(AnimatorController), false) as AnimatorController;
                    if (EditorGUI.EndChangeCheck())
                    {
                        _manualController = newCtrl;
                        Refresh(model);
                    }
                }
                else if (_fxOverrideUnsupported)
                {
                    GUILayout.Label(DenEmoLoc.T("ui.fx.fx.overrideUnsupported"), _warnCaptionStyle);
                }
                else if (_fxMissing && _controller == null)
                {
                    GUILayout.Label(DenEmoLoc.T("ui.fx.fx.missing"), _warnCaptionStyle);
                }
            }

            if (_controller != null)
            {
                GUILayout.Space(2);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(
                    DenEmoLoc.Tf("ui.fx.fx.label", _controller.name) + " — " + DenEmoLoc.Tf("ui.fx.fx.stats", _entries.Count),
                    DenEmoTheme.SecondaryTextStyle);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(DenEmoLoc.T("ui.fx.rescan"), DenEmoTheme.MiniButtonStyle, GUILayout.Width(70), GUILayout.Height(18)))
                {
                    Refresh(model);
                    window.Repaint();
                }
                EditorGUILayout.EndHorizontal();
            }

            DenEmoTheme.EndSection();
        }

        // ─── 差し替えマッピング一覧 ───────────────────────────────────────────

        private void DrawMappingSection(ShapeKeyModel model, EditorWindow window, ref AnimationClip hoveredClip)
        {
            DenEmoTheme.BeginSection(DenEmoLoc.T("ui.fx.section.list"));

            // 検索 + フィルタチップ
            EditorGUILayout.BeginHorizontal();
            _search = EditorGUILayout.TextField(_search, DenEmoTheme.SearchTextFieldStyle);
            var assignedStyle = _showOnlyAssigned ? DenEmoTheme.ChipOnStyle : DenEmoTheme.ChipOffStyle;
            if (GUILayout.Button(DenEmoLoc.T("ui.fx.filter.assignedOnly"), assignedStyle, GUILayout.Width(110)))
                _showOnlyAssigned = !_showOnlyAssigned;
            EditorGUILayout.EndHorizontal();

            var visible = CollectVisibleEntries();

            int matchCount = 0;
            foreach (var entry in _entries)
                if (entry.MatchesTargetMesh) matchCount++;

            if (matchCount == 0 && _entries.Count > 0 && !_showAllClips)
            {
                // 対象メッシュに一致するクリップが 0 件 → パスフィルタ解除の救済導線
                GUILayout.Label(DenEmoLoc.T("ui.fx.list.empty"), DenEmoTheme.SecondaryTextStyle);
                if (GUILayout.Button(DenEmoLoc.T("ui.fx.filter.showAll"), DenEmoTheme.SecondaryButtonStyle))
                    _showAllClips = true;
            }
            else if (_entries.Count == 0)
            {
                GUILayout.Label(DenEmoLoc.T("ui.fx.list.empty"), DenEmoTheme.SecondaryTextStyle);
            }
            else if (visible.Count == 0)
            {
                GUILayout.Label(DenEmoLoc.T("ui.fx.list.emptyFiltered"), DenEmoTheme.CaptionStyle);
            }
            else
            {
                float height = Mathf.Clamp(visible.Count * 26f + 12f, 60f, 320f);
                _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.Height(height));
                foreach (var entry in visible)
                    DrawEntryRow(entry, window, ref hoveredClip);
                EditorGUILayout.EndScrollView();
            }

            // プレビュー状態バー
            GUILayout.Space(2);
            if (_hover.IsActive && _hover.ActiveClip != null)
            {
                EditorGUILayout.BeginHorizontal(_infoBandStyle);
                GUILayout.Label(DenEmoLoc.Tf("ui.fx.hover.playing", _hover.ActiveClip.name), DenEmoTheme.SecondaryTextStyle);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(DenEmoLoc.T("ui.fx.hover.stop"), DenEmoTheme.MiniButtonStyle, GUILayout.Width(50), GUILayout.Height(16)))
                    _hover.Stop();
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label(DenEmoLoc.T("ui.fx.hover.hint"), DenEmoTheme.CaptionStyle);
            }

            DenEmoTheme.EndSection();
        }

        private List<FxExpressionEntry> CollectVisibleEntries()
        {
            var tokens = ShapeKeyModel.BuildSearchTokens(_search);
            var list = new List<FxExpressionEntry>();
            foreach (var entry in _entries)
            {
                if (entry.Clip == null) continue;
                if (!_showAllClips && !entry.MatchesTargetMesh) continue;
                if (_showOnlyAssigned && !_mappings.ContainsKey(entry.Clip)) continue;
                if (tokens.Length > 0)
                {
                    bool match = true;
                    var nameLower = entry.Clip.name.ToLowerInvariant();
                    foreach (var t in tokens)
                        if (nameLower.IndexOf(t.ToLowerInvariant(), System.StringComparison.Ordinal) < 0) { match = false; break; }
                    if (!match) continue;
                }
                list.Add(entry);
            }
            return list;
        }

        private void DrawEntryRow(FxExpressionEntry entry, EditorWindow window, ref AnimationClip hoveredClip)
        {
            _mappings.TryGetValue(entry.Clip, out var mapping);
            bool multi    = entry.Slots.Count > 1;
            bool expanded = multi && _expanded.Contains(entry.Clip);
            bool rowHovered = _uiHoveredClip != null &&
                              (_uiHoveredClip == entry.Clip || (mapping != null && _uiHoveredClip == mapping.NewClip));

            // RowStyle / RowHoverStyle はメトリクスが同一のため Layout/Repaint 間で切り替えても安全
            EditorGUILayout.BeginHorizontal(rowHovered ? DenEmoTheme.RowHoverStyle : DenEmoTheme.RowStyle);

            // 複数参照の展開トグル（単一参照は目立たせない）
            if (multi)
            {
                if (GUILayout.Button(expanded ? "▾" : "▸", DenEmoTheme.MiniButtonStyle, GUILayout.Width(18), GUILayout.Height(18)))
                {
                    if (expanded) _expanded.Remove(entry.Clip);
                    else          _expanded.Add(entry.Clip);
                }
            }
            else
            {
                GUILayout.Space(24);
            }

            // クリップ名（ホバーでシーンプレビュー）
            string label = entry.Clip.name;
            if (multi) label += " " + DenEmoLoc.Tf("ui.fx.row.usage", entry.Slots.Count);
            if (!entry.MatchesTargetMesh) label = "⚠ " + label;

            string tooltip = BuildSlotTooltip(entry);
            GUILayout.Label(new GUIContent(label, tooltip), DenEmoTheme.SecondaryTextStyle, GUILayout.MinWidth(60));

            GUILayout.Label("→", DenEmoTheme.CaptionStyle, GUILayout.Width(16));

            // 差し替え先スロット（クリックでピッカー、D&D 受付、ホバーで新クリップをプレビュー）
            var slotContent = new GUIContent(
                mapping != null && mapping.NewClip != null ? mapping.NewClip.name : DenEmoLoc.T("ui.fx.slot.none"));
            var slotRect = GUILayoutUtility.GetRect(slotContent, DenEmoTheme.MiniButtonStyle,
                GUILayout.Width(150), GUILayout.Height(18));

            if (GUI.Button(slotRect, slotContent, DenEmoTheme.MiniButtonStyle))
            {
                _pickerOpen = true;
                _hover.SetHover(null);
                PopupWindow.Show(slotRect, new FxClipPickerPopup(this, entry, window));
            }
            HandleSlotDragAndDrop(slotRect, entry, window);

            // 割当て解除
            if (mapping != null)
            {
                if (GUILayout.Button(new GUIContent("✕", DenEmoLoc.T("ui.fx.slot.clear.tip")),
                        DenEmoTheme.MiniButtonStyle, GUILayout.Width(20), GUILayout.Height(18)))
                {
                    _mappings.Remove(entry.Clip);
                }
            }
            else
            {
                GUILayout.Space(24);
            }

            EditorGUILayout.EndHorizontal();

            // 行全体のホバー判定を Repaint イベントで行う
            if (Event.current.type == EventType.Repaint)
            {
                var rowRect = GUILayoutUtility.GetLastRect();
                if (rowRect.Contains(Event.current.mousePosition))
                {
                    if (mapping != null && mapping.NewClip != null && slotRect.Contains(Event.current.mousePosition))
                        hoveredClip = mapping.NewClip;
                    else
                        hoveredClip = entry.Clip;
                }
            }

            // 割当て済み行の左端アクセントバー
            if (Event.current.type == EventType.Repaint && mapping != null && mapping.NewClip != null)
            {
                var rowRect = GUILayoutUtility.GetLastRect();
                EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.y, 2f, rowRect.height), DenEmoTheme.SemanticSuccess);
            }

            // パス不一致の注記
            if (!entry.MatchesTargetMesh)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(28);
                GUILayout.Label(DenEmoLoc.Tf("ui.fx.row.pathMismatch", entry.FirstBindingPath ?? ""), _warnCaptionStyle);
                EditorGUILayout.EndHorizontal();
            }

            // 参照箇所ごとの対象チェック（展開時のみ）
            if (expanded)
            {
                foreach (var slot in entry.Slots)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(28);
                    bool enabled = mapping == null || !mapping.DisabledSlotKeys.Contains(slot.SlotKey);
                    EditorGUI.BeginChangeCheck();
                    bool newEnabled = EditorGUILayout.ToggleLeft(slot.DisplayPath, enabled);
                    if (EditorGUI.EndChangeCheck() && mapping != null)
                    {
                        if (newEnabled) mapping.DisabledSlotKeys.Remove(slot.SlotKey);
                        else            mapping.DisabledSlotKeys.Add(slot.SlotKey);
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        private static string BuildSlotTooltip(FxExpressionEntry entry)
        {
            var lines = new List<string>();
            foreach (var slot in entry.Slots) lines.Add(slot.DisplayPath);
            return string.Join("\n", lines.ToArray());
        }

        private void HandleSlotDragAndDrop(Rect slotRect, FxExpressionEntry entry, EditorWindow window)
        {
            var e = Event.current;
            if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform) return;
            if (!slotRect.Contains(e.mousePosition)) return;

            AnimationClip dragged = null;
            foreach (var obj in DragAndDrop.objectReferences)
                if (obj is AnimationClip c) { dragged = c; break; }
            if (dragged == null) return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Link;
            if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                TryAssign(entry, dragged, window);
            }
            e.Use();
        }

        /// <summary>差し替え先クリップの割当て（検証込み）。ピッカー / D&D の両方から呼ばれる。</summary>
        public void TryAssign(FxExpressionEntry entry, AnimationClip newClip, EditorWindow window)
        {
            if (newClip == null)
            {
                _mappings.Remove(entry.Clip);
                window.Repaint();
                return;
            }
            if (newClip == entry.Clip)
            {
                StatusSink?.Invoke(DenEmoLoc.T("ui.fx.err.selfAssign"), 3);
                return;
            }
            if (!EditorUtility.IsPersistent(newClip))
            {
                StatusSink?.Invoke(DenEmoLoc.T("ui.fx.err.sceneClip"), 3);
                return;
            }

            if (!_mappings.TryGetValue(entry.Clip, out var mapping))
            {
                mapping = new FxMapping();
                _mappings.Add(entry.Clip, mapping);
            }
            mapping.NewClip = newClip;
            window.Repaint();
        }

        // ─── 適用セクション ───────────────────────────────────────────────────

        private void DrawApplySection(ShapeKeyModel model, EditorWindow window)
        {
            DenEmoTheme.BeginSection(DenEmoLoc.T("ui.fx.section.apply"));

            var jobs = BuildJobs(out int slotCount);

            GUILayout.Label(DenEmoLoc.Tf("ui.fx.apply.count", jobs.Count), DenEmoTheme.SecondaryTextStyle);
            GUILayout.Space(2);

            // 適用モード（複製 = 推奨デフォルト / 直接）
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(DenEmoLoc.T("ui.fx.mode.duplicate"),
                    !_applyDirect ? DenEmoTheme.ChipOnStyle : DenEmoTheme.ChipOffStyle, GUILayout.Width(150)))
                _applyDirect = false;
            GUILayout.Space(2);
            if (GUILayout.Button(DenEmoLoc.T("ui.fx.mode.direct"),
                    _applyDirect ? DenEmoTheme.ChipOnStyle : DenEmoTheme.ChipOffStyle, GUILayout.Width(150)))
                _applyDirect = true;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (_applyDirect)
                GUILayout.Label(DenEmoLoc.T("ui.fx.mode.direct.desc"), _warnCaptionStyle);
            else
                GUILayout.Label(DenEmoLoc.T("ui.fx.mode.duplicate.desc"), DenEmoTheme.CaptionStyle);

            if (!_applyDirect && _descriptor == null)
                GUILayout.Label(DenEmoLoc.T("ui.fx.mode.manualNote"), _warnCaptionStyle);

            GUILayout.Space(4);

            using (new EditorGUI.DisabledScope(jobs.Count == 0 || _controller == null))
            {
                if (GUILayout.Button(DenEmoLoc.Tf("ui.fx.apply.button", jobs.Count), DenEmoTheme.ActionButtonStyle))
                    ExecuteApply(model, window, jobs, slotCount);
            }

            DrawResultCard();

            DenEmoTheme.EndSection();
        }

        private List<(FxExpressionEntry entry, FxMapping mapping)> BuildJobs(out int slotCount)
        {
            var jobs = new List<(FxExpressionEntry, FxMapping)>();
            slotCount = 0;
            foreach (var entry in _entries)
            {
                if (!_mappings.TryGetValue(entry.Clip, out var mapping)) continue;
                if (mapping.NewClip == null) continue;

                int enabled = 0;
                foreach (var slot in entry.Slots)
                    if (!mapping.DisabledSlotKeys.Contains(slot.SlotKey)) enabled++;
                if (enabled == 0) continue;

                slotCount += enabled;
                jobs.Add((entry, mapping));
            }
            return jobs;
        }

        private void ExecuteApply(ShapeKeyModel model, EditorWindow window,
            List<(FxExpressionEntry entry, FxMapping mapping)> jobs, int slotCount)
        {
            // 確認ダイアログ（モード・対象一覧を明記）
            var lines = new List<string>();
            foreach (var (entry, mapping) in jobs)
            {
                if (lines.Count >= 10)
                {
                    lines.Add(DenEmoLoc.Tf("ui.fx.confirm.more", jobs.Count - 10));
                    break;
                }
                lines.Add(entry.Clip.name + " → " + mapping.NewClip.name);
            }

            string head = _applyDirect
                ? DenEmoLoc.T("ui.fx.confirm.direct.head")
                : DenEmoLoc.T("ui.fx.confirm.duplicate.head");
            string msg = head + "\n\n" + string.Join("\n", lines.ToArray()) + "\n\n"
                + DenEmoLoc.Tf("ui.fx.confirm.slotCount", slotCount);

            if (!EditorUtility.DisplayDialog(DenEmoLoc.T("ui.fx.confirm.title"), msg,
                    DenEmoLoc.T("ui.fx.confirm.ok"), DenEmoLoc.T("dlg.cancel")))
                return;

            _hover.Stop();

            var result = _applyDirect
                ? FxMotionReplacer.ReplaceDirect(_controller, jobs, backup: true)
                : FxMotionReplacer.ReplaceWithDuplicate(_controller, jobs, _descriptor, _targetSmrPaths);

            _lastResult = result;

            if (result.Success)
            {
                _mappings.Clear();
                _expanded.Clear();

                // 複製モードで Descriptor 不在なら、以降は複製側を作業対象にする
                if (!_applyDirect && !result.DescriptorUpdated && !string.IsNullOrEmpty(result.NewControllerPath))
                {
                    var copy = AssetDatabase.LoadAssetAtPath<AnimatorController>(result.NewControllerPath);
                    if (copy != null) _manualController = copy;
                }

                Refresh(model);
                StatusSink?.Invoke(DenEmoLoc.T("status.fx.applied"), 1);
            }
            else
            {
                StatusSink?.Invoke(result.Error ?? "error", 3);
            }
            window.Repaint();
        }

        private void DrawResultCard()
        {
            if (_lastResult == null || !_lastResult.Success) return;

            GUILayout.Space(4);
            EditorGUILayout.BeginVertical(_infoBandStyle);

            GUILayout.Label(DenEmoLoc.Tf("ui.fx.result.success", _lastResult.ReplacedCount), _successCaptionStyle);

            if (!string.IsNullOrEmpty(_lastResult.NewControllerPath))
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(DenEmoLoc.Tf("ui.fx.result.newController", _lastResult.NewControllerPath), DenEmoTheme.CaptionStyle);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(DenEmoLoc.T("ui.fx.result.ping"), DenEmoTheme.MiniButtonStyle, GUILayout.Width(40), GUILayout.Height(16)))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<AnimatorController>(_lastResult.NewControllerPath);
                    if (asset != null) EditorGUIUtility.PingObject(asset);
                }
                EditorGUILayout.EndHorizontal();

                GUILayout.Label(
                    _lastResult.DescriptorUpdated
                        ? DenEmoLoc.T("ui.fx.result.descriptorSet")
                        : DenEmoLoc.T("ui.fx.result.manualSet"),
                    _lastResult.DescriptorUpdated ? DenEmoTheme.CaptionStyle : _warnCaptionStyle);
            }

            if (!string.IsNullOrEmpty(_lastResult.BackupPath))
                GUILayout.Label(DenEmoLoc.Tf("ui.fx.result.backup", _lastResult.BackupPath), DenEmoTheme.CaptionStyle);

            EditorGUILayout.EndVertical();
        }

        // ─── スタイル ─────────────────────────────────────────────────────────

        private void EnsureStyles()
        {
            if (_warnCaptionStyle == null)
            {
                _warnCaptionStyle = new GUIStyle(DenEmoTheme.CaptionStyle) { wordWrap = true };
                DenEmoTheme.FixAllTextColors(_warnCaptionStyle, DenEmoTheme.SemanticWarning);
            }
            if (_successCaptionStyle == null)
            {
                _successCaptionStyle = new GUIStyle(DenEmoTheme.SecondaryTextStyle);
                DenEmoTheme.FixAllTextColors(_successCaptionStyle, DenEmoTheme.SemanticSuccess);
            }
            if (_infoBandStyle == null || _infoBandStyle.normal.background == null)
            {
                _infoBandStyle = new GUIStyle();
                _infoBandStyle.normal.background = DenEmoTheme.MakeBorderedTex(
                    Color.Lerp(DenEmoTheme.Surface1, DenEmoTheme.SemanticInfo, 0.12f), DenEmoTheme.Outline);
                _infoBandStyle.border  = new RectOffset(1, 1, 1, 1);
                _infoBandStyle.padding = new RectOffset(8, 8, 4, 4);
                _infoBandStyle.margin  = new RectOffset(0, 0, 2, 2);
            }
        }
    }
}
