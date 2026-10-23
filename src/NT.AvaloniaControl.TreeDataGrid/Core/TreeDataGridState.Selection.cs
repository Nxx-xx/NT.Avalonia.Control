namespace NT.AvaloniaControl.TreeDataGrid;

public enum TreeDataGridSelectionUnit { Row, Cell }
public enum TreeDataGridSelectionMode { Single, Multiple }

public sealed partial class TreeDataGridState
{
    private readonly HashSet<NodeId> _cellRows = [];
    private readonly HashSet<ColumnId> _cellColumns = [];
    private ColumnId[] _columns = [];
    private int _notificationDepth;
    private bool _selectionPending;
    private bool _notificationPending;
    private bool _suspended;
    private bool _disposeWhenCallbackFinishes;
    private NodeId? _columnlessCurrentNode;
    internal bool IsRowSelected(NodeId id) => _selected.Contains(id);
    internal void SynchronizeOwnerColumns(IReadOnlyList<ColumnId> columns)
    {
        VerifyAccess();
        if (_columns.SequenceEqual(columns)) return;
        var previous = _columns;
        var current = CurrentCell;
        if (current is { } cell && !columns.Contains(cell.Column))
        {
            var index = Array.IndexOf(previous, cell.Column);
            var next = previous.Skip(Math.Max(0, index + 1)).Where(columns.Contains)
                .Concat(previous.Take(Math.Max(0, index)).Reverse().Where(columns.Contains))
                .Select(id => (ColumnId?)id).FirstOrDefault() ?? columns.Select(id => (ColumnId?)id).FirstOrDefault();
            CurrentCell = next is { } id ? new CellAddress(cell.Node, id) : null;
            _columnlessCurrentNode = next is null ? cell.Node : null;
        }
        else if (current is null && _columnlessCurrentNode is { } node && columns.Count > 0)
        {
            if (_source.GetViewIndex(node) >= 0) CurrentCell = new CellAddress(node, columns[0]);
            _columnlessCurrentNode = null;
        }
        _columns = columns.ToArray();
        var removed = _cellColumns.RemoveWhere(column => !columns.Contains(column)) > 0;
        if (_cellColumns.Count == 0) _cellRows.Clear();
        if (SelectionAnchor is { } anchor && !columns.Contains(anchor.Column)) SelectionAnchor = null;
        if (EditSession is { } edit && !columns.Contains(edit.Cell.Column))
        { edit.ValueWriter = null; EditSession = null; }
        removed |= ReconcileCellSelection();
        NotifyChanged(selection: removed);
    }
    internal void CancelOwnedEdit()
    {
        if (!_committing) { CancelEdit(); return; }
        if (EditSession is { } edit) { edit.ValueWriter = null; EditSession = null; _pendingChanged = true; }
    }
    internal void ReleaseOwner()
    {
        if (!_committing) { Dispose(); return; }
        _source.ProjectionChanged -= OnProjectionChanged;
        Changed = null; SelectionChanged = null;
        _disposeWhenCallbackFinishes = true;
    }
    private TreeDataGridSelectionMode _selectionMode = TreeDataGridSelectionMode.Multiple;
    public TreeDataGridSelectionMode SelectionMode
    {
        get => _selectionMode;
        set { VerifyMutation(); if (_selectionMode == value) return; _selectionMode = value; ClearSelection(); }
    }
    public CellAddress? SelectionAnchor { get; private set; }
    public IEnumerable<CellAddress> SelectedCells
    {
        get { VerifyAccess(); var rows = _cellRows.ToArray(); var columns = _cellColumns.ToArray(); return rows.SelectMany(row => columns.Select(column => new CellAddress(row, column))); }
    }
    public event EventHandler? SelectionChanged;
    public bool IsCellSelected(CellAddress cell) { VerifyAccess(); return _cellRows.Contains(cell.Node) && _cellColumns.Contains(cell.Column); }
    public void SetColumns(IReadOnlyList<ColumnId> columns)
    {
        VerifyMutation(); ArgumentNullException.ThrowIfNull(columns);
        if (columns.Any(x => x.Value <= 0) || columns.Distinct().Count() != columns.Count) throw new ArgumentException("Column identities must be allocated and unique.", nameof(columns));
        using (DeferSelectionNotifications())
        {
            foreach (var old in _columns.Except(columns).ToArray()) RemoveColumn(old);
            _columns = columns.ToArray();
            if (ReconcileCellSelection()) NotifyChanged(selection: true);
        }
    }
    public void SelectCell(CellAddress cell, bool extend = false)
    {
        VerifyMutation(); ValidateCell(cell);
        if (extend && SelectionMode == TreeDataGridSelectionMode.Multiple && SelectionAnchor is { } anchor)
        { SelectCellRange(anchor, cell, _columns); return; }
        _selected.Clear(); _cellRows.Clear(); _cellColumns.Clear();
        _cellRows.Add(cell.Node); _cellColumns.Add(cell.Column);
        SelectionAnchor = CurrentCell = cell;
        NotifyChanged(selection: true);
    }
    public void SelectCellRange(CellAddress anchor, CellAddress end, IReadOnlyList<ColumnId> columns)
    {
        VerifyMutation(); ValidateCell(anchor); ValidateCell(end); ArgumentNullException.ThrowIfNull(columns);
        if (columns.Any(x => x.Value <= 0) || columns.Distinct().Count() != columns.Count) throw new ArgumentException("Column identities must be allocated and unique.", nameof(columns));
        if (SelectionMode == TreeDataGridSelectionMode.Single) { SelectCell(end); return; }
        var a = Array.IndexOf(columns.ToArray(), anchor.Column); var b = Array.IndexOf(columns.ToArray(), end.Column);
        if (a < 0 || b < 0) throw new ArgumentException("Range columns must belong to the supplied order.", nameof(columns));
        _columns = columns.ToArray();
        var first = _source.GetViewIndex(anchor.Node); var last = _source.GetViewIndex(end.Node);
        _selected.Clear(); _cellRows.Clear(); _cellColumns.Clear();
        for (var i = Math.Min(first, last); i <= Math.Max(first, last); ++i) _cellRows.Add(_source.Rows[i].Id);
        for (var i = Math.Min(a, b); i <= Math.Max(a, b); ++i) _cellColumns.Add(columns[i]);
        SelectionAnchor = anchor; CurrentCell = end;
        NotifyChanged(selection: true);
    }
    public void SelectRowRange(NodeId anchor, NodeId end)
    {
        VerifyMutation();
        var first = _source.GetViewIndex(anchor); var last = _source.GetViewIndex(end);
        if (first < 0 || last < 0) throw new ArgumentException("Range nodes must be visible.");
        if (SelectionMode == TreeDataGridSelectionMode.Single) { Select(end); return; }
        _selected.Clear(); _cellRows.Clear(); _cellColumns.Clear();
        for (var i = Math.Min(first, last); i <= Math.Max(first, last); ++i) _selected.Add(_source.Rows[i].Id);
        NotifyChanged(selection: true);
    }
    public void ToggleSelection(NodeId node)
    {
        VerifyMutation(); if (!_source.TryGetRow(node, out _)) throw new ArgumentException("Unknown node.", nameof(node));
        if (SelectionMode == TreeDataGridSelectionMode.Single) { Select(node); return; }
        _cellRows.Clear(); _cellColumns.Clear();
        if (!_selected.Remove(node)) _selected.Add(node);
        NotifyChanged(selection: true);
    }
    public IDisposable DeferSelectionNotifications() { VerifyMutation(); ++_notificationDepth; return new SelectionScope(this); }
    public bool CommitEdit() => CommitEdit(EditSession?.Value ?? (EditSession is null ? throw new InvalidOperationException("No edit is active.") : null));
    internal void SetValidationError(string error) { if (EditSession is { } edit) { edit.ValidationError = error; NotifyChanged(); } }
    internal void Suspend()
    {
        VerifyAccess(); if (_suspended) return; _source.ProjectionChanged -= OnProjectionChanged; _suspended = true;
    }
    internal void Resume()
    {
        VerifyAccess(); if (!_suspended) return;
        _source.ProjectionChanged += OnProjectionChanged; _suspended = false;
        OnProjectionChanged(_source, new ProjectionChangedEventArgs(_source.Version, ProjectionChangeReason.Refresh, []));
    }
    private void StageEdit(TreeDataGridEditSession session, object? value)
    {
        VerifyMutation(); if (!ReferenceEquals(session, EditSession)) throw new InvalidOperationException("The edit session is no longer active.");
        session.SetValue(value); session.ValidationError = null; NotifyChanged();
    }
    private bool ReconcileCellSelection()
    {
        var changed = _cellRows.RemoveWhere(id => !_source.TryGetRow(id, out _)) > 0;
        if (_cellRows.Count == 0) { changed |= _cellColumns.Count > 0; _cellColumns.Clear(); return changed; }
        var rows = _cellRows.Select(_source.GetViewIndex).Where(i => i >= 0).Order().ToArray();
        var columns = _cellColumns.Select(id => Array.IndexOf(_columns, id)).Where(i => i >= 0).Order().ToArray();
        var rectangular = (rows.Length == 0 || rows[^1] - rows[0] + 1 == rows.Length) && (columns.Length == 0 || columns[^1] - columns[0] + 1 == columns.Length);
        if (!rectangular)
        {
            _cellRows.Clear(); _cellColumns.Clear();
            if (CurrentCell is { } current && _source.GetViewIndex(current.Node) >= 0)
            { _cellRows.Add(current.Node); _cellColumns.Add(current.Column); SelectionAnchor = current; }
            changed = true;
        }
        return changed;
    }
    private void NotifyChanged(bool selection = false)
    {
        if (_committing) { _pendingChanged = true; _selectionPending |= selection; return; }
        _notificationPending = true; _selectionPending |= selection;
        if (_notificationDepth != 0) return;
        var selectionChanged = _selectionPending;
        _selectionPending = _notificationPending = false;
        if (selectionChanged) SelectionChanged?.Invoke(this, EventArgs.Empty);
        Changed?.Invoke(this, EventArgs.Empty);
    }
    private sealed class SelectionScope(TreeDataGridState owner) : IDisposable
    {
        private TreeDataGridState? _owner = owner;
        public void Dispose()
        {
            if (_owner is not { } current) return; current.VerifyMutation(); _owner = null;
            if (--current._notificationDepth == 0 && current._notificationPending) current.NotifyChanged();
        }
    }
}
