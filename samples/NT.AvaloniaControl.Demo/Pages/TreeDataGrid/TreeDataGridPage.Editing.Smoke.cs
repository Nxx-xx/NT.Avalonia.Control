using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NT.AvaloniaControl.TreeDataGrid;

namespace NT.AvaloniaControl.Demo.Pages.TreeDataGrid;

internal sealed partial class TreeDataGridPage
{
    private async Task RunP4P5SmokeAsync(Dictionary<string, bool> checks, List<object> snapshots, string? imagePath)
    {
        ShowEditingScenario();
        _hostWindow.RequestedThemeVariant = ThemeVariant.Light;
        await Task.Delay(100);
        var source = _source!;
        var item = _editingItems![0];
        var node = source.Rows[0].Id;
        var nameCell = new CellAddress(node, _grid.Columns[1].Id);
        var quantityCell = new CellAddress(node, _grid.Columns[2].Id);
        SelectDemoRectangle();
        UpdateLayout();
        checks["rectangleSelection"] = _grid.State!.SelectedCells.Count() == 9
            && _grid.GetVisualDescendants().OfType<TreeDataGridCellPresenter>().Count(cell => cell.Classes.Contains(":selected")) == 9;
        snapshots.Add(Snapshot("editable-rectangle-selection"));
        if (imagePath is not null) CaptureSmokeImage(ScenarioImagePath(imagePath, "selection"));

        _grid.State.SelectCell(quantityCell);
        _grid.Focus();
        RouteSmokeKey(_grid, Key.F2);
        UpdateLayout();
        var editor = SmokeEditor();
        checks["f2EditorCreated"] = editor is not null && _grid.State.EditSession?.Cell == quantityCell;
        var originalQuantity = item.Quantity;
        editor!.Text = "200";
        RouteSmokeKey(editor, Key.Enter);
        UpdateLayout();
        checks["invalidEditRejected"] = item.Quantity == originalQuantity && _grid.State.EditSession?.ValidationError is not null
            && _grid.GetVisualDescendants().OfType<TreeDataGridCellPresenter>().Any(cell => cell.Classes.Contains(":invalid"));
        checks["invalidEditBlocksSort"] = !_grid.SortByColumn(_grid.Columns[1].Id, TreeDataGridSortDirection.Ascending)
            && source.SortDirection == TreeDataGridSortDirection.None;
        _lastAction.Text = "校验失败：数量 200 超出 0–100；红色单元格保留草稿，模型未写入，排序被拒绝。";
        snapshots.Add(Snapshot("invalid-integer-editor"));
        if (imagePath is not null) CaptureSmokeImage(ScenarioImagePath(imagePath, "editing"));
        SmokeEditor()!.Text = "42";
        RouteSmokeKey(SmokeEditor()!, Key.Enter);
        UpdateLayout();
        checks["integerEditCommitted"] = item.Quantity == 42 && _grid.State.EditSession is null;
        _grid.BeginEdit(quantityCell);
        UpdateLayout();
        SmokeEditor()!.Text = "9";
        RouteSmokeKey(SmokeEditor()!, Key.Escape);
        checks["escapeCancelsEdit"] = item.Quantity == 42 && _grid.State.EditSession is null;

        _grid.BeginEdit(new CellAddress(node, _grid.Columns[3].Id));
        UpdateLayout();
        var checkbox = _grid.GetVisualDescendants().OfType<TreeDataGridCellPresenter>()
            .Single(cell => cell.Row?.Id == node && cell.ColumnIndex == 3).GetVisualDescendants().OfType<CheckBox>().Single();
        checkbox.IsChecked = true;
        var boolDraft = item.Enabled is null;
        checks["nullableBooleanEdit"] = boolDraft && _grid.CommitEdit() && item.Enabled == true;
        _grid.BeginEdit(new CellAddress(node, _grid.Columns[4].Id));
        UpdateLayout();
        var customEditor = _grid.GetVisualDescendants().OfType<TextBox>().Single(control => control.Name == "PART_TemplateEditor");
        const string committedNote = "通过自定义编辑模板提交的说明";
        customEditor.Text = committedNote;
        checks["templateEditorCommit"] = _grid.State.EditSession?.Value as string == committedNote
            && _grid.CommitEdit() && item.Note == committedNote;

        _grid.BeginEdit(nameCell);
        UpdateLayout();
        SmokeEditor()!.Text = "跨主题和回收的草稿";
        var session = _grid.State.EditSession;
        _hostWindow.RequestedThemeVariant = ThemeVariant.Dark;
        ToggleApplicationTemplate();
        await Task.Delay(100);
        checks["editingThemeTemplatePreservesDraft"] = ReferenceEquals(session, _grid.State.EditSession)
            && SmokeEditor()?.Text == "跨主题和回收的草稿";
        ToggleApplicationTemplate();
        await Task.Delay(100);
        _grid.RowsPresenter!.Offset = new Vector(0, 5000);
        await Task.Delay(100);
        var editorRecycled = !_grid.GetVisualDescendants().OfType<TreeDataGridCellPresenter>().Any(cell => cell.Row?.Id == node);
        source.Sort(_grid.Columns[1].Id, (a, b) => StringComparer.Ordinal.Compare(((EditingItem)a).Name, ((EditingItem)b).Name), TreeDataGridSortDirection.Descending);
        _grid.RowsPresenter.ScrollIntoView(node);
        await Task.Delay(100);
        checks["editRecyclingIdentity"] = editorRecycled && ReferenceEquals(session, _grid.State.EditSession)
            && SmokeEditor()?.Text == "跨主题和回收的草稿";
        source.SetFilter(model => !ReferenceEquals(model, item));
        UpdateLayout();
        checks["filterHidesEdit"] = _grid.State.EditSession is null && item.Name == "项目 0000" && source.GetViewIndex(node) < 0;
        source.SetFilter(null);
        source.ClearSort();
        _grid.RowsPresenter.ScrollIntoView(node);
        await Task.Delay(100);

        var header = _grid.GetVisualDescendants().OfType<TreeDataGridColumnHeader>().Single(control => control.Column == _grid.Columns[2]);
        header.Focus();
        RouteSmokeKey(header, Key.Enter);
        UpdateLayout();
        checks["headerSortKeyboard"] = source.SortColumn == _grid.Columns[2].Id && source.SortDirection == TreeDataGridSortDirection.Ascending && header.SortIndicator == "▲";
        _grid.BeginEdit(quantityCell);
        UpdateLayout();
        SmokeEditor()!.Text = "7";
        checks["sortedEditTargetsIdentity"] = _grid.CommitEdit() && item.Quantity == 7 && source.Rows[0].Id == node
            && _editingItems[1].Quantity == 17;
        source.SetFilter(model => ((EditingItem)model).Quantity >= 5);
        _grid.BeginEdit(quantityCell);
        UpdateLayout();
        SmokeEditor()!.Text = "3";
        checks["commitUpdatesFilteredProjection"] = _grid.CommitEdit() && item.Quantity == 3 && source.GetViewIndex(node) < 0 && _grid.State.EditSession is null;
        _filterText.Text = "项目 01";
        _minimumQuantity.Text = "40";
        ApplyFilter();
        await Task.Delay(100);
        _lastAction.Text = "数量列升序 + 名称“项目 01”与数量 ≥ 40 筛选。编辑提交后重新排列 / 隐藏，节点身份保持稳定。";
        snapshots.Add(Snapshot("sorted-and-filtered-editable-records"));
        if (imagePath is not null) CaptureSmokeImage(ScenarioImagePath(imagePath, "sort-filter"));

        var visibleNode = source.Rows[0].Id;
        var visibleItem = (EditingItem)source.Rows[0].Model;
        var originalName = visibleItem.Name;
        _grid.BeginEdit(new CellAddress(visibleNode, _grid.Columns[1].Id));
        UpdateLayout();
        SmokeEditor()!.Text = "此草稿不应写回";
        var oldState = _grid.State;
        ShowColumnTypes();
        await Task.Delay(100);
        checks["sourceSwitchCancelsEdit"] = !ReferenceEquals(oldState, _grid.State)
            && _grid.State!.EditSession is null && visibleItem.Name == originalName;
    }

    private TextBox? SmokeEditor() => _grid.GetVisualDescendants().OfType<TextBox>().SingleOrDefault(control => control.Name == "PART_Editor");

    // Routes normal control input handling in a native Window; this is not native keyboard or IME automation.
    private static void RouteSmokeKey(Control target, Key key) => target.RaiseEvent(new KeyEventArgs
    {
        RoutedEvent = InputElement.KeyDownEvent, Key = key, Source = target,
    });
}
