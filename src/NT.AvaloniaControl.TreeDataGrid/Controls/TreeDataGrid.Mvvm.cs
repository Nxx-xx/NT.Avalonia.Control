using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Data;
using Avalonia.Threading;

namespace NT.AvaloniaControl.TreeDataGrid;

/// <summary>A nonvisual command endpoint that a view model can own and bind to one grid.</summary>
public sealed class TreeDataGridEditCommands : INotifyPropertyChanged
{
    private TreeDataGrid? _owner;
    private TreeDataGridEditSession? _session;
    private readonly EditCommand _begin;
    private readonly EditCommand _commit;
    private readonly EditCommand _cancel;
    public TreeDataGridEditCommands()
    {
        _begin = new(parameter => _owner?.CanBeginMvvmEdit(parameter) == true, parameter => _owner?.BeginMvvmEdit(parameter));
        _commit = new(_ => _owner?.EditSession is not null && !_owner.IsInputMethodComposing, _ => _owner?.CommitEdit());
        _cancel = new(_ => _owner?.EditSession is not null, _ => _owner?.CancelEdit());
    }
    public ICommand BeginEditCommand => _begin;
    public ICommand CommitEditCommand => _commit;
    public ICommand CancelEditCommand => _cancel;
    public bool IsEditing => _session is not null;
    public TreeDataGridEditSession? EditSession => _session;
    public event PropertyChangedEventHandler? PropertyChanged;
    internal void Attach(TreeDataGrid owner)
    {
        if (_owner is not null && !ReferenceEquals(_owner, owner)) throw new InvalidOperationException("An edit command controller can be attached to only one TreeDataGrid at a time.");
        _owner = owner; Update();
    }
    internal void Detach(TreeDataGrid owner) { if (ReferenceEquals(_owner, owner)) { _owner = null; Update(); } }
    internal void Update()
    {
        var session = _owner?.EditSession;
        if (!ReferenceEquals(_session, session))
        {
            var wasEditing = IsEditing; _session = session;
            PropertyChanged?.Invoke(this, new(nameof(EditSession)));
            if (wasEditing != IsEditing) PropertyChanged?.Invoke(this, new(nameof(IsEditing)));
        }
        _begin.Raise(); _commit.Raise(); _cancel.Raise();
    }
    private sealed class EditCommand(Func<object?, bool> canExecute, Action<object?> execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => canExecute(parameter);
        public void Execute(object? parameter) { if (CanExecute(parameter)) execute(parameter); }
        internal void Raise() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}

public partial class TreeDataGrid
{
    public static readonly StyledProperty<object?> SelectedItemProperty = AvaloniaProperty.Register<TreeDataGrid, object?>(nameof(SelectedItem), defaultBindingMode: BindingMode.TwoWay);
    public static readonly StyledProperty<IndexPath> SelectedIndexProperty = AvaloniaProperty.Register<TreeDataGrid, IndexPath>(nameof(SelectedIndex), defaultBindingMode: BindingMode.TwoWay);
    public static readonly StyledProperty<IList?> SelectedItemsProperty = AvaloniaProperty.Register<TreeDataGrid, IList?>(nameof(SelectedItems), defaultBindingMode: BindingMode.TwoWay);
    public static readonly StyledProperty<CellAddress?> CurrentCellProperty = AvaloniaProperty.Register<TreeDataGrid, CellAddress?>(nameof(CurrentCell), defaultBindingMode: BindingMode.TwoWay);
    public static readonly StyledProperty<TreeDataGridEditCommands?> EditCommandsProperty = AvaloniaProperty.Register<TreeDataGrid, TreeDataGridEditCommands?>(nameof(EditCommands));
    public static readonly DirectProperty<TreeDataGrid, bool> IsEditingProperty = AvaloniaProperty.RegisterDirect<TreeDataGrid, bool>(nameof(IsEditing), owner => owner.IsEditing);
    public static readonly DirectProperty<TreeDataGrid, TreeDataGridEditSession?> EditSessionProperty = AvaloniaProperty.RegisterDirect<TreeDataGrid, TreeDataGridEditSession?>(nameof(EditSession), owner => owner.EditSession);
    public static readonly DirectProperty<TreeDataGrid, ICommand> BeginEditCommandProperty = AvaloniaProperty.RegisterDirect<TreeDataGrid, ICommand>(nameof(BeginEditCommand), owner => owner.BeginEditCommand);
    public static readonly DirectProperty<TreeDataGrid, ICommand> CommitEditCommandProperty = AvaloniaProperty.RegisterDirect<TreeDataGrid, ICommand>(nameof(CommitEditCommand), owner => owner.CommitEditCommand);
    public static readonly DirectProperty<TreeDataGrid, ICommand> CancelEditCommandProperty = AvaloniaProperty.RegisterDirect<TreeDataGrid, ICommand>(nameof(CancelEditCommand), owner => owner.CancelEditCommand);

