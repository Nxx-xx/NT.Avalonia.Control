using System.ComponentModel;

namespace NT.AvaloniaControl.TreeDataGrid;

/// <summary>A stable cell address; neither member is a display index.</summary>
public readonly record struct CellAddress(NodeId Node, ColumnId Column);

/// <summary>An edit staged outside recycled controls. Validation precedes the single model write.</summary>
public sealed class TreeDataGridEditSession : INotifyPropertyChanged
{
    internal TreeDataGridEditSession(CellAddress cell, object model, object? originalValue,
        Action<object, object?> writer, Func<object?, string?>? validator)
    {
        Cell = cell;
        Model = model;
        OriginalValue = originalValue;
        Writer = writer;
        Validator = validator;
        _value = originalValue;
    }

    public CellAddress Cell { get; }
    public object? OriginalValue { get; }
    private object? _value;
    private string? _validationError;
    internal Action<TreeDataGridEditSession, object?>? ValueWriter { get; set; }
    public object? Value { get => _value; set { if (Equals(_value, value)) return; if (ValueWriter is { } writer) writer(this, value); else SetValue(value); } }
    public string? ValidationError { get => _validationError; internal set { if (_validationError == value) return; _validationError = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ValidationError))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    internal void SetValue(object? value) { _value = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value))); }
    internal TreeDataGridColumn.EditAccess? BindingAccess { get; set; }
    internal object Model { get; }
    internal Action<object, object?> Writer { get; }
    internal Func<object?, string?>? Validator { get; }
}

/// <summary>
/// Identity-based selection, navigation and staged editing. The owner disposes this state before its source.
/// Hidden selections survive; hidden edits are cancelled; current cell moves to the nearest visible ancestor.
/// The control adds input gestures and column conversion; the state also supports programmatic use.
/// </summary>
public sealed partial class TreeDataGridState : IDisposable
{
    private readonly TreeDataGridSource _source;
    private readonly HashSet<NodeId> _selected = [];
    private readonly int _ownerThread = Environment.CurrentManagedThreadId;
    private bool _disposed;
    private bool _committing;
    private bool _pendingChanged;
    private bool _pendingProjectionReconciled;
    private List<Action>? _ownedSourceDisposals;

    internal void DisposeOwnedSourceWhenSafe(Action dispose)
    {
        if (_committing) (_ownedSourceDisposals ??= []).Add(dispose);
        else dispose();
    }

    public TreeDataGridState(TreeDataGridSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _ = source.Rows; // Checks the source's access and disposal contract.
        _source = source;
        _source.ProjectionChanged += OnProjectionChanged;
    }

    public IReadOnlyCollection<NodeId> SelectedNodes { get { VerifyAccess(); return Array.AsReadOnly(_selected.ToArray()); } }
    public CellAddress? CurrentCell { get; private set; }
    public TreeDataGridEditSession? EditSession { get; private set; }
    public event EventHandler? Changed;
    internal event EventHandler? ProjectionReconciled;
    internal Func<TreeDataGridEditSession, IDisposable>? ObserveEditWrite { get; set; }
    internal Action<TreeDataGridEditSession, object?>? EditCommitted { get; set; }
    internal long EditWriteVersion { get; private set; }

    public void Select(NodeId node, bool additive = false)
    {
        using var transaction = BeginSelectionMutation();
        if (!_source.TryGetRow(node, out _))
            throw new ArgumentException("The node does not belong to this source.", nameof(node));
        if (!additive && _selected.Count == 1 && _selected.Contains(node) && _cellRows.Count == 0) return;
        if (!additive || SelectionMode == TreeDataGridSelectionMode.Single) _selected.Clear();
        if (!additive || SelectionAnchor is null) SelectionAnchor = CurrentCell;
        _cellRows.Clear(); _cellColumns.Clear();
        if (_selected.Add(node)) NotifyChanged(selection: true);
    }

    public void ClearSelection()
    {
        using var transaction = BeginSelectionMutation();
        if (_selected.Count == 0 && _cellRows.Count == 0) return;
        _selected.Clear();
        _cellRows.Clear(); _cellColumns.Clear(); SelectionAnchor = null;
        NotifyChanged(selection: true);
    }

    public void SetCurrentCell(CellAddress? cell)
    {
        using var transaction = BeginSelectionMutation();
        if (cell is { } address) ValidateCell(address);
        if (CurrentCell == cell) return;
        CurrentCell = cell;
        NotifyChanged();
    }

    public void BeginEdit(CellAddress cell, Func<object, object?> reader, Action<object, object?> writer,
        Func<object?, string?>? validator = null)
        => BeginEditCore(cell, reader, writer, validator, null, null);

