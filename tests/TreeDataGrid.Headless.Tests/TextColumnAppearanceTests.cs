using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class TextColumnAppearanceTests
{
    [AvaloniaFact]
    public void Alignment_changes_the_actual_text_line_position_and_retains_existing_defaults()
    {
        using var source = new FlatTreeDataGridSource<string>(["short"]);
        var column = new TreeDataGridTextColumn { Width = new GridLength(240), ValueGetter = value => value };
        var grid = Create(source, column);
        var window = Show(grid);
        try
        {
            var text = DisplayText(Cell(grid, 0));
            Assert.Equal(TextAlignment.Left, text.TextAlignment);
            Assert.Same(TextTrimming.CharacterEllipsis, text.TextTrimming);
            var left = text.TextLayout.TextLines[0].Start;

            column.TextAlignment = TextAlignment.Center;
            window.UpdateLayout();
            var center = DisplayText(Cell(grid, 0)).TextLayout.TextLines[0].Start;
            column.TextAlignment = TextAlignment.Right;
            window.UpdateLayout();
            text = DisplayText(Cell(grid, 0));
            var right = text.TextLayout.TextLines[0].Start;
            Assert.True(center > left + 10, $"Center={center}, Left={left}");
            Assert.True(right > center + 10, $"Right={right}, Center={center}");
            Assert.Equal(center - left, right - center, 2);
            Assert.Same(TextTrimming.CharacterEllipsis, text.TextTrimming);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Trimming_changes_actual_collapsing_and_rejects_invalid_configuration_before_notification()
    {
        using var source = new FlatTreeDataGridSource<string>(["a considerably long value that cannot fit"]);
        var column = new TreeDataGridTextColumn { Width = new GridLength(95), ValueGetter = value => value };
        var grid = Create(source, column);
        var window = Show(grid);
        try
        {
            foreach (var trimming in new[] { TextTrimming.CharacterEllipsis, TextTrimming.WordEllipsis, TextTrimming.None })
            {
                column.TextTrimming = trimming;
                window.UpdateLayout();
                var text = DisplayText(Cell(grid, 0));
                Assert.Same(trimming, text.TextTrimming);
                Assert.Equal(!ReferenceEquals(trimming, TextTrimming.None), text.TextLayout.TextLines[0].HasCollapsed);
            }
            var notifications = 0;
            column.PropertyChanged += (_, _) => notifications++;
            Assert.Throws<ArgumentNullException>(() => column.TextTrimming = null!);
            Assert.Throws<ArgumentOutOfRangeException>(() => column.TextAlignment = (TextAlignment)(-1));
            Assert.Same(TextTrimming.None, column.TextTrimming);
            Assert.Equal(TextAlignment.Left, column.TextAlignment);
            Assert.Equal(0, notifications);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Frozen_and_recycled_cells_reapply_their_own_column_configuration_in_both_scroll_directions()
    {
        using var source = new FlatTreeDataGridSource<string>(Enumerable.Range(0, 300).Select(i => $"long row {i} description for clipping"));
        var columns = Enumerable.Range(0, 24).Select(i => new TreeDataGridTextColumn
        {
            Width = new GridLength(100), ValueGetter = value => value,
            TextAlignment = i is 0 or 1 ? TextAlignment.Right : i == 2 ? TextAlignment.Center : TextAlignment.Left,
            TextTrimming = i is 0 or 1 ? TextTrimming.WordEllipsis : i == 2 ? TextTrimming.None : TextTrimming.CharacterEllipsis,
        }).ToArray();
        var grid = Create(source, columns);
        grid.FrozenColumnCount = 1;
        var window = Show(grid);
        try
        {
            var rows = grid.RowsPresenter!;
            var originalCells = rows.RealizedRows[0].RealizedCells.Where(cell => cell.ColumnIndex > 0).ToArray();
            rows.Offset = new Vector(950, 0);
            window.UpdateLayout();
            var scrolled = rows.RealizedRows[0].RealizedCells;
            Assert.Contains(scrolled, cell => originalCells.Contains(cell));
            Assert.Contains(scrolled, cell => cell.ColumnIndex >= 10);
            Assert.True(rows.RecycledCellCount > 0);
            AssertAppearance(grid);

            rows.Offset = new Vector(950, 1600);
            window.UpdateLayout();
            Assert.True(rows.RealizedRows[0].RowIndex > 0);
            Assert.True(rows.RecycledRowCount > 0);
            AssertAppearance(grid);
            rows.Offset = default;
            window.UpdateLayout();
            Assert.Equal(0, rows.RealizedRows[0].RowIndex);
            AssertAppearance(grid);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Tree_inner_column_updates_and_replacement_release_the_previous_column_subscription()
    {
        var root = new Node("root", [new Node("child", [])]);
        using var source = new HierarchicalTreeDataGridSource<Node>([root], node => node.Children);
        source.ExpandAll();
        var inner = new TreeDataGridTextColumn { ValueGetter = value => ((Node)value).Name };
        var tree = new TreeDataGridHierarchicalExpanderColumn { Width = new GridLength(240), InnerColumn = inner };
        var grid = Create(source, tree);
        var window = Show(grid);
        try
        {
            var ids = source.Rows.Select(row => row.Id).ToArray();
            inner.TextAlignment = TextAlignment.Right;
            inner.TextTrimming = TextTrimming.None;
            window.UpdateLayout();
            foreach (var row in grid.RowsPresenter!.RealizedRows)
            {
                var text = DisplayText(row.RealizedCells.Single());
                Assert.Equal(TextAlignment.Right, text.TextAlignment);
                Assert.Same(TextTrimming.None, text.TextTrimming);
                Assert.True(text.TextLayout.TextLines[0].Start > 10);
            }
            tree.InnerColumn = new TreeDataGridTextColumn { ValueGetter = value => ((Node)value).Name };
            window.UpdateLayout();
            var replacement = DisplayText(Cell(grid, 0));
            Assert.Equal(TextAlignment.Left, replacement.TextAlignment);
            Assert.Same(TextTrimming.CharacterEllipsis, replacement.TextTrimming);
            inner.TextAlignment = TextAlignment.Center;
            window.UpdateLayout();
            Assert.Same(replacement, DisplayText(Cell(grid, 0)));
            Assert.Equal(ids, source.Rows.Select(row => row.Id));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Runtime_changes_preserve_selection_and_draft_without_modifying_application_template_text()
    {
        var model = new EditableRow();
        using var source = new FlatTreeDataGridSource<EditableRow>([model]);
        var writes = 0;
        var column = new TreeDataGridTextColumn
        {
            Width = new GridLength(150), ValueGetter = value => ((EditableRow)value).Name,
            ValueSetter = (value, text) => { writes++; ((EditableRow)value).Name = (string)text!; },
        };
        var template = new TreeDataGridTemplateColumn
        {
            Width = new GridLength(150), CellTemplate = new FuncDataTemplate<EditableRow>((_, _) => new TextBlock
            { Name = "ApplicationText", Text = "application", TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.None }),
        };
        var grid = Create(source, column, template);
        grid.SelectionUnit = TreeDataGridSelectionUnit.Cell;
        var window = Show(grid);
        try
        {
            var address = new CellAddress(source.Rows[0].Id, column.Id);
            grid.State!.SelectCell(address);
            Assert.True(grid.BeginEdit(address));
            var draft = grid.State.EditSession!;
            draft.Value = "uncommitted";
            column.TextAlignment = TextAlignment.Right;
            column.TextTrimming = TextTrimming.WordEllipsis;
            window.UpdateLayout();
            Assert.Same(draft, grid.State.EditSession);
            Assert.Equal("uncommitted", draft.Value);
            Assert.Equal(address, grid.State.CurrentCell);
            Assert.Equal(new[] { address }, grid.State.SelectedCells);
            Assert.Equal("original", model.Name);
            Assert.Equal(0, writes);
            var applicationText = grid.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "ApplicationText");
            Assert.Equal(TextAlignment.Center, applicationText.TextAlignment);
            Assert.Same(TextTrimming.None, applicationText.TextTrimming);
            grid.CancelEdit();
            window.UpdateLayout();
            Assert.Equal("original", DisplayText(Cell(grid, 0)).Text);
            Assert.Equal(TextAlignment.Right, DisplayText(Cell(grid, 0)).TextAlignment);
            Assert.Same(TextTrimming.WordEllipsis, DisplayText(Cell(grid, 0)).TextTrimming);
            Assert.Equal(0, writes);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Wrapping_and_auto_row_height_are_remeasured_after_runtime_changes()
    {
        using var source = new FlatTreeDataGridSource<string>(["one two three four five six seven eight nine ten eleven twelve"]);
        var column = new TreeDataGridTextColumn { Width = new GridLength(115), ValueGetter = value => value };
        var grid = Create(source, column);
        grid.AutoRowHeight = true;
        var window = Show(grid);
        try
        {
            var initialHeight = grid.RowsPresenter!.Layout!.GetRowHeight(0);
            column.TextWrapping = TextWrapping.Wrap;
            column.TextAlignment = TextAlignment.Right;
            column.TextTrimming = TextTrimming.None;
            window.UpdateLayout();
            var wrappedHeight = grid.RowsPresenter.Layout!.GetRowHeight(0);
            Assert.True(wrappedHeight > initialHeight + 20);
            Assert.True(DisplayText(Cell(grid, 0)).TextLayout.TextLines.Count > 1);
            column.TextAlignment = TextAlignment.Center;
            column.TextTrimming = TextTrimming.WordEllipsis;
            window.UpdateLayout();
            Assert.Equal(wrappedHeight, grid.RowsPresenter.Layout!.GetRowHeight(0), 2);
            column.TextWrapping = TextWrapping.NoWrap;
            window.UpdateLayout();
            Assert.Equal(initialHeight, grid.RowsPresenter.Layout!.GetRowHeight(0), 2);
            Assert.True(DisplayText(Cell(grid, 0)).TextLayout.TextLines[0].HasCollapsed);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Compiled_xaml_consumes_alignment_trimming_and_wrapping_with_nested_bindings()
    {
        var model = new CompiledBindingItem();
        var view = new TextColumnAppearanceView();
        var grid = view.FindControl<GridControl>("AppearanceGrid")!;
        grid.ItemsSource = new[] { model };
        var window = Show(view);
        try
        {
            Assert.Equal(TextAlignment.Right, DisplayText(Cell(grid, 0)).TextAlignment);
            Assert.Same(TextTrimming.None, DisplayText(Cell(grid, 0)).TextTrimming);
            var nested = DisplayText(Cell(grid, 1));
            Assert.Equal(TextAlignment.Center, nested.TextAlignment);
            Assert.Same(TextTrimming.WordEllipsis, nested.TextTrimming);
            Assert.Equal(TextWrapping.Wrap, nested.TextWrapping);
            model.Details.Name = "updated nested text";
            window.UpdateLayout();
            Assert.Equal("updated nested text", DisplayText(Cell(grid, 1)).Text);
            Assert.Equal(TextAlignment.Center, DisplayText(Cell(grid, 1)).TextAlignment);
        }
        finally { window.Close(); grid.ItemsSource = null; }
    }

    private static void AssertAppearance(GridControl grid)
    {
        foreach (var row in grid.RowsPresenter!.RealizedRows)
        {
            Assert.Contains(row.RealizedCells, cell => cell.ColumnIndex == 0);
            foreach (var cell in row.RealizedCells)
            {
                var column = (TreeDataGridTextColumn)cell.Column!;
                var text = DisplayText(cell);
                Assert.Equal(column.TextAlignment, text.TextAlignment);
                Assert.Same(column.TextTrimming, text.TextTrimming);
                Assert.Equal(column.TextWrapping, text.TextWrapping);
            }
        }
    }

    private static TreeDataGridCellPresenter Cell(GridControl grid, int column) => grid.RowsPresenter!.RealizedRows[0].RealizedCells.Single(cell => cell.ColumnIndex == column);
    private static TextBlock DisplayText(TreeDataGridCellPresenter cell) => cell.Content is TextBlock text ? text : ((Grid)cell.Content!).Children.OfType<TextBlock>().Single();
    private static GridControl Create(TreeDataGridSource source, params TreeDataGridColumn[] columns)
    {
        var grid = new GridControl
        {
            Source = source,
            Template = new FuncControlTemplate<GridControl>((_, scope) =>
            {
                var rows = new TreeDataGridRowsPresenter();
                var scroll = new ScrollViewer { Content = rows, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
                scope.Register("PART_RowsPresenter", rows);
                scope.Register("PART_ScrollViewer", scroll);
                return scroll;
            }),
        };
        foreach (var column in columns) grid.Columns.Add(column);
        return grid;
    }
    private static Window Show(Control content)
    {
        var window = new Window { Width = 360, Height = 220, Content = content };
        window.Show();
        window.UpdateLayout();
        return window;
    }
    private sealed record Node(string Name, Node[] Children);
    private sealed class EditableRow { public string Name { get; set; } = "original"; }
}
