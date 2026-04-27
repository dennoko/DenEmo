using UnityEditor;
using UnityEngine;
using DenEmo.Models;

namespace DenEmo.UI
{
    public partial class ShapeKeyListUI
    {
        // ─── 行描画 ───────────────────────────────────────────────────────────

        private void DrawMergedRow(string baseName, ShapeKeyItem left, ShapeKeyItem right, bool spaceLeft, ShapeKeyModel model, AnimationDrawContext animContext)
        {
            var smr = left.OwnerSmr ?? model.TargetSkinnedMesh;

            EditorGUILayout.BeginHorizontal();
            if (spaceLeft) GUILayout.Space(20);

            DrawFavButton(left, model);

            bool bothInc = left.IsIncluded && right.IsIncluded;
            bool newInc = EditorGUILayout.Toggle(bothInc, GUILayout.Width(16));
            if (newInc != bothInc)
            {
                left.IsIncluded = newInc;
                right.IsIncluded = newInc;
                OnIncludeFlagsChanged?.Invoke();
            }

            float nameWidth = Mathf.Min(200f, EditorGUIUtility.currentViewWidth * 0.32f);
            EditorGUILayout.LabelField(baseName + "_LR", DenEmoTheme.SecondaryTextStyle, GUILayout.Width(nameWidth));

            if (GUILayout.Button(
                new GUIContent("0", DenEmoLoc.EnglishMode ? "Reset both L/R values to zero" : "左右の値を0にリセット"),
                DenEmoTheme.MiniButtonStyle, GUILayout.Width(20)))
            {
                if (animContext == null && smr != null)
                    Undo.RecordObject(smr, "Reset Shape Key");
                left.Value = 0f; right.Value = 0f;
                if (animContext != null)
                {
                    animContext.OnValueChanged?.Invoke(left, model, 0f);
                    animContext.OnValueChanged?.Invoke(right, model, 0f);
                }
                else if (smr != null)
                {
                    smr.SetBlendShapeWeight(left.Index, 0f);
                    smr.SetBlendShapeWeight(right.Index, 0f);
                }
            }

            float oldValue = left.Value;
            int sliderId = GUIUtility.GetControlID(FocusType.Passive);
            EditorGUI.BeginChangeCheck();
            float newValue = EditorGUILayout.Slider(oldValue, 0f, 100f);
            bool changed = EditorGUI.EndChangeCheck();
            bool isHot = GUIUtility.hotControl == sliderId || GUIUtility.hotControl == sliderId + 1;

            if (animContext == null && changed && isHot && (!isSliderDragging || currentDraggingIndex != left.Index))
            {
                if (smr != null) Undo.RecordObject(smr, "Change Shape Key");
                isSliderDragging = true;
                currentDraggingIndex = left.Index;
            }

            if (changed)
            {
                if (animContext != null)
                {
                    animContext.OnValueChanged?.Invoke(left, model, newValue);
                    animContext.OnValueChanged?.Invoke(right, model, newValue);
                }
                else
                {
                    if (!isHot && !isSliderDragging && smr != null)
                        Undo.RecordObject(smr, "Change Shape Key");
                    left.Value = newValue;
                    right.Value = newValue;
                    if (smr != null)
                    {
                        if (isHot || isSliderDragging) { QueuePendingApply(left, newValue); QueuePendingApply(right, newValue); }
                        else { smr.SetBlendShapeWeight(left.Index, newValue); smr.SetBlendShapeWeight(right.Index, newValue); }
                    }
                }
            }

            if (animContext == null && isSliderDragging && currentDraggingIndex == left.Index && !isHot)
            {
                isSliderDragging = false; currentDraggingIndex = -1; StopThrottle();
                if (smr != null)
                {
                    smr.SetBlendShapeWeight(left.Index, left.Value);
                    smr.SetBlendShapeWeight(right.Index, right.Value);
                }
            }

            if (animContext != null)
            {
                bool hasKey = animContext.HasKeyframeAtCurrentTime?.Invoke(left.Name) ?? false;
                string kfIcon = hasKey ? "◆" : "◇";
                string kfTip = hasKey
                    ? (DenEmoLoc.EnglishMode ? "Remove keyframe at current time" : "現在時刻のキーフレームを削除")
                    : (DenEmoLoc.EnglishMode ? "Add keyframe at current time" : "現在時刻にキーフレームを追加");
                var kfStyle = hasKey ? DenEmoTheme.FavOnStyle : DenEmoTheme.FavOffStyle;
                if (GUILayout.Button(new GUIContent(kfIcon, kfTip), kfStyle, GUILayout.Width(18)))
                {
                    float rightVal = right.Value;
                    animContext.OnKeyframeToggle?.Invoke(left, model);
                    right.Value = rightVal;
                    animContext.OnKeyframeToggle?.Invoke(right, model);
                }
            }

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(5f);
        }

