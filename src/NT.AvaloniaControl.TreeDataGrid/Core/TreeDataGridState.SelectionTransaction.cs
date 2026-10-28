namespace NT.AvaloniaControl.TreeDataGrid;

public sealed partial class TreeDataGridState
{
    private int _selectionDraftDepth;
    private TreeDataGridSelectionSnapshot? _selectionDraft;
    private ColumnId[]? _draftColumns;
    private long _draftVersion;
    private long _selectionRevision;
    private bool _selecting;
    private bool _draftInvalidated;
    private bool _draftNotificationPending;
    private bool _selectionCommitting;
    internal bool IsSelecting => _selecting;
    internal Action? SelectionTransactionFinished { get; set; }
    private TreeDataGridSelectionChangeReason _selectionReason;
    private TreeDataGridSelectionChangedEventArgs? _selectionCompletion;
    internal Func<bool>? CommitSelectionEdit { get; set; }
    internal Func<bool>? IsSelectionOwnerCurrent { get; set; }
    internal Func<long>? SelectionOwnerVersion { get; set; }
    internal Func<bool>? ValidateSelectionTarget { get; set; }
    public event EventHandler<TreeDataGridSelectionChangingEventArgs>? SelectionChanging;
    public event EventHandler<TreeDataGridSelectionChangedEventArgs>? SelectionChangeCompleted;

    internal TreeDataGridSelectionSnapshot CaptureSelection() => new(_selected, _cellRows, _cellColumns, CurrentCell, SelectionAnchor);
    private void RestoreSelection(TreeDataGridSelectionSnapshot snapshot)
    {
        _selected.Clear(); _selected.UnionWith(snapshot.SelectedNodes);
        _cellRows.Clear(); _cellRows.UnionWith(snapshot.CellRows);
        _cellColumns.Clear(); _cellColumns.UnionWith(snapshot.CellColumns);
        CurrentCell = snapshot.CurrentCell; SelectionAnchor = snapshot.SelectionAnchor;
    }
    private SelectionTransactionScope BeginSelectionMutation()
    {
        VerifyMutation();
        if (_selectionDraftDepth > 0 && _draftInvalidated) throw new InvalidOperationException("The selection batch was invalidated by a source or column change.");
        if (_selectionDraftDepth++ == 0)
        { _selectionDraft = CaptureSelection(); _draftColumns = _columns; _draftVersion = _source.Version; _draftInvalidated = _draftNotificationPending = false; }
        return new SelectionTransactionScope(this);
    }
    private bool FinishSelectionMutation()
    {
        if (--_selectionDraftDepth != 0) return false;
        var previous = _selectionDraft!; var previousColumns = _draftColumns!;
        _selectionDraft = null; _draftColumns = null;
        if (_draftInvalidated) { NotifyChanged(); return false; }
        var proposed = CaptureSelection(); var proposedColumns = _columns;
        RestoreSelection(previous); _columns = previousColumns;
        if (previous.SameAs(proposed))
        { _columns = proposedColumns; if (_draftNotificationPending) NotifyChanged(); return true; }
        if (_source.Version != _draftVersion || _source.IsRefreshDeferred) return false;
        var revision = _selectionRevision;
        var version = _source.Version;
        var ownerVersion = SelectionOwnerVersion?.Invoke();
        _selecting = true;
        try
        {
            var args = new TreeDataGridSelectionChangingEventArgs(previous, proposed, _selectionReason);
            SelectionChanging?.Invoke(this, args);
            if (args.Cancel || !IsProposalCurrent(version, revision) || SelectionOwnerVersion?.Invoke() != ownerVersion) return false;
            if (EditSession is { } edit && (proposed.CurrentCell != edit.Cell ||
                !previous.SelectedNodes.ToHashSet().SetEquals(proposed.SelectedNodes) ||
                !previous.CellRows.ToHashSet().SetEquals(proposed.CellRows) || !previous.CellColumns.ToHashSet().SetEquals(proposed.CellColumns)))
            {
                _selectionCommitting = true;
                bool committed;
                try { committed = CommitSelectionEdit?.Invoke() ?? CommitEdit(); }
                finally { _selectionCommitting = false; }
                // A successful writer can refresh the projection. Validate the complete proposed identity set again.
                if (!committed || _disposed || IsSelectionOwnerCurrent?.Invoke() == false || _selectionRevision != revision) return false;
            }
            if (_disposed || IsSelectionOwnerCurrent?.Invoke() == false || _source.IsRefreshDeferred ||
                proposed.SelectedNodes.Concat(proposed.CellRows).Any(id => !_source.TryGetRow(id, out _)) ||
                proposed.CurrentCell is { } current && (_source.GetViewIndex(current.Node) < 0 || proposedColumns.Length > 0 && !proposedColumns.Contains(current.Column)) ||
                proposedColumns.Length > 0 && proposed.CellColumns.Any(id => !proposedColumns.Contains(id)) ||
                !_columns.SequenceEqual(previousColumns) || SelectionOwnerVersion?.Invoke() != ownerVersion || !IsRectangleCurrent(previous, proposed, proposedColumns)) return false;
            if (ValidateSelectionTarget?.Invoke() == false || _disposed || IsSelectionOwnerCurrent?.Invoke() == false
                || _selectionRevision != revision) return false;
            RestoreSelection(proposed); _columns = proposedColumns;
            _selectionRevision++;
            _selectionCompletion = new(previous, proposed, args.Reason, false);
            NotifyChanged(selection: !previous.SelectedNodes.ToHashSet().SetEquals(proposed.SelectedNodes)
                || !previous.CellRows.ToHashSet().SetEquals(proposed.CellRows) || !previous.CellColumns.ToHashSet().SetEquals(proposed.CellColumns));
            return true;
        }
        finally
        {
            _selecting = false;
            if (_disposeWhenCallbackFinishes) { _disposeWhenCallbackFinishes = false; Dispose(); }
            else SelectionTransactionFinished?.Invoke();
        }
    }
    private bool IsProposalCurrent(long version, long revision) => !_disposed && _source.Version == version
        && _selectionRevision == revision && !_source.IsRefreshDeferred && IsSelectionOwnerCurrent?.Invoke() != false;

