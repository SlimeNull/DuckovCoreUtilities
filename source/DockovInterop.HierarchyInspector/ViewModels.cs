using SlimeNull.DuckovInterop;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
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

public sealed class InspectorGameObjectViewModel : INotifyPropertyChanged
{
    private readonly MainViewModel _owner;
    private IReadOnlyList<InspectorComponentViewModel> _components;
    private bool _activeSelf;
    private bool _isUpdatingActive;
    private bool _detailsLoaded;
    private bool _detailsLoading;
    private string? _activationError;

    internal InspectorGameObjectViewModel(InspectorGameObject source, MainViewModel owner)
    {
        Source = source;
        _owner = owner;
        _activeSelf = source.ActiveSelf;
        _components = CreateComponentViewModels(source.Components);
    }

    public InspectorGameObject Source { get; }
    public string Name => Source.Name ?? "GameObject";
    public int InstanceID => Source.InstanceID;
    public int ComponentCount => Source.ComponentCount;
    public bool ActiveSelf
    {
        get => _activeSelf;
        set
        {
            if (_activeSelf == value || _isUpdatingActive)
            {
                return;
            }

            var previous = _activeSelf;
            _activeSelf = value;
            OnPropertyChanged();
            _ = UpdateActiveAsync(value, previous);
        }
    }
    public string Tag => Source.Tag ?? "Untagged";
    public int Layer => Source.Layer;
    public IReadOnlyList<InspectorComponentViewModel> Components => _components;
    public bool CanEditActive => !_isUpdatingActive;
    public string? ActivationError => _activationError;
    public Visibility ActivationErrorVisibility => string.IsNullOrWhiteSpace(ActivationError) ? Visibility.Collapsed : Visibility.Visible;

    internal bool TryBeginDetailsLoad(bool forceRefresh = false)
    {
        if (_detailsLoading || (_detailsLoaded && !forceRefresh))
        {
            return false;
        }

        _detailsLoading = true;
        return true;
    }

    internal void ApplyDetails(List<InspectorComponent> components)
    {
        Source.Components = components;
        Source.ComponentCount = components.Count;
        _components = CreateComponentViewModels(components);
        _detailsLoaded = true;
        _detailsLoading = false;
        OnPropertyChanged(nameof(ComponentCount));
        OnPropertyChanged(nameof(Components));
    }

    internal void EndDetailsLoad()
    {
        _detailsLoading = false;
    }

    private IReadOnlyList<InspectorComponentViewModel> CreateComponentViewModels(IEnumerable<InspectorComponent> components)
    {
        return components.Select(component => new InspectorComponentViewModel(component, _owner)).ToList();
    }

