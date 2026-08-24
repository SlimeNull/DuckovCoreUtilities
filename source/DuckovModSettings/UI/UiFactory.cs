using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.UI.ProceduralImage;

namespace SlimeNull.DuckovModSettings.UI
{
    internal static class UiFactory
    {
        public const float PanelRadius = 9f;
        public const float ControlRadius = 7f;
        public const float SmallRadius = 5f;

        public static readonly Color PageBackground = new Color32(4, 24, 38, 238);
        public static readonly Color PanelBackground = new Color32(17, 55, 73, 248);
        public static readonly Color TreeBackground = new Color32(8, 31, 44, 242);
        public static readonly Color RaisedBackground = new Color32(66, 166, 216, 255);
        public static readonly Color GroupBackground = new Color32(39, 105, 139, 255);
        public static readonly Color SelectedBackground = new Color32(87, 195, 247, 255);
        public static readonly Color InputBackground = new Color32(224, 240, 247, 255);
        public static readonly Color SliderTrack = new Color32(43, 113, 148, 255);
        public static readonly Color Accent = new Color32(81, 190, 244, 255);
        public static readonly Color SecondaryAccent = new Color32(255, 174, 99, 255);
        public static readonly Color TextPrimary = new Color32(250, 253, 255, 255);
        public static readonly Color TextSecondary = new Color32(184, 215, 228, 255);
        public static readonly Color InputText = new Color32(48, 65, 74, 255);
        public static readonly Color Divider = new Color32(192, 228, 243, 80);

