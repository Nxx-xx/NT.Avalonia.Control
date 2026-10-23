using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class ItemsSourceRangeImportTests
{
    [AvaloniaFact]
    public void Begin_edit_button_is_reenabled_after_controlled_range_import_completes()
    {
        var items = new TreeDataGridRowMoveCollection<Item>([new Item()]);
        var commands = new TreeDataGridEditCommands();
        var grid = new GridControl { ItemsSource = items, EditCommands = commands };
        var column = new TreeDataGridTextColumn { ValueGetter = model => ((Item)model).Name, ValueSetter = (model, value) => ((Item)model).Name = (string)value! };
        grid.Columns.Add(column);
        var button = new Button { Command = commands.BeginEditCommand };
        var window = new Window { Content = new StackPanel { Children = { button, grid } } };
        window.Show(); window.UpdateLayout();
        try
        {
            grid.CurrentCell = new(grid.Source!.Rows[0].Id, column.Id);
            Assert.True(button.IsEffectivelyEnabled);
            items.AddRange([new Item()]);
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            Assert.True(commands.BeginEditCommand.CanExecute(null));
            Assert.True(button.IsEffectivelyEnabled);
        }
        finally { window.Close(); grid.ItemsSource = null; }
    }

    [AvaloniaFact]
    public void Owned_ItemsSource_adapter_forwards_controlled_transaction_completion_without_stale_identities()
    {
        var shared = new Item();
        var items = new TreeDataGridRowMoveCollection<Item>([shared, shared]);
        var grid = new GridControl { ItemsSource = items };
        var column = new TreeDataGridTextColumn { ValueGetter = model => ((Item)model).Name, ValueSetter = (model, value) => ((Item)model).Name = (string)value! };
        grid.Columns.Add(column);
        try
        {
            var source = grid.Source!;
            var ids = source.Rows.Select(row => row.Id).ToArray();
            var notifications = 0;
            source.ProjectionChanged += (_, _) => notifications++;
            items.PropertyChanged += (_, _) =>
            {
                Assert.True(source.IsRefreshDeferred);
                source.Refresh();
                Assert.False(grid.BeginEdit(new(ids[0], column.Id)));
            };
            items.CollectionChanged += (_, _) => source.Refresh(); // After the source's indexed notification.
            items.AddRange([new Item(), shared]);
            Assert.False(source.IsRefreshDeferred);
            Assert.Equal(4, source.Rows.Count);
            Assert.Equal(ids, source.Rows.Take(2).Select(row => row.Id));
            Assert.Equal(4, source.Rows.Select(row => row.Id).Distinct().Count());
            Assert.InRange(notifications, 1, 2); // A late explicit Refresh may publish one additional refresh.
            Assert.True(grid.BeginEdit(new(ids[0], column.Id)));
            grid.State!.EditSession!.Value = "committed";
            Assert.True(grid.CommitEdit()); Assert.Equal("committed", shared.Name);
        }
        finally { grid.ItemsSource = null; }
    }

    private sealed class Item { public string Name { get; set; } = "original"; }
}