        private void DrawSingleRow(ShapeKeyItem item, bool spaceLeft, ShapeKeyModel model, AnimationDrawContext animContext = null)
        {
            var smr = item.OwnerSmr ?? model.TargetSkinnedMesh;

            EditorGUILayout.BeginHorizontal();
            if (spaceLeft) GUILayout.Space(20);

            DrawFavButton(item, model);

            bool newInc = EditorGUILayout.Toggle(item.IsIncluded, GUILayout.Width(16));
            if (newInc != item.IsIncluded)
            {
                item.IsIncluded = newInc;
                OnIncludeFlagsChanged?.Invoke();
            }

            float nameWidth = Mathf.Min(200f, EditorGUIUtility.currentViewWidth * 0.32f);
            var nameStyle = item.IsIncluded ? DenEmoTheme.SecondaryTextStyle : DenEmoTheme.CaptionStyle;
            EditorGUILayout.LabelField(item.Name, nameStyle, GUILayout.Width(nameWidth));

            if (GUILayout.Button(
                new GUIContent("0", DenEmoLoc.EnglishMode ? "Reset value to zero" : "値を0にリセット"),
                DenEmoTheme.MiniButtonStyle, GUILayout.Width(20)))
            {
                if (animContext != null)
                {
                    animContext.OnValueChanged?.Invoke(item, model, 0f);
                }
                else if (item.Value != 0f)
                {
                    if (smr != null) Undo.RecordObject(smr, "Reset Shape Key");
                    item.Value = 0f;
                    if (smr != null) smr.SetBlendShapeWeight(item.Index, 0f);
                }
            }

            float oldValue = item.Value;
            int sliderId = GUIUtility.GetControlID(FocusType.Passive);
            EditorGUI.BeginChangeCheck();
            float newValue = EditorGUILayout.Slider(oldValue, 0f, 100f);
            bool changed = EditorGUI.EndChangeCheck();
            bool isHot = GUIUtility.hotControl == sliderId || GUIUtility.hotControl == sliderId + 1;

            if (animContext == null && changed && isHot && (!isSliderDragging || currentDraggingIndex != item.Index))
            {
                if (smr != null) Undo.RecordObject(smr, "Change Shape Key");
                isSliderDragging = true;
                currentDraggingIndex = item.Index;
            }

            if (changed)
            {
                if (animContext != null)
                {
                    animContext.OnValueChanged?.Invoke(item, model, newValue);
                }
                else
                {
                    if (!isHot && !isSliderDragging && smr != null)
                        Undo.RecordObject(smr, "Change Shape Key");
                    item.Value = newValue;
                    if (smr != null)
                    {
                        if (isHot || isSliderDragging) QueuePendingApply(item, newValue);
                        else smr.SetBlendShapeWeight(item.Index, newValue);
                    }
                }
            }

            if (animContext == null && isSliderDragging && currentDraggingIndex == item.Index && !isHot)
            {
                isSliderDragging = false; currentDraggingIndex = -1; StopThrottle();
                if (smr != null) smr.SetBlendShapeWeight(item.Index, item.Value);
            }

            if (animContext != null)
            {
                bool hasKey = animContext.HasKeyframeAtCurrentTime?.Invoke(item.Name) ?? false;
                string kfIcon = hasKey ? "◆" : "◇";
                string kfTip = hasKey
                    ? (DenEmoLoc.EnglishMode ? "Remove keyframe at current time" : "現在時刻のキーフレームを削除")
                    : (DenEmoLoc.EnglishMode ? "Add keyframe at current time" : "現在時刻にキーフレームを追加");
                var kfStyle = hasKey ? DenEmoTheme.FavOnStyle : DenEmoTheme.FavOffStyle;
                if (GUILayout.Button(new GUIContent(kfIcon, kfTip), kfStyle, GUILayout.Width(18)))
                    animContext.OnKeyframeToggle?.Invoke(item, model);
            }

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(5f);
        }

        private void DrawFavButton(ShapeKeyItem item, ShapeKeyModel model)
        {
            var style = item.IsFavorite ? DenEmoTheme.FavOnStyle : DenEmoTheme.FavOffStyle;
            string icon = item.IsFavorite ? "★" : "☆";
            string tip = item.IsFavorite
                ? (DenEmoLoc.EnglishMode ? "Remove from favorites" : "お気に入り解除")
                : (DenEmoLoc.EnglishMode ? "Add to favorites" : "お気に入り追加");
            if (GUILayout.Button(new GUIContent(icon, tip), style, GUILayout.Width(18), GUILayout.Height(18)))
            {
                item.IsFavorite = !item.IsFavorite;
                OnFavoriteChanged?.Invoke(item.Name, item.IsFavorite);
            }
        }
    }
}
