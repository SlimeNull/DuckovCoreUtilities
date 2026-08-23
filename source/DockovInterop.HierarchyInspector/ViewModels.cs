using SlimeNull.DuckovInterop;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace DockovInterop.HierarchyInspector;

public sealed class HierarchyItem
{
    public required string Name { get; init; }
    public string Detail { get; init; } = string.Empty;
    public InspectorGameObjectViewModel? GameObject { get; init; }
    public ObservableCollection<HierarchyItem> Children { get; init; } = new();
    public Brush MarkerBrush { get; init; } = Brushes.Gray;
    public Brush ForegroundBrush { get; init; } = new SolidColorBrush(Color.FromRgb(216, 216, 216));
}

public sealed class InspectorGameObjectViewModel
{
    public InspectorGameObjectViewModel(InspectorGameObject source)
    {
        Source = source;
        Components = source.Components.Select(component => new InspectorComponentViewModel(component)).ToList();
    }

    public InspectorGameObject Source { get; }
    public string Name => Source.Name ?? "GameObject";
    public int InstanceID => Source.InstanceID;
    public bool ActiveSelf => Source.ActiveSelf;
    public string Tag => Source.Tag ?? "Untagged";
    public int Layer => Source.Layer;
    public IReadOnlyList<InspectorComponentViewModel> Components { get; }
}

public sealed class InspectorComponentViewModel
{
    public InspectorComponentViewModel(InspectorComponent source) => Source = source;

    public InspectorComponent Source { get; }
    public string Name => Source.Name ?? "Component";
    public string ShortType => (Source.Type ?? string.Empty).Split('.').LastOrDefault() ?? string.Empty;
    public bool? Enabled => Source.Enabled;
    public string? Error => Source.Error;
    public IReadOnlyList<SerializedFieldInfo> Fields => Source.Fields;
    public Visibility EnabledVisibility => Source.Enabled.HasValue ? Visibility.Visible : Visibility.Hidden;
    public Visibility ErrorVisibility => string.IsNullOrWhiteSpace(Source.Error) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility EmptyFieldsVisibility => Source.Fields.Count == 0 && string.IsNullOrWhiteSpace(Source.Error) ? Visibility.Visible : Visibility.Collapsed;
}

