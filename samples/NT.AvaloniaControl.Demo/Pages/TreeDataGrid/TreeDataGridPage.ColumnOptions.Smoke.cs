using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using NT.AvaloniaControl.TreeDataGrid;

namespace NT.AvaloniaControl.Demo.Pages.TreeDataGrid;

internal sealed partial class TreeDataGridPage
{
    private async Task RunColumnOptionsDemoSmokeAsync(Dictionary<string, bool> checks, List<object> snapshots, string? imagePath, List<string> images)
    {
        await DemoSelectScenarioAsync("column-options");
        var name = _grid.Columns[1]; var quantity = _grid.Columns[2]; var note = _grid.Columns[4];
        var target = _source!.Rows[7].Id;
        _prefixSearch.Text = "模板搜索定位示例";
        DemoPressButton(DemoFind<Button>(this, "LocatePrefixButton"));
        checks["a1TemplateBindingSearchLocatesNote"] = _grid.State!.CurrentCell == new CellAddress(target, note.Id) && _source.Rows.Count == 2000;
        note.IsTextSearchEnabled = false;
        checks["a1DisabledSearchColumnIsExcluded"] = _grid.SearchText("模板搜索定位示例") is null;
        note.IsTextSearchEnabled = true;
        _grid.State.SetCurrentCell(new(target, name.Id));
        _grid.Focus(); RouteSmokeKey(_grid, Key.F2); UpdateLayout();
        var edit = _grid.State.EditSession;
        checks["a1ColumnF2StartsStagedEdit"] = edit?.Cell == new CellAddress(target, name.Id);
        if (edit is not null) edit.Value = "列头显隐草稿";
        var height = _grid.RowsPresenter!.Viewport.Height;
        _showHeaders.IsChecked = false; UpdateLayout();
        checks["a1HeadersCollapseWithDraftPreserved"] = !_grid.ShowColumnHeaders
            && !_grid.GetVisualDescendants().OfType<TreeDataGridColumnHeader>().Any()
            && _grid.RowsPresenter.Viewport.Height >= height + 28 && ReferenceEquals(edit, _grid.State.EditSession);
        if (imagePath is not null) DemoCapture(imagePath, "a1-headers-hidden", images);
        _showHeaders.IsChecked = true; UpdateLayout();
        checks["a1HeadersRestoreWithoutLosingDraft"] = _grid.ShowColumnHeaders && _grid.GetVisualDescendants().OfType<TreeDataGridColumnHeader>().Any()
            && ReferenceEquals(edit, _grid.State.EditSession) && edit?.Value?.ToString() == "列头显隐草稿";
        _grid.CancelEdit();
        var global = _grid.IsThreeStateSort;
        _grid.IsThreeStateSort = false;
        _source.ClearSort();
        for (var i = 0; i < 3; i++)
        {
            var header = _grid.GetVisualDescendants().OfType<TreeDataGridColumnHeader>().Single(item => item.Column == name);
            header.Focus(); RouteSmokeKey(header, Key.Enter); UpdateLayout();
        }
        checks["a1ColumnTriStateOverridesGlobalFalse"] = _source.SortDirection == TreeDataGridSortDirection.None;
        _grid.IsThreeStateSort = true;
        for (var i = 0; i < 3; i++)
        {
            var header = _grid.GetVisualDescendants().OfType<TreeDataGridColumnHeader>().Single(item => item.Column == quantity);
            header.Focus(); RouteSmokeKey(header, Key.Enter); UpdateLayout();
        }
        checks["a1ColumnTwoStateOverridesGlobalTrue"] = _source.SortDirection == TreeDataGridSortDirection.Ascending;
        _grid.SortByColumn(quantity.Id, TreeDataGridSortDirection.Descending); UpdateLayout();
        var values = _source.Rows.Select(row => ((EditingItem)row.Model).Quantity).ToArray();
        checks["a1DescendingComparerUsesFinalOrder"] = values.Zip(values.Skip(1)).All(pair => pair.First >= pair.Second);
        _grid.IsThreeStateSort = global;
        snapshots.Add(Snapshot("a1-column-options"));
        if (imagePath is not null) DemoCapture(imagePath, "a1-column-options", images);
    }
}
