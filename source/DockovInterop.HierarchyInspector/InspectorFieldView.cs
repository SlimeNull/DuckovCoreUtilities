using SlimeNull.DuckovInterop;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
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
        if (DataContext is not SerializedFieldInfo field)
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
        if (!string.IsNullOrWhiteSpace(field.Tooltip))
        {
            container.ToolTip = field.Tooltip;
        }
        Content = container;
    }

    private static bool IsCompound(SerializedFieldInfo field) =>
        field.Kind is "Array" or "Object" || field.Children.Count > 0 && field.Kind is not ("Vector2" or "Vector3" or "Vector4" or "Quaternion");

    private static FrameworkElement CreateCompound(SerializedFieldInfo field)
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

    private static FrameworkElement CreateCompoundHeader(SerializedFieldInfo field)
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

    private static FrameworkElement CreateRow(SerializedFieldInfo field)
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

    private static FrameworkElement CreateEditor(SerializedFieldInfo field)
    {
        switch (field.Kind)
        {
            case "Boolean":
                return new CheckBox
                {
                    IsChecked = bool.TryParse(field.Value, out var value) && value,
                    IsHitTestVisible = false,
                    Focusable = false,
                    VerticalAlignment = VerticalAlignment.Center
                };
            case "Enum":
                return new ComboBox
                {
                    ItemsSource = field.EnumNames,
                    SelectedItem = field.Value,
                    IsHitTestVisible = false,
                    Focusable = false,
                    Height = 22,
                    Style = ThemeStyle("InspectorComboBoxStyle")
                };
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
                return CreateTextBox(field.Value, true, Math.Max(48, (field.TextAreaMinLines ?? 3) * 18));
            case "Float" when field.RangeMin.HasValue && field.RangeMax.HasValue:
                return CreateRangeEditor(field);
            case "Error":
                return new TextBlock { Text = field.Value, Foreground = ThemeBrush("ErrorBrush"), TextWrapping = TextWrapping.Wrap };
            case "Null":
            case "Reference":
            case "Truncated":
                return new TextBlock { Text = field.Value, Foreground = ThemeBrush("MutedTextBrush"), VerticalAlignment = VerticalAlignment.Center };
            default:
                return CreateTextBox(field.Value, false, 22);
        }
    }

    private static FrameworkElement CreateVectorEditor(SerializedFieldInfo field)
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
            var input = CreateTextBox(child.Value, false, 22);
            input.MinWidth = 35;
            Grid.SetColumn(input, i * 2 + 1);
            grid.Children.Add(input);
        }
        return grid;
    }

    private static FrameworkElement CreateColorEditor(SerializedFieldInfo field)
    {
        var panel = new Grid();
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var swatch = new Border
        {
            Margin = new Thickness(0, 1, 5, 1),
            Background = ParseColor(field.Value),
            Style = ThemeStyle("InspectorColorSwatchStyle")
        };
        panel.Children.Add(swatch);
        var input = CreateTextBox(field.Value, false, 22);
        Grid.SetColumn(input, 1);
        panel.Children.Add(input);
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

    private static FrameworkElement CreateObjectReference(SerializedFieldInfo field)
    {
        var border = new Border
        {
            Height = 22,
            Style = ThemeStyle("InspectorObjectReferenceStyle")
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(new TextBlock { Text = field.Value ?? "None", VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
        if (field.InstanceID.HasValue)
        {
            var id = new TextBlock { Text = field.InstanceID.Value.ToString(CultureInfo.InvariantCulture), Foreground = ThemeBrush("MutedTextBrush"), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(id, 1);
            grid.Children.Add(id);
        }
        border.Child = grid;
        return border;
    }

    private static FrameworkElement CreateRangeEditor(SerializedFieldInfo field)
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
            IsHitTestVisible = false,
            Focusable = false,
            Margin = new Thickness(2, 0, 6, 0)
        };
        grid.Children.Add(slider);
        var input = CreateTextBox(field.Value, false, 22);
        Grid.SetColumn(input, 1);
        grid.Children.Add(input);
        return grid;
    }

    private static TextBox CreateTextBox(string? value, bool multiline, double height) => new()
    {
        Text = value ?? string.Empty,
        IsReadOnly = true,
        Height = height,
        MinWidth = 20,
        Style = ThemeStyle("InspectorReadOnlyTextBoxStyle"),
        VerticalContentAlignment = multiline ? VerticalAlignment.Top : VerticalAlignment.Center,
        TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
        AcceptsReturn = multiline,
        VerticalScrollBarVisibility = multiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Hidden
    };

    private static Brush ThemeBrush(string key) => (Brush)Application.Current.FindResource(key);

    private static Style ThemeStyle(string key) => (Style)Application.Current.FindResource(key);
}
