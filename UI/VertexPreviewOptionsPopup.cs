using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DenEmo.UI
{
    internal class VertexPreviewOptionsPopup : PopupWindowContent
    {
        private readonly DenEmoWindow _window;

        internal VertexPreviewOptionsPopup(DenEmoWindow window)
        {
            _window = window;
        }

        public override Vector2 GetWindowSize() => new Vector2(300, 152);

        public override void OnOpen()
        {
            var root = editorWindow.rootVisualElement;
            DenEmoUiAssets.SetupRoot(root);

            var tree = DenEmoUiAssets.LoadVisualTree(DenEmoUiAssets.VertexPreviewPopupUxmlGuid);
            if (tree == null) return;
            tree.CloneTree(root);

            root.Q<Label>("title-label").text = DenEmoLoc.T("ui.vertexPreview.title");

            var normalField = root.Q<ColorField>("normal-color-field");
            normalField.label = DenEmoLoc.T("ui.vertexPreview.normalColor");
            normalField.value = DenEmoWindow.VertexPreviewColor;
            normalField.labelElement.style.minWidth = 90;
            normalField.RegisterValueChangedCallback(evt =>
            {
                DenEmoWindow.VertexPreviewColor = evt.newValue;
                OnSettingsChanged();
            });

            var selectedField = root.Q<ColorField>("selected-color-field");
            selectedField.label = DenEmoLoc.T("ui.vertexPreview.selectedColor");
            selectedField.value = DenEmoWindow.VertexPreviewSelectedColor;
            selectedField.labelElement.style.minWidth = 90;
            selectedField.RegisterValueChangedCallback(evt =>
            {
                DenEmoWindow.VertexPreviewSelectedColor = evt.newValue;
                OnSettingsChanged();
            });

            var sizeSlider = root.Q<Slider>("size-slider");
            sizeSlider.label = DenEmoLoc.T("ui.vertexPreview.size");
            sizeSlider.value = DenEmoWindow.VertexPreviewSizeMultiplier;
            sizeSlider.labelElement.style.minWidth = 90;
            sizeSlider.RegisterValueChangedCallback(evt =>
            {
                DenEmoWindow.VertexPreviewSizeMultiplier = evt.newValue;
                OnSettingsChanged();
            });
        }

        // 描画は OnOpen() で構築した UI Toolkit 要素が行うため IMGUI は空
        public override void OnGUI(Rect rect) { }

        private void OnSettingsChanged()
        {
            SavePrefs();
            SceneView.RepaintAll();
            _window?.Repaint();
        }

        private static void SavePrefs()
        {
            DenEmoProjectPrefs.SetString("DenEmo_VertexPreviewColor",
                DenEmoWindow.ColorToPrefsString(DenEmoWindow.VertexPreviewColor));
            DenEmoProjectPrefs.SetString("DenEmo_VertexPreviewSelectedColor",
                DenEmoWindow.ColorToPrefsString(DenEmoWindow.VertexPreviewSelectedColor));
            DenEmoProjectPrefs.SetString("DenEmo_VertexPreviewSize",
                DenEmoWindow.VertexPreviewSizeMultiplier.ToString("F4", System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}
