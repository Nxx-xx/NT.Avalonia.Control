using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class IntegrationReentrancyTests
{
    [AvaloniaFact]
    public void HeightCallbackSourceReplacementCannotPublishOldGeometry()
    {
        using var source = new FlatTreeDataGridSource<string>(Enumerable.Range(0, 100).Select(i => "old " + i));
        using var replacement = new FlatTreeDataGridSource<string>(["replacement"]);
        var grid = CreateGrid(source);
        var window = Show(grid);
        try
        {
            var changed = false;
            grid.RowHeightSelector = _ =>
            {
                if (!changed) { changed = true; grid.Source = replacement; }
                return 28;
            };
            window.UpdateLayout();
            Assert.True(changed);
            Assert.Equal(1, grid.RowsPresenter!.Layout!.RowCount);
            Assert.Equal("replacement", Assert.Single(grid.RowsPresenter.RealizedRows).DataContext);
            Assert.Equal(replacement.Rows[0].Id, grid.RowsPresenter.HitTestCell(new Point(20, 5))!.Value.NodeId);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void HeaderCanMeasureBeforeRowsAfterColumnsAreRemoved()
    {
        using var source = new FlatTreeDataGridSource<string>(["row"]);
        var grid = CreateGrid(source);
        var window = Show(grid);
        try
        {
            var header = grid.GetVisualDescendants().OfType<TreeDataGridColumnHeadersPresenter>().Single();
            grid.Columns.Clear();
            header.Measure(new Size(359, 28));
            window.UpdateLayout();
            Assert.Empty(header.Children);
            Assert.All(grid.RowsPresenter!.RealizedRows, row => Assert.Empty(row.Children));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void GetterThatClearsColumnsCannotRepopulateAClearedCell()
    {
        var item = new object();
        using var source = new FlatTreeDataGridSource<object>([item]);
        var grid = CreateGrid(source);
        grid.Columns.Clear();
        TreeDataGridCellPresenter? captured = null;
        grid.Columns.Add(new TreeDataGridTextColumn
        {
            ValueGetter = model =>
            {
                if (captured is null)
                {
                    captured = grid.RowsPresenter!.RealizedRows[0].RealizedCells[0];
                    grid.Columns.Clear();
                }
                return model;
            },
        });
        var window = Show(grid);
        try
        {
            Assert.NotNull(captured);
            Assert.Null(captured.Row);
            Assert.Null(captured.DataContext);
            Assert.Null(captured.Value);
            Assert.Null(captured.Content);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void BindingInitializedDuringColumnRemovalReleasesItsSubscription()
    {
        var item = new BindingItem();
        using var source = new FlatTreeDataGridSource<BindingItem>([item]);
        Assert.Equal(1, item.SubscriberCount); // The source owns one independent model subscription.
        var grid = CreateGrid(source);
        grid.Columns.Clear();
        TreeDataGridCellPresenter? captured = null;
        item.OnRead = () =>
        {
            if (captured is not null) return;
            captured = grid.RowsPresenter!.RealizedRows[0].RealizedCells[0];
            grid.Columns.Clear();
        };
        grid.Columns.Add(new TreeDataGridTextColumn { Binding = new Binding(nameof(BindingItem.Value)) { Mode = BindingMode.OneWay } });
        var window = Show(grid);
        try
        {
            Assert.NotNull(captured);
            Assert.Null(captured.Row);
            Assert.Null(captured.Value);
            Assert.Equal(1, item.SubscriberCount);
        }
        finally { window.Close(); }
    }

    private static GridControl CreateGrid(TreeDataGridSource source)
    {
        var grid = new GridControl
        {
            Source = source,
            Template = new FuncControlTemplate<GridControl>((_, scope) =>
            {
                var rows = new TreeDataGridRowsPresenter();
                var scroll = new ScrollViewer { Content = rows };
                var headers = new TreeDataGridColumnHeadersPresenter();
                scope.Register("PART_RowsPresenter", rows);
                scope.Register("PART_ScrollViewer", scroll);
                scope.Register("PART_ColumnHeadersPresenter", headers);
                Grid.SetRow(scroll, 1);
                return new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Children = { headers, scroll } };
            }),
        };
        for (var index = 0; index < 4; index++)
            grid.Columns.Add(new TreeDataGridTextColumn { Header = "C" + index, Width = new GridLength(100), ValueGetter = model => model });
        return grid;
    }

    private static Window Show(Control content)
    {
        var window = new Window { Width = 360, Height = 240, Content = content };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private sealed class BindingItem : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _changed;
        public Action? OnRead { get; set; }
        public object Value { get { OnRead?.Invoke(); return this; } }
        public int SubscriberCount => _changed?.GetInvocationList().Length ?? 0;
        public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
    }
}