    private bool IsRectangleCurrent(TreeDataGridSelectionSnapshot previous, TreeDataGridSelectionSnapshot proposed, ColumnId[] columns)
    {
        if (proposed.CellRows.Count == 0) return true;
        if (previous.CellRows.ToHashSet().SetEquals(proposed.CellRows) && previous.CellColumns.ToHashSet().SetEquals(proposed.CellColumns)) return true;
        var rows = proposed.CellRows.Select(_source.GetViewIndex).Order().ToArray();
        if (rows[0] < 0 || rows[^1] - rows[0] + 1 != rows.Length) return false;
        if (columns.Length == 0) return true; // Standalone state can use allocated cells without an owner column order.
        var indices = proposed.CellColumns.Select(id => Array.IndexOf(columns, id)).Order().ToArray();
        return indices.Length > 0 && indices[0] >= 0 && indices[^1] - indices[0] + 1 == indices.Length;
    }

    internal bool RunSelection(TreeDataGridSelectionChangeReason reason, Action action)
    {
        var previous = _selectionReason; _selectionReason = reason;
        try { var scope = BeginSelectionMutation(); using (scope) action(); return scope.Succeeded; }
        finally { _selectionReason = previous; }
    }

    internal bool SelectAddress(CellAddress address, TreeDataGridSelectionUnit unit, bool extend, bool toggle)
    {
        var scope = BeginSelectionMutation();
        using (scope)
        {
            if (unit == TreeDataGridSelectionUnit.Cell) SelectCell(address, extend);
            else
            {
                var anchor = SelectionAnchor;
                SetCurrentCell(address);
                if (extend && SelectionMode == TreeDataGridSelectionMode.Multiple && anchor is { } origin && _source.GetViewIndex(origin.Node) >= 0)
                    SelectRowRange(origin.Node, address.Node);
                else if (toggle) ToggleSelection(address.Node);
                else Select(address.Node);
            }
        }
        return scope.Succeeded;
    }
    private void PrepareForcedSelectionChange(TreeDataGridSelectionSnapshot previous, TreeDataGridSelectionChangeReason reason)
    {
        var next = CaptureSelection();
        if (previous.SameAs(next)) return;
        _selectionRevision++;
        _selectionCompletion = new(_selectionCompletion?.OldSelection ?? previous, next, reason, true);
    }
    private void AbortSelectionDraft()
    {
        if (_selectionDraftDepth == 0 || _draftInvalidated) return;
        RestoreSelection(_selectionDraft!); _columns = _draftColumns!; _draftInvalidated = true;
    }
    internal void ClearSelectionForOwner()
    {
        VerifyAccess(); AbortSelectionDraft();
        _selectionRevision++;
        var previous = CaptureSelection();
        _selected.Clear(); _cellRows.Clear(); _cellColumns.Clear(); SelectionAnchor = null;
        PrepareForcedSelectionChange(previous, TreeDataGridSelectionChangeReason.SelectionModeChanged);
        NotifyChanged(selection: _selectionCompletion is not null);
    }
    private sealed class SelectionTransactionScope(TreeDataGridState owner) : IDisposable
    {
        private TreeDataGridState? _owner = owner;
        internal bool Succeeded { get; private set; }
        public void Dispose() { var owner = _owner; if (owner is null) return; owner.VerifyAccess(); _owner = null; Succeeded = owner.FinishSelectionMutation(); }
    }
}
