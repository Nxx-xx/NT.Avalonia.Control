using Avalonia.Controls;
using NT.AvaloniaControl.TreeDataGrid;

namespace NT.AvaloniaControl.Demo.Pages.TreeDataGrid;

internal sealed partial class TreeDataGridPage
{
    private async Task RunBatchMoveDemoSmokeAsync(Dictionary<string, bool> checks, List<object> snapshots, string? imagePath, List<string> images)
    {
        await DemoSelectScenarioAsync("rowdrag-batch");
        var source = _source!; var roots = _rowMoveRoots!;
        NodeId Id(params int[] path) => source.TryGetNodeId(new(path), out var id) ? id : throw new InvalidOperationException("Missing batch demo node.");
        checks["a4ItemsSourceAdapterAndWholeRow"] = ReferenceEquals(_grid.ItemsSource, roots) && source is HierarchicalTreeDataGridSource<object>
            && _grid.RowDragHandle == TreeDataGridRowDragHandle.WholeRow;
        DemoPressButton(DemoFind<Button>(this, "SelectA4BatchButton"));
        var nodes = new[] { Id(0, 0), Id(0, 1) }; var target = Id(1); var current = _grid.CurrentCell;
        checks["a4BatchSelectionButton"] = _grid.State!.SelectedNodes.Count == 2 && nodes.All(_grid.State.SelectedNodes.Contains);
        _grid.BeginEdit(new(nodes[0], _grid.Columns[1].Id)); _grid.EditSession!.Value = "";
        var invalid = _grid.TryMoveRows(nodes, target, TreeDataGridRowDropPosition.Inside);
        checks["a4InvalidEditBlocksBatch"] = invalid.Status == TreeDataGridRowMoveStatus.ValidationFailed && _batchRequests == 0
            && roots[0].Children.Count == 3 && roots[1].Children.Count == 3;
        _grid.CancelEdit();
        EventHandler<TreeDataGridRowsMoveRequestedEventArgs> cancel = (_, args) => args.Cancel = true;
        _grid.RowsMoveRequested += cancel;
        var cancelled = _grid.TryMoveRows(nodes, target, TreeDataGridRowDropPosition.Inside);
        _grid.RowsMoveRequested -= cancel;
        checks["a4BatchRequestCancellation"] = cancelled.Status == TreeDataGridRowMoveStatus.Cancelled && roots[0].Children.Count == 3 && roots[1].Children.Count == 3;
        DemoPressButton(DemoFind<Button>(this, "SelectA4BatchButton"));
        TreeDataGridRowsMoveResult? captured = null;
        EventHandler<TreeDataGridRowsMoveCompletedEventArgs> completed = (_, args) => captured = args.Result;
        _grid.RowsMoveCompleted += completed;
        DemoPressButton(DemoFind<Button>(this, "MoveA4BatchButton"));
        _grid.RowsMoveCompleted -= completed;
        checks["a4BatchMoveButtonAndIdentityMapping"] = captured is { IsCommitted: true } && captured.IdentityMap.Count == 2
            && roots[0].Children.Count == 1 && roots[1].Children.Count == 5 && _grid.State.SelectedNodes.Count == 2
            && nodes.All(old => !source.TryGetRow(old, out _) && _grid.State.SelectedNodes.Contains(captured.IdentityMap[old]))
            && _grid.CurrentCell?.Node == captured.IdentityMap[current!.Value.Node];
        var local = new[] { Id(1, 0), Id(1, 1) };
        var reordered = _grid.TryMoveRows(local, Id(1, 4), TreeDataGridRowDropPosition.After);
        checks["a4SameParentBatchKeepsIdentities"] = reordered.IsCommitted && reordered.IdentityMap[local[0]] == local[0]
            && reordered.IdentityMap[local[1]] == local[1] && Id(1, 3) == local[0] && Id(1, 4) == local[1];
        EventHandler<ProjectionChangedEventArgs> fail = (_, _) => throw new InvalidOperationException("A4 observer probe");
        source.ProjectionChanged += fail;
        var observed = _grid.TryMoveRows(local, Id(1, 0), TreeDataGridRowDropPosition.Before);
        source.ProjectionChanged -= fail;
        checks["a4BatchObserverErrorIsCommitted"] = observed.Status == TreeDataGridRowMoveStatus.CommittedWithObserverError
            && observed.Error is not null && Id(1, 0) == local[0] && Id(1, 1) == local[1];
        UpdateLayout(); snapshots.Add(Snapshot("a4-batch-moving"));
        if (imagePath is not null) DemoCapture(imagePath, "a4-batch-moving", images);
    }
}