    public object? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }
    public IndexPath SelectedIndex { get => GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }
    public IList? SelectedItems { get => GetValue(SelectedItemsProperty); set => SetValue(SelectedItemsProperty, value); }
    public CellAddress? CurrentCell { get => GetValue(CurrentCellProperty); set => SetValue(CurrentCellProperty, value); }
    public TreeDataGridEditCommands? EditCommands { get => GetValue(EditCommandsProperty); set => SetValue(EditCommandsProperty, value); }
    public bool IsEditing => _isEditing;
    public TreeDataGridEditSession? EditSession => _mvvmEditSession;
    public ICommand BeginEditCommand => ActiveEditCommands.BeginEditCommand;
    public ICommand CommitEditCommand => ActiveEditCommands.CommitEditCommand;
    public ICommand CancelEditCommand => ActiveEditCommands.CancelEditCommand;

    private bool _isEditing;
    private TreeDataGridEditSession? _mvvmEditSession;
    private TreeDataGridEditCommands? _defaultEditCommands;
    private TreeDataGridEditCommands? _attachedEditCommands;
    private TreeDataGridEditCommands ActiveEditCommands => EditCommands ?? (_defaultEditCommands ??= new());
    private INotifyCollectionChanged? _selectedItemsNotifier;
    private bool _mvvmSynchronizing;
    private bool _mvvmDataContextUpdating;
    private bool _mvvmRefreshPending;
    private bool _writingItems;
    private int _selectedItemsNotificationDepth;
    private bool _itemsOutputScheduled;
    private bool _updatingEditCommands;
    private bool _editCommandsUpdatePending;
    private bool _mvvmAttached;
    private object?[]? _detachedSelectedItems;
    private AvaloniaProperty? _mvvmOutputProperty;
    private object? _mvvmOutputValue;
    private (AvaloniaProperty Property, object? Value)? _selectionInput;
    private bool _selectionInputFromSource;
    private CellAddress? _currentInput;
    private bool _hasCurrentInput;
    private NodeId? _primarySelectedNode;

    protected override void OnDataContextBeginUpdate()
    {
        _mvvmDataContextUpdating = true;
        base.OnDataContextBeginUpdate();
    }
    protected override void OnDataContextEndUpdate()
    {
        base.OnDataContextEndUpdate();
        _mvvmDataContextUpdating = false;
        SynchronizeMvvm();
    }

