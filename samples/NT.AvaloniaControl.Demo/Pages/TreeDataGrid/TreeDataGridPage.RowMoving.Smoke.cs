using Avalonia.Controls;
using NT.AvaloniaControl.TreeDataGrid;

namespace NT.AvaloniaControl.Demo.Pages.TreeDataGrid;

internal sealed partial class TreeDataGridPage
{
    private async Task RunRowMoveDemoSmokeAsync(Dictionary<string, bool> checks, List<object> snapshots, string? imagePath, List<string> images)
    {
        await DemoSelectScenarioAsync("rowmoving");
        var source = _source!;
        var roots = _rowMoveRoots!;
        var item = roots[0].Children[0];
        source.TryGetNodeId(new IndexPath(0, 0), out var node);
        source.TryGetNodeId(new IndexPath(1), out var target);
        var column = _grid.Columns[1];
        checks["p6LocationSelectsSourcePath"] = _grid.TryScrollIntoView(new IndexPath(0, 0), select: true)
            && _grid.CurrentCell?.Node == node && _grid.State!.SelectedNodes.Contains(node);
        checks["p6LocationKeepsExistingCurrentCell"] = _grid.TryScrollIntoView(node) && _grid.CurrentCell?.Node == node;
        _grid.State!.SetCurrentCell(new CellAddress(node, column.Id));
        _grid.BeginEdit(new CellAddress(node, column.Id));
        _grid.State.EditSession!.Value = "";
        var invalid = _grid.TryMoveRow(node, target, TreeDataGridRowDropPosition.Inside);
        checks["p6RowMoveInvalidEditRejected"] = invalid.Status == TreeDataGridRowMoveStatus.ValidationFailed
            && _grid.State.EditSession is not null && item.Name == "成员 1.1" && roots[0].Children.Count == 3 && roots[1].Children.Count == 3;
        _grid.State.EditSession!.Value = "已提交后移动";
        _cancelRowMoves.IsChecked = true;
        var cancelled = _grid.TryMoveRow(node, target, TreeDataGridRowDropPosition.Inside);
        checks["p6RowMoveRequestCanCancelAfterEditCommit"] = cancelled.Status == TreeDataGridRowMoveStatus.Cancelled
            && _grid.State.EditSession is null && item.Name == "已提交后移动" && roots[0].Children.Count == 3 && roots[1].Children.Count == 3;
        _cancelRowMoves.IsChecked = false;
        var moved = _grid.TryMoveRow(node, target, TreeDataGridRowDropPosition.Inside);
        source.TryGetNodeId(new IndexPath(1, 3), out var newNode);
        checks["p6CrossParentMoveReconcilesIdentityAndSelection"] = moved.IsCommitted && moved.NodeId == newNode && newNode != node
            && !source.TryGetRow(node, out _) && roots[0].Children.Count == 2 && roots[1].Children.Count == 4
            && ReferenceEquals(roots[1].Children[3], item) && _grid.CurrentCell == new CellAddress(newNode, column.Id)
            && _grid.State.SelectedNodes.Contains(newNode);
        source.TryGetNodeId(new IndexPath(1, 0), out var firstChild);
        var reordered = _grid.TryMoveRow(newNode, firstChild, TreeDataGridRowDropPosition.Before);
        checks["p6SameParentMoveKeepsNodeIdentity"] = reordered.IsCommitted && reordered.NodeId == newNode
            && ReferenceEquals(roots[1].Children[0], item) && _grid.CurrentCell == new CellAddress(newNode, column.Id);
        checks["p6RowMoveCannotEnterDescendant"] = _grid.TryMoveRow(target, newNode, TreeDataGridRowDropPosition.Inside).Status == TreeDataGridRowMoveStatus.Rejected;
        source.SetFilter(_ => true);
        checks["p6DefaultRowMoveRejectsActiveFilter"] = _grid.TryMoveRow(newNode, firstChild, TreeDataGridRowDropPosition.After).Status == TreeDataGridRowMoveStatus.Rejected
            && ReferenceEquals(roots[1].Children[0], item);
        source.SetFilter(null);
        DemoPressButton(DemoFind<Button>(this, "MoveWithinGroupButton"));
        checks["p6RowMoveButtonUsesSameRequest"] = _lastAction.Text!.Contains("移动完成", StringComparison.Ordinal);
        await Task.Delay(100);
        snapshots.Add(Snapshot("p6-row-moving"));
        if (imagePath is not null) DemoCapture(imagePath, "p6-row-moving", images);
    }
}
