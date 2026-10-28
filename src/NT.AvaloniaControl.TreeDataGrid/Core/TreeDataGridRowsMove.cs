using System.Collections.ObjectModel;

namespace NT.AvaloniaControl.TreeDataGrid;

/// <summary>A batch outcome. IdentityMap includes surviving moved descendants; observer failures do not undo storage.</summary>
public sealed class TreeDataGridRowsMoveResult
{
    public TreeDataGridRowsMoveResult(TreeDataGridRowMoveStatus status, IReadOnlyDictionary<NodeId, NodeId>? identityMap = null, Exception? error = null)
    {
        Status = status; Error = error;
        IdentityMap = new ReadOnlyDictionary<NodeId, NodeId>(identityMap is null ? [] : new Dictionary<NodeId, NodeId>(identityMap));
    }
    public TreeDataGridRowMoveStatus Status { get; }
    public IReadOnlyDictionary<NodeId, NodeId> IdentityMap { get; }
    public Exception? Error { get; }
    public bool IsCommitted => Status is TreeDataGridRowMoveStatus.Committed or TreeDataGridRowMoveStatus.CommittedWithObserverError;
}

public abstract partial class TreeDataGridSource
{
    /// <summary>Moves source-ordered nodes atomically, excluding descendants covered by selected ancestors.</summary>
    public virtual TreeDataGridRowsMoveResult MoveRows(IReadOnlyList<NodeId> nodes, NodeId target, TreeDataGridRowDropPosition position) =>
        new(TreeDataGridRowMoveStatus.Unsupported);
}

internal static class RowMoveNormalization
{
    internal static TreeDataGridRow[]? Rows(TreeDataGridSource source, IReadOnlyList<NodeId> nodes)
    {
        var rows = new List<TreeDataGridRow>();
        foreach (var id in nodes.Distinct())
        {
            if (!source.TryGetRow(id, out var row)) return null;
            rows.Add(row!);
        }
        rows.Sort((left, right) => Compare(left.ModelIndex, right.ModelIndex));
        var normalized = new List<TreeDataGridRow>();
        foreach (var row in rows)
            if (normalized.Count == 0 || !Prefix(normalized[^1].ModelIndex, row.ModelIndex)) normalized.Add(row);
        return normalized.ToArray();
    }
    internal static bool Prefix(IndexPath prefix, IndexPath path) => path.Count >= prefix.Count && prefix.SequenceEqual(path.Take(prefix.Count));
    private static int Compare(IndexPath left, IndexPath right)
    {
        for (var index = 0; index < Math.Min(left.Count, right.Count); index++)
            if (left[index] != right[index]) return left[index].CompareTo(right[index]);
        return left.Count.CompareTo(right.Count);
    }
}
