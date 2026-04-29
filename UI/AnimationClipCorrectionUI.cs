using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using DenEmo.Models;
using DenEmo.Core;

namespace DenEmo.UI
{
    /// <summary>
    /// Collapsible section that remaps per-shape blend-shape value ranges
    /// in the current animation clip.
    ///
    /// Formula applied to every keyframe value v of a shape:
    ///   new_v = v * (max - min) / 100 + min
    ///
    /// Keeping max=100 / min=0 (defaults) leaves the shape unchanged.
    /// Setting max=80 scales all values into [0, 80].
    /// Setting min=20 scales all values into [20, 100].
    /// </summary>
    public class AnimationClipCorrectionUI
    {
        private bool _expanded = false;
        private readonly Dictionary<string, float> _minValues = new Dictionary<string, float>();
        private readonly Dictionary<string, float> _maxValues = new Dictionary<string, float>();
        private Vector2 _scroll;

        // ─── Public draw entry point ──────────────────────────────────────────

        public void Draw(
            AnimationClipModel clipModel,
            AnimationPreviewController preview,
            string smrPath,
            Action<string, int> setStatus,
            EditorWindow window)
        {
            if (clipModel.Clip == null) return;

            GUILayout.BeginVertical(DenEmoTheme.CardStyle);

            // ── Collapsible header ────────────────────────────────────────────
            string title = DenEmoLoc.EnglishMode
                ? "SHAPE KEY VALUE CORRECTION"
                : "シェイプキー値補正";
            var headerStyle = _expanded
                ? DenEmoTheme.ToggleSectionOnStyle
                : DenEmoTheme.ToggleSectionOffStyle;
            string arrow = _expanded ? "▼  " : "▶  ";

            var headerRect = GUILayoutUtility.GetRect(
                new GUIContent(arrow + title), headerStyle,
                GUILayout.ExpandWidth(true));
            GUI.Label(headerRect, arrow + title, headerStyle);

            if (Event.current.type == EventType.MouseDown
                && headerRect.Contains(Event.current.mousePosition))
            {
                _expanded = !_expanded;
                Event.current.Use();
                window.Repaint();
            }

            // ── Content (only when expanded) ──────────────────────────────────
            if (_expanded)
            {
                DenEmoTheme.DrawSeparator(4);
                DrawContent(clipModel, preview, smrPath, setStatus, window);
            }

            GUILayout.EndVertical();
        }

        // ─── Content ──────────────────────────────────────────────────────────

        private void DrawContent(
            AnimationClipModel clipModel,
            AnimationPreviewController preview,
            string smrPath,
            Action<string, int> setStatus,
            EditorWindow window)
        {
            var shapeNames = clipModel.GetShapeNamesWithKeys(smrPath);

            if (shapeNames.Count == 0)
            {
                GUILayout.Label(
                    DenEmoLoc.EnglishMode
                        ? "No shape keys with keyframes found in this clip."
                        : "このクリップにキーフレームのあるシェイプキーがありません。",
                    DenEmoTheme.CaptionStyle);
                return;
            }

            // Section description
            GUILayout.Label(
                DenEmoLoc.EnglishMode
                    ? "Rescale keyframe values of individual shape keys across the entire clip.\nUseful when an expression edit makes a shape key's full range look broken (e.g. blink conflicts in VRChat)."
                    : "クリップ全体の各シェイプキーのキーフレーム値を再スケールします。\n表情改変によりシェイプキーの最大値が破綻する場合（VRChat のまばたき競合など）に使用してください。",
                DenEmoTheme.CaptionStyle);
            GUILayout.Space(4);

            // Column headers
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(
                DenEmoLoc.EnglishMode ? "Shape Key" : "シェイプキー",
                DenEmoTheme.CaptionStyle, GUILayout.ExpandWidth(true));
            GUILayout.Label(
                new GUIContent(
                    DenEmoLoc.EnglishMode ? "Min (0–100)" : "最小値 (0–100)",
                    DenEmoLoc.EnglishMode
                        ? "Lower bound after correction (default 0).\nWhen above 0, the original value 0 is raised to this value while the original 100 maps to the Max setting.\nExample: Min=20 prevents the shape key from fully returning to neutral."
                        : "補正後の下限値（デフォルト 0）。\n0 より大きい値にすると、元の値 0 がこの値になるようリスケールされます（元の値 100 は最大値の設定値に変わります）。\n例：Min=20 にすると、シェイプキーが完全にニュートラルに戻ることを防げます。"),
                DenEmoTheme.CaptionStyle, GUILayout.Width(90));
            GUILayout.Label(
                new GUIContent(
                    DenEmoLoc.EnglishMode ? "Max (0–100)" : "最大値 (0–100)",
                    DenEmoLoc.EnglishMode
                        ? "Upper bound after correction (default 100).\nWhen below 100, the original value 100 is lowered to this value while the original 0 maps to the Min setting.\nExample: Max=80 on a blink shape prevents the eye from fully closing in this animation."
                        : "補正後の上限値（デフォルト 100）。\n100 未満の値にすると、元の値 100 がこの値になるようリスケールされます（元の値 0 は最小値の設定値に変わります）。\n例：まばたきシェイプキーの Max=80 にすると、このアニメーション内で目が完全に閉じないように制限できます。"),
                DenEmoTheme.CaptionStyle, GUILayout.Width(90));
            EditorGUILayout.EndHorizontal();

            DenEmoTheme.DrawSeparator(2);

            // Scrollable shape key list
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(300), GUILayout.MaxHeight(600));

