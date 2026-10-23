namespace NT.AvaloniaControl.TreeDataGrid;

/// <summary>Identifies one occurrence of a model in a data source. The default value is not an allocated identity.</summary>
public readonly record struct NodeId(long Value);

internal static class NodeIdentity
{
    private static long _lastId;

    public static NodeId Allocate() => new(Interlocked.Increment(ref _lastId));
}