    internal void BeginBindingEdit(CellAddress cell, TreeDataGridColumn.EditAccess access, Func<object?, string?>? validator)
    {
        BeginEditCore(cell, access.ReadOriginal, access.Write, validator, access.CreateDraft, access);
    }

    private void BeginEditCore(CellAddress cell, Func<object, object?> reader, Action<object, object?> writer,
        Func<object?, string?>? validator, Func<object?, object?>? createDraft, TreeDataGridColumn.EditAccess? bindingAccess)
    {
        VerifyMutation();
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(writer);
        VerifyProjectionIsCurrent();
        if (EditSession is not null)
            throw new InvalidOperationException("Commit or cancel the active edit before starting another.");
        SetCurrentCell(cell);
        if (_disposed || CurrentCell != cell) return;
        var row = ValidateCell(cell);
        _committing = true;
        try
        {
            var original = reader(row.Model);
            var draft = createDraft is null ? original : createDraft(original);
            VerifyProjectionIsCurrent();
            ValidateCell(cell); // User callbacks can change the underlying collection.
            EditSession = new TreeDataGridEditSession(cell, row.Model, original, writer, validator);
            EditSession.BindingAccess = bindingAccess;
            EditSession.SetValue(draft);
            EditSession.ValueWriter = StageEdit;
            CurrentCell = cell;
            _pendingChanged = true;
        }
        finally { FinishCallback(); }
    }

    /// <summary>
    /// Validates, writes, ends the session, then releases deferred projection notifications.
    /// A writer must either succeed or throw without modifying the model; arbitrary setter side effects cannot be rolled back.
    /// </summary>
    public bool CommitEdit(object? value)
    {
        VerifyMutation(allowSelectionCommit: true);
        VerifyProjectionIsCurrent();
        var edit = EditSession ?? throw new InvalidOperationException("No edit is active.");
        var success = false;
        _committing = true;
        try
        {
            var error = edit.Validator?.Invoke(value);
            VerifyProjectionIsCurrent();
            if (!ReferenceEquals(EditSession, edit) || !_source.TryGetRow(edit.Cell.Node, out var row)
                || !ReferenceEquals(row!.Model, edit.Model) || _source.GetViewIndex(edit.Cell.Node) < 0)
                throw new InvalidOperationException("The edit target changed during validation.");
            edit.ValidationError = error;
            VerifyProjectionIsCurrent();
            if (!ReferenceEquals(EditSession, edit) || !_source.TryGetRow(edit.Cell.Node, out row)
                || !ReferenceEquals(row!.Model, edit.Model) || _source.GetViewIndex(edit.Cell.Node) < 0)
                throw new InvalidOperationException("The edit target changed during validation notification.");
            _pendingChanged = true;
            if (error is null)
            {
                using var observation = ObserveEditWrite?.Invoke(edit);
                var committed = EditCommitted;
                var written = false;
                List<Exception>? errors = null;
                try
                {
                    using (_source.DeferRefresh())
                    {
                        edit.Writer(edit.Model, value);
                        EditWriteVersion++;
                        written = true;
                        // Plain POCO writers need a refresh too. The deferral coalesces notifying writers.
                        if (!_disposeWhenCallbackFinishes) _source.Refresh();
                        edit.ValueWriter = null;
                        EditSession = null;
                    }
                }
                catch (Exception writeFailure) { (errors ??= []).Add(writeFailure); }
                // Projection or clearing observers can throw after the writer has succeeded.
                // The commit notification must still run once and must never become a validation failure.
                if (written)
                    try { committed?.Invoke(edit, value); } catch (Exception notificationFailure) { (errors ??= []).Add(notificationFailure); }
                success = written && !_disposeWhenCallbackFinishes;
                TreeDataGridEventDispatch.Throw(errors);
            }
        }
        finally { FinishCallback(); }
        return success;
    }

    internal object? ConvertOwnedEdit(Func<object?> convert)
    {
        VerifyMutation(allowSelectionCommit: true);
        _committing = true;
        try { return convert(); }
        finally { FinishCallback(); }
    }

    public void CancelEdit()
    {
        VerifyMutation();
        if (EditSession is null) return;
        EditSession.ValueWriter = null;
        EditSession = null; // No model write happened before CommitEdit.
        NotifyChanged();
    }

    /// <summary>Called by the column owner when a column is removed.</summary>
    public void RemoveColumn(ColumnId column)
    {
        VerifyMutation();
        AbortSelectionDraft();
        var previous = CaptureSelection();
        var changed = false;
        if (EditSession?.Cell.Column == column) { EditSession.ValueWriter = null; EditSession = null; changed = true; }
        if (CurrentCell?.Column == column) { CurrentCell = null; changed = true; }
        if (_cellColumns.Remove(column)) { changed = true; if (_cellColumns.Count == 0) _cellRows.Clear(); }
        _columns = _columns.Where(id => id != column).ToArray();
        if (SelectionAnchor?.Column == column) SelectionAnchor = null;
        PrepareForcedSelectionChange(previous, TreeDataGridSelectionChangeReason.ColumnsChanged);
        if (changed) NotifyChanged(selection: true);
    }

