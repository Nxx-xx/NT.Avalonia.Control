namespace NT.AvaloniaControl.TreeDataGrid;

/// <summary>An immutable snapshot of a node in one projection version.</summary>
public sealed class TreeDataGridRow
{
    internal TreeDataGridRow(NodeId id, object model, IndexPath modelIndex, int depth, bool isExpanded, bool hasChildren)
    {
        Id = id;
        Model = model;
        ModelIndex = modelIndex;
        Depth = depth;
        IsExpanded = isExpanded;
        HasChildren = hasChildren;
    }

    public NodeId Id { get; }
    public object Model { get; }
    public IndexPath ModelIndex { get; }
    public int Depth { get; }
    public bool IsExpanded { get; }
    public bool HasChildren { get; }
}
