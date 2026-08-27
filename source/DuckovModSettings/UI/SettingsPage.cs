using SlimeNull.DuckovModSettings.Core;
using SlimeNull.DuckovModSettings.Localization;
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
        private readonly Dictionary<string, NavigationItem> _navigationItems = new Dictionary<string, NavigationItem>(StringComparer.Ordinal);
        private readonly Dictionary<string, Sprite> _previewSprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private SettingsCatalog? _catalog;
        private Action<SettingsPage>? _onPageOpening;
        private Action<SettingsPage>? _onPageClosing;
        private TMP_FontAsset? _font;
        private RectTransform? _navigationContent;
        private RectTransform? _settingsContent;
        private TextMeshProUGUI? _emptyState;
        private TextMeshProUGUI? _tooltip;
        private TMP_InputField? _search;
        private Button? _resetButton;
        private ScrollRect? _navigationScroll;
        private ScrollRect? _settingsScroll;
        private LayoutElement? _pageLayout;
        private RectTransform? _layoutTemplate;
        private ModSettingsModel? _selectedMod;
        private bool _initialized;
        private bool _menuOpen;
        private bool _loading;
        private bool _navigationRebuildRequested;
        private bool _settingsRebuildRequested;
        private float _lastTemplateHeight = -1f;
        private float _lastViewportHeight = -1f;

        internal bool IsMenuOpen => _menuOpen;

        public void Initialize(
            SettingsCatalog catalog,
            Action<SettingsPage> onPageOpening,
            Action<SettingsPage> onPageClosing,
            TMP_FontAsset? font,
            RectTransform? layoutTemplate)
        {
            _catalog = catalog;
            _onPageOpening = onPageOpening;
            _onPageClosing = onPageClosing;
            _font = font ?? Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault();
            _layoutTemplate = layoutTemplate;
            _pageLayout = gameObject.AddComponent<LayoutElement>();
            RefreshPageLayout();
            BuildShell();
            _catalog.StructureChanged += OnStructureChanged;
            _catalog.ValueChanged += OnValueChanged;
            _initialized = true;
        }

        internal void NotifyMenuOpened()
        {
            if (!_initialized || _menuOpen)
            {
                return;
            }

            _menuOpen = true;
            RefreshPageLayout();
            BeginLoading();
            _onPageOpening?.Invoke(this);
        }

        internal void NotifyMenuClosed()
        {
            if (!_menuOpen)
            {
                return;
            }

            _menuOpen = false;
            ShowTooltip(string.Empty);
            _onPageClosing?.Invoke(this);
        }

        internal void ReportLoadingProgress(int processed, int total)
        {
            if (!_loading || _emptyState == null)
            {
                return;
            }

            _emptyState.text = total > 0
                ? string.Format(SettingsText.Culture, SettingsText.LoadingProgress, processed, total)
                : SettingsText.Loading;
        }

        internal void CompleteLoading()
        {
            if (!_menuOpen)
            {
                return;
            }

            _loading = false;
            _navigationRebuildRequested = false;
            _settingsRebuildRequested = false;
            if (_navigationScroll != null)
            {
                _navigationScroll.gameObject.SetActive(true);
            }
            if (_search != null)
            {
                _search.interactable = true;
            }
            RebuildNavigation();
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

        internal void RefreshLocalization()
        {
            if (_search?.placeholder is TMP_Text placeholder)
            {
                placeholder.text = SettingsText.SearchSettings;
            }
            if (_resetButton != null)
            {
                var label = _resetButton.GetComponentInChildren<TMP_Text>(includeInactive: true);
                if (label != null)
                {
                    label.text = SettingsText.RestoreDefaults;
                }
            }

            if (_loading && _emptyState != null)
            {
                _emptyState.text = SettingsText.Loading;
            }
            else if (_menuOpen)
            {
                _navigationRebuildRequested = true;
            }
        }

        private void OnEnable()
        {
            RefreshPageLayout();
        }

        private void OnDisable()
        {
            NotifyMenuClosed();
        }

        private void RefreshPageLayout()
        {
            if (_pageLayout == null || _layoutTemplate == null)
            {
                return;
            }

            var templateHeight = Mathf.Max(0f, _layoutTemplate.rect.height);
            _lastTemplateHeight = templateHeight;
            _lastViewportHeight = GetViewportHeight();
            var minimumHeight = Mathf.Max(0f, LayoutUtility.GetMinHeight(_layoutTemplate));
            var preferredHeight = Mathf.Max(
                templateHeight,
                LayoutUtility.GetPreferredHeight(_layoutTemplate),
                GetAvailablePageHeight());
            if (preferredHeight <= 0f)
            {
                return;
            }

            _pageLayout.minHeight = Mathf.Min(minimumHeight, preferredHeight);
            _pageLayout.preferredHeight = preferredHeight;
            _pageLayout.flexibleHeight = Mathf.Max(0f, LayoutUtility.GetFlexibleHeight(_layoutTemplate));
        }

        private float GetAvailablePageHeight()
        {
            if (!gameObject.activeInHierarchy ||
                transform.parent is not RectTransform content ||
                content.parent is not RectTransform viewport)
            {
                return 0f;
            }

            var availableHeight = viewport.rect.height;
            var layout = content.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                return Mathf.Max(0f, availableHeight);
            }

            availableHeight -= layout.padding.top + layout.padding.bottom;
            var activeSiblings = 0;
            for (var i = 0; i < content.childCount; i++)
            {
                var child = content.GetChild(i) as RectTransform;
                if (child == null || child == transform || !child.gameObject.activeInHierarchy)
                {
                    continue;
                }

                activeSiblings++;
                availableHeight -= Mathf.Max(0f, LayoutUtility.GetPreferredHeight(child));
            }
            availableHeight -= activeSiblings * layout.spacing;
            return Mathf.Max(0f, availableHeight);
        }

        private float GetViewportHeight()
        {
            return transform.parent is RectTransform content && content.parent is RectTransform viewport
                ? Mathf.Max(0f, viewport.rect.height)
                : 0f;
        }

        private bool LayoutDimensionsChanged()
        {
            return _layoutTemplate != null &&
                (!Mathf.Approximately(_lastTemplateHeight, Mathf.Max(0f, _layoutTemplate.rect.height)) ||
                    !Mathf.Approximately(_lastViewportHeight, GetViewportHeight()));
        }

        private void OnDestroy()
        {
            NotifyMenuClosed();
            if (_catalog != null)
            {
                _catalog.StructureChanged -= OnStructureChanged;
                _catalog.ValueChanged -= OnValueChanged;
            }
            foreach (var sprite in _previewSprites.Values)
            {
                if (sprite != null)
                {
                    Destroy(sprite);
                }
            }
            _previewSprites.Clear();
        }

        private void Update()
        {
            if (!_menuOpen)
            {
                return;
            }

            if (LayoutDimensionsChanged())
            {
                RefreshPageLayout();
            }
            if (_navigationRebuildRequested)
            {
                _navigationRebuildRequested = false;
                _settingsRebuildRequested = false;
                RebuildNavigation();
            }
            else if (_settingsRebuildRequested)
            {
                _settingsRebuildRequested = false;
                RebuildSettings();
            }
        }

        internal void BeginLoading()
        {
            _loading = true;
            _navigationRebuildRequested = false;
            _settingsRebuildRequested = false;
            if (_navigationScroll != null)
            {
                _navigationScroll.gameObject.SetActive(false);
            }
            if (_search != null)
            {
                _search.interactable = false;
            }
            if (_resetButton != null)
            {
                _resetButton.interactable = false;
            }
            if (_settingsScroll != null)
            {
                _settingsScroll.gameObject.SetActive(false);
            }
            if (_emptyState != null)
            {
                _emptyState.text = SettingsText.Loading;
                _emptyState.gameObject.SetActive(true);
            }
        }

        private void BuildShell()
        {
            UiFactory.AddImage(gameObject, UiFactory.PageBackground, UiFactory.PanelRadius);
            var root = UiFactory.Rect("Layout", transform);
            UiFactory.Stretch(root);
            var horizontal = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            horizontal.spacing = 14f;
            horizontal.childControlHeight = true;
            horizontal.childControlWidth = true;
            horizontal.childForceExpandHeight = true;
            horizontal.childForceExpandWidth = false;

            var navigation = UiFactory.Rect("Mod Navigation", root);
            UiFactory.AddImage(navigation.gameObject, UiFactory.PanelBackground, UiFactory.PanelRadius);
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

            _navigationScroll = UiFactory.ScrollView("Mods", navigation, out _navigationContent);

            _search = UiFactory.Input(
                "Search",
                navigation,
                _font,
                string.Empty,
                SettingsText.SearchSettings,
                36f);
            _search.onValueChanged.AddListener(_ => RebuildSettings());

            _resetButton = UiFactory.Button(
                "Reset",
                navigation,
                _font,
                SettingsText.RestoreDefaults,
                ResetSelectedMod,
                36f,
                UiFactory.SecondaryAccent,
                TextAlignmentOptions.Center,
                UiFactory.InputText);

            var main = UiFactory.Rect("Settings", root);
            var mainLayout = main.gameObject.AddComponent<LayoutElement>();
            mainLayout.flexibleWidth = 1f;
            var mainVertical = main.gameObject.AddComponent<VerticalLayoutGroup>();
            mainVertical.spacing = 0f;
            mainVertical.childControlHeight = true;
            mainVertical.childControlWidth = true;
            mainVertical.childForceExpandHeight = false;
            mainVertical.childForceExpandWidth = true;

            _settingsScroll = UiFactory.ScrollView("Setting Tree", main, out _settingsContent);

            _emptyState = UiFactory.Text(
                "Empty",
                main,
                _font,
                SettingsText.NoEditableSettings,
                20f,
                UiFactory.TextSecondary,
                TextAlignmentOptions.Center);
            var emptyLayout = _emptyState.gameObject.AddComponent<LayoutElement>();
            emptyLayout.minHeight = 46f;
            emptyLayout.preferredHeight = 46f;
            emptyLayout.flexibleHeight = 1f;
            _emptyState.gameObject.SetActive(false);

            var tooltipRect = UiFactory.Rect("Tooltip", transform);
            tooltipRect.anchorMin = new Vector2(0.30f, 0f);
            tooltipRect.anchorMax = new Vector2(1f, 0f);
            tooltipRect.pivot = new Vector2(1f, 0f);
            tooltipRect.offsetMin = new Vector2(0f, 8f);
            tooltipRect.offsetMax = new Vector2(-22f, 44f);
            UiFactory.AddImage(tooltipRect.gameObject, UiFactory.PageBackground, UiFactory.ControlRadius);
            _tooltip = UiFactory.Text("Text", tooltipRect, _font, string.Empty, 16f, UiFactory.TextPrimary);
            UiFactory.Stretch(_tooltip.rectTransform, 12f, 2f, 12f, 2f);
            tooltipRect.gameObject.SetActive(false);
        }

        private void RebuildNavigation()
        {
            if (_loading || _catalog == null || _navigationContent == null)
            {
                return;
            }
            _navigationItems.Clear();
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
                    58f,
                    selected ? UiFactory.SelectedBackground : UiFactory.RaisedBackground,
                    TextAlignmentOptions.MidlineLeft);
                var label = button.GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    label.rectTransform.offsetMin = new Vector2(58f, label.rectTransform.offsetMin.y);
                }
                AddModPreview(button.transform, mod);
                var marker = UiFactory.Rect("Selection", button.transform);
                marker.anchorMin = new Vector2(0f, 0f);
                marker.anchorMax = new Vector2(0f, 1f);
                marker.pivot = new Vector2(0f, 0.5f);
                marker.sizeDelta = new Vector2(5f, -12f);
                marker.anchoredPosition = Vector2.zero;
                var markerImage = UiFactory.AddImage(marker.gameObject, selected ? Color.white : Color.clear, 2.5f);
                _navigationItems[mod.Id] = new NavigationItem(button, markerImage);
            }

            RebuildSettings();
        }

        private void RebuildSettings()
        {
            if (_loading || _settingsContent == null)
            {
                return;
            }
            ClearChildren(_settingsContent);
            ShowTooltip(string.Empty);

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
                    ? SettingsText.NoEditableModSettings
                    : string.IsNullOrEmpty(query)
                        ? SettingsText.NoEditableSettings
                        : SettingsText.NoMatchingSettings;
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

            if (_selectedMod?.Components.Count == 1)
            {
                foreach (var node in component.Nodes)
                {
                    if (NodeMatches(node, query))
                    {
                        RenderNode(_settingsContent, node, query, forceChildren: DirectMatch(node, query));
                    }
                }
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

            RectTransform? body = null;
            TMP_Text? foldoutLabel = null;
            var foldoutButton = UiFactory.Button(
                "Foldout",
                root,
                _font,
                (open ? "v  " : ">  ") + title,
                () =>
                {
                    var nextOpen = !_foldoutStates[key];
                    _foldoutStates[key] = nextOpen;
                    body?.gameObject.SetActive(nextOpen);
                    if (foldoutLabel != null)
                    {
                        foldoutLabel.text = (nextOpen ? "v  " : ">  ") + title;
                    }
                    LayoutRebuilder.ForceRebuildLayoutImmediate(root);
                    if (_settingsContent != null)
                    {
                        LayoutRebuilder.ForceRebuildLayoutImmediate(_settingsContent);
                    }
                },
                42f,
                UiFactory.GroupBackground,
                TextAlignmentOptions.MidlineLeft);
            foldoutLabel = foldoutButton.GetComponentInChildren<TMP_Text>();
            AttachTooltip(foldoutButton.gameObject, tooltip);

            body = UiFactory.Rect("Indented Content", root);
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
            UiFactory.AddImage(row.gameObject, UiFactory.RaisedBackground, UiFactory.ControlRadius);
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
            var compactColor = node.Kind == SettingNodeKind.Color;
            controlLayout.minWidth = compactColor ? 220f : 310f;
            controlLayout.preferredWidth = compactColor ? 240f : 380f;
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
                () => ColorPickerDialog.Show(this, node, _font), 40f, UiFactory.InputBackground,
                TextAlignmentOptions.MidlineLeft, UiFactory.InputText);
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
            if (_selectedMod?.Id == mod.Id)
            {
                return;
            }
            _selectedMod = mod;
            UpdateNavigationSelection();
            RebuildSettings();
        }

        private void UpdateNavigationSelection()
        {
            foreach (var pair in _navigationItems)
            {
                var selected = _selectedMod?.Id == pair.Key;
                var background = selected ? UiFactory.SelectedBackground : UiFactory.RaisedBackground;
                pair.Value.Button.colors = UiFactory.ButtonColors(background);
                pair.Value.Marker.color = selected ? Color.white : Color.clear;
            }
        }

        private void AddModPreview(Transform parent, ModSettingsModel mod)
        {
            var preview = UiFactory.Rect("Preview", parent);
            preview.anchorMin = new Vector2(0f, 0.5f);
            preview.anchorMax = new Vector2(0f, 0.5f);
            preview.pivot = new Vector2(0f, 0.5f);
            preview.anchoredPosition = new Vector2(10f, 0f);
            preview.sizeDelta = new Vector2(38f, 38f);

            var sprite = GetPreviewSprite(mod);
            var image = UiFactory.AddImage(
                preview.gameObject,
                sprite != null ? Color.white : UiFactory.GroupBackground,
                UiFactory.SmallRadius);
            if (sprite != null)
            {
                image.sprite = sprite;
                image.preserveAspect = true;
                return;
            }

            var fallback = UiFactory.Text(
                "Fallback",
                preview,
                _font,
                string.IsNullOrEmpty(mod.DisplayName) ? "?" : mod.DisplayName.Substring(0, 1).ToUpperInvariant(),
                18f,
                UiFactory.TextPrimary,
                TextAlignmentOptions.Center);
            UiFactory.Stretch(fallback.rectTransform);
        }

        private Sprite? GetPreviewSprite(ModSettingsModel mod)
        {
            if (_previewSprites.TryGetValue(mod.Id, out var cached))
            {
                return cached;
            }

            var texture = mod.Info.preview;
            if (texture == null)
            {
                return null;
            }

            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            sprite.name = "DuckovModSettings Preview " + mod.Info.name;
            _previewSprites[mod.Id] = sprite;
            return sprite;
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
            if (_menuOpen && !_loading)
            {
                _navigationRebuildRequested = true;
            }
        }

        private void OnValueChanged(SettingNode node, SettingChangeOrigin origin)
        {
            if (_menuOpen && !_loading && origin == SettingChangeOrigin.External && _selectedMod?.Id == node.Owner.Mod.Id)
            {
                _settingsRebuildRequested = true;
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

        private sealed class NavigationItem
        {
            public NavigationItem(Button button, Image marker)
            {
                Button = button;
                Marker = marker;
            }

            public Button Button { get; }
            public Image Marker { get; }
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