            foreach (var name in shapeNames)
            {
                if (!_minValues.TryGetValue(name, out float min)) min = 0f;
                if (!_maxValues.TryGetValue(name, out float max)) max = 100f;

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(name, DenEmoTheme.CaptionStyle, GUILayout.ExpandWidth(true));

                EditorGUI.BeginChangeCheck();
                float newMin = EditorGUILayout.FloatField(min, GUILayout.Width(90));
                float newMax = EditorGUILayout.FloatField(max, GUILayout.Width(90));
                if (EditorGUI.EndChangeCheck())
                {
                    _minValues[name] = Mathf.Clamp(newMin, 0f, 100f);
                    _maxValues[name] = Mathf.Clamp(newMax, 0f, 100f);
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            GUILayout.Space(4);
            DenEmoTheme.DrawSeparator(4);

            // Apply button
            GUILayout.BeginHorizontal();
            GUILayout.Space(6);
            if (GUILayout.Button(
                DenEmoLoc.EnglishMode ? "Apply Correction" : "補正を反映",
                DenEmoTheme.ActionButtonStyle, GUILayout.ExpandWidth(true)))
            {
                ApplyCorrection(clipModel, preview, smrPath, setStatus);
                window.Repaint();
            }
            GUILayout.Space(6);
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
        }

        // ─── Correction logic ─────────────────────────────────────────────────

        private void ApplyCorrection(
            AnimationClipModel clipModel,
            AnimationPreviewController preview,
            string smrPath,
            Action<string, int> setStatus)
        {
            if (clipModel.Clip == null) return;

            Undo.RecordObject(clipModel.Clip, "Apply Shape Key Correction");
            bool changed = false;

            foreach (var binding in AnimationUtility.GetCurveBindings(clipModel.Clip))
            {
                if (binding.type != typeof(SkinnedMeshRenderer)) continue;
                if (!binding.propertyName.StartsWith("blendShape.")) continue;
                if (smrPath != null && binding.path != smrPath) continue;

                string shapeName = binding.propertyName.Substring("blendShape.".Length);

                float min = _minValues.TryGetValue(shapeName, out float storedMin) ? storedMin : 0f;
                float max = _maxValues.TryGetValue(shapeName, out float storedMax) ? storedMax : 100f;

                if (Mathf.Approximately(min, 0f) && Mathf.Approximately(max, 100f)) continue;

                var curve = AnimationUtility.GetEditorCurve(clipModel.Clip, binding);
                if (curve == null) continue;

                float scale  = (max - min) / 100f;
                float offset = min;

                var keys = curve.keys;
                for (int i = 0; i < keys.Length; i++)
                {
                    float newVal = Mathf.Clamp(keys[i].value * scale + offset, 0f, 100f);
                    var newKey = new Keyframe(
                        keys[i].time,
                        newVal,
                        keys[i].inTangent  * scale,
                        keys[i].outTangent * scale);
                    newKey.weightedMode = keys[i].weightedMode;
                    newKey.inWeight     = keys[i].inWeight;
                    newKey.outWeight    = keys[i].outWeight;
                    keys[i] = newKey;
                }

                curve.keys = keys;
                AnimationUtility.SetEditorCurve(clipModel.Clip, binding, curve);
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(clipModel.Clip);
                preview.SetCacheDirty();
                if (preview.IsActive)
                    preview.SampleAt(clipModel.CurrentTime);

                setStatus?.Invoke(
                    DenEmoLoc.EnglishMode
                        ? "Correction applied."
                        : "補正を反映しました。",
                    1);
            }
            else
            {
                setStatus?.Invoke(
                    DenEmoLoc.EnglishMode
                        ? "No corrections to apply (all values are at their defaults)."
                        : "補正対象がありません（全て既定値です）。",
                    0);
            }
        }
    }
}
