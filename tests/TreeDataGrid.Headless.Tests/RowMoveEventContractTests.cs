using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class RowMoveEventContractTests
{
    [AvaloniaFact]
    public void Cancellation_stops_later_request_handlers_before_any_default_transaction()
    {
        var items = new TreeDataGridRowMoveCollection<string> { "a", "b", "c" };
        using var source = new FlatTreeDataGridSource<string>(items); var grid = Create(source); var window = Show(grid);
        try
        {
            var later = false;
            grid.RowMoveRequested += (_, args) => args.Cancel = true;
            grid.RowMoveRequested += (_, args) => { later = true; args.Cancel = false; };
            var result = grid.TryMoveRow(source.Rows[0].Id, source.Rows[1].Id, TreeDataGridRowDropPosition.After);
            Assert.Equal(TreeDataGridRowMoveStatus.Cancelled, result.Status); Assert.False(later);
            Assert.Equal(new[] { "a", "b", "c" }, items);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Handled_request_has_one_transaction_owner_and_does_not_run_later_request_handlers()
    {
        var items = new TreeDataGridRowMoveCollection<string> { "a", "b", "c" };
        using var source = new FlatTreeDataGridSource<string>(items); var grid = Create(source); var window = Show(grid);
        try
        {
            var first = source.Rows[0].Id; var target = source.Rows[1].Id; var later = 0;
            grid.RowMoveRequested += (_, args) =>
            {
                args.Handled = true;
                args.Result = args.Source.MoveRow(args.Row.Id, args.Target.Id, args.Position);
            };
            grid.RowMoveRequested += (_, _) => { later++; throw new InvalidOperationException("second owner"); };
            var result = grid.TryMoveRow(first, target, TreeDataGridRowDropPosition.After);
            Assert.True(result.IsCommitted); Assert.Equal(first, result.NodeId); Assert.Equal(0, later);
            Assert.Equal(new[] { "b", "a", "c" }, items);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void All_completion_observers_run_and_their_errors_preserve_the_already_committed_result()
    {
        var items = new TreeDataGridRowMoveCollection<string> { "a", "b", "c" };
        using var source = new FlatTreeDataGridSource<string>(items); var grid = Create(source); var window = Show(grid);
        try
        {
            var first = source.Rows[0].Id; var target = source.Rows[1].Id; var later = 0;
            source.ProjectionChanged += (_, _) => throw new InvalidOperationException("projection error");
            grid.RowMoveCompleted += (_, _) => throw new InvalidOperationException("completion error");
            grid.RowMoveCompleted += (_, args) => { Assert.True(args.Result.IsCommitted); later++; };
            var result = grid.TryMoveRow(first, target, TreeDataGridRowDropPosition.After);
            Assert.Equal(TreeDataGridRowMoveStatus.CommittedWithObserverError, result.Status);
            Assert.Equal(first, result.NodeId); Assert.Equal(1, later); Assert.Equal(new[] { "b", "a", "c" }, items);
            var errors = Assert.IsType<AggregateException>(result.Error).Flatten().InnerExceptions.Select(error => error.Message).ToArray();
            Assert.Contains("projection error", errors); Assert.Contains("completion error", errors);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Completion_callback_cannot_reenter_but_a_new_move_is_available_after_return()
    {
        var items = new TreeDataGridRowMoveCollection<string> { "a", "b", "c" };
        using var source = new FlatTreeDataGridSource<string>(items); var grid = Create(source); var window = Show(grid);
        try
        {
            var first = source.Rows[0].Id; var middle = source.Rows[1].Id; var last = source.Rows[2].Id;
            TreeDataGridRowMoveResult? nested = null; var entered = false;
            grid.RowMoveCompleted += (_, _) =>
            {
                if (entered) return;
                entered = true; nested = grid.TryMoveRow(first, last, TreeDataGridRowDropPosition.After);
            };
            Assert.True(grid.TryMoveRow(first, middle, TreeDataGridRowDropPosition.After).IsCommitted);
            Assert.Equal(TreeDataGridRowMoveStatus.Rejected, nested!.Status); Assert.Equal(new[] { "b", "a", "c" }, items);
            Assert.True(grid.TryMoveRow(first, last, TreeDataGridRowDropPosition.After).IsCommitted);
            Assert.Equal(new[] { "b", "c", "a" }, items);
        }
        finally { window.Close(); }
    }

    private static GridControl Create(TreeDataGridSource source)
    {
        var grid = new GridControl
        {
            Source = source, CanUserMoveRows = true,
            Template = new FuncControlTemplate<GridControl>((_, scope) =>
            {
                var rows = new TreeDataGridRowsPresenter();
                var scroll = new ScrollViewer { Content = rows, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
                scope.Register("PART_RowsPresenter", rows); scope.Register("PART_ScrollViewer", scroll); return scroll;
            }),
        };
        grid.Columns.Add(new TreeDataGridTextColumn { Width = new GridLength(100), ValueGetter = model => model });
        return grid;
    }
    private static Window Show(Control content)
    {
        var window = new Window { Width = 400, Height = 240, Content = content };
        window.Show(); window.UpdateLayout(); return window;
    }
}