    private void OnMvvmPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        var property = change.Property;
        if (property == SelectedItemsProperty) SubscribeSelectedItems();
        if (property == EditCommandsProperty) { if (!_mvvmDataContextUpdating) UpdateEditCommands(); return; }
        if (property == IsReadOnlyProperty) { UpdateEditCommands(); return; }
        if (property != SelectedItemProperty && property != SelectedIndexProperty && property != SelectedItemsProperty && property != CurrentCellProperty) return;
        var value = GetValue(property);
        if (property == _mvvmOutputProperty && SameMvvmValue(property, value, _mvvmOutputValue)) return;
        if (property == CurrentCellProperty) { _currentInput = (CellAddress?)value; _hasCurrentInput = true; }
        else
        {
            // Empty defaults of secondary mirrors must not erase an explicit selection while all
            // DataContext bindings are being transferred to a new view model.
            if (!_mvvmDataContextUpdating || _selectionInputFromSource || _selectionInput is not { } pending || !HasSelectionValue(pending.Property, pending.Value) || HasSelectionValue(property, value))
                _selectionInput = (property, value);
            _selectionInputFromSource = false;
        }
        SynchronizeMvvm();
    }
    private static bool HasSelectionValue(AvaloniaProperty property, object? value) => property == SelectedIndexProperty ? value is IndexPath { Count: > 0 } : property == SelectedItemsProperty ? value is ICollection { Count: > 0 } : value is not null;
    private static bool SameMvvmValue(AvaloniaProperty property, object? a, object? b) =>
        property == SelectedIndexProperty || property == CurrentCellProperty ? Equals(a, b) : ReferenceEquals(a, b);
    private void SubscribeSelectedItems()
    {
        if (_selectedItemsNotifier is not null) _selectedItemsNotifier.CollectionChanged -= SelectedItemsChanged;
        _selectedItemsNotifier = _mvvmAttached ? SelectedItems as INotifyCollectionChanged : null;
        if (_selectedItemsNotifier is not null) _selectedItemsNotifier.CollectionChanged += SelectedItemsChanged;
    }
    private void SelectedItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_writingItems) return;
        ++_selectedItemsNotificationDepth;
        try { _selectionInput = (SelectedItemsProperty, SelectedItems); SynchronizeMvvm(); }
        finally { --_selectedItemsNotificationDepth; }
    }
    private void MvvmSourceChanged(bool hadSource)
    {
        _primarySelectedNode = null;
        if (hadSource && Source is null)
        {
            _selectionInput = null; _selectionInputFromSource = false;
            _currentInput = null; _hasCurrentInput = false;
            SynchronizeMvvm(); return;
        }
        // Remap a surviving model to the replacement source; never reuse a stale path for another model.
        if (_selectionInput is null)
        {
            _selectionInput = SelectedItems is { Count: > 0 } items ? (SelectedItemsProperty, items.Cast<object?>().ToArray()) : (SelectedItemProperty, SelectedItem);
            _selectionInputFromSource = true;
        }
        if (!_hasCurrentInput) { _currentInput = CurrentCell; _hasCurrentInput = true; }
        SynchronizeMvvm();
    }
    private void MvvmProjectionReconciled(object? sender, EventArgs e) => SynchronizeMvvm();
    private void ResumeMvvm()
    {
        _mvvmAttached = true; SubscribeSelectedItems();
        if (_detachedSelectedItems is { } previous && SelectedItems is { } items && !ReferenceSequenceEqual(items, previous)) _selectionInput ??= (SelectedItemsProperty, items.Cast<object?>().ToArray());
        _detachedSelectedItems = null;
        SynchronizeMvvm(); UpdateEditCommands();
    }
    private void SuspendMvvm()
    {
        _detachedSelectedItems = SelectedItems?.Cast<object?>().ToArray();
        _mvvmAttached = false; SubscribeSelectedItems();
        var previous = _attachedEditCommands; _attachedEditCommands = null; previous?.Detach(this);
    }
    private void UpdateEditCommands()
    {
        _editCommandsUpdatePending = true;
        if (_updatingEditCommands) return;
        _updatingEditCommands = true;
        try
        {
            while (_editCommandsUpdatePending)
            {
                _editCommandsUpdatePending = false;
                var next = ActiveEditCommands;
                if (_mvvmAttached && !ReferenceEquals(next, _attachedEditCommands))
                {
                    var old = _attachedEditCommands;
                    _attachedEditCommands = next;
                    old?.Detach(this);
                    if (!_mvvmAttached || !ReferenceEquals(_attachedEditCommands, next)) continue;
                    try { next.Attach(this); }
                    catch
                    {
                        _attachedEditCommands = old;
                        SetCurrentValue(EditCommandsProperty, ReferenceEquals(old, _defaultEditCommands) ? null : old);
                        old?.Attach(this);
                        throw;
                    }
                    if (!_mvvmAttached || !ReferenceEquals(_attachedEditCommands, next)) { next.Detach(this); continue; }
                    if (!ReferenceEquals(ActiveEditCommands, next)) { _editCommandsUpdatePending = true; continue; }
                    if (old is not null)
                    {
                        RaisePropertyChanged(BeginEditCommandProperty, old.BeginEditCommand, next.BeginEditCommand);
                        RaisePropertyChanged(CommitEditCommandProperty, old.CommitEditCommand, next.CommitEditCommand);
                        RaisePropertyChanged(CancelEditCommandProperty, old.CancelEditCommand, next.CancelEditCommand);
                    }
                }
                _attachedEditCommands?.Update();
            }
        }
        finally { _updatingEditCommands = false; }
    }
    private void SynchronizeMvvm()
    {
        _mvvmRefreshPending = true;
        if (_mvvmSynchronizing || _mvvmDataContextUpdating) return;
        _mvvmSynchronizing = true;
        try
        {
            while (_mvvmRefreshPending)
            {
                _mvvmRefreshPending = false;
                var state = _state; var source = Source;
                if (state is not null && source is not null)
                {
                    // Two-way output setters may redirect a completed selection. Leave their input
                    // queued until the state closes its current transaction, preserving the binding.
                    if (state.IsSelecting && (_selectionInput is not null || _hasCurrentInput))
                    { _mvvmRefreshPending = true; break; }
                    if (_selectionInput is { } input) { _selectionInput = null; _selectionInputFromSource = false; ApplyMvvmSelection(state, source, input.Property, input.Value); }
                    if (!ReferenceEquals(state, _state) || !ReferenceEquals(source, Source)) { _mvvmRefreshPending = true; continue; }
                    if (_hasCurrentInput)
                    {
                        var cell = _currentInput; _hasCurrentInput = false;
                        if (cell is null || source.GetViewIndex(cell.Value.Node) >= 0 && GetColumnIndex(cell.Value.Column) >= 0)
                            state.RunSelection(TreeDataGridSelectionChangeReason.Binding, () => state.SetCurrentCell(cell));
                        else if (Columns.Count == 0) { _currentInput = cell; _hasCurrentInput = true; }
                        else state.SetCurrentCell(null);
                    }
                    PublishMvvmSelection(state, source);
                }
                else if (source is null && _selectionInput is null)
                {
                    PublishEmptyMvvmSelection();
                }
                PublishMvvmEditState();
            }
        }
        finally { _mvvmSynchronizing = false; }
    }
    private void PublishMvvmEditState()
    {
        SetAndRaise(EditSessionProperty, ref _mvvmEditSession, _state?.EditSession);
        SetAndRaise(IsEditingProperty, ref _isEditing, _mvvmEditSession is not null);
        UpdateEditCommands();
    }
    private void ApplyMvvmSelection(TreeDataGridState state, TreeDataGridSource source, AvaloniaProperty property, object? value)
    {
        var nodes = new List<NodeId>();
        if (property == SelectedIndexProperty && value is IndexPath path && path.Count > 0 && source.TryGetNodeId(path, out var id)) nodes.Add(id);
        else if (property == SelectedItemProperty && value is not null)
        {
            var row = source.Rows.FirstOrDefault(row => ReferenceEquals(row.Model, value)); if (row is not null) nodes.Add(row.Id);
        }
        else if (property == SelectedItemsProperty && value is IEnumerable items)
        {
            var old = state.SelectedNodes.ToArray();
            foreach (var item in items)
            {
                var existing = old.FirstOrDefault(id => !nodes.Contains(id) && source.TryGetRow(id, out var row) && ReferenceEquals(row!.Model, item));
                if (existing.Value > 0) nodes.Add(existing);
                else if (source.Rows.FirstOrDefault(row => !nodes.Contains(row.Id) && ReferenceEquals(row.Model, item)) is { } row) nodes.Add(row.Id);
                if (SelectionMode == TreeDataGridSelectionMode.Single && nodes.Count > 0) break;
            }
        }
        var accepted = state.RunSelection(TreeDataGridSelectionChangeReason.Binding, () =>
        {
            state.ClearSelection();
            foreach (var selectedNode in nodes) state.Select(selectedNode, additive: true);
        });
        if (accepted) _primarySelectedNode = nodes.Count > 0 ? nodes[0] : null;
    }
    private void PublishMvvmSelection(TreeDataGridState state, TreeDataGridSource source)
    {
        var nodes = state.SelectedNodes.ToArray();
        if (nodes.Length == 0 && SelectionUnit == TreeDataGridSelectionUnit.Cell) nodes = state.SelectedCells.Select(cell => cell.Node).Distinct().ToArray();
        if (_primarySelectedNode is not { } primary || !nodes.Contains(primary)) _primarySelectedNode = nodes.Length > 0 ? nodes[0] : null;
        TreeDataGridRow? selected = null;
        if (_primarySelectedNode is { } id) source.TryGetRow(id, out selected);
        PublishMvvm(SelectedItemProperty, selected?.Model);
        if (_selectionInput is not null || !ReferenceEquals(state, _state)) { _mvvmRefreshPending = true; return; }
        PublishMvvm(SelectedIndexProperty, selected?.ModelIndex ?? default);
        if (_selectionInput is not null || !ReferenceEquals(state, _state)) { _mvvmRefreshPending = true; return; }
        var models = nodes.Select(id => source.TryGetRow(id, out var row) ? row!.Model : null).Where(model => model is not null).ToArray();
        var list = SelectedItems;
        if (list is null || list.IsReadOnly || list.IsFixedSize)
        {
            if (list is null || !ReferenceSequenceEqual(list, models)) PublishMvvm(SelectedItemsProperty, new ObservableCollection<object?>(models));
        }
        else if (!ReferenceSequenceEqual(list, models))
        {
            if (DeferSelectedItemsOutput()) { if (!_hasCurrentInput) PublishMvvm(CurrentCellProperty, state.CurrentCell); return; }
            _writingItems = true;
            try
            {
                list.Clear();
                if (IsMvvmOutputCurrent(state, source))
                    foreach (var model in models) { list.Add(model); if (!IsMvvmOutputCurrent(state, source)) break; }
            }
            finally { _writingItems = false; }
            if (!IsMvvmOutputCurrent(state, source)) { _mvvmRefreshPending = true; return; }
            if (!ReferenceSequenceEqual(list, models)) { _selectionInput = (SelectedItemsProperty, list.Cast<object?>().ToArray()); _mvvmRefreshPending = true; return; }
        }
        if (!_hasCurrentInput) PublishMvvm(CurrentCellProperty, state.CurrentCell);
    }
    private bool DeferSelectedItemsOutput()
    {
        if (_selectedItemsNotificationDepth == 0) return false;
        if (!_itemsOutputScheduled)
        {
            _itemsOutputScheduled = true;
            Dispatcher.UIThread.Post(() => { _itemsOutputScheduled = false; SynchronizeMvvm(); }, DispatcherPriority.Normal);
        }
        return true;
    }
    private void PublishEmptyMvvmSelection()
    {
        PublishMvvm(SelectedItemProperty, null);
        if (Source is not null || _selectionInput is not null) return;
        PublishMvvm(SelectedIndexProperty, default(IndexPath));
        if (Source is not null || _selectionInput is not null) return;
        if (!_hasCurrentInput) PublishMvvm(CurrentCellProperty, (CellAddress?)null);
        if (Source is not null || _selectionInput is not null || SelectedItems is not { Count: > 0 } list) return;
        if (list.IsReadOnly || list.IsFixedSize) PublishMvvm(SelectedItemsProperty, new ObservableCollection<object?>());
        else if (!DeferSelectedItemsOutput())
        {
            _writingItems = true;
            try { list.Clear(); }
            finally { _writingItems = false; }
        }
    }
    private bool IsMvvmOutputCurrent(TreeDataGridState state, TreeDataGridSource source) => ReferenceEquals(state, _state) && ReferenceEquals(source, Source) && _selectionInput is null;
    private void PublishMvvm<T>(StyledProperty<T> property, T value)
    {
        var previousProperty = _mvvmOutputProperty; var previousValue = _mvvmOutputValue;
        _mvvmOutputProperty = property; _mvvmOutputValue = value;
        try { SetCurrentValue(property, value); }
        finally { _mvvmOutputProperty = previousProperty; _mvvmOutputValue = previousValue; }
    }
    private static bool ReferenceSequenceEqual(IList list, object?[] values) => list.Count == values.Length && list.Cast<object?>().Zip(values).All(pair => ReferenceEquals(pair.First, pair.Second));
    internal bool CanBeginMvvmEdit(object? parameter)
    {
        var cell = parameter is CellAddress explicitCell ? explicitCell : CurrentCell;
        if (IsReadOnly || cell is null || Source is not { } source || source.IsRefreshDeferred || source.GetViewIndex(cell.Value.Node) < 0 || !source.TryGetRow(cell.Value.Node, out var row)) return false;
        var column = Columns.FirstOrDefault(column => column.Id == cell.Value.Column);
        return column is { IsVisible: true } && !column.IsReadOnly && !column.EffectiveColumn.IsReadOnly && column.GetWriter(row!.Model) is not null;
    }
    internal void BeginMvvmEdit(object? parameter)
    {
        if ((parameter is CellAddress cell ? cell : CurrentCell) is { } address) BeginEdit(address);
    }
}
