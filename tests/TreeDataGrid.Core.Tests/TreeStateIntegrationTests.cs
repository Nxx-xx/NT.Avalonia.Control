using System.Collections.ObjectModel;
using System.ComponentModel;
using NT.AvaloniaControl.TreeDataGrid;

namespace TreeDataGrid.Core.Tests;

public sealed class TreeStateIntegrationTests
{
    [Fact]
    public void CancelledCollapsePreservesEditAndAcceptedCollapseCancelsWithoutWriting()
    {
        var root = new Item("root");
        root.Children.Add(new("child"));
        using var source = new HierarchicalTreeDataGridSource<Item>([root], x => x.Children);
        source.ExpandAll();
        using var state = new TreeDataGridState(source);
        var rootId = source.Rows[0].Id;
        var childId = source.Rows[1].Id;
        var writes = 0;
        state.Select(childId);
        state.BeginEdit(new(childId, new(1)), x => ((Item)x).Name, (_, _) => writes++);
        var edit = state.EditSession;
        var veto = true;
        source.RowCollapsing += (_, args) => args.Cancel = veto;
        Assert.False(source.SetExpanded(rootId, false));
        Assert.Same(edit, state.EditSession);
        Assert.Equal(childId, state.CurrentCell!.Value.Node);
        veto = false;
        Assert.True(source.SetExpanded(rootId, false));
        Assert.Null(state.EditSession);
        Assert.Equal(0, writes);
        Assert.Contains(childId, state.SelectedNodes);
        Assert.Equal(rootId, state.CurrentCell!.Value.Node);
    }

    [Fact]
    public void LazyTreeMutationAndBulkCollapseKeepStateOnTheCorrectOccurrence()
    {
        var root = new Item("lazy");
        using var source = new HierarchicalTreeDataGridSource<Item>([root], x => x.Children);
        source.RowExpanding += (_, args) =>
        {
            if (ReferenceEquals(args.Row.Model, root) && root.Children.Count == 0)
                root.Children.Add(new("loaded"));
        };
        var rootId = source.Rows[0].Id;
        source.SetExpanded(rootId, true);
        Assert.Equal(2, source.Rows.Count);
        var loadedId = source.Rows[1].Id;
        using var state = new TreeDataGridState(source);
        state.Select(loadedId);
        state.BeginEdit(new(loadedId, new(1)), x => ((Item)x).Name,
            (x, value) => ((Item)x).Name = (string)value!);
        root.Children.Insert(0, new("inserted"));
        Assert.Equal(2, source.GetViewIndex(loadedId));
        Assert.True(state.CommitEdit("edited loaded"));
        Assert.Equal("inserted", root.Children[0].Name);
        Assert.Equal("edited loaded", root.Children[1].Name);
        var projections = 0;
        source.ProjectionChanged += (_, _) => projections++;
        source.CollapseAll();
        Assert.Equal(1, projections);
        Assert.Contains(loadedId, state.SelectedNodes);
        Assert.Equal(rootId, state.CurrentCell!.Value.Node);
        root.Children.RemoveAt(1);
        Assert.Empty(state.SelectedNodes);
        source.ExpandAll();
        Assert.DoesNotContain(source.Rows, row => row.Id == loadedId);
    }

    [Fact]
    public void ModelExpansionBindingAndViewStateAgreeAfterExternalCollapse()
    {
        var root = new Item("root") { Expanded = true };
        root.Children.Add(new("child"));
        using var source = new HierarchicalTreeDataGridSource<Item>([root], x => x.Children,
            new HierarchicalTreeDataGridSourceOptions<Item>
            {
                IsExpandedSelector = x => x.Expanded,
                IsExpandedWriter = (x, value) => x.Expanded = value,
            });
        using var state = new TreeDataGridState(source);
        var childId = source.Rows[1].Id;
        state.Select(childId);
        state.BeginEdit(new(childId, new(1)), x => ((Item)x).Name, (_, _) => throw new InvalidOperationException("No write expected."));
        root.Expanded = false;
        Assert.Single(source.Rows);
        Assert.Null(state.EditSession);
        Assert.Contains(childId, state.SelectedNodes);
        Assert.Equal(source.Rows[0].Id, state.CurrentCell!.Value.Node);
        source.ExpandAll();
        Assert.True(root.Expanded);
        Assert.Equal(childId, source.Rows[1].Id);
    }

    private sealed class Item(string name) : INotifyPropertyChanged
    {
        private string _name = name;
        private bool _expanded;
        public string Name { get => _name; set { _name = value; PropertyChanged?.Invoke(this, new(nameof(Name))); } }
        public bool Expanded { get => _expanded; set { _expanded = value; PropertyChanged?.Invoke(this, new(nameof(Expanded))); } }
        public ObservableCollection<Item> Children { get; } = [];
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
