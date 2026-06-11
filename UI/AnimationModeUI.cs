using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
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
        public AnimationTimelineUI        TimelineUI   { get; } = new AnimationTimelineUI();
        public AnimationClipCorrectionUI  CorrectionUI { get; } = new AnimationClipCorrectionUI();

        public AnimationModeUI()
        {
            Editor = new AnimationClipEditor(ClipModel);
        }

        // ─── State ────────────────────────────────────────────────────────────

        public bool              IsRecording        { get; set; }
        public InterpolationType CurrentInterp      { get; set; } = InterpolationType.Ease;
        public bool              TrackFilterEnabled { get; set; }

        // 再利用する描画コンテキストとトラック名セット
        private AnimationDrawContext _drawContext;
        private readonly HashSet<string> _trackNames = new HashSet<string>();
        private int _trackNamesRevision = -1;

        // Cached styles
        private GUIStyle _recBannerStyle;
        private GUIStyle _workflowGuideStyle;

        // ─── Lifecycle ────────────────────────────────────────────────────────

        public void OnEnable(ShapeKeyModel shapeModel)
        {
            if (ClipModel.Clip != null && shapeModel.TargetSkinnedMesh != null)
                StartPreview(shapeModel);
        }

        public void OnDisable()
        {
            Playback.Stop();
            IsRecording = false;
            Preview.Stop();
        }

        /// <summary>EditorApplication.update から呼ぶ。再生を進める。</summary>
        public void OnUpdate(EditorWindow window)
        {
            Playback.Tick(ClipModel, Preview, window);
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
            Playback.Stop();
            Preview.Stop();
        }

        public void OnTargetChanged(ShapeKeyModel shapeModel)
        {
            Preview.Stop();
            if (shapeModel?.TargetSkinnedMesh != null)
            {
                ClipModel.SmrPath = ShapeKeyModel.ComputeSmrPath(shapeModel.TargetSkinnedMesh);
                if (ClipModel.Clip != null)
                    StartPreview(shapeModel);
            }
        }

        // ─── Draw: Clip settings ──────────────────────────────────────────────

        public void DrawAnimationClipSection(ShapeKeyModel shapeModel, string saveFolder, EditorWindow window)
        {
            DenEmoTheme.BeginSection(DenEmoLoc.EnglishMode ? "ANIMATION CLIP" : "アニメーションクリップ");

            // ── Clip field + New button ──────────────────────────────────────
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(
                DenEmoLoc.T("ui.animMode.clip.label"),
                DenEmoTheme.CaptionStyle, GUILayout.Width(46));

            EditorGUI.BeginChangeCheck();
            var newClip = EditorGUILayout.ObjectField(ClipModel.Clip, typeof(AnimationClip), false) as AnimationClip;
            if (EditorGUI.EndChangeCheck())
            {
                StopPreview();
                ClipModel.SmoothLoopEnabled = false;
                ClipModel.SetClip(newClip);
                if (newClip != null && shapeModel.TargetSkinnedMesh != null)
                    StartPreview(shapeModel);
                window.Repaint();
            }

            if (GUILayout.Button(DenEmoLoc.T("ui.animMode.clip.new"), DenEmoTheme.MiniButtonStyle, GUILayout.Width(48)))
                CreateNewClip(shapeModel, saveFolder, window);

            EditorGUILayout.EndHorizontal();

            if (ClipModel.Clip == null)
            {
                GUILayout.Space(4);
                EnsureWorkflowGuideStyle();
                GUILayout.Label(DenEmoLoc.T("ui.animMode.guide.step1"), _workflowGuideStyle);
                GUILayout.Space(2);
                GUILayout.Label(DenEmoLoc.T("ui.animMode.guide.step2"), _workflowGuideStyle);
                GUILayout.Space(2);
                GUILayout.Label(DenEmoLoc.T("ui.animMode.guide.step3"), _workflowGuideStyle);
                GUILayout.Space(4);
            }

            DenEmoTheme.EndSection();
        }

        // ─── Draw: Clip correction ────────────────────────────────────────────

        public void DrawClipCorrectionSection(Action<string, int> setStatus, EditorWindow window)
        {
            CorrectionUI.Draw(ClipModel, Editor, Preview, setStatus, window);
        }

        // ─── Draw: Recording banner ───────────────────────────────────────────

        /// <summary>
        /// REC モード中はスライダー操作で自動的にキーが打たれることを示すバナーを表示する。
        /// </summary>
        public void DrawRecordingBanner()
        {
            if (!IsRecording || ClipModel.Clip == null) return;
            EnsureRecBannerStyle();
            GUILayout.BeginVertical(DenEmoTheme.CardStyle);
            GUILayout.Label(DenEmoLoc.T("ui.animMode.rec.banner"), _recBannerStyle);
            GUILayout.EndVertical();
        }

        // ─── Draw: Timeline ───────────────────────────────────────────────────

        public void DrawTimeline(ShapeKeyModel shapeModel, EditorWindow window)
        {
            if (ClipModel.Clip == null) return;
            TimelineUI.Draw(this, shapeModel, window);
        }

        // ─── Animation draw context ───────────────────────────────────────────

        public AnimationDrawContext BuildDrawContext(ShapeKeyModel shapeModel)
        {
            if (_drawContext == null)
            {
                _drawContext = new AnimationDrawContext
                {
                    OnToggleTrackFilter = () => TrackFilterEnabled = !TrackFilterEnabled,

                    HasKeyframeAtCurrentTime = shapeName =>
                        ClipModel.HasKeyframeAt(shapeName, ClipModel.CurrentTime),

                    OnValueChanged = (item, model, newValue) =>
                    {
                        item.Value = newValue;
                        if (ClipModel.Clip != null &&
                            (IsRecording || ClipModel.HasKeyframeAt(item.Name, ClipModel.CurrentTime)))
                        {
                            // 録画中、または既存キー上でのスライダー操作 → キーを直接更新
                            Editor.RecordKey(item.Name, ClipModel.CurrentTime, newValue, CurrentInterp);
                            Preview.SampleAt(ClipModel.CurrentTime);
                        }
                        else
                        {
                            if (model.TargetSkinnedMesh != null)
                                model.TargetSkinnedMesh.SetBlendShapeWeight(item.Index, newValue);
                        }
                    },

                    OnKeyframeToggle = (item, model) =>
                    {
                        if (ClipModel.Clip == null) return;
                        if (ClipModel.HasKeyframeAt(item.Name, ClipModel.CurrentTime))
                        {
                            Editor.DeleteKey(item.Name, ClipModel.CurrentTime);
                        }
                        else
                        {
                            Editor.RecordKey(item.Name, ClipModel.CurrentTime, item.Value, CurrentInterp);
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

        private void EnsureRecBannerStyle()
        {
            if (_recBannerStyle == null)
            {
                _recBannerStyle = new GUIStyle(DenEmoTheme.CaptionStyle)
                {
                    wordWrap = true,
                    fontSize = 11,
                };
                DenEmoTheme.FixAllTextColors(_recBannerStyle, new Color(1f, 0.45f, 0.45f));
            }
        }

        private void EnsureWorkflowGuideStyle()
        {
            if (_workflowGuideStyle == null)
            {
                _workflowGuideStyle = new GUIStyle(DenEmoTheme.CaptionStyle)
                {
                    wordWrap = true,
                };
            }
        }
    }
}
