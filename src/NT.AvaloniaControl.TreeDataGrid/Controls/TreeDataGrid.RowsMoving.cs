using System.ComponentModel;

namespace NT.AvaloniaControl.TreeDataGrid;

public sealed class TreeDataGridRowsMoveRequestedEventArgs : CancelEventArgs
{
    public TreeDataGridRowsMoveRequestedEventArgs(TreeDataGridSource source, IReadOnlyList<TreeDataGridRow> rows,
        TreeDataGridRow target, TreeDataGridRowDropPosition position, long version)
    { Source = source; Rows = Array.AsReadOnly(rows.ToArray()); Target = target; Position = position; SourceVersion = version; }
    public TreeDataGridSource Source { get; }
    public IReadOnlyList<TreeDataGridRow> Rows { get; }
    public TreeDataGridRow Target { get; }
    public TreeDataGridRowDropPosition Position { get; }
    public long SourceVersion { get; }
    public bool Handled { get; set; }
    public TreeDataGridRowsMoveResult? Result { get; set; }
}

public sealed class TreeDataGridRowsMoveCompletedEventArgs : EventArgs
{
    public TreeDataGridRowsMoveCompletedEventArgs(IReadOnlyList<NodeId> nodes, NodeId target,
        TreeDataGridRowDropPosition position, TreeDataGridRowsMoveResult result)
    { NodeIds = Array.AsReadOnly(nodes.ToArray()); TargetNodeId = target; Position = position; Result = result; }
    public IReadOnlyList<NodeId> NodeIds { get; }
    public NodeId TargetNodeId { get; }
    public TreeDataGridRowDropPosition Position { get; }
    public TreeDataGridRowsMoveResult Result { get; }
}

public partial class TreeDataGrid
{
    public event EventHandler<TreeDataGridRowsMoveRequestedEventArgs>? RowsMoveRequested;
    public event EventHandler<TreeDataGridRowsMoveCompletedEventArgs>? RowsMoveCompleted;

    /// <summary>Requests one batch transaction. Single-row legacy events are not raised for each member.</summary>
    public TreeDataGridRowsMoveResult TryMoveRows(IReadOnlyList<NodeId> nodes, NodeId target, TreeDataGridRowDropPosition position)
    {
        VerifyAccess(); ArgumentNullException.ThrowIfNull(nodes);
        if (_rowMoving || !CanUserMoveRows || Source is not { } source || source.IsRefreshDeferred)
            return new(TreeDataGridRowMoveStatus.Rejected);
        var rows = RowMoveNormalization.Rows(source, nodes);
        if (rows is not { Length: > 0 } || !source.TryGetRow(target, out var destination)
            || rows.Any(row => source.GetViewIndex(row.Id) < 0 || !IsValidRowDrop(row, destination!, position))
            || source.GetViewIndex(target) < 0) return new(TreeDataGridRowMoveStatus.Rejected);
        var state = State; var selection = state?.CaptureSelection();
        var operation = ColumnOperationVersion;
        using var moving = new RowMoveScope(this);
        TreeDataGridRowsMoveResult result;
        bool Current() => CanUserMoveRows && ColumnOperationVersion == operation && rows.All(row => IsMoveTargetCurrent(source, row, destination!));
        try
        {
            if (!CommitEdit()) result = new(Current() ? TreeDataGridRowMoveStatus.ValidationFailed : TreeDataGridRowMoveStatus.Stale);
            else if (!Current()) result = new(TreeDataGridRowMoveStatus.Stale);
            else
            {
                var version = source.Version;
                var request = new TreeDataGridRowsMoveRequestedEventArgs(source, rows, destination!, position, version);
                if (RowsMoveRequested is { } handlers)
                    foreach (EventHandler<TreeDataGridRowsMoveRequestedEventArgs> handler in handlers.GetInvocationList())
                    {
                        try { handler(this, request); }
                        catch (Exception error)
                        {
                            if (request.Result is { IsCommitted: true } committed)
                            { request.Handled = true; request.Result = BatchObserverError(committed, error); }
                            else throw;
                        }
                        if (request.Handled || request.Cancel) break;
                    }
                if (request.Result is { IsCommitted: true } committedResult && request.Handled) result = committedResult;
                else if (request.Cancel) result = new(TreeDataGridRowMoveStatus.Cancelled);
                else if (request.Handled) result = request.Result ?? new(TreeDataGridRowMoveStatus.HandlerFailed,
                    error: new InvalidOperationException("A handled batch move must supply its transaction result."));
                else if (source.Version != version || ColumnOperationVersion != operation || !Current()) result = new(TreeDataGridRowMoveStatus.Stale);
                else result = source.MoveRows(rows.Select(row => row.Id).ToArray(), target, position);
            }
        }
        catch (Exception error) { result = new(TreeDataGridRowMoveStatus.HandlerFailed, error: error); }
        try
        {
            if (result.IsCommitted && ReferenceEquals(Source, source) && ReferenceEquals(State, state) && selection is not null && ColumnOperationVersion == operation)
            {
                state!.RestoreMovedSelection(selection, result.IdentityMap);
                if (ReferenceEquals(Source, source) && State?.CurrentCell is { } cell)
                { RowsPresenter?.ScrollIntoView(cell.Node); BringColumnIntoView(cell.Column); }
            }
        }
        catch (Exception error) { result = BatchObserverError(result, error); }
        if (RowsMoveCompleted is { } observers)
        {
            var args = new TreeDataGridRowsMoveCompletedEventArgs(rows.Select(row => row.Id).ToArray(), target, position, result);
            foreach (EventHandler<TreeDataGridRowsMoveCompletedEventArgs> observer in observers.GetInvocationList())
                try { observer(this, args); } catch (Exception error) { result = BatchObserverError(result, error); }
        }
        return result;
    }
    private static TreeDataGridRowsMoveResult BatchObserverError(TreeDataGridRowsMoveResult result, Exception error) =>
        new(result.IsCommitted ? TreeDataGridRowMoveStatus.CommittedWithObserverError : TreeDataGridRowMoveStatus.HandlerFailed,
            result.IdentityMap, result.Error is null ? error : new AggregateException(result.Error, error));
}
