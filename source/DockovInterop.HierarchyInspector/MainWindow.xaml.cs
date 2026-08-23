using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace DockovInterop.HierarchyInspector;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += async (_, _) => await _viewModel.RefreshAsync();
    }

    private void Hierarchy_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is HierarchyItem { GameObject: not null } item)
        {
            _viewModel.SelectedObject = item.GameObject;
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _viewModel.Dispose();
        base.OnClosing(e);
    }
}
