namespace NT.AvaloniaControl.TreeDataGrid;

public enum TreeDataGridRowDropPosition { Before, After, Inside }

public enum TreeDataGridRowMoveStatus
{
    Rejected, Cancelled, Stale, Unsupported, ValidationFailed, Committed, CommittedWithObserverError, HandlerFailed,
}

/// <summary>A move outcome. Observer errors may occur after storage has committed; they do not undo the move.</summary>
public sealed record TreeDataGridRowMoveResult(TreeDataGridRowMoveStatus Status, NodeId? NodeId = null, Exception? Error = null)
{
    public bool IsCommitted => Status is TreeDataGridRowMoveStatus.Committed or TreeDataGridRowMoveStatus.CommittedWithObserverError;
}

public abstract partial class TreeDataGridSource
{
    /// <summary>
    /// Moves a node within this source using a supported collection adapter. Sorting and filtering reject default moves.
    /// The base implementation is unsupported. Control-level requests handle selection and active editing separately.
    /// </summary>
    public virtual TreeDataGridRowMoveResult MoveRow(NodeId node, NodeId target, TreeDataGridRowDropPosition position) =>
        new(TreeDataGridRowMoveStatus.Unsupported);
}
