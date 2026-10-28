using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using NT.AvaloniaControl.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class ColumnHeaderVisibilityTests
{
    [AvaloniaFact]
    public void Hiding_headers_releases_the_region_without_changing_auto_widths_selection_or_a_staged_editor()
    {
        using var source = new FlatTreeDataGridSource<SearchOptionRow>(Enumerable.Range(0, 80).Select(i => new SearchOptionRow("row " + i)));
        var column = new TreeDataGridTextColumn
        {
            Header = "A deliberately long column header", Width = GridLength.Auto, MaxWidth = 220,
            ValueGetter = row => ((SearchOptionRow)row).Name, ValueSetter = (row, value) => ((SearchOptionRow)row).Name = (string)value!,
        };
        var grid = A1TestHost.Create(source, column, new TreeDataGridTextColumn { Header = "Detail", Width = new GridLength(400), ValueGetter = row => row });
        grid.FrozenColumnCount = 1;
        var window = A1TestHost.Show(grid);
        try
        {
            var address = new CellAddress(source.Rows[0].Id, column.Id);
            grid.State!.Select(address.Node);
            Assert.True(grid.BeginEdit(address));
            var edit = grid.State.EditSession!; edit.Value = "uncommitted";
            window.UpdateLayout();
            var width = grid.RowsPresenter!.Layout!.GetColumnWidth(0);
            var height = grid.RowsPresenter.Viewport.Height;
            var ids = source.Rows.Select(row => row.Id).ToArray();
            grid.ShowColumnHeaders = false; window.UpdateLayout();
            Assert.Empty(grid.GetVisualDescendants().OfType<TreeDataGridColumnHeader>());
            Assert.True(grid.RowsPresenter.Viewport.Height >= height + 28);
            Assert.Equal(width, grid.RowsPresenter.Layout!.GetColumnWidth(0), 4);
            Assert.Same(edit, grid.State.EditSession); Assert.Equal("uncommitted", edit.Value);
            Assert.Equal(address, grid.State.CurrentCell); Assert.Contains(address.Node, grid.State.SelectedNodes);
            Assert.Equal("row 0", ((SearchOptionRow)source.Rows[0].Model).Name);
            grid.RowsPresenter.Offset = new Vector(70, 0); window.UpdateLayout();
            Assert.Contains(grid.RowsPresenter.RealizedRows[0].RealizedCells, cell => cell.ColumnIndex == 0);
            grid.ShowColumnHeaders = true; window.UpdateLayout();
            Assert.NotNull(A1TestHost.Header(grid));
            Assert.Equal(width, grid.RowsPresenter.Layout!.GetColumnWidth(0), 4);
            Assert.Equal(height, grid.RowsPresenter.Viewport.Height, 4);
            Assert.Equal(ids, source.Rows.Select(row => row.Id));
            Assert.Same(edit, grid.State.EditSession);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Hidden_headers_leave_the_automation_tree_and_cached_header_actions_are_disabled_until_restored()
    {
        using var source = new FlatTreeDataGridSource<SearchOptionRow>([new("b"), new("a")]);
        var column = new TreeDataGridTextColumn { Header = "Name", ValueGetter = row => ((SearchOptionRow)row).Name };
        var grid = A1TestHost.Create(source, column);
        var window = A1TestHost.Show(grid);
        try
        {
            var peer = (TreeDataGridAutomationPeer)ControlAutomationPeer.CreatePeerForElement(grid)!;
            var header = peer.GetColumnHeaderPeer(column.Id)!;
            Assert.Contains(peer.GetChildren()!, child => child.GetAutomationControlType() == AutomationControlType.HeaderItem);
            grid.ShowColumnHeaders = false; window.UpdateLayout();
            Assert.Null(peer.GetColumnHeaderPeer(column.Id));
            Assert.False(header.IsEnabled()); Assert.True(header.IsOffscreen());
            Assert.Throws<ElementNotEnabledException>(() => header.GetProvider<IInvokeProvider>()!.Invoke());
            Assert.Throws<InvalidOperationException>(() => header.BringIntoView());
            Assert.DoesNotContain(peer.GetChildren()!, child => child.GetAutomationControlType() == AutomationControlType.HeaderItem);
            Assert.Equal(1, peer.ColumnCount); Assert.Null(source.SortColumn);
            grid.ShowColumnHeaders = true; window.UpdateLayout();
            Assert.True(header.IsEnabled());
            header.GetProvider<IInvokeProvider>()!.Invoke();
            Assert.Equal(TreeDataGridSortDirection.Ascending, source.SortDirection);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Hiding_during_header_press_or_reorder_cancels_the_operation_without_sorting_or_moving_columns()
    {
        using var source = new FlatTreeDataGridSource<string>(["b", "a"]);
        var grid = A1TestHost.Create(source,
            new TreeDataGridTextColumn { Header = "First", Width = new GridLength(150), ValueGetter = row => row },
            new TreeDataGridTextColumn { Header = "Second", Width = new GridLength(150), ValueGetter = row => row });
        grid.CanUserReorderColumns = true;
        var window = A1TestHost.Show(grid);
        try
        {
            var original = grid.Columns.ToArray();
            var header = A1TestHost.Header(grid);
            var point = header.TranslatePoint(new Point(25, 12), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            grid.ShowColumnHeaders = false; window.UpdateLayout();
            window.MouseUp(point, MouseButton.Left); window.UpdateLayout();
            Assert.Null(source.SortColumn); Assert.Equal(original, grid.Columns);
            grid.ShowColumnHeaders = true; window.UpdateLayout();
            header = A1TestHost.Header(grid);
            point = header.TranslatePoint(new Point(25, 12), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseMove(point + new Vector(175, 0)); window.UpdateLayout();
            Assert.Contains(":reorder-dragging", header.Classes);
            grid.ShowColumnHeaders = false; window.UpdateLayout();
            window.MouseUp(point + new Vector(175, 0), MouseButton.Left); window.UpdateLayout();
            Assert.DoesNotContain(":reorder-dragging", header.Classes);
            Assert.Equal(original, grid.Columns); Assert.Null(source.SortColumn);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Hiding_during_resize_releases_the_pointer_and_a_restored_header_can_resize_again()
    {
        using var source = new FlatTreeDataGridSource<string>(["row"]);
        var column = new TreeDataGridTextColumn { Header = "Name", Width = new GridLength(160), ValueGetter = row => row };
        var grid = A1TestHost.Create(source, column);
        var window = A1TestHost.Show(grid);
        try
        {
            var thumb = A1TestHost.Header(grid).GetVisualDescendants().OfType<Thumb>().Single();
            var point = thumb.TranslatePoint(new Point(4, 12), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseMove(point + new Vector(20, 0)); window.UpdateLayout();
            Assert.True(column.Width.Value > 160);
            grid.ShowColumnHeaders = false; window.UpdateLayout();
            var hiddenWidth = column.Width.Value;
            window.MouseMove(point + new Vector(70, 0));
            window.MouseUp(point + new Vector(70, 0), MouseButton.Left); window.UpdateLayout();
            Assert.Equal(hiddenWidth, column.Width.Value);
            grid.ShowColumnHeaders = true; window.UpdateLayout();
            thumb = A1TestHost.Header(grid).GetVisualDescendants().OfType<Thumb>().Single();
            point = thumb.TranslatePoint(new Point(4, 12), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseMove(point + new Vector(15, 0));
            window.MouseUp(point + new Vector(15, 0), MouseButton.Left); window.UpdateLayout();
            Assert.True(column.Width.Value > hiddenWidth);
        }
        finally { window.Close(); }
    }
}
