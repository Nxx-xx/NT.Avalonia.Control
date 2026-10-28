namespace NT.AvaloniaControl.TreeDataGrid;

public sealed partial class TreeDataGridState
{
    internal void RestoreMovedSelection(TreeDataGridSelectionSnapshot selection, IReadOnlyDictionary<NodeId, NodeId> identities)
    {
        VerifyMutation();
        var previous = CaptureSelection();
        NodeId Map(NodeId node) => identities.TryGetValue(node, out var mapped) ? mapped : node;
        CellAddress? MapCell(CellAddress? cell, bool visible)
        {
            if (cell is not { } address || !_columns.Contains(address.Column)) return null;
            var mapped = Map(address.Node);
            if (!_source.TryGetRow(mapped, out var row)) return null;
            if (!visible || _source.GetViewIndex(mapped) >= 0) return new(mapped, address.Column);
            for (var length = row!.ModelIndex.Count - 1; length > 0; length--)
                if (_source.TryGetNodeId(new(row.ModelIndex.Take(length).ToArray()), out var ancestor) && _source.GetViewIndex(ancestor) >= 0)
                    return new(ancestor, address.Column);
            return null;
        }
        _selected.Clear();
        foreach (var node in selection.SelectedNodes.Select(Map).Where(node => _source.TryGetRow(node, out _))) _selected.Add(node);
        _cellRows.Clear();
        foreach (var node in selection.CellRows.Select(Map).Where(node => _source.TryGetRow(node, out _))) _cellRows.Add(node);
        _cellColumns.Clear(); _cellColumns.UnionWith(selection.CellColumns.Where(_columns.Contains));
        CurrentCell = MapCell(selection.CurrentCell, true); SelectionAnchor = MapCell(selection.SelectionAnchor, false);
        ReconcileCellSelection();
        PrepareForcedSelectionChange(previous, TreeDataGridSelectionChangeReason.ProjectionChanged);
        NotifyChanged(selection: _selectionCompletion is not null);
    }
}
