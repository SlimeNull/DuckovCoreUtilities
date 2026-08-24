using Newtonsoft.Json;
using SlimeNull.DuckovInterop;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace DockovInterop.HierarchyInspector;

public sealed class InspectorFieldView : ContentControl
{
    public InspectorFieldView()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        DataContextChanged += (_, _) => Rebuild();
    }

    private void Rebuild()
    {
        if (DataContext is not InspectorFieldViewModel field)
        {
            Content = null;
            return;
        }

        var container = new StackPanel { Margin = new Thickness(0, 1, 0, 1) };
        if (!string.IsNullOrWhiteSpace(field.Header))
        {
            container.Children.Add(new TextBlock
            {
                Text = field.Header,
                FontWeight = FontWeights.SemiBold,
                Foreground = ThemeBrush("TextBrush"),
                Margin = new Thickness(2, 8, 0, 3)
            });
        }

        container.Children.Add(IsCompound(field) ? CreateCompound(field) : CreateRow(field));
        var error = new TextBlock
        {
            Foreground = ThemeBrush("ErrorBrush"),
            Margin = new Thickness(170, 2, 0, 3),
            TextWrapping = TextWrapping.Wrap
        };
        error.SetBinding(TextBlock.TextProperty, new Binding(nameof(InspectorFieldViewModel.Error)));
        error.SetBinding(VisibilityProperty, new Binding(nameof(InspectorFieldViewModel.ErrorVisibility)));
        container.Children.Add(error);
        if (!string.IsNullOrWhiteSpace(field.Tooltip))
        {
            container.ToolTip = field.Tooltip;
        }
        Content = container;
    }

    private static bool IsCompound(InspectorFieldViewModel field) =>
        field.Kind is "Array" or "Object" || field.Children.Count > 0 && field.Kind is not ("Vector2" or "Vector3" or "Vector4" or "Quaternion" or "Color");

    private static FrameworkElement CreateCompound(InspectorFieldViewModel field)
    {
        var expander = new Expander
        {
            IsExpanded = field.Kind == "Object",
            Header = CreateCompoundHeader(field),
            Margin = new Thickness(0, 1, 0, 1)
        };
        var children = new StackPanel { Margin = new Thickness(17, 2, 0, 2) };
        foreach (var child in field.Children)
        {
            children.Children.Add(new InspectorFieldView { DataContext = child });
        }
        expander.Content = children;
        return expander;
    }

    private static FrameworkElement CreateCompoundHeader(InspectorFieldViewModel field)
    {
        var grid = CreateBaseGrid();
        grid.Children.Add(CreateLabel(field.DisplayName ?? field.Name ?? "Field"));
        var summary = field.Kind == "Array" ? $"Array  Size {field.Value ?? "0"}" : field.Type?.Split('.').LastOrDefault() ?? field.Kind;
        var text = new TextBlock
        {
            Text = summary,
            Foreground = ThemeBrush("MutedTextBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        return grid;
    }

    private static FrameworkElement CreateRow(InspectorFieldViewModel field)
    {
        var grid = CreateBaseGrid();
        grid.Children.Add(CreateLabel(field.DisplayName ?? field.Name ?? "Field"));
        var editor = CreateEditor(field);
        Grid.SetColumn(editor, 1);
        grid.Children.Add(editor);
        return grid;
    }

    private static Grid CreateBaseGrid()
    {
        var grid = new Grid { MinHeight = 24 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        return grid;
    }

    private static TextBlock CreateLabel(string label) => new()
    {
        Text = label,
        VerticalAlignment = VerticalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis,
        Margin = new Thickness(3, 0, 8, 0)
    };

    private static FrameworkElement CreateEditor(InspectorFieldViewModel field)
    {
        switch (field.Kind)
        {
            case "Boolean":
                var checkBox = new CheckBox
                {
                    IsChecked = bool.TryParse(field.Value, out var value) && value,
                    IsEnabled = field.CanWrite,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var suppressCheckBoxCommit = false;
                async Task CommitCheckBoxAsync(bool checkedValue)
                {
                    if (suppressCheckBoxCommit || !field.CanEdit)
                    {
                        return;
                    }

                    suppressCheckBoxCommit = true;
                    checkBox.IsEnabled = false;
                    if (!await field.CommitAsync(checkedValue.ToString(CultureInfo.InvariantCulture), checkedValue ? "true" : "false"))
                    {
                        checkBox.IsChecked = bool.TryParse(field.Value, out var original) && original;
                    }
                    checkBox.IsEnabled = field.CanWrite;
                    suppressCheckBoxCommit = false;
                }
                checkBox.Checked += async (_, _) => await CommitCheckBoxAsync(true);
                checkBox.Unchecked += async (_, _) => await CommitCheckBoxAsync(false);
                return checkBox;
            case "Enum":
                var comboBox = new ComboBox
                {
                    ItemsSource = field.EnumNames,
                    SelectedItem = field.Value,
                    IsEnabled = field.CanWrite,
                    Height = 22,
                    Style = ThemeStyle("InspectorComboBoxStyle")
                };
                var suppressComboBoxCommit = false;
                comboBox.SelectionChanged += async (_, _) =>
                {
                    if (suppressComboBoxCommit || !field.CanEdit || comboBox.SelectedItem is not string selectedValue || selectedValue == field.Value)
                    {
                        return;
                    }

                    suppressComboBoxCommit = true;
                    comboBox.IsEnabled = false;
                    if (!await field.CommitAsync(selectedValue, JsonConvert.SerializeObject(selectedValue)))
                    {
                        comboBox.SelectedItem = field.Value;
                    }
                    comboBox.IsEnabled = field.CanWrite;
                    suppressComboBoxCommit = false;
                };
                return comboBox;
            case "Vector2":
            case "Vector3":
            case "Vector4":
            case "Quaternion":
                return CreateVectorEditor(field);
            case "Color":
                return CreateColorEditor(field);
            case "ObjectReference":
                return CreateObjectReference(field);
            case "String" when field.Multiline:
                return CreateTextBox(field, true, Math.Max(48, (field.TextAreaMinLines ?? 3) * 18));
            case "Float" when field.RangeMin.HasValue && field.RangeMax.HasValue:
                return CreateRangeEditor(field);
            case "Error":
                return new TextBlock { Text = field.Value, Foreground = ThemeBrush("ErrorBrush"), TextWrapping = TextWrapping.Wrap };
            case "Null":
            case "Reference":
            case "Truncated":
                return new TextBlock { Text = field.Value, Foreground = ThemeBrush("MutedTextBrush"), VerticalAlignment = VerticalAlignment.Center };
            default:
                return CreateTextBox(field, false, 22);
        }
    }

    private static FrameworkElement CreateVectorEditor(InspectorFieldViewModel field)
    {
        var grid = new Grid();
        for (var i = 0; i < field.Children.Count; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var child = field.Children[i];
            var label = new TextBlock
            {
                Text = child.DisplayName,
                Foreground = ThemeBrush(i switch { 0 => "AxisXBrush", 1 => "AxisYBrush", 2 => "AxisZBrush", _ => "AxisWBrush" }),
                Margin = new Thickness(i == 0 ? 3 : 7, 0, 3, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(label, i * 2);
            grid.Children.Add(label);
            var input = CreateTextBox(child, false, 22);
            input.MinWidth = 35;
            Grid.SetColumn(input, i * 2 + 1);
            grid.Children.Add(input);
        }
        return grid;
    }

    private static FrameworkElement CreateColorEditor(InspectorFieldViewModel field)
    {
        var panel = new Grid();
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var swatch = new Border
        {
            Margin = new Thickness(0, 1, 5, 1),
            Style = ThemeStyle("InspectorColorSwatchStyle")
        };
        swatch.SetBinding(Border.BackgroundProperty, new Binding(nameof(InspectorFieldViewModel.Value))
        {
            Converter = ColorBrushConverter.Instance
        });
        panel.Children.Add(swatch);
        var channels = (Grid)CreateVectorEditor(field);
        Grid.SetColumn(channels, 1);
        panel.Children.Add(channels);
        return panel;
    }

    private static Brush ParseColor(string? text)
    {
        var values = (text ?? string.Empty).Split(',');
        if (values.Length == 4 && values.Select(value => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _)).All(valid => valid))
        {
            var parsed = values.Select(value => Math.Clamp(double.Parse(value, CultureInfo.InvariantCulture), 0, 1)).ToArray();
            return new SolidColorBrush(Color.FromArgb((byte)(parsed[3] * 255), (byte)(parsed[0] * 255), (byte)(parsed[1] * 255), (byte)(parsed[2] * 255)));
        }
        return Brushes.Transparent;
    }

    private sealed class ColorBrushConverter : IValueConverter
    {
        public static readonly ColorBrushConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => ParseColor(value as string);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    private static FrameworkElement CreateObjectReference(InspectorFieldViewModel field)
    {
        var border = new Border
        {
            Height = 22,
            Style = ThemeStyle("InspectorObjectReferenceStyle")
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(105) });
        grid.Children.Add(new TextBlock { Text = field.Value ?? "None", VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
        var idField = CreateTextBox(field, false, 20, field.InstanceID?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, objectReference: true);
        idField.Margin = new Thickness(8, -1, -5, -1);
        Grid.SetColumn(idField, 1);
        grid.Children.Add(idField);
        border.Child = grid;
        return border;
    }

    private static FrameworkElement CreateRangeEditor(InspectorFieldViewModel field)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        _ = double.TryParse(field.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value);
        var slider = new Slider
        {
            Minimum = field.RangeMin!.Value,
            Maximum = field.RangeMax!.Value,
            Value = value,
            IsEnabled = field.CanWrite,
            Margin = new Thickness(2, 0, 6, 0)
        };
        slider.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(async (_, _) =>
        {
            var text = slider.Value.ToString("R", CultureInfo.InvariantCulture);
            if (!await field.CommitAsync(text, text))
            {
                _ = double.TryParse(field.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var original);
                slider.Value = original;
            }
        }));
        grid.Children.Add(slider);
        var input = CreateTextBox(field, false, 22);
        Grid.SetColumn(input, 1);
        grid.Children.Add(input);
        return grid;
    }

    private static TextBox CreateTextBox(InspectorFieldViewModel field, bool multiline, double height, string? initialValue = null, bool objectReference = false)
    {
        var textBox = new TextBox
        {
            Text = initialValue ?? field.Value ?? string.Empty,
            IsReadOnly = !field.CanWrite,
            Height = height,
            MinWidth = 20,
            Style = ThemeStyle("InspectorReadOnlyTextBoxStyle"),
            VerticalContentAlignment = multiline ? VerticalAlignment.Top : VerticalAlignment.Center,
            TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            AcceptsReturn = multiline,
            VerticalScrollBarVisibility = multiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Hidden
        };

        var committing = false;
        async Task CommitAsync()
        {
            if (committing || !field.CanWrite)
            {
                return;
            }

            var original = objectReference ? field.InstanceID?.ToString(CultureInfo.InvariantCulture) ?? string.Empty : field.Value ?? string.Empty;
            if (textBox.Text == original)
            {
                return;
            }

            committing = true;
            textBox.IsReadOnly = true;
            var json = ToJson(field, textBox.Text, objectReference);
            if (json == null || !await field.CommitAsync(textBox.Text, json))
            {
                textBox.Text = original;
            }
            textBox.IsReadOnly = !field.CanWrite;
            committing = false;
        }

        textBox.LostKeyboardFocus += async (_, _) => await CommitAsync();
        textBox.PreviewKeyDown += async (_, e) =>
        {
            var shouldCommit = e.Key == Key.Enter && (!multiline || Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
            if (!shouldCommit)
            {
                return;
            }

            e.Handled = true;
            await CommitAsync();
        };
        return textBox;
    }

    private static string? ToJson(InspectorFieldViewModel field, string input, bool objectReference)
    {
        if (objectReference)
        {
            return string.IsNullOrWhiteSpace(input) ? "null" : JsonConvert.SerializeObject(input.Trim());
        }

        return field.Kind is "String" or "Enum" ? JsonConvert.SerializeObject(input) : input.Trim();
    }

    private static Brush ThemeBrush(string key) => (Brush)Application.Current.FindResource(key);

    private static Style ThemeStyle(string key) => (Style)Application.Current.FindResource(key);
}
