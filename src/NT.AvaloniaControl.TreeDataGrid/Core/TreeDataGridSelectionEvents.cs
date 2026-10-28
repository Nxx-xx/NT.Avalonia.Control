namespace NT.AvaloniaControl.TreeDataGrid;

public enum TreeDataGridSelectionChangeReason
{
    Programmatic, Pointer, Keyboard, Search, Binding, Automation,
    ProjectionChanged, ColumnsChanged, SourceChanged, SelectionModeChanged
}

/// <summary>An immutable identity snapshot. Cell rows and columns describe a Cartesian rectangle without allocating its cells.</summary>
public sealed class TreeDataGridSelectionSnapshot
{
    internal TreeDataGridSelectionSnapshot(IEnumerable<NodeId> nodes, IEnumerable<NodeId> rows,
        IEnumerable<ColumnId> columns, CellAddress? current, CellAddress? anchor)
    {
        SelectedNodes = Array.AsReadOnly(nodes.ToArray());
        CellRows = Array.AsReadOnly(rows.ToArray());
        CellColumns = Array.AsReadOnly(columns.ToArray());
        CurrentCell = current;
        SelectionAnchor = anchor;
    }
    public IReadOnlyList<NodeId> SelectedNodes { get; }
    public IReadOnlyList<NodeId> CellRows { get; }
    public IReadOnlyList<ColumnId> CellColumns { get; }
    public CellAddress? CurrentCell { get; }
    public CellAddress? SelectionAnchor { get; }
    internal bool SameAs(TreeDataGridSelectionSnapshot other) => CurrentCell == other.CurrentCell
        && SelectionAnchor == other.SelectionAnchor && SelectedNodes.ToHashSet().SetEquals(other.SelectedNodes)
        && CellRows.ToHashSet().SetEquals(other.CellRows) && CellColumns.ToHashSet().SetEquals(other.CellColumns);
}

public sealed class TreeDataGridSelectionChangingEventArgs : EventArgs
{
    internal TreeDataGridSelectionChangingEventArgs(TreeDataGridSelectionSnapshot oldSelection,
        TreeDataGridSelectionSnapshot proposedSelection, TreeDataGridSelectionChangeReason reason)
    { OldSelection = oldSelection; ProposedSelection = proposedSelection; Reason = reason; }
    public TreeDataGridSelectionSnapshot OldSelection { get; }
    public TreeDataGridSelectionSnapshot ProposedSelection { get; }
    public TreeDataGridSelectionChangeReason Reason { get; }
    public bool Cancel { get; set; }
}

/// <summary>Passed through the existing EventHandler SelectionChanged event, preserving its signature.</summary>
public sealed class TreeDataGridSelectionChangedEventArgs : EventArgs
{
    internal TreeDataGridSelectionChangedEventArgs(TreeDataGridSelectionSnapshot oldSelection,
        TreeDataGridSelectionSnapshot newSelection, TreeDataGridSelectionChangeReason reason, bool isForced)
    { OldSelection = oldSelection; NewSelection = newSelection; Reason = reason; IsForced = isForced; }
    public TreeDataGridSelectionSnapshot OldSelection { get; }
    public TreeDataGridSelectionSnapshot NewSelection { get; }
    public TreeDataGridSelectionChangeReason Reason { get; }
    public bool IsForced { get; }
}

internal static class TreeDataGridEventDispatch
{
    internal static void Invoke<T>(EventHandler<T>? handlers, object sender, T args, ref List<Exception>? errors)
    {
        if (handlers is null) return;
        foreach (EventHandler<T> handler in handlers.GetInvocationList())
            try { handler(sender, args); } catch (Exception error) { (errors ??= []).Add(error); }
    }
    internal static void Invoke(EventHandler? handlers, object sender, EventArgs args, ref List<Exception>? errors)
    {
        if (handlers is null) return;
        foreach (EventHandler handler in handlers.GetInvocationList())
            try { handler(sender, args); } catch (Exception error) { (errors ??= []).Add(error); }
    }
    internal static void Throw(List<Exception>? errors)
    {
        if (errors is { Count: 1 }) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors is { Count: > 1 }) throw new AggregateException("TreeDataGrid event listeners failed.", errors);
    }
}
