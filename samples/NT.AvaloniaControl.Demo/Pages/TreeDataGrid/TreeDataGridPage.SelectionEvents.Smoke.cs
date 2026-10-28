using Avalonia;
using NT.AvaloniaControl.TreeDataGrid;

namespace NT.AvaloniaControl.Demo.Pages.TreeDataGrid;

internal sealed partial class TreeDataGridPage
{
    private async Task RunSelectionEventsDemoSmokeAsync(Dictionary<string, bool> checks, List<object> snapshots, string? imagePath, List<string> images)
    {
        await DemoSelectScenarioAsync("selection-events");
        var source = _source!; var name = _grid.Columns[1];
        var first = new CellAddress(source.Rows[0].Id, name.Id);
        _grid.State!.SelectCell(first); _grid.BeginEdit(first);
        var edit = _grid.EditSession!; edit.Value = "事务提交示例";
        var original = _editingItems![0].Name;
        var offset = _grid.RowsPresenter!.Offset;
        var completed = _a2Completed;
        _cancelSelection.IsChecked = true;
        checks["a2CancelledLocatorKeepsDraftAndViewport"] = !_grid.TryScrollIntoView(source.Rows[100].Id, select: true)
            && ReferenceEquals(edit, _grid.EditSession) && _grid.CurrentCell == first && _grid.RowsPresenter.Offset == offset
            && _editingItems[0].Name == original && _a2Completed == completed && _a2ValueChanges == 0;
        checks["a2CancelledSearchDoesNotReportFound"] = _grid.SearchText(_editingItems[100].Name) is null && _grid.CurrentCell == first
            && _a2Completed == completed && _a2ValueChanges == 0;
        _cancelSelection.IsChecked = false;
        checks["a2AcceptedSelectionCommitsExactlyOnce"] = _grid.TryScrollIntoView(source.Rows[1].Id, select: true)
            && _grid.EditSession is null && _editingItems[0].Name == "事务提交示例" && _a2ValueChanges == 1
            && _a2LastValue?.Reason == TreeDataGridCellValueChangeReason.EditCommit;
        UpdateLayout();
        var values = _a2ValueChanges;
        _editingItems[1].Name = "外部模型更新示例"; UpdateLayout();
        checks["a2ObservableModelValueNotifiesOnce"] = _a2ValueChanges == values + 1
            && _a2LastValue?.Reason == TreeDataGridCellValueChangeReason.ModelNotification;
        values = _a2ValueChanges;
        var prepared = _a2Prepared; var cleared = _a2Cleared;
        _grid.RowsPresenter.Offset = new Vector(0, 1800); UpdateLayout();
        checks["a2RecyclePairsLifetimesWithoutValueChanges"] = _a2Prepared > prepared && _a2Cleared > cleared
            && _a2Prepared - _a2Cleared == _a2ActiveCells.Count && _a2ActiveCells.Count == _grid.RowsPresenter.RealizedCellCount
            && _a2ValueChanges == values;
        var current = _grid.CurrentCell;
        _cancelSelection.IsChecked = true; name.IsVisible = false; UpdateLayout();
        checks["a2ForcedColumnCleanupCannotBeCancelled"] = _grid.CurrentCell?.Column != name.Id && _grid.CurrentCell?.Node == current?.Node;
        name.IsVisible = true; _cancelSelection.IsChecked = false; UpdateLayout();
        snapshots.Add(Snapshot("a2-selection-events"));
        if (imagePath is not null) DemoCapture(imagePath, "a2-selection-events", images);
    }
}
