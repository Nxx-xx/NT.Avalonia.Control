using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class MvvmReviewTests
{
    [AvaloniaFact]
    public void SingleSelectionNormalizesObservableInputOutsideItsCollectionNotification()
    {
        var first = new Item("first");
        var second = new Item("second");
        using var source = new FlatTreeDataGridSource<Item>([first, second]);
        var selected = new ObservableCollection<Item> { first };
        // A normal VM subscription makes ObservableCollection reject reentrant mutations.
        var collectionNotifications = 0;
        selected.CollectionChanged += (_, _) => collectionNotifications++;
        var grid = Create(source);
        grid.SelectedItems = selected;
        var window = Show(grid);
        try
        {
            selected.Add(second);
            Dispatcher.UIThread.RunJobs();
            Assert.Single(grid.State!.SelectedNodes);
            Assert.Same(first, Assert.Single(selected));
            Assert.True(collectionNotifications > 0);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ReentrantCommandAttachmentKeepsTheControllerCurrentlyAssignedByTheVm()
    {
        using var source = new FlatTreeDataGridSource<Item>([new("first")]);
        var grid = Create(source);
        var window = Show(grid);
        try
        {
            grid.CurrentCell = new CellAddress(source.Rows[0].Id, grid.Columns[0].Id);
            grid.BeginEditCommand.Execute(null);
            Assert.True(grid.IsEditing);
            var incoming = new TreeDataGridEditCommands();
            var replacement = new TreeDataGridEditCommands();
            var redirected = false;
            incoming.PropertyChanged += (_, args) =>
            {
                if (redirected || args.PropertyName != nameof(TreeDataGridEditCommands.EditSession) || incoming.EditSession is null) return;
                redirected = true;
                grid.EditCommands = replacement;
            };
            grid.EditCommands = incoming;
            Assert.Same(replacement, grid.EditCommands);
            Assert.True(replacement.IsEditing);
            Assert.False(incoming.IsEditing);
            replacement.CancelEditCommand.Execute(null);
            Assert.False(grid.IsEditing);
        }
        finally { window.Close(); }
    }

    private static GridControl Create(TreeDataGridSource source)
    {
        var grid = new GridControl
        {
            Source = source,
            Template = new FuncControlTemplate<GridControl>((_, scope) =>
            {
                var rows = new TreeDataGridRowsPresenter();
                var scroll = new ScrollViewer { Content = rows };
                scope.Register("PART_RowsPresenter", rows);
                scope.Register("PART_ScrollViewer", scroll);
                return scroll;
            }),
        };
        grid.Columns.Add(new TreeDataGridTextColumn
        {
            Header = "Name", Width = new GridLength(200),
            ValueGetter = model => ((Item)model).Name,
            ValueSetter = (model, value) => ((Item)model).Name = (string)value!,
        });
        return grid;
    }
    private static Window Show(Control content) { var window = new Window { Content = content, Width = 400, Height = 240 }; window.Show(); window.UpdateLayout(); return window; }
    private sealed class Item(string name) { public string Name { get; set; } = name; }
}
