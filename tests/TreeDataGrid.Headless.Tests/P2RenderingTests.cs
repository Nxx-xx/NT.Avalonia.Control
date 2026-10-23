using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class P2RenderingTests
{
    [AvaloniaFact]
    public void Star_constraints_are_order_independent_and_redistribute_remaining_space()
    {
        var a = new TreeDataGridTextColumn { Width = new GridLength(1, GridUnitType.Star), MinWidth = 0, MaxWidth = 40 };
        var b = new TreeDataGridTextColumn { Width = new GridLength(1, GridUnitType.Star), MinWidth = 80 };
        var forward = new TreeDataGridLayout([], [a, b], 100);
        var reverse = new TreeDataGridLayout([], [b, a], 100);
        Assert.Equal(20, forward.GetColumnWidth(0), 8);
        Assert.Equal(80, forward.GetColumnWidth(1), 8);
        Assert.Equal(100, forward.Extent.Width, 8);
        Assert.Equal(forward.GetColumnWidth(0), reverse.GetColumnWidth(1), 8);
        a.MaxWidth = 100;
        a.MinWidth = 60;
        b.MinWidth = 0;
        b.MaxWidth = 20;
        var mixed = new TreeDataGridLayout([], [a, b], 100);
        Assert.Equal(80, mixed.GetColumnWidth(0), 8);
        Assert.Equal(20, mixed.GetColumnWidth(1), 8);
        var overconstrained = new TreeDataGridLayout([], [a, b], 30);
        Assert.Equal(60, overconstrained.Extent.Width, 8);
    }

    [AvaloniaFact]
    public void Auto_only_measures_headers_and_realized_models_and_expands_when_new_values_appear()
    {
        var models = Enumerable.Range(0, 1000).Select(i => new Model(i == 999 ? new string('W', 70) : "x")).ToArray();
        using var source = new FlatTreeDataGridSource<Model>(models);
        var grid = Create(source);
        var calls = 0;
        var column = new TreeDataGridTextColumn { Header = "A considerably wider header", Width = GridLength.Auto, ValueGetter = model => { calls++; return ((Model)model).Name; } };
        grid.Columns.Add(column);
        var window = Show(grid);
        try
        {
            Assert.InRange(calls, 1, 30);
            var initialWidth = grid.RowsPresenter!.Layout!.GetColumnWidth(0);
            Assert.True(initialWidth > 100);
            grid.RowsPresenter.ScrollIntoView(source.Rows[^1].Id);
            window.UpdateLayout();
            Assert.InRange(calls, 1, 60);
            var expanded = grid.RowsPresenter.Layout!.GetColumnWidth(0);
            Assert.True(expanded > initialWidth);
            grid.RowsPresenter.Offset = default;
            window.UpdateLayout();
            Assert.Equal(expanded, grid.RowsPresenter.Layout!.GetColumnWidth(0));
            grid.FontSize = 30;
            window.UpdateLayout();
            Assert.True(grid.RowsPresenter.Layout.GetColumnWidth(0) > initialWidth);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Formatting_checkbox_states_templates_and_row_headers_have_correct_display_values()
    {
        using var source = new FlatTreeDataGridSource<Model>([new Model("a", null, 12), new Model("b", true, 7), new Model("c", false, 3)]);
        var grid = Create(source);
        grid.Columns.Add(new TreeDataGridRowHeaderColumn { Width = new GridLength(40) });
        grid.Columns.Add(new TreeDataGridTextColumn { Width = new GridLength(90), StringFormat = "{0:000}", ValueGetter = x => ((Model)x).Number });
        grid.Columns.Add(new TreeDataGridCheckBoxColumn { Width = new GridLength(60), IsThreeState = true, ValueGetter = x => ((Model)x).Flag });
        grid.Columns.Add(new TreeDataGridCheckBoxColumn { Width = new GridLength(60), ValueGetter = x => ((Model)x).Flag });
        var editor = new FuncDataTemplate<Model>((_, _) => new TextBox());
        grid.Columns.Add(new TreeDataGridTemplateColumn { Width = new GridLength(100), CellTemplate = new FuncDataTemplate<Model>((item, _) => new TextBlock { Text = "badge " + item!.Name }), CellEditingTemplate = editor });
        var window = Show(grid);
        try
        {
            var rows = grid.RowsPresenter!.RealizedRows;
            Assert.Equal(3, rows.Count);
            Assert.Equal("1", ((TextBlock)rows[0].RealizedCells[0].Content!).Text);
            Assert.Equal("012", ((TextBlock)rows[0].RealizedCells[1].Content!).Text);
            Assert.Null(((CheckBox)rows[0].RealizedCells[2].Content!).IsChecked);
            Assert.False(((CheckBox)rows[0].RealizedCells[3].Content!).IsChecked);
            Assert.True(((CheckBox)rows[1].RealizedCells[2].Content!).IsChecked);
            Assert.False(((CheckBox)rows[2].RealizedCells[2].Content!).IsChecked);
            Assert.Contains(grid.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == "badge c");
            Assert.Same(editor, ((TreeDataGridTemplateColumn)grid.Columns[4]).CellEditingTemplate);
            Assert.Empty(grid.GetVisualDescendants().OfType<TextBox>()); // editor contract does not implicitly enter edit mode
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Cells_and_text_controls_are_reused_in_both_axes_with_bounded_counts()
    {
        using var source = new FlatTreeDataGridSource<Model>(Enumerable.Range(0, 1000).Select(i => new Model("row " + i)));
        var grid = Create(source);
        grid.FrozenColumnCount = 2;
        for (var column = 0; column < 100; ++column)
        {
            var suffix = column;
            grid.Columns.Add(new TreeDataGridTextColumn { Width = new GridLength(80), ValueGetter = x => ((Model)x).Name + ":" + suffix });
        }
        var window = Show(grid);
        try
        {
            var presenter = grid.RowsPresenter!;
            var originals = presenter.RealizedRows.SelectMany(row => row.RealizedCells).ToArray();
            var displays = originals.Select(cell => cell.Content).ToHashSet();
            var created = presenter.CreatedCellCount;
            for (var index = 1; index <= 10; ++index)
            {
                presenter.Offset = new Vector(index * 480, index * 500);
                window.UpdateLayout();
                Assert.InRange(presenter.RealizedRowCount, 1, 12);
                Assert.True(presenter.RealizedCellCount <= presenter.RealizedRowCount * 6);
                Assert.All(presenter.RealizedRows.SelectMany(row => row.RealizedCells), cell =>
                    Assert.Equal(((Model)cell.Row!.Model).Name + ":" + cell.ColumnIndex, ((TextBlock)cell.Content!).Text));
            }
            Assert.True(presenter.RecycledCellCount > 100);
            Assert.InRange(presenter.CreatedCellCount, created, created + 12);
            Assert.Contains(presenter.RealizedRows.SelectMany(row => row.RealizedCells), cell => originals.Contains(cell));
            Assert.Contains(presenter.RealizedRows.SelectMany(row => row.RealizedCells), cell => displays.Contains(cell.Content));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Wrapped_height_is_cached_across_horizontal_scroll_and_resets_after_column_width_change()
    {
        using var source = new FlatTreeDataGridSource<Model>([new Model(string.Join(' ', Enumerable.Repeat("wrap", 35))), new Model("small")]);
        var grid = Create(source);
        grid.AutoRowHeight = true;
        var wrapping = new TreeDataGridTextColumn { Width = new GridLength(100), TextWrapping = TextWrapping.Wrap, ValueGetter = x => ((Model)x).Name };
        grid.Columns.Add(wrapping);
        for (var i = 0; i < 8; ++i) grid.Columns.Add(new TreeDataGridTextColumn { Width = new GridLength(100), ValueGetter = _ => "short" });
        var window = Show(grid);
        try
        {
            var presenter = grid.RowsPresenter!;
            var tall = presenter.Layout!.GetRowHeight(0);
            Assert.True(tall > 100);
            presenter.Offset = new Vector(500, 0);
            window.UpdateLayout();
            Assert.Equal(tall, presenter.Layout!.GetRowHeight(0));
            wrapping.Width = new GridLength(300);
            presenter.Offset = default;
            window.UpdateLayout();
            Assert.InRange(presenter.Layout!.GetRowHeight(0), 29, tall - 1);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Projection_insert_remove_move_preserves_the_top_node_and_pixel_offset()
    {
        var items = new ObservableCollection<Model>(Enumerable.Range(0, 300).Select(i => new Model("row " + i)));
        using var source = new FlatTreeDataGridSource<Model>(items);
        var grid = Create(source);
        grid.Columns.Add(new TreeDataGridTextColumn { ValueGetter = x => ((Model)x).Name });
        var window = Show(grid);
        try
        {
            var presenter = grid.RowsPresenter!;
            presenter.Offset = new Vector(0, 2805);
            window.UpdateLayout();
            var id = presenter.HitTestCell(new Point(5, 1))!.Value.NodeId;
            items.Insert(0, new Model("inserted"));
            Assert.Equal(id, presenter.HitTestCell(new Point(5, 1))!.Value.NodeId);
            Assert.Equal(2833, presenter.Offset.Y);
            items.RemoveAt(0);
            Assert.Equal(id, presenter.HitTestCell(new Point(5, 1))!.Value.NodeId);
            Assert.Equal(2805, presenter.Offset.Y);
            items.Move(100, 130);
            Assert.Equal(id, presenter.HitTestCell(new Point(5, 1))!.Value.NodeId);
            Assert.Equal(3645, presenter.Offset.Y);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Fast_header_drag_accumulates_without_intermediate_layout_and_respects_limits()
    {
        using var source = new FlatTreeDataGridSource<Model>([new Model("row")]);
        var grid = Create(source);
        grid.FrozenColumnCount = 1;
        grid.Columns.Add(new TreeDataGridTextColumn { Width = new GridLength(80) });
        var column = new TreeDataGridTextColumn { Width = new GridLength(160), MinWidth = 100, MaxWidth = 210 };
        grid.Columns.Add(column);
        grid.Columns.Add(new TreeDataGridTextColumn { Width = new GridLength(400) });
        var window = Show(grid);
        try
        {
            grid.RowsPresenter!.Offset = new Vector(40, 0);
            window.UpdateLayout();
            var header = grid.GetVisualDescendants().OfType<TreeDataGridColumnHeader>().Single(x => ReferenceEquals(x.Column, column));
            var thumb = header.GetVisualDescendants().OfType<Thumb>().Single();
            var point = thumb.TranslatePoint(new Point(4, thumb.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseMove(point + new Vector(20, 0));
            window.MouseMove(point + new Vector(40, 0));
            Assert.Equal(200, column.Width.Value, 3);
            window.MouseMove(point + new Vector(80, 0));
            window.MouseUp(point + new Vector(80, 0), MouseButton.Left);
            window.UpdateLayout();
            Assert.Equal(210, column.Width.Value, 3);
            Assert.Equal(210, grid.RowsPresenter.Layout!.GetColumnWidth(1), 3);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Duplicate_column_identity_is_rejected_before_the_collection_changes()
    {
        using var source = new FlatTreeDataGridSource<Model>([new Model("row")]);
        var grid = Create(source);
        var first = new TreeDataGridTextColumn();
        var second = new TreeDataGridTextColumn();
        grid.Columns.Add(first); grid.Columns.Add(second);
        Assert.Throws<ArgumentException>(() => grid.Columns.Add(first));
        Assert.Throws<ArgumentException>(() => grid.Columns[1] = first);
        Assert.Equal(2, grid.Columns.Count);
        Assert.Same(second, grid.Columns[1]);
        var window = Show(grid);
        try { Assert.Equal(2, grid.RowsPresenter!.Layout!.ColumnCount); }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Template_bindings_are_released_when_the_grid_is_unloaded()
    {
        var model = new CountingModel();
        using var source = new FlatTreeDataGridSource<CountingModel>([model]);
        var grid = Create(source);
        grid.Columns.Add(new TreeDataGridTemplateColumn
        {
            CellTemplate = new FuncDataTemplate<CountingModel>((_, _) =>
            {
                var text = new TextBlock();
                text.Bind(TextBlock.TextProperty, new ReflectionBinding(nameof(CountingModel.Name)));
                return text;
            })
        });
        var window = Show(grid);
        try
        {
            Assert.True(model.SubscriberCount > 1);
            window.Content = null;
            Assert.Equal(1, model.SubscriberCount);
            Assert.Equal(0, grid.RowsPresenter!.RealizedCellCount);
        }
        finally { window.Close(); }
    }

    private static GridControl Create(TreeDataGridSource source) => new()
    {
        Source = source,
        Template = new FuncControlTemplate<GridControl>((_, scope) =>
        {
            var rows = new TreeDataGridRowsPresenter { Name = "PART_RowsPresenter" };
            var headers = new TreeDataGridColumnHeadersPresenter { Name = "PART_ColumnHeadersPresenter" };
            var scroll = new ScrollViewer { Name = "PART_ScrollViewer", Content = rows, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            scope.Register("PART_RowsPresenter", rows); scope.Register("PART_ColumnHeadersPresenter", headers); scope.Register("PART_ScrollViewer", scroll);
            Grid.SetRow(scroll, 1);
            return new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Children = { headers, scroll } };
        })
    };
    private static Window Show(Control content)
    {
        var window = new Window { Width = 400, Height = 280, Content = content };
        window.Show(); window.UpdateLayout(); return window;
    }
    private sealed record Model(string Name, bool? Flag = null, int Number = 0);
    private sealed class CountingModel : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _changed;
        public string Name => "bound";
        public int SubscriberCount => _changed?.GetInvocationList().Length ?? 0;
        public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
    }
}