internal sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly DuckovRpcConnection _connection = new();
    private readonly AsyncCommand _refreshCommand;
    private SceneSnapshot? _snapshot;
    private string _filterText = string.Empty;
    private InspectorGameObjectViewModel? _selectedObject;
    private bool _isBusy;
    private string _statusText = "Ready";
    private string _connectionText = "Disconnected";
    private string _snapshotTimeText = string.Empty;
    private Brush _connectionBrush = new SolidColorBrush(Color.FromRgb(130, 130, 130));

    public MainViewModel()
    {
        _refreshCommand = new AsyncCommand(RefreshAsync, () => !IsBusy);
    }

    public ObservableCollection<HierarchyItem> Hierarchy { get; } = new();
    public ICommand RefreshCommand => _refreshCommand;

    public string FilterText
    {
        get => _filterText;
        set
        {
            if (Set(ref _filterText, value))
            {
                OnPropertyChanged(nameof(IsFilterHintVisible));
                RebuildHierarchy();
            }
        }
    }

    public Visibility IsFilterHintVisible => string.IsNullOrEmpty(FilterText) ? Visibility.Visible : Visibility.Collapsed;

    public InspectorGameObjectViewModel? SelectedObject
    {
        get => _selectedObject;
        set
        {
            if (Set(ref _selectedObject, value))
            {
                OnPropertyChanged(nameof(EmptySelectionVisibility));
                OnPropertyChanged(nameof(InspectorVisibility));
                StatusText = value == null
                    ? SnapshotSummary()
                    : $"GameObject {value.InstanceID} | {value.Components.Count} components";
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (Set(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(BusyVisibility));
                _refreshCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }
    public string ConnectionText { get => _connectionText; private set => Set(ref _connectionText, value); }
    public string SnapshotTimeText { get => _snapshotTimeText; private set => Set(ref _snapshotTimeText, value); }
    public Brush ConnectionBrush { get => _connectionBrush; private set => Set(ref _connectionBrush, value); }
    public Visibility EmptySelectionVisibility => SelectedObject == null ? Visibility.Visible : Visibility.Collapsed;
    public Visibility InspectorVisibility => SelectedObject != null ? Visibility.Visible : Visibility.Collapsed;
    public Visibility BusyVisibility => IsBusy ? Visibility.Visible : Visibility.Collapsed;

    public async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusText = "Requesting a complete scene snapshot...";
        var selectedId = SelectedObject?.InstanceID;
        try
        {
            var result = await Task.Run(() => _connection.Invoke(api => api.GetSceneSnapshot()));
            if (!result.Ok || result.Data == null)
            {
                ConnectionText = "Disconnected";
                ConnectionBrush = new SolidColorBrush(Color.FromRgb(192, 78, 72));
                StatusText = result.Error ?? "DuckovInterop returned no scene data.";
                return;
            }

            _snapshot = result.Data;
            ConnectionText = "Connected";
            ConnectionBrush = new SolidColorBrush(Color.FromRgb(92, 166, 96));
            SnapshotTimeText = DateTime.TryParse(result.Data.CapturedAtUtc, out var captured)
                ? "Captured " + captured.ToLocalTime().ToString("HH:mm:ss")
                : string.Empty;
            RebuildHierarchy();
            SelectedObject = selectedId.HasValue ? FindObject(selectedId.Value) : null;
            StatusText = SnapshotSummary();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RebuildHierarchy()
    {
        Hierarchy.Clear();
        if (_snapshot == null)
        {
            return;
        }

        foreach (var scene in _snapshot.Scenes)
        {
            var children = scene.Roots.Select(root => BuildGameObjectItem(root)).Where(item => item != null).Cast<HierarchyItem>().ToList();
            var sceneMatches = Contains(scene.Name, FilterText);
            if (!sceneMatches && children.Count == 0)
            {
                continue;
            }

            if (sceneMatches && !string.IsNullOrWhiteSpace(FilterText))
            {
                children = scene.Roots.Select(root => BuildGameObjectItem(root, includeAll: true)!).ToList();
            }

            Hierarchy.Add(new HierarchyItem
            {
                Name = scene.Name ?? "Untitled Scene",
                Detail = $"Scene | Build index {scene.BuildIndex}",
                MarkerBrush = new SolidColorBrush(Color.FromRgb(91, 151, 190)),
                Children = new ObservableCollection<HierarchyItem>(children)
            });
        }
    }

    private HierarchyItem? BuildGameObjectItem(InspectorGameObject source, bool includeAll = false)
    {
        var children = source.Children.Select(child => BuildGameObjectItem(child, includeAll)).Where(item => item != null).Cast<HierarchyItem>().ToList();
        var matches = includeAll || Contains(source.Name, FilterText);
        if (!matches && children.Count == 0)
        {
            return null;
        }

        if (matches && !includeAll && !string.IsNullOrWhiteSpace(FilterText))
        {
            children = source.Children.Select(child => BuildGameObjectItem(child, includeAll: true)!).ToList();
        }

        var viewModel = new InspectorGameObjectViewModel(source);
        return new HierarchyItem
        {
            Name = viewModel.Name,
            Detail = $"Instance ID {source.InstanceID} | {source.Components.Count} components",
            GameObject = viewModel,
            MarkerBrush = new SolidColorBrush(source.ActiveSelf ? Color.FromRgb(226, 172, 73) : Color.FromRgb(110, 105, 95)),
            ForegroundBrush = new SolidColorBrush(source.ActiveInHierarchy ? Color.FromRgb(216, 216, 216) : Color.FromRgb(125, 125, 125)),
            Children = new ObservableCollection<HierarchyItem>(children)
        };
    }

    private InspectorGameObjectViewModel? FindObject(int instanceId)
    {
        return Flatten(Hierarchy).Select(item => item.GameObject).FirstOrDefault(item => item?.InstanceID == instanceId);
    }

    private static IEnumerable<HierarchyItem> Flatten(IEnumerable<HierarchyItem> items)
    {
        foreach (var item in items)
        {
            yield return item;
            foreach (var child in Flatten(item.Children))
            {
                yield return child;
            }
        }
    }

    private string SnapshotSummary()
    {
        if (_snapshot == null)
        {
            return StatusText;
        }

        var objects = _snapshot.Scenes.SelectMany(scene => FlattenObjects(scene.Roots)).ToList();
        return $"{_snapshot.Scenes.Count} scenes | {objects.Count} GameObjects | {objects.Sum(item => item.Components.Count)} components";
    }

    private static IEnumerable<InspectorGameObject> FlattenObjects(IEnumerable<InspectorGameObject> roots)
    {
        foreach (var root in roots)
        {
            yield return root;
            foreach (var child in FlattenObjects(root.Children))
            {
                yield return child;
            }
        }
    }

    private static bool Contains(string? value, string query) => string.IsNullOrWhiteSpace(query) || (value?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    public void Dispose() => _connection.Dispose();
}

internal sealed class AsyncCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool> _canExecute;

    public AsyncCommand(Func<Task> execute, Func<bool> canExecute)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute();
    public async void Execute(object? parameter) => await _execute();
    public event EventHandler? CanExecuteChanged;
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