        public static RectTransform Rect(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        public static void Stretch(RectTransform rect, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        public static Image AddImage(GameObject target, Color color, float cornerRadius = ControlRadius)
        {
            var image = target.GetComponent<Image>();
            if (image == null)
            {
                var procedural = target.AddComponent<ProceduralImage>();
                procedural.ModifierType = typeof(UniformModifier);
                image = procedural;
            }
            if (image is ProceduralImage proceduralImage)
            {
                proceduralImage.ModifierType = typeof(UniformModifier);
                var modifier = target.GetComponent<UniformModifier>();
                if (modifier != null)
                {
                    modifier.Radius = Mathf.Max(0f, cornerRadius);
                }
            }
            image.color = color;
            return image;
        }

        public static TextMeshProUGUI Text(
            string name,
            Transform parent,
            TMP_FontAsset? font,
            string text,
            float size,
            Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
        {
            var rect = Rect(name, parent);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            return label;
        }

        public static Button Button(
            string name,
            Transform parent,
            TMP_FontAsset? font,
            string label,
            UnityAction onClick,
            float height = 42f,
            Color? background = null,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center,
            Color? foreground = null)
        {
            var rect = Rect(name, parent);
            rect.gameObject.SetActive(false);
            var image = AddImage(rect.gameObject, Color.white, ControlRadius);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = ButtonColors(background ?? RaisedBackground);
            button.onClick.AddListener(onClick);

            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;

            var text = Text("Label", rect, font, label, 20f, foreground ?? TextPrimary, alignment);
            Stretch(text.rectTransform, 12f, 2f, 12f, 2f);
            rect.gameObject.SetActive(true);
            return button;
        }

        public static TMP_InputField Input(
            string name,
            Transform parent,
            TMP_FontAsset? font,
            string value,
            string placeholder,
            float height = 40f,
            bool multiline = false)
        {
            var root = Rect(name, parent);
            root.gameObject.SetActive(false);
            var background = AddImage(root.gameObject, InputBackground, ControlRadius);
            var field = root.gameObject.AddComponent<TMP_InputField>();
            field.targetGraphic = background;
            field.lineType = multiline ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine;
            field.richText = false;
            field.customCaretColor = true;
            field.caretColor = InputText;
            field.selectionColor = new Color(Accent.r, Accent.g, Accent.b, 0.45f);

            var layout = root.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;

            var viewport = Rect("Text Area", root);
            Stretch(viewport, 12f, 5f, 12f, 5f);
            var mask = viewport.gameObject.AddComponent<RectMask2D>();
            mask.padding = Vector4.zero;

            var text = Text("Text", viewport, font, value, 18f, InputText);
            Stretch(text.rectTransform);
            text.enableWordWrapping = multiline;
            text.overflowMode = multiline ? TextOverflowModes.Masking : TextOverflowModes.Ellipsis;

            var hint = Text("Placeholder", viewport, font, placeholder, 18f, new Color32(92, 116, 127, 255));
            Stretch(hint.rectTransform);
            hint.fontStyle = FontStyles.Italic;

            field.textViewport = viewport;
            field.textComponent = text;
            field.placeholder = hint;
            field.text = value;
            root.gameObject.SetActive(true);
            return field;
        }

        public static Toggle Toggle(string name, Transform parent, bool value, UnityAction<bool> onChanged)
        {
            var root = Rect(name, parent);
            var layout = root.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = 30f;
            layout.preferredWidth = 30f;
            layout.minHeight = 30f;
            layout.preferredHeight = 30f;

            var backgroundRect = Rect("Background", root);
            Stretch(backgroundRect, 2f, 2f, 2f, 2f);
            var background = AddImage(backgroundRect.gameObject, InputBackground, SmallRadius);
            var checkRect = Rect("Checkmark", backgroundRect);
            Stretch(checkRect, 6f, 6f, 6f, 6f);
            var check = AddImage(checkRect.gameObject, new Color32(22, 128, 160, 255), 3f);

            var toggle = root.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = check;
            toggle.isOn = value;
            toggle.onValueChanged.AddListener(onChanged);
            return toggle;
        }

        public static Slider Slider(string name, Transform parent, float minimum, float maximum, float value, bool wholeNumbers)
        {
            var root = Rect(name, parent);
            var slider = root.gameObject.AddComponent<Slider>();
            var layout = root.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 36f;
            layout.preferredHeight = 36f;
            layout.flexibleWidth = 1f;

            var backgroundRect = Rect("Background", root);
            backgroundRect.anchorMin = new Vector2(0f, 0.5f);
            backgroundRect.anchorMax = new Vector2(1f, 0.5f);
            backgroundRect.sizeDelta = new Vector2(0f, 6f);
            AddImage(backgroundRect.gameObject, SliderTrack, 3f);

            var fillArea = Rect("Fill Area", root);
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.offsetMin = new Vector2(4f, -3f);
            fillArea.offsetMax = new Vector2(-4f, 3f);
            var fill = Rect("Fill", fillArea);
            Stretch(fill);
            AddImage(fill.gameObject, SecondaryAccent, 3f);

            var handleArea = Rect("Handle Slide Area", root);
            handleArea.anchorMin = new Vector2(0f, 0.5f);
            handleArea.anchorMax = new Vector2(1f, 0.5f);
            handleArea.offsetMin = new Vector2(8f, -8f);
            handleArea.offsetMax = new Vector2(-8f, 8f);
            var handle = Rect("Handle", handleArea);
            handle.anchorMin = new Vector2(0f, 0f);
            handle.anchorMax = new Vector2(0f, 1f);
            handle.pivot = new Vector2(0.5f, 0.5f);
            handle.sizeDelta = new Vector2(16f, 8f);
            var handleImage = AddImage(handle.gameObject, InputBackground, 8f);

            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = minimum;
            slider.maxValue = maximum;
            slider.wholeNumbers = wholeNumbers;
            slider.value = value;
            return slider;
        }

        public static TMP_Dropdown Dropdown(string name, Transform parent, TMP_FontAsset? font, string[] options, int selected)
        {
            var root = Rect(name, parent);
            var image = AddImage(root.gameObject, InputBackground, ControlRadius);
            var dropdown = root.gameObject.AddComponent<TMP_Dropdown>();
            dropdown.targetGraphic = image;
            var layout = root.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 40f;
            layout.preferredHeight = 40f;

            var label = Text("Label", root, font, string.Empty, 18f, InputText);
            Stretch(label.rectTransform, 12f, 2f, 34f, 2f);
            var arrow = Text("Arrow", root, font, "v", 16f, InputText, TextAlignmentOptions.Center);
            arrow.rectTransform.anchorMin = new Vector2(1f, 0f);
            arrow.rectTransform.anchorMax = new Vector2(1f, 1f);
            arrow.rectTransform.pivot = new Vector2(1f, 0.5f);
            arrow.rectTransform.sizeDelta = new Vector2(32f, 0f);
            arrow.rectTransform.anchoredPosition = Vector2.zero;

            var template = CreateDropdownTemplate(root, font, out var itemText);
            dropdown.template = template;
            dropdown.captionText = label;
            dropdown.itemText = itemText;
            dropdown.ClearOptions();
            dropdown.AddOptions(new System.Collections.Generic.List<string>(options));
            dropdown.value = Mathf.Clamp(selected, 0, Math.Max(0, options.Length - 1));
            dropdown.RefreshShownValue();
            return dropdown;
        }

        public static ScrollRect ScrollView(string name, Transform parent, out RectTransform content)
        {
            var root = Rect(name, parent);
            AddImage(root.gameObject, TreeBackground, PanelRadius);
            var layout = root.gameObject.AddComponent<LayoutElement>();
            layout.flexibleHeight = 1f;
            layout.flexibleWidth = 1f;

            var viewport = Rect("Viewport", root);
            Stretch(viewport, 2f, 2f, 2f, 2f);
            AddImage(viewport.gameObject, new Color(0f, 0f, 0f, 0.01f), PanelRadius - 2f);
            viewport.gameObject.AddComponent<RectMask2D>();

            content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            var vertical = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vertical.padding = new RectOffset(8, 8, 8, 8);
            vertical.spacing = 6f;
            vertical.childControlHeight = true;
            vertical.childControlWidth = true;
            vertical.childForceExpandHeight = false;
            vertical.childForceExpandWidth = true;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 1f;
            return scroll;
        }

        public static ColorBlock ButtonColors(Color normal)
        {
            return new ColorBlock
            {
                normalColor = normal,
                highlightedColor = Color.Lerp(normal, Color.white, 0.10f),
                pressedColor = Color.Lerp(normal, Color.black, 0.18f),
                selectedColor = Color.Lerp(normal, Accent, 0.08f),
                disabledColor = new Color(normal.r, normal.g, normal.b, 0.45f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f,
            };
        }

        private static RectTransform CreateDropdownTemplate(RectTransform root, TMP_FontAsset? font, out TextMeshProUGUI itemText)
        {
            var template = Rect("Template", root);
            template.anchorMin = new Vector2(0f, 0f);
            template.anchorMax = new Vector2(1f, 0f);
            template.pivot = new Vector2(0.5f, 1f);
            template.anchoredPosition = new Vector2(0f, -2f);
            template.sizeDelta = new Vector2(0f, 220f);
            AddImage(template.gameObject, InputBackground, ControlRadius);

            var scrollRect = template.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            var viewport = Rect("Viewport", template);
            Stretch(viewport, 2f, 2f, 2f, 2f);
            AddImage(viewport.gameObject, new Color(0f, 0f, 0f, 0.01f), ControlRadius - 1f);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            var content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, 34f);

            var item = Rect("Item", content);
            item.anchorMin = new Vector2(0f, 0.5f);
            item.anchorMax = new Vector2(1f, 0.5f);
            item.sizeDelta = new Vector2(0f, 34f);
            var itemBackground = AddImage(item.gameObject, new Color32(205, 230, 240, 255), SmallRadius);
            var toggle = item.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = itemBackground;

            var check = Rect("Item Checkmark", item);
            check.anchorMin = new Vector2(0f, 0.5f);
            check.anchorMax = new Vector2(0f, 0.5f);
            check.sizeDelta = new Vector2(8f, 22f);
            check.anchoredPosition = new Vector2(7f, 0f);
            var checkImage = AddImage(check.gameObject, Accent);
            toggle.graphic = checkImage;

            itemText = Text("Item Label", item, font, "Option", 17f, InputText);
            Stretch(itemText.rectTransform, 20f, 1f, 8f, 1f);

            scrollRect.viewport = viewport;
            scrollRect.content = content;
            template.gameObject.SetActive(false);
            return template;
        }
    }
}
