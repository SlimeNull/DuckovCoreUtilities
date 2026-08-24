using SlimeNull.DuckovModSettings.Core;
using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlimeNull.DuckovModSettings.UI
{
    internal sealed class ColorPickerDialog : MonoBehaviour
    {
        private readonly Slider[] _sliders = new Slider[4];
        private readonly TMP_InputField[] _inputs = new TMP_InputField[4];
        private SettingsPage? _page;
        private SettingNode? _node;
        private TMP_FontAsset? _font;
        private Image? _preview;
        private TMP_InputField? _hexInput;
        private bool _updating;

        public static void Show(SettingsPage page, SettingNode node, TMP_FontAsset? font)
        {
            var existing = page.transform.Find("Color Picker Overlay");
            if (existing != null)
            {
                existing.gameObject.SetActive(false);
                Destroy(existing.gameObject);
            }

            var overlay = UiFactory.Rect("Color Picker Overlay", page.transform);
            UiFactory.Stretch(overlay);
            overlay.SetAsLastSibling();
            var dialog = overlay.gameObject.AddComponent<ColorPickerDialog>();
            dialog.Initialize(page, node, font);
        }

        private void Initialize(SettingsPage page, SettingNode node, TMP_FontAsset? font)
        {
            _page = page;
            _node = node;
            _font = font;

            var blocker = UiFactory.AddImage(gameObject, new Color(0f, 0f, 0f, 0.72f));
            var blockerButton = gameObject.AddComponent<Button>();
            blockerButton.targetGraphic = blocker;
            blockerButton.onClick.AddListener(Close);

            var panel = UiFactory.Rect("Dialog", transform);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(560f, 510f);
            panel.anchoredPosition = Vector2.zero;
            UiFactory.AddImage(panel.gameObject, UiFactory.PanelBackground);
            var panelBlocker = panel.gameObject.AddComponent<Button>();
            panelBlocker.targetGraphic = panel.GetComponent<Image>();
            panelBlocker.transition = Selectable.Transition.None;

            var vertical = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            vertical.padding = new RectOffset(22, 22, 20, 20);
            vertical.spacing = 12f;
            vertical.childControlHeight = true;
            vertical.childControlWidth = true;
            vertical.childForceExpandHeight = false;
            vertical.childForceExpandWidth = true;

            var titleRow = UiFactory.Rect("Title", panel);
            var titleLayout = titleRow.gameObject.AddComponent<LayoutElement>();
            titleLayout.minHeight = 42f;
            titleLayout.preferredHeight = 42f;
            var titleHorizontal = titleRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            titleHorizontal.spacing = 8f;
            titleHorizontal.childControlHeight = true;
            titleHorizontal.childControlWidth = true;
            titleHorizontal.childForceExpandHeight = true;
            titleHorizontal.childForceExpandWidth = false;
            var title = UiFactory.Text("Label", titleRow, _font, node.DisplayName, 25f, UiFactory.TextPrimary);
            title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var close = UiFactory.Button("Close", titleRow, _font, "X", Close, 38f, UiFactory.RaisedBackground);
            var closeLayout = close.GetComponent<LayoutElement>();
            closeLayout.minWidth = 42f;
            closeLayout.preferredWidth = 42f;

            var previewRect = UiFactory.Rect("Preview", panel);
            _preview = UiFactory.AddImage(previewRect.gameObject, GetColor());
            var previewLayout = previewRect.gameObject.AddComponent<LayoutElement>();
            previewLayout.minHeight = 58f;
            previewLayout.preferredHeight = 58f;

            CreateChannel(panel, 0, "R");
            CreateChannel(panel, 1, "G");
            CreateChannel(panel, 2, "B");
            CreateChannel(panel, 3, "A");

            var hexRow = UiFactory.Rect("HEX", panel);
            var hexLayout = hexRow.gameObject.AddComponent<LayoutElement>();
            hexLayout.minHeight = 42f;
            hexLayout.preferredHeight = 42f;
            var hexHorizontal = hexRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            hexHorizontal.spacing = 10f;
            hexHorizontal.childControlHeight = true;
            hexHorizontal.childControlWidth = true;
            hexHorizontal.childForceExpandHeight = true;
            hexHorizontal.childForceExpandWidth = false;
            var hexLabel = UiFactory.Text("Label", hexRow, _font, "HEX", 18f, UiFactory.TextSecondary, TextAlignmentOptions.Center);
            var hexLabelLayout = hexLabel.gameObject.AddComponent<LayoutElement>();
            hexLabelLayout.minWidth = 42f;
            hexLabelLayout.preferredWidth = 42f;
            _hexInput = UiFactory.Input("Value", hexRow, _font, string.Empty, "#RRGGBBAA", 40f);
            _hexInput.GetComponent<LayoutElement>().flexibleWidth = 1f;
            _hexInput.onEndEdit.AddListener(SetHex);

            UiFactory.Button("Done", panel, _font, "完成", Close, 44f, new Color(0.12f, 0.36f, 0.27f, 1f));
            UpdateControls(GetColor());
        }

        private void CreateChannel(RectTransform parent, int index, string labelText)
        {
            var row = UiFactory.Rect(labelText, parent);
            var rowLayout = row.gameObject.AddComponent<LayoutElement>();
            rowLayout.minHeight = 38f;
            rowLayout.preferredHeight = 38f;
            var horizontal = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            horizontal.spacing = 10f;
            horizontal.childAlignment = TextAnchor.MiddleCenter;
            horizontal.childControlHeight = true;
            horizontal.childControlWidth = true;
            horizontal.childForceExpandHeight = true;
            horizontal.childForceExpandWidth = false;

            var label = UiFactory.Text("Label", row, _font, labelText, 18f, UiFactory.TextSecondary, TextAlignmentOptions.Center);
            var labelLayout = label.gameObject.AddComponent<LayoutElement>();
            labelLayout.minWidth = 42f;
            labelLayout.preferredWidth = 42f;

            _sliders[index] = UiFactory.Slider("Slider", row, 0f, 1f, 0f, wholeNumbers: false);
            _inputs[index] = UiFactory.Input("Value", row, _font, "0", string.Empty, 36f);
            var inputLayout = _inputs[index].GetComponent<LayoutElement>();
            inputLayout.minWidth = 76f;
            inputLayout.preferredWidth = 76f;
            inputLayout.flexibleWidth = 0f;

            _sliders[index].onValueChanged.AddListener(value => SetChannel(index, value));
            _inputs[index].onEndEdit.AddListener(text =>
            {
                if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    SetChannel(index, Mathf.Clamp01(value));
                }
                UpdateControls(GetColor());
            });
        }

        private void SetChannel(int index, float value)
        {
            if (_updating || _node == null)
            {
                return;
            }

            var color = GetColor();
            color[index] = Mathf.Clamp01(value);
            SetColor(color);
            UpdateControls(GetColor());
        }

        private void SetHex(string text)
        {
            if (_updating || _node == null)
            {
                return;
            }

            text = text.Trim();
            if (!text.StartsWith("#", StringComparison.Ordinal))
            {
                text = "#" + text;
            }
            if (ColorUtility.TryParseHtmlString(text, out var color))
            {
                if (text.Length == 7)
                {
                    color.a = GetColor().a;
                }
                SetColor(color);
            }
            UpdateControls(GetColor());
        }

        private void SetColor(Color color)
        {
            if (_node == null)
            {
                return;
            }
            object value = (Nullable.GetUnderlyingType(_node.ValueType) ?? _node.ValueType) == typeof(Color32)
                ? (object)(Color32)color
                : color;
            _node.TrySetValue(value, SettingChangeOrigin.User);
        }

        private Color GetColor()
        {
            return _node?.GetValue() switch
            {
                Color color => color,
                Color32 color32 => color32,
                _ => Color.white,
            };
        }

        private void UpdateControls(Color color)
        {
            _updating = true;
            try
            {
                for (var i = 0; i < 4; i++)
                {
                    _sliders[i]?.SetValueWithoutNotify(color[i]);
                    _inputs[i]?.SetTextWithoutNotify(color[i].ToString("0.###", CultureInfo.InvariantCulture));
                }
                if (_preview != null)
                {
                    _preview.color = color;
                }
                _hexInput?.SetTextWithoutNotify("#" + ColorUtility.ToHtmlStringRGBA(color));
            }
            finally
            {
                _updating = false;
            }
        }

        private void Close()
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
