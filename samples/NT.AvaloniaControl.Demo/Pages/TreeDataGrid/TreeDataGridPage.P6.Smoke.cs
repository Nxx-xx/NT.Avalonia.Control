using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NT.AvaloniaControl.TreeDataGrid;

namespace NT.AvaloniaControl.Demo.Pages.TreeDataGrid;

internal sealed partial class TreeDataGridPage
{
    private async Task RunP6DemoSmokeAsync(Dictionary<string, bool> checks, List<object> snapshots, string? imagePath, List<string> images)
    {
        await DemoSelectScenarioAsync("visibility");
        var columns = _grid.Columns.ToArray();
        var widths = columns.Select(column => column.Width).ToArray();
        var ids = columns.Select(column => column.Id).ToArray();
        var item = _editingItems![0];
        var node = _source!.Rows[0].Id;
        var originalName = item.Name;
        var writes = 0;
        var nameColumn = columns[1];
        var writer = nameColumn.ValueSetter!;
        nameColumn.ValueSetter = (model, value) => { writes++; writer(model, value); };
        _grid.State!.Select(node);
        _grid.State.SetCurrentCell(new CellAddress(node, nameColumn.Id));
        _grid.FrozenColumnCount = 1;
        var beganEdit = _grid.BeginEdit(new CellAddress(node, nameColumn.Id));
        _grid.State.EditSession!.Value = "隐藏后不能写入的草稿";
        var session = _grid.State.EditSession;
        checks["p6ReorderPreservesEditorIdentity"] = _grid.TryReorderColumn(nameColumn.Id, 2)
            && _grid.Columns[2] == nameColumn && ReferenceEquals(_grid.State.EditSession, session)
            && _grid.CurrentCell == new CellAddress(node, nameColumn.Id) && writes == 0;
        checks["p6ReorderRejectsFrozenBoundary"] = !_grid.TryReorderColumn(nameColumn.Id, 0)
            && _grid.Columns[2] == nameColumn && ReferenceEquals(_grid.State.EditSession, session);
        var restoredOrder = _grid.TryReorderColumn(nameColumn.Id, 1);
        checks["p6ReorderRestoresOriginalOrder"] = restoredOrder && _grid.Columns.SequenceEqual(columns);
        _grid.FrozenColumnCount = 2;
        P6ColumnToggle(nameColumn).IsChecked = false;
        await Task.Delay(100);
        UpdateLayout();
        checks["p6HiddenEditorCancelsWithZeroWrites"] = beganEdit && writes == 0 && item.Name == originalName && _grid.State.EditSession is null;
        checks["p6HiddenCurrentMovesRight"] = _grid.CurrentCell == new CellAddress(node, columns[2].Id);
        checks["p6FrozenVisibleColumns"] = _grid.VisibleColumns.SequenceEqual(new[] { columns[0], columns[2], columns[3], columns[4] })
            && _grid.RowsPresenter!.Layout!.FrozenColumnCount == 2
            && _grid.RowsPresenter.Layout.Columns[1].Id == columns[2].Id;
        checks["p6HeaderAndCellIndicesMatchVisibleColumns"] = _grid.GetVisualDescendants().OfType<TreeDataGridColumnHeader>()
                .All(header => header.Column is { } column && _grid.GetColumnIndex(column.Id) >= 0
                    && header.Column == _grid.VisibleColumns[_grid.GetColumnIndex(column.Id)])
            && _grid.GetVisualDescendants().OfType<TreeDataGridCellPresenter>()
                .All(cell => cell.Column == _grid.VisibleColumns[cell.ColumnIndex]);
        DemoPressButton(DemoFind<Button>(this, "HideAllColumnsButton"));
        await Task.Delay(100);
        checks["p6AllHiddenClearsCurrentPreservesRow"] = _grid.VisibleColumns.Count == 0 && _grid.CurrentCell is null
            && _grid.State.SelectedNodes.Contains(node) && !_beginEditButton.IsEnabled;
        DemoPressButton(DemoFind<Button>(this, "RestoreAllColumnsButton"));
        await Task.Delay(100);
        checks["p6RestorePreservesColumnIdentityWidthAndRowFocus"] = _grid.Columns.Select(column => column.Id).SequenceEqual(ids)
            && _grid.Columns.Select(column => column.Width).SequenceEqual(widths) && _grid.VisibleColumns.SequenceEqual(columns)
            && _grid.CurrentCell == new CellAddress(node, columns[0].Id);
        snapshots.Add(Snapshot("p6-visibility-restored"));

        await DemoSelectScenarioAsync("appearance");
        var requestedTheme = _hostWindow.RequestedThemeVariant;
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            _hostWindow.RequestedThemeVariant = theme;
            await DemoWaitForAsync(() => Equals(_grid.ActualThemeVariant, theme));
            foreach (var mode in Enum.GetValues<TreeDataGridGridLinesVisibility>())
            {
                _gridLinesMode.SelectedIndex = (int)mode;
                UpdateLayout();
                var expected = mode switch
                {
                    TreeDataGridGridLinesVisibility.Horizontal => new Thickness(0, 0, 0, 1),
                    TreeDataGridGridLinesVisibility.Vertical => new Thickness(0, 0, 1, 0),
                    TreeDataGridGridLinesVisibility.All => new Thickness(0, 0, 1, 1),
                    _ => new Thickness(0),
                };
                var cells = _grid.GetVisualDescendants().OfType<TreeDataGridCellPresenter>().ToArray();
                checks[$"p6GridLines:{theme.Key}:{mode}"] = _grid.GridLinesVisibility == mode && cells.Length > 0
                    && cells.All(cell => cell.BorderThickness == expected);
            }
        }
        _hostWindow.RequestedThemeVariant = requestedTheme;
        _emptyPresentation.SelectedIndex = 1;
        DemoPressButton(DemoFind<Button>(this, "FilterToEmptyButton"));
        await Task.Delay(100);
        checks["p6FilteredEmptyShowsCustomContent"] = _appearanceItems!.Count == 32 && _source!.Rows.Count == 0
            && _grid.GetVisualDescendants().OfType<TextBlock>().Any(text => text.IsEffectivelyVisible && text.Text == _grid.EmptyContent?.ToString());
        _emptyPresentation.SelectedIndex = 2;
        await Task.Delay(100);
        var emptyAction = DemoFind<Button>(_grid, "EmptyRestoreDataButton");
        checks["p6EmptyTemplateIsActionable"] = emptyAction.IsEffectivelyVisible && _grid.EmptyContentTemplate is not null;
        if (imagePath is not null) DemoCapture(imagePath, "p6-empty-template", images);
        DemoPressButton(emptyAction);
        await Task.Delay(100);
        checks["p6EmptyTemplateRestoresRows"] = _source!.Rows.Count == 32 && _grid.RowsPresenter?.RealizedRowCount > 0;
        DemoPressButton(DemoFind<Button>(this, "ClearAppearanceDataButton"));
        await Task.Delay(100);
        checks["p6EmptySourceShowsSameTemplate"] = _appearanceItems.Count == 0 && _source.Rows.Count == 0
            && DemoFind<Button>(_grid, "EmptyRestoreDataButton").IsEffectivelyVisible;
        DemoPressButton(DemoFind<Button>(this, "RestoreAppearanceDataButton"));
        await Task.Delay(100);
        checks["p6DataReturnHidesEmptyContent"] = _appearanceItems.Count == 32 && _source.Rows.Count == 32
            && !_grid.GetVisualDescendants().OfType<Button>().Any(button => button.Name == "EmptyRestoreDataButton" && button.IsEffectivelyVisible);
        snapshots.Add(Snapshot("p6-appearance-restored"));
        await RunNestedBindingDemoSmokeAsync(checks, snapshots, imagePath, images);
    }

    private CheckBox P6ColumnToggle(TreeDataGridColumn column) => _columnVisibilityOptions.Children.OfType<CheckBox>()
        .Single(toggle => Equals(toggle.Content, column.Header));
}