    public void Dispose()
    {
        if (_disposed) return;
        VerifyMutation();
        _source.ProjectionChanged -= OnProjectionChanged;
        _selected.Clear();
        _cellRows.Clear(); _cellColumns.Clear();
        CurrentCell = null;
        if (EditSession is { } edit) edit.ValueWriter = null;
        EditSession = null;
        Changed = null;
        ProjectionReconciled = null;
        SelectionChanged = null;
        SelectionChanging = null;
        SelectionChangeCompleted = null;
        CommitSelectionEdit = null;
        IsSelectionOwnerCurrent = null;
        SelectionOwnerVersion = null;
        ValidateSelectionTarget = null;
        ObserveEditWrite = null;
        EditCommitted = null;
        SelectionTransactionFinished = null;
        _disposed = true;
    }

    private void OnProjectionChanged(object? sender, ProjectionChangedEventArgs args)
    {
        VerifyAccess();
        AbortSelectionDraft();
        var previous = CaptureSelection();
        var changed = _selected.RemoveWhere(id => !_source.TryGetRow(id, out _)) != 0;
        var selectionChanged = changed;
        if (EditSession is { } edit && _source.GetViewIndex(edit.Cell.Node) < 0)
        {
            edit.ValueWriter = null;
            EditSession = null;
            changed = true;
        }
        if (CurrentCell is { } current && _source.GetViewIndex(current.Node) < 0)
        {
            CurrentCell = null;
            if (_source.TryGetRow(current.Node, out var hidden))
            {
                var path = hidden!.ModelIndex;
                for (var length = path.Count - 1; length > 0; --length)
                {
                    if (_source.TryGetNodeId(new IndexPath(path.Take(length).ToArray()), out var ancestor)
                        && _source.GetViewIndex(ancestor) >= 0)
                    {
                        CurrentCell = new CellAddress(ancestor, current.Column);
                        break;
                    }
                }
            }
            changed = true;
        }
        selectionChanged |= ReconcileCellSelection();
        changed |= selectionChanged;
        if (SelectionAnchor is { } anchor && !_source.TryGetRow(anchor.Node, out _)) SelectionAnchor = null;
        PrepareForcedSelectionChange(previous, TreeDataGridSelectionChangeReason.ProjectionChanged);
        if (changed)
        {
            NotifyChanged(selection: selectionChanged);
        }
        if (_committing) _pendingProjectionReconciled = true;
        else ProjectionReconciled?.Invoke(this, EventArgs.Empty);
    }

    private TreeDataGridRow ValidateCell(CellAddress cell)
    {
        if (cell.Column.Value <= 0) throw new ArgumentException("The column identity is not allocated.", nameof(cell));
        if (!_source.TryGetRow(cell.Node, out var row) || _source.GetViewIndex(cell.Node) < 0)
            throw new ArgumentException("The cell's node must be visible in this source.", nameof(cell));
        return row!;
    }

    private void VerifyAccess()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_ownerThread != Environment.CurrentManagedThreadId)
            throw new InvalidOperationException("TreeDataGrid state must be accessed on its creating thread.");
    }

    private void VerifyMutation(bool allowSelectionCommit = false)
    {
        VerifyAccess();
        if (_selecting && !(allowSelectionCommit && _selectionCommitting))
            throw new InvalidOperationException("Selection cannot be reentered from a selection callback.");
        if (_committing)
            throw new InvalidOperationException("State cannot be changed from an edit reader, validator or writer callback.");
    }

    private void VerifyProjectionIsCurrent()
    {
        if (_disposeWhenCallbackFinishes) throw new InvalidOperationException("The edit owner changed during the callback.");
        if (_source.IsRefreshDeferred)
            throw new InvalidOperationException("Close external DeferRefresh scopes before beginning or committing an edit.");
    }

    private void FinishCallback()
    {
        _committing = false;
        try
        {
            if (_disposeWhenCallbackFinishes) { _disposeWhenCallbackFinishes = false; Dispose(); return; }
            if (_pendingChanged)
            {
                _pendingChanged = false;
                NotifyChanged();
            }
            if (_pendingProjectionReconciled)
            {
                _pendingProjectionReconciled = false;
                ProjectionReconciled?.Invoke(this, EventArgs.Empty);
            }
        }
        finally
        {
            var disposals = _ownedSourceDisposals; _ownedSourceDisposals = null;
            if (disposals is not null) foreach (var dispose in disposals) dispose();
        }
    }
}
