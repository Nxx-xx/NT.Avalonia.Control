using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class CompiledBindingAccessorsTests
{
    [AvaloniaFact]
    public void Compiled_xaml_binding_displays_tracks_nested_replacement_and_requires_a_staged_writer()
    {
        var item = new CompiledBindingItem();
        using var source = new FlatTreeDataGridSource<CompiledBindingItem>([item]);
        var view = new CompiledBindingConsumerView();
        var grid = view.FindControl<GridControl>("CompiledGrid")!;
        grid.Source = source;
        var window = new Window { Content = view, Width = 440, Height = 240 };
        window.Show();
        window.UpdateLayout();
        try
        {
            Assert.All(grid.Columns, column => Assert.IsAssignableFrom<CompiledBinding>(((TreeDataGridTextColumn)column).Binding));
            Assert.Equal(new[] { "name", "nested", "12" }, Values(grid));
            var old = item.Details;
            item.Details = new() { Name = "replacement" };
            window.UpdateLayout();
            Assert.Equal("replacement", Values(grid)[1]);
            old.Name = "detached";
            Assert.Equal("replacement", Values(grid)[1]);
            Assert.False(grid.BeginEdit(new(source.Rows[0].Id, grid.Columns[0].Id)));
            grid.Columns[0].SetValueAccessors<CompiledBindingItem, string>(model => model.Name, (model, value) => model.Name = value);
            Assert.True(grid.BeginEdit(new(source.Rows[0].Id, grid.Columns[0].Id)));
            Assert.Equal("name", grid.State!.EditSession!.Value);
            grid.State.EditSession.Value = "draft";
            Assert.Equal("name", item.Name);
            grid.CancelEdit();
            Assert.Equal("name", item.Name);
            Assert.True(grid.BeginEdit(new(source.Rows[0].Id, grid.Columns[0].Id)));
            grid.State.EditSession!.Value = "committed";
            Assert.True(grid.CommitEdit());
            Assert.Equal("committed", item.Name);
            window.UpdateLayout();
            Assert.Equal("committed", Values(grid)[0]);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Typed_accessors_infer_value_type_and_validate_before_calling_the_writer()
    {
        var item = new CompiledBindingItem();
        using var source = new FlatTreeDataGridSource<CompiledBindingItem>([item]);
        var grid = new GridControl { Source = source };
        var column = new TreeDataGridTextColumn();
        var writes = 0;
        column.SetValueAccessors<CompiledBindingItem, int>(model => model.Quantity, (model, value) => { writes++; model.Quantity = value; });
        column.ValueValidator = value => (int)value! < 0 ? "nonnegative" : null;
        grid.Columns.Add(column);
        var address = new CellAddress(source.Rows[0].Id, column.Id);
        Assert.Equal(typeof(int), column.ValueType);
        Assert.True(grid.BeginEdit(address));
        grid.State!.EditSession!.Value = "invalid";
        Assert.False(grid.CommitEdit());
        grid.State.EditSession.Value = "-1";
        Assert.False(grid.CommitEdit());
        Assert.Equal(0, writes);
        Assert.Equal(12, item.Quantity);
        grid.State.EditSession.Value = "42";
        Assert.True(grid.CommitEdit());
        Assert.Equal(1, writes);
        Assert.Equal(42, item.Quantity);
        column.SetValueAccessors<CompiledBindingItem, int>(model => model.Quantity);
        Assert.False(grid.BeginEdit(address));
    }

    private static string?[] Values(GridControl grid) => grid.RowsPresenter!.RealizedRows.Single().RealizedCells
        .OrderBy(cell => cell.ColumnIndex).Select(cell => ((TextBlock)cell.Content!).Text).ToArray();
}