    private async Task UpdateActiveAsync(bool value, bool previous)
    {
        _isUpdatingActive = true;
        _activationError = null;
        OnPropertyChanged(nameof(CanEditActive));
        OnPropertyChanged(nameof(ActivationError));
        OnPropertyChanged(nameof(ActivationErrorVisibility));

        var error = await _owner.SetGameObjectActiveAsync(Source.InstanceID, value);
        if (error != null)
        {
            _activationError = error;
            _activeSelf = previous;
            OnPropertyChanged(nameof(ActiveSelf));
        }
        else
        {
            Source.ActiveSelf = value;
        }

        _isUpdatingActive = false;
        OnPropertyChanged(nameof(CanEditActive));
        OnPropertyChanged(nameof(ActivationError));
        OnPropertyChanged(nameof(ActivationErrorVisibility));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class InspectorComponentViewModel : INotifyPropertyChanged
{
    private readonly MainViewModel _owner;
    private IReadOnlyList<InspectorFieldViewModel>? _fields;
    private bool? _enabled;
    private bool _isUpdatingEnabled;
    private string? _editError;

    internal InspectorComponentViewModel(InspectorComponent source, MainViewModel owner)
    {
        Source = source;
        _owner = owner;
        _enabled = source.Enabled;
    }

    public InspectorComponent Source { get; }
    public string Name => Source.Name ?? "Component";
    public string ShortType => (Source.Type ?? string.Empty).Split('.').LastOrDefault() ?? string.Empty;
    public bool? Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value || !value.HasValue || _isUpdatingEnabled)
            {
                return;
            }

            var previous = _enabled;
            _enabled = value;
            OnPropertyChanged();
            _ = UpdateEnabledAsync(value.Value, previous);
        }
    }

    public string? Error => _editError ?? Source.Error;
    public IReadOnlyList<InspectorFieldViewModel> Fields => _fields ??= Source.Fields.Select(field => new InspectorFieldViewModel(field, Source.InstanceID, _owner)).ToList();
    public Visibility EnabledVisibility => Source.Enabled.HasValue ? Visibility.Visible : Visibility.Hidden;
    public bool CanEditEnabled => Source.Enabled.HasValue && !_isUpdatingEnabled;
    public Visibility ErrorVisibility => string.IsNullOrWhiteSpace(Error) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility EmptyFieldsVisibility => Source.Fields.Count == 0 && string.IsNullOrWhiteSpace(Source.Error) ? Visibility.Visible : Visibility.Collapsed;

    private async Task UpdateEnabledAsync(bool value, bool? previous)
    {
        _isUpdatingEnabled = true;
        _editError = null;
        OnPropertyChanged(nameof(CanEditEnabled));
        OnPropertyChanged(nameof(Error));
        OnPropertyChanged(nameof(ErrorVisibility));

        var error = await _owner.SetRemoteValueAsync(Source.InstanceID, "enabled", value ? "true" : "false");
        if (error != null)
        {
            _editError = error;
            _enabled = previous;
            OnPropertyChanged(nameof(Enabled));
        }
        else
        {
            Source.Enabled = value;
        }

        _isUpdatingEnabled = false;
        OnPropertyChanged(nameof(CanEditEnabled));
        OnPropertyChanged(nameof(Error));
        OnPropertyChanged(nameof(ErrorVisibility));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class InspectorFieldViewModel : INotifyPropertyChanged
{
    private readonly MainViewModel _owner;
    private readonly int _componentInstanceId;
    private readonly InspectorFieldViewModel? _parent;
    private IReadOnlyList<InspectorFieldViewModel>? _children;
    private string? _error;
    private bool _isBusy;

    internal InspectorFieldViewModel(SerializedFieldInfo source, int componentInstanceId, MainViewModel owner, InspectorFieldViewModel? parent = null)
    {
        Source = source;
        _componentInstanceId = componentInstanceId;
        _owner = owner;
        _parent = parent;
    }

    public SerializedFieldInfo Source { get; }
    public string? Name => Source.Name;
    public string? DisplayName => Source.DisplayName;
    public string? Type => Source.Type;
    public string? Path => Source.Path;
    public string? Kind => Source.Kind;
    public string? Value => Source.Value;
    public int? InstanceID => Source.InstanceID;
    public string? ObjectName => Source.ObjectName;
    public string? Header => Source.Header;
    public string? Tooltip => Source.Tooltip;
    public float? RangeMin => Source.RangeMin;
    public float? RangeMax => Source.RangeMax;
    public bool Multiline => Source.Multiline;
    public int? TextAreaMinLines => Source.TextAreaMinLines;
    public int? TextAreaMaxLines => Source.TextAreaMaxLines;
    public IReadOnlyList<string> EnumNames => Source.EnumNames;
    public IReadOnlyList<InspectorFieldViewModel> Children => _children ??= Source.Children.Select(child => new InspectorFieldViewModel(child, _componentInstanceId, _owner, this)).ToList();
    public bool CanWrite => Source.CanWrite && !string.IsNullOrWhiteSpace(Source.Path);
    public bool IsBusy { get => _isBusy; private set { if (_isBusy == value) return; _isBusy = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanEdit)); } }
    public bool CanEdit => CanWrite && !IsBusy;
    public string? Error { get => _error; private set { if (_error == value) return; _error = value; OnPropertyChanged(); OnPropertyChanged(nameof(ErrorVisibility)); } }
    public Visibility ErrorVisibility => string.IsNullOrWhiteSpace(Error) ? Visibility.Collapsed : Visibility.Visible;

    public async Task<bool> CommitAsync(string displayValue, string valueJson)
    {
        if (!CanEdit || string.IsNullOrWhiteSpace(Path))
        {
            return false;
        }

        IsBusy = true;
        Error = null;
        var error = await _owner.SetRemoteValueAsync(_componentInstanceId, Path, valueJson);
        if (error != null)
        {
            Error = error;
            IsBusy = false;
            return false;
        }

        Source.Value = displayValue;
        if (Kind == "ObjectReference")
        {
            Source.InstanceID = int.TryParse(displayValue, out var id) ? id : null;
            Source.ObjectName = null;
            Source.Value = Source.InstanceID.HasValue ? displayValue : "None";
        }

        _parent?.OnChildValueChanged();
        OnPropertyChanged(nameof(Value));
        OnPropertyChanged(nameof(InstanceID));
        IsBusy = false;
        return true;
    }

    private void OnChildValueChanged()
    {
        if (Kind == "Color")
        {
            Source.Value = string.Join(",", Children.Select(child => child.Value));
            OnPropertyChanged(nameof(Value));
        }
        _parent?.OnChildValueChanged();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

internal sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly DuckovRpcConnection _connection = new();
    private readonly SemaphoreSlim _rpcGate = new(1, 1);
    private readonly AsyncCommand _refreshCommand;
    private readonly AsyncCommand _refreshComponentsCommand;
    private SceneSnapshot? _snapshot;
    private string _filterText = string.Empty;
    private bool _hideDisabledObjects;
    private bool _hideObjectsWithoutRenderers;
    private bool _hideNonUiObjects;
    private InspectorGameObjectViewModel? _selectedObject;
    private bool _isBusy;
    private string _statusText = "Ready";
    private string _connectionText = "Disconnected";
    private string _snapshotTimeText = string.Empty;
    private Brush _connectionBrush = new SolidColorBrush(Color.FromRgb(130, 130, 130));
    private int _selectionVersion;
    private int _componentLoadsInProgress;

    public MainViewModel()
    {
        _refreshCommand = new AsyncCommand(RefreshAsync, () => !IsBusy);
        _refreshComponentsCommand = new AsyncCommand(
            RefreshComponentsAsync,
            () => SelectedObject != null && !IsBusy && _componentLoadsInProgress == 0);
    }

    public ObservableCollection<HierarchyItem> Hierarchy { get; } = new();
    public ICommand RefreshCommand => _refreshCommand;
    public ICommand RefreshComponentsCommand => _refreshComponentsCommand;

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

    public bool HideDisabledObjects
    {
        get => _hideDisabledObjects;
        set
        {
            if (Set(ref _hideDisabledObjects, value))
            {
                RebuildHierarchy();
            }
        }
    }

    public bool HideObjectsWithoutRenderers
    {
        get => _hideObjectsWithoutRenderers;
        set
        {
            if (Set(ref _hideObjectsWithoutRenderers, value))
            {
                RebuildHierarchy();
            }
        }
    }

    public bool HideNonUiObjects
    {
        get => _hideNonUiObjects;
        set
        {
            if (Set(ref _hideNonUiObjects, value))
            {
                RebuildHierarchy();
            }
        }
    }

    public InspectorGameObjectViewModel? SelectedObject
    {
        get => _selectedObject;
        set
        {
            if (Set(ref _selectedObject, value))
            {
                OnPropertyChanged(nameof(EmptySelectionVisibility));
                OnPropertyChanged(nameof(InspectorVisibility));
                _refreshComponentsCommand.RaiseCanExecuteChanged();
                StatusText = value == null
                    ? SnapshotSummary()
                    : $"GameObject {value.InstanceID} | {value.ComponentCount} components";

                var selectionVersion = ++_selectionVersion;
                if (value != null)
                {
                    _ = LoadObjectDetailsAsync(value, selectionVersion);
                }
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
                _refreshComponentsCommand.RaiseCanExecuteChanged();
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
        StatusText = "Requesting scene hierarchy...";
        var selectedId = SelectedObject?.InstanceID;
        try
        {
            var result = await Task.Run(() => _connection.Invoke(api => api.GetSceneOverview()));
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
            StatusText = SnapshotSummary();
            SelectedObject = selectedId.HasValue ? FindObject(selectedId.Value) : null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshComponentsAsync()
    {
        var target = SelectedObject;
        if (target == null || IsBusy)
        {
            return;
        }

        await LoadObjectDetailsAsync(target, _selectionVersion, forceRefresh: true);
    }

    private async Task LoadObjectDetailsAsync(InspectorGameObjectViewModel target, int selectionVersion, bool forceRefresh = false)
    {
        if (!target.TryBeginDetailsLoad(forceRefresh))
        {
            return;
        }

        _componentLoadsInProgress++;
        _refreshComponentsCommand.RaiseCanExecuteChanged();

        if (ReferenceEquals(SelectedObject, target))
        {
            StatusText = $"Loading components for GameObject {target.InstanceID}...";
        }

        try
        {
            var result = await Task.Run(() =>
                _connection.Invoke(api => api.GetInspectorComponents(target.InstanceID.ToString())));

            if (result.Ok && result.Data != null)
            {
                target.ApplyDetails(result.Data);
            }

            if (selectionVersion != _selectionVersion || !ReferenceEquals(SelectedObject, target))
            {
                return;
            }

            StatusText = result.Ok
                ? $"GameObject {target.InstanceID} | {target.ComponentCount} components"
                : result.Error ?? $"Failed to load GameObject {target.InstanceID}.";
        }
        finally
        {
            target.EndDetailsLoad();
            _componentLoadsInProgress--;
            _refreshComponentsCommand.RaiseCanExecuteChanged();
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
            var sceneMatches = Contains(scene.Name, FilterText);
            var includeAllForText = sceneMatches && !string.IsNullOrWhiteSpace(FilterText);
            var children = scene.Roots
                .Select(root => BuildGameObjectItem(root, includeAllForText))
                .Where(item => item != null)
                .Cast<HierarchyItem>()
                .ToList();
            if (!sceneMatches && children.Count == 0)
            {
                continue;
            }

            if (children.Count == 0 &&
                (HideDisabledObjects || HideObjectsWithoutRenderers || HideNonUiObjects))
            {
                continue;
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

    private HierarchyItem? BuildGameObjectItem(InspectorGameObject source, bool includeAllForText = false)
    {
        var matchesText = includeAllForText || Contains(source.Name, FilterText);
        var includeChildrenForText = includeAllForText ||
            (matchesText && !string.IsNullOrWhiteSpace(FilterText));
        var children = source.Children
            .Select(child => BuildGameObjectItem(child, includeChildrenForText))
            .Where(item => item != null)
            .Cast<HierarchyItem>()
            .ToList();

        var matchesFilters = (!HideDisabledObjects || source.ActiveInHierarchy) &&
            (!HideObjectsWithoutRenderers || source.HasRenderer) &&
            (!HideNonUiObjects || source.IsGUI);
        if ((!matchesText || !matchesFilters) && children.Count == 0)
        {
            return null;
        }

        var viewModel = new InspectorGameObjectViewModel(source, this);
        return new HierarchyItem
        {
            Name = viewModel.Name,
            Detail = $"Instance ID {source.InstanceID} | {source.ComponentCount} components",
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
        return $"{_snapshot.Scenes.Count} scenes | {objects.Count} GameObjects | {objects.Sum(item => item.ComponentCount)} components";
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

    internal async Task<string?> SetRemoteValueAsync(int instanceId, string path, string valueJson)
    {
        await _rpcGate.WaitAsync();
        try
        {
            var result = await Task.Run(() => _connection.Invoke(api => api.SetValue(instanceId.ToString(), path, valueJson, storeResult: false)));
            if (!result.Ok)
            {
                StatusText = result.Error ?? $"Failed to update {path}.";
                return StatusText;
            }

            StatusText = $"Updated {path} on component {instanceId}";
            return null;
        }
        finally
        {
            _rpcGate.Release();
        }
    }

    internal async Task<string?> SetGameObjectActiveAsync(int instanceId, bool active)
    {
        await _rpcGate.WaitAsync();
        try
        {
            var result = await Task.Run(() => _connection.Invoke(api => api.SetGameObjectActive(instanceId.ToString(), active)));
            if (!result.Ok)
            {
                StatusText = result.Error ?? $"Failed to update GameObject {instanceId}.";
                return StatusText;
            }

            StatusText = $"Set GameObject {instanceId} activeSelf to {active}";
            return null;
        }
        finally
        {
            _rpcGate.Release();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    public void Dispose()
    {
        _connection.Dispose();
        _rpcGate.Dispose();
    }
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
