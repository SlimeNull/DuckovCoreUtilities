using SlimeNull.DuckovModSettings.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SlimeNull.DuckovModSettings.UI
{
    internal sealed class SettingsPage : MonoBehaviour
    {
        private readonly Dictionary<string, bool> _foldoutStates = new Dictionary<string, bool>(StringComparer.Ordinal);
        private SettingsCatalog? _catalog;
        private Action? _onPageClosing;
        private TMP_FontAsset? _font;
        private RectTransform? _navigationContent;
        private RectTransform? _settingsContent;
        private TextMeshProUGUI? _title;
        private TextMeshProUGUI? _emptyState;
        private TextMeshProUGUI? _tooltip;
        private TMP_InputField? _search;
        private Button? _resetButton;
        private ScrollRect? _settingsScroll;
        private ModSettingsModel? _selectedMod;
        private bool _initialized;
        private bool _wasVisible;
        private bool _rebuildRequested;

        public void Initialize(SettingsCatalog catalog, Action onPageClosing, TMP_FontAsset? font)
        {
            _catalog = catalog;
            _onPageClosing = onPageClosing;
            _font = font ?? Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault();
            BuildShell();
            _catalog.StructureChanged += OnStructureChanged;
            _catalog.ValueChanged += OnValueChanged;
            RebuildNavigation();
            _initialized = true;
        }

        public void CommitPendingChanges()
        {
            _onPageClosing?.Invoke();
        }

        public void ShowTooltip(string text)
        {
            if (_tooltip == null)
            {
                return;
            }
            _tooltip.text = text;
            _tooltip.gameObject.SetActive(!string.IsNullOrWhiteSpace(text));
        }

        private void OnEnable()
        {
            if (!_initialized)
            {
                return;
            }
            _wasVisible = true;
            RebuildNavigation();
            RebuildSettings();
        }

        private void OnDisable()
        {
            if (_initialized && _wasVisible)
            {
                _wasVisible = false;
                CommitPendingChanges();
            }
        }

        private void OnDestroy()
        {
            if (_catalog != null)
            {
                _catalog.StructureChanged -= OnStructureChanged;
                _catalog.ValueChanged -= OnValueChanged;
            }
        }

        private void Update()
        {
            if (_rebuildRequested && gameObject.activeInHierarchy)
            {
                _rebuildRequested = false;
                RebuildSettings();
            }
        }

        private void BuildShell()
        {
            UiFactory.AddImage(gameObject, UiFactory.PageBackground);
            var root = UiFactory.Rect("Layout", transform);
            UiFactory.Stretch(root, 16f, 16f, 16f, 16f);
            var horizontal = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            horizontal.spacing = 14f;
            horizontal.childControlHeight = true;
            horizontal.childControlWidth = true;
            horizontal.childForceExpandHeight = true;
            horizontal.childForceExpandWidth = false;

            var navigation = UiFactory.Rect("Mod Navigation", root);
            UiFactory.AddImage(navigation.gameObject, UiFactory.PanelBackground);
            var navigationLayout = navigation.gameObject.AddComponent<LayoutElement>();
            navigationLayout.minWidth = 250f;
            navigationLayout.preferredWidth = 270f;
            navigationLayout.flexibleWidth = 0f;
            var navigationVertical = navigation.gameObject.AddComponent<VerticalLayoutGroup>();
            navigationVertical.padding = new RectOffset(10, 10, 12, 12);
            navigationVertical.spacing = 10f;
            navigationVertical.childControlHeight = true;
            navigationVertical.childControlWidth = true;
            navigationVertical.childForceExpandHeight = false;
            navigationVertical.childForceExpandWidth = true;

            var navTitle = UiFactory.Text("Title", navigation, _font, "模组设置", 27f, UiFactory.TextPrimary);
            var navTitleLayout = navTitle.gameObject.AddComponent<LayoutElement>();
            navTitleLayout.minHeight = 42f;
            navTitleLayout.preferredHeight = 42f;
            UiFactory.ScrollView("Mods", navigation, out _navigationContent);

            var main = UiFactory.Rect("Settings", root);
            var mainLayout = main.gameObject.AddComponent<LayoutElement>();
            mainLayout.flexibleWidth = 1f;
            var mainVertical = main.gameObject.AddComponent<VerticalLayoutGroup>();
            mainVertical.spacing = 10f;
            mainVertical.childControlHeight = true;
            mainVertical.childControlWidth = true;
            mainVertical.childForceExpandHeight = false;
            mainVertical.childForceExpandWidth = true;

            var toolbar = UiFactory.Rect("Toolbar", main);
            var toolbarLayout = toolbar.gameObject.AddComponent<LayoutElement>();
            toolbarLayout.minHeight = 48f;
            toolbarLayout.preferredHeight = 48f;
            var toolbarHorizontal = toolbar.gameObject.AddComponent<HorizontalLayoutGroup>();
            toolbarHorizontal.spacing = 10f;
            toolbarHorizontal.childAlignment = TextAnchor.MiddleCenter;
            toolbarHorizontal.childControlHeight = true;
            toolbarHorizontal.childControlWidth = true;
            toolbarHorizontal.childForceExpandHeight = true;
            toolbarHorizontal.childForceExpandWidth = false;

            _title = UiFactory.Text("Selected Mod", toolbar, _font, string.Empty, 25f, UiFactory.TextPrimary);
            var titleLayout = _title.gameObject.AddComponent<LayoutElement>();
            titleLayout.flexibleWidth = 1f;

            _search = UiFactory.Input("Search", toolbar, _font, string.Empty, "搜索设置", 42f);
            var searchLayout = _search.GetComponent<LayoutElement>();
            searchLayout.minWidth = 260f;
            searchLayout.preferredWidth = 330f;
            searchLayout.flexibleWidth = 0f;
            _search.onValueChanged.AddListener(_ => RebuildSettings());

            _resetButton = UiFactory.Button("Reset", toolbar, _font, "恢复默认", ResetSelectedMod, 42f, UiFactory.RaisedBackground);
            var resetLayout = _resetButton.GetComponent<LayoutElement>();
            resetLayout.minWidth = 128f;
            resetLayout.preferredWidth = 128f;

            _settingsScroll = UiFactory.ScrollView("Setting Tree", main, out _settingsContent);

            _emptyState = UiFactory.Text("Empty", main, _font, "没有发现可编辑的设置", 20f, UiFactory.TextSecondary, TextAlignmentOptions.Center);
            var emptyLayout = _emptyState.gameObject.AddComponent<LayoutElement>();
            emptyLayout.minHeight = 46f;
            emptyLayout.preferredHeight = 46f;
            _emptyState.gameObject.SetActive(false);

            var tooltipRect = UiFactory.Rect("Tooltip", transform);
            tooltipRect.anchorMin = new Vector2(0.30f, 0f);
            tooltipRect.anchorMax = new Vector2(1f, 0f);
            tooltipRect.pivot = new Vector2(1f, 0f);
            tooltipRect.offsetMin = new Vector2(0f, 8f);
            tooltipRect.offsetMax = new Vector2(-22f, 44f);
            UiFactory.AddImage(tooltipRect.gameObject, new Color(0.02f, 0.02f, 0.02f, 0.96f));
            _tooltip = UiFactory.Text("Text", tooltipRect, _font, string.Empty, 16f, UiFactory.TextPrimary);
            UiFactory.Stretch(_tooltip.rectTransform, 12f, 2f, 12f, 2f);
            tooltipRect.gameObject.SetActive(false);
        }

        private void RebuildNavigation()
        {
            if (_catalog == null || _navigationContent == null)
            {
                return;
            }
            ClearChildren(_navigationContent);

            var mods = _catalog.Mods;
            if (_selectedMod == null || !mods.Any(mod => mod.Id == _selectedMod.Id))
            {
                _selectedMod = mods.FirstOrDefault();
            }
            else
            {
                _selectedMod = mods.First(mod => mod.Id == _selectedMod.Id);
            }

            foreach (var mod in mods)
            {
                var captured = mod;
                var selected = _selectedMod?.Id == mod.Id;
                var button = UiFactory.Button(
                    "Mod " + mod.Info.name,
                    _navigationContent,
                    _font,
                    mod.DisplayName,
                    () => SelectMod(captured),
                    48f,
                    selected ? new Color(0.12f, 0.28f, 0.22f, 1f) : UiFactory.RaisedBackground,
                    TextAlignmentOptions.MidlineLeft);
                var marker = UiFactory.Rect("Selection", button.transform);
                marker.anchorMin = new Vector2(0f, 0f);
                marker.anchorMax = new Vector2(0f, 1f);
                marker.pivot = new Vector2(0f, 0.5f);
                marker.sizeDelta = new Vector2(4f, 0f);
                marker.anchoredPosition = Vector2.zero;
                UiFactory.AddImage(marker.gameObject, selected ? UiFactory.Accent : Color.clear);
            }

            RebuildSettings();
        }

        private void RebuildSettings()
        {
            if (_settingsContent == null)
            {
                return;
            }
            ClearChildren(_settingsContent);
            ShowTooltip(string.Empty);

            if (_title != null)
            {
                _title.text = _selectedMod?.DisplayName ?? "模组设置";
            }
            if (_resetButton != null)
            {
                _resetButton.interactable = _selectedMod != null;
            }

            var query = _search?.text?.Trim() ?? string.Empty;
            var rendered = 0;
            if (_selectedMod != null)
            {
                foreach (var component in _selectedMod.Components)
                {
                    if (!component.Nodes.Any(node => NodeMatches(node, query)))
                    {
                        continue;
                    }
                    RenderComponent(component, query);
                    rendered++;
                }
            }

            if (_emptyState != null)
            {
                _emptyState.gameObject.SetActive(rendered == 0);
                _emptyState.text = _selectedMod == null
                    ? "没有发现可编辑的模组设置"
                    : string.IsNullOrEmpty(query) ? "没有发现可编辑的设置" : "没有匹配的设置";
            }
            if (_settingsScroll != null)
            {
                _settingsScroll.gameObject.SetActive(rendered > 0);
            }
        }

        private void RenderComponent(ComponentSettingsModel component, string query)
        {
            if (_settingsContent == null)
            {
                return;
            }

            var group = CreateGroupShell(
                _settingsContent,
                component.ComponentKey,
                component.DisplayName,
                tooltip: component.Target.GetType().FullName ?? component.DisplayName,
                defaultOpen: true,
                out var body);
            var rendered = 0;
            foreach (var node in component.Nodes)
            {
                if (NodeMatches(node, query))
                {
                    RenderNode(body, node, query, forceChildren: DirectMatch(node, query));
                    rendered++;
                }
            }
            group.SetActive(rendered > 0);
        }

        private void RenderNode(RectTransform parent, SettingNode node, string query, bool forceChildren)
        {
            if (!string.IsNullOrWhiteSpace(node.Header))
            {
                var header = UiFactory.Text("Header", parent, _font, node.Header!, 18f, UiFactory.SecondaryAccent);
                var layout = header.gameObject.AddComponent<LayoutElement>();
                layout.minHeight = 32f;
                layout.preferredHeight = 32f;
            }

            if (node.Kind == SettingNodeKind.Group)
            {
                CreateGroupShell(parent, node.StoreKey, node.DisplayName, node.Tooltip, defaultOpen: true, out var body);
                foreach (var child in node.Children)
                {
                    if (forceChildren || NodeMatches(child, query))
                    {
                        RenderNode(body, child, query, forceChildren || DirectMatch(child, query));
                    }
                }
                return;
            }

            RenderValue(parent, node);
        }

        private GameObject CreateGroupShell(
            RectTransform parent,
            string key,
            string title,
            string tooltip,
            bool defaultOpen,
            out RectTransform bodyContent)
        {
            if (!_foldoutStates.TryGetValue(key, out var open))
            {
                open = defaultOpen;
                _foldoutStates[key] = open;
            }

            var root = UiFactory.Rect("Group " + title, parent);
            var rootVertical = root.gameObject.AddComponent<VerticalLayoutGroup>();
            rootVertical.spacing = 4f;
            rootVertical.childControlHeight = true;
            rootVertical.childControlWidth = true;
            rootVertical.childForceExpandHeight = false;
            rootVertical.childForceExpandWidth = true;

            Button? foldoutButton = null;
            foldoutButton = UiFactory.Button(
                "Foldout",
                root,
                _font,
                (open ? "v  " : ">  ") + title,
                () =>
                {
                    _foldoutStates[key] = !_foldoutStates[key];
                    RebuildSettings();
                },
                42f,
                UiFactory.RaisedBackground,
                TextAlignmentOptions.MidlineLeft);
            AttachTooltip(foldoutButton.gameObject, tooltip);

            var body = UiFactory.Rect("Indented Content", root);
            var bodyHorizontal = body.gameObject.AddComponent<HorizontalLayoutGroup>();
            bodyHorizontal.padding = new RectOffset(10, 0, 2, 4);
            bodyHorizontal.spacing = 12f;
            bodyHorizontal.childControlHeight = true;
            bodyHorizontal.childControlWidth = true;
            bodyHorizontal.childForceExpandHeight = true;
            bodyHorizontal.childForceExpandWidth = false;

            var line = UiFactory.Rect("Hierarchy Line", body);
            UiFactory.AddImage(line.gameObject, UiFactory.Accent * new Color(1f, 1f, 1f, 0.45f));
            var lineLayout = line.gameObject.AddComponent<LayoutElement>();
            lineLayout.minWidth = 3f;
            lineLayout.preferredWidth = 3f;

            bodyContent = UiFactory.Rect("Children", body);
            var contentLayout = bodyContent.gameObject.AddComponent<LayoutElement>();
            contentLayout.flexibleWidth = 1f;
            var vertical = bodyContent.gameObject.AddComponent<VerticalLayoutGroup>();
            vertical.spacing = 5f;
            vertical.childControlHeight = true;
            vertical.childControlWidth = true;
            vertical.childForceExpandHeight = false;
            vertical.childForceExpandWidth = true;

            body.gameObject.SetActive(open);
            return root.gameObject;
        }

        private void RenderValue(RectTransform parent, SettingNode node)
        {
            var multiline = node.Kind == SettingNodeKind.String && node.TextArea != null;
            var editorHeight = multiline
                ? Mathf.Clamp(node.TextArea!.MinimumLines, 2, 8) * 24f + 16f
                : 40f;
            var row = UiFactory.Rect("Setting " + node.MemberPath, parent);
            UiFactory.AddImage(row.gameObject, new Color(1f, 1f, 1f, 0.025f));
            var rowLayout = row.gameObject.AddComponent<LayoutElement>();
            rowLayout.minHeight = multiline ? editorHeight + 12f : 48f;
            rowLayout.preferredHeight = multiline ? editorHeight + 12f : 48f;
            var horizontal = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            horizontal.padding = new RectOffset(12, 10, 5, 5);
            horizontal.spacing = 14f;
            horizontal.childAlignment = TextAnchor.MiddleCenter;
            horizontal.childControlHeight = true;
            horizontal.childControlWidth = true;
            horizontal.childForceExpandHeight = true;
            horizontal.childForceExpandWidth = false;

            var label = UiFactory.Text("Name", row, _font, node.DisplayName, 18f, UiFactory.TextPrimary);
            var labelLayout = label.gameObject.AddComponent<LayoutElement>();
            labelLayout.flexibleWidth = 1f;
            labelLayout.minWidth = 180f;

            var control = UiFactory.Rect("Control", row);
            var controlLayout = control.gameObject.AddComponent<LayoutElement>();
            controlLayout.minWidth = 310f;
            controlLayout.preferredWidth = 380f;
            controlLayout.flexibleWidth = 0f;
            CreateEditor(control, node, multiline, editorHeight);
            AttachTooltip(row.gameObject, node.Tooltip);
        }

        private void CreateEditor(RectTransform parent, SettingNode node, bool multiline, float editorHeight)
        {
            switch (node.Kind)
            {
                case SettingNodeKind.Boolean:
                    Toggle? toggle = null;
                    toggle = UiFactory.Toggle("Toggle", parent, node.GetValue() is bool current && current, value =>
                    {
                        node.TrySetValue(value, SettingChangeOrigin.User);
                        toggle?.SetIsOnWithoutNotify(node.GetValue() is bool applied && applied);
                    });
                    var toggleRect = (RectTransform)toggle.transform;
                    toggleRect.anchorMin = new Vector2(1f, 0.5f);
                    toggleRect.anchorMax = new Vector2(1f, 0.5f);
                    toggleRect.pivot = new Vector2(1f, 0.5f);
                    toggleRect.anchoredPosition = Vector2.zero;
                    toggleRect.sizeDelta = new Vector2(30f, 30f);
                    break;

                case SettingNodeKind.Enum:
                case SettingNodeKind.Key:
                    CreateEnumEditor(parent, node);
                    break;

                case SettingNodeKind.Color:
                    CreateColorEditor(parent, node);
                    break;

                case SettingNodeKind.Integer:
                case SettingNodeKind.FloatingPoint:
                    if (node.Range != null)
                    {
                        CreateRangeEditor(parent, node);
                    }
                    else
                    {
                        CreateScalarInput(parent, node);
                    }
                    break;

                case SettingNodeKind.String:
                    CreateStringInput(parent, node, multiline, editorHeight);
                    break;
            }
        }

        private void CreateEnumEditor(RectTransform parent, SettingNode node)
        {
            var type = Nullable.GetUnderlyingType(node.ValueType) ?? node.ValueType;
            var names = SettingValueCodec.GetEnumNames(type);
            var displayNames = SettingValueCodec.GetEnumDisplayNames(type);
            var current = node.GetValue()?.ToString() ?? string.Empty;
            var selected = Math.Max(0, Array.IndexOf(names, current));
            var dropdown = UiFactory.Dropdown("Enum", parent, _font, displayNames, selected);
            UiFactory.Stretch((RectTransform)dropdown.transform);
            dropdown.onValueChanged.AddListener(index =>
            {
                if (index >= 0 && index < names.Length)
                {
                    node.TrySetValue(names[index], SettingChangeOrigin.User);
                    var applied = node.GetValue()?.ToString() ?? string.Empty;
                    dropdown.SetValueWithoutNotify(Math.Max(0, Array.IndexOf(names, applied)));
                }
            });
        }

        private void CreateColorEditor(RectTransform parent, SettingNode node)
        {
            var color = GetColor(node);
            var button = UiFactory.Button("Color", parent, _font, "#" + ColorUtility.ToHtmlStringRGBA(color),
                () => ColorPickerDialog.Show(this, node, _font), 40f, UiFactory.InputBackground, TextAlignmentOptions.MidlineRight);
            UiFactory.Stretch((RectTransform)button.transform);
            var swatch = UiFactory.Rect("Swatch", button.transform);
            swatch.anchorMin = new Vector2(0f, 0.5f);
            swatch.anchorMax = new Vector2(0f, 0.5f);
            swatch.pivot = new Vector2(0f, 0.5f);
            swatch.anchoredPosition = new Vector2(8f, 0f);
            swatch.sizeDelta = new Vector2(48f, 26f);
            UiFactory.AddImage(swatch.gameObject, color);
            var text = button.GetComponentInChildren<TMP_Text>();
            if (text != null)
            {
                text.rectTransform.offsetMin = new Vector2(64f, text.rectTransform.offsetMin.y);
            }
        }

        private void CreateRangeEditor(RectTransform parent, SettingNode node)
        {
            var row = UiFactory.Rect("Range", parent);
            UiFactory.Stretch(row);
            var horizontal = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            horizontal.spacing = 8f;
            horizontal.childAlignment = TextAnchor.MiddleCenter;
            horizontal.childControlHeight = true;
            horizontal.childControlWidth = true;
            horizontal.childForceExpandHeight = true;
            horizontal.childForceExpandWidth = false;

            var number = Convert.ToSingle(node.GetValue() ?? 0f, CultureInfo.InvariantCulture);
            var slider = UiFactory.Slider("Slider", row, node.Range!.Minimum, node.Range.Maximum, number,
                node.Kind == SettingNodeKind.Integer);
            var input = UiFactory.Input("Value", row, _font, FormatNumber(node.GetValue()), string.Empty, 36f);
            var inputLayout = input.GetComponent<LayoutElement>();
            inputLayout.minWidth = 78f;
            inputLayout.preferredWidth = 78f;
            inputLayout.flexibleWidth = 0f;

            slider.onValueChanged.AddListener(value =>
            {
                object converted = node.Kind == SettingNodeKind.Integer ? (object)Mathf.RoundToInt(value) : value;
                node.TrySetValue(converted, SettingChangeOrigin.User);
                slider.SetValueWithoutNotify(Convert.ToSingle(node.GetValue(), CultureInfo.InvariantCulture));
                input.SetTextWithoutNotify(FormatNumber(node.GetValue()));
            });
            input.onEndEdit.AddListener(text =>
            {
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    node.TrySetValue(value, SettingChangeOrigin.User);
                    slider.SetValueWithoutNotify(Convert.ToSingle(node.GetValue(), CultureInfo.InvariantCulture));
                }
                input.SetTextWithoutNotify(FormatNumber(node.GetValue()));
            });
        }

        private void CreateScalarInput(RectTransform parent, SettingNode node)
        {
            var input = UiFactory.Input("Value", parent, _font, FormatNumber(node.GetValue()), string.Empty, 40f);
            UiFactory.Stretch((RectTransform)input.transform);
            input.onEndEdit.AddListener(text =>
            {
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    node.TrySetValue(value, SettingChangeOrigin.User);
                }
                input.SetTextWithoutNotify(FormatNumber(node.GetValue()));
            });
        }

        private void CreateStringInput(RectTransform parent, SettingNode node, bool multiline, float editorHeight)
        {
            var input = UiFactory.Input("Value", parent, _font, node.GetValue()?.ToString() ?? string.Empty, string.Empty,
                editorHeight, multiline);
            UiFactory.Stretch((RectTransform)input.transform);
            input.onEndEdit.AddListener(value =>
            {
                node.TrySetValue(value, SettingChangeOrigin.User);
                input.SetTextWithoutNotify(node.GetValue()?.ToString() ?? string.Empty);
            });
        }

        private void SelectMod(ModSettingsModel mod)
        {
            _selectedMod = mod;
            RebuildNavigation();
        }

        private void ResetSelectedMod()
        {
            if (_catalog == null || _selectedMod == null)
            {
                return;
            }
            _catalog.Reset(_selectedMod);
            RebuildSettings();
        }

        private void OnStructureChanged()
        {
            _rebuildRequested = true;
        }

        private void OnValueChanged(SettingNode node, SettingChangeOrigin origin)
        {
            if (origin == SettingChangeOrigin.External && _selectedMod?.Id == node.Owner.Mod.Id)
            {
                _rebuildRequested = true;
            }
        }

        private void AttachTooltip(GameObject target, string tooltip)
        {
            if (string.IsNullOrWhiteSpace(tooltip))
            {
                return;
            }
            var trigger = target.AddComponent<SettingsTooltipTrigger>();
            trigger.Initialize(this, tooltip);
        }

        private static bool NodeMatches(SettingNode node, string query)
        {
            return string.IsNullOrEmpty(query) || DirectMatch(node, query) || node.Children.Any(child => NodeMatches(child, query));
        }

        private static bool DirectMatch(SettingNode node, string query)
        {
            return string.IsNullOrEmpty(query) ||
                node.DisplayName.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                node.MemberPath.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                node.Tooltip.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        private static string FormatNumber(object? value)
        {
            return value is IFormattable formattable ? formattable.ToString(null, CultureInfo.InvariantCulture) : value?.ToString() ?? string.Empty;
        }

        private static Color GetColor(SettingNode node)
        {
            return node.GetValue() switch
            {
                Color color => color,
                Color32 color32 => color32,
                _ => Color.white,
            };
        }

        private static void ClearChildren(Transform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }
    }

    internal sealed class SettingsTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private SettingsPage? _page;
        private string _text = string.Empty;

        public void Initialize(SettingsPage page, string text)
        {
            _page = page;
            _text = text;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _page?.ShowTooltip(_text);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _page?.ShowTooltip(string.Empty);
        }
    }
}
