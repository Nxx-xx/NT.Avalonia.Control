using System.Collections.ObjectModel;
using System.ComponentModel;
using NT.AvaloniaControl.TreeDataGrid;

namespace TreeDataGrid.Core.Tests;

public class StateTests
{
    [Fact]
    public void ExternalDeferralCannotWriteADeletedTargetFromAnOldProjection()
    {
        var items = new ObservableCollection<Model> { new("target") };
        using var source = new FlatTreeDataGridSource<Model>(items);
        using var state = new TreeDataGridState(source);
        var writes = 0;
        state.BeginEdit(new(source.Rows[0].Id, new(1)), x => ((Model)x).Name, (_, _) => writes++);
        using (source.DeferRefresh())
        {
            items.Clear();
            Assert.Throws<InvalidOperationException>(() => state.CommitEdit("value"));
        }
        Assert.Equal(0, writes);
        Assert.Null(state.EditSession);
    }

    [Fact]
    public void ValidationSideEffectsStillNotifyStateWhenCommitThrows()
    {
        var items = new ObservableCollection<Model> { new("target") };
        using var source = new FlatTreeDataGridSource<Model>(items);
        using var state = new TreeDataGridState(source);
        state.BeginEdit(new(source.Rows[0].Id, new(1)), x => ((Model)x).Name, (_, _) => { },
            _ => { items.Clear(); return null; });
        var notifications = 0;
        state.Changed += (_, _) => notifications++;
        Assert.Throws<InvalidOperationException>(() => state.CommitEdit("value"));
        Assert.Equal(1, notifications);
        Assert.Null(state.EditSession);
    }

    [Fact]
    public void ValidationCannotWriteADeletedTargetOrReenterCommit()
    {
        var items = new ObservableCollection<Model> { new("target") };
        using var source = new FlatTreeDataGridSource<Model>(items);
        using var state = new TreeDataGridState(source);
        var writes = 0;
        state.BeginEdit(new(source.Rows[0].Id, new(1)), x => ((Model)x).Name, (_, _) => writes++,
            _ => { items.Clear(); return null; });
        Assert.Throws<InvalidOperationException>(() => state.CommitEdit("value"));
        Assert.Equal(0, writes);
        Assert.Null(state.EditSession);
        items.Add(new("new target"));
        state.BeginEdit(new(source.Rows[0].Id, new(1)), x => ((Model)x).Name, (_, _) => writes++,
            _ => { state.CommitEdit("recursive"); return null; });
        Assert.Throws<InvalidOperationException>(() => state.CommitEdit("value"));
        Assert.Equal(0, writes);
        state.CancelEdit();
    }

    [Fact]
    public void ReaderCannotStartEditOnRemovedNodeAndThrowingWriterKeepsSession()
    {
        var items = new ObservableCollection<Model> { new("target") };
        using var source = new FlatTreeDataGridSource<Model>(items);
        using var state = new TreeDataGridState(source);
        Assert.Throws<ArgumentException>(() => state.BeginEdit(new(source.Rows[0].Id, new(1)),
            _ => { items.Clear(); return "old"; }, (_, _) => { }));
        Assert.Null(state.EditSession);
        items.Add(new("new target"));
        state.BeginEdit(new(source.Rows[0].Id, new(1)), x => ((Model)x).Name,
            (_, _) => throw new InvalidOperationException("Writer rejected the value."));
        Assert.Throws<InvalidOperationException>(() => state.CommitEdit("new"));
        Assert.NotNull(state.EditSession);
        Assert.Equal("new target", items[0].Name);
        state.CancelEdit();
    }

    [Fact]
    public void SelectionAndEditFollowIdentityAcrossMoveAndInsertion()
    {
        var model = new Model("target");
        var items = new ObservableCollection<Model> { model, new("other") };
        using var source = new FlatTreeDataGridSource<Model>(items);
        using var state = new TreeDataGridState(source);
        var id = source.Rows[0].Id;
        var cell = new CellAddress(id, new ColumnId(1));
        state.Select(id);
        state.BeginEdit(cell, x => ((Model)x).Name, (x, v) => ((Model)x).Name = (string)v!);
        items.Move(0, 1);
        items.Insert(0, new("new"));
        Assert.Equal(2, source.GetViewIndex(id));
        Assert.True(state.CommitEdit("edited"));
        Assert.Equal("edited", model.Name);
        Assert.Equal("other", items[1].Name);
        Assert.Contains(id, state.SelectedNodes);
        Assert.Equal(cell, state.CurrentCell);
    }

    [Fact]
    public void ValidationCancelAndCommitHaveDefinedWriteAndNotificationOrder()
    {
        var model = new Model("old");
        using var source = new FlatTreeDataGridSource<Model>([model]);
        using var state = new TreeDataGridState(source);
        var cell = new CellAddress(source.Rows[0].Id, new ColumnId(1));
        var writes = 0;
        var events = 0;
        state.BeginEdit(cell, x => ((Model)x).Name, (x, v) => { ++writes; ((Model)x).Name = (string)v!; },
            v => string.IsNullOrWhiteSpace(v as string) ? "Required" : null);
        Assert.False(state.CommitEdit(""));
        Assert.Equal("Required", state.EditSession!.ValidationError);
        Assert.Equal(0, writes);
        state.CancelEdit();
        Assert.Equal("old", model.Name);
        state.BeginEdit(cell, x => ((Model)x).Name, (x, v) => { ++writes; ((Model)x).Name = (string)v!; });
        source.ProjectionChanged += (_, _) => { ++events; Assert.Null(state.EditSession); Assert.Equal("new", model.Name); };
        Assert.True(state.CommitEdit("new"));
        Assert.Equal(1, writes);
        Assert.Equal(1, events);
    }

    [Fact]
    public void CollapseRetainsSelectionCancelsEditAndMovesCurrentToAncestor()
    {
        var child = new Model("child");
        var root = new Model("root");
        root.Children.Add(child);
        using var source = new HierarchicalTreeDataGridSource<Model>([root], x => x.Children);
        using var state = new TreeDataGridState(source);
        var rootId = source.Rows[0].Id;
        source.SetExpanded(rootId, true);
        var childId = source.Rows[1].Id;
        state.Select(childId);
        state.BeginEdit(new(childId, new(1)), x => ((Model)x).Name, (x, v) => ((Model)x).Name = (string)v!);
        source.SetExpanded(rootId, false);
        Assert.Null(state.EditSession);
        Assert.Contains(childId, state.SelectedNodes);
        Assert.Equal(rootId, state.CurrentCell!.Value.Node);
        root.Children.Clear();
        Assert.Empty(state.SelectedNodes);
        Assert.Equal("child", child.Name);
    }

    [Fact]
    public void ReplaceCannotCarryStateToNewOccurrenceAndColumnRemovalCancelsEdit()
    {
        var items = new ObservableCollection<Model> { new("first") };
        using var source = new FlatTreeDataGridSource<Model>(items);
        using var state = new TreeDataGridState(source);
        state.Select(source.Rows[0].Id);
        state.SetCurrentCell(new(source.Rows[0].Id, new(1)));
        items[0] = items[0];
        Assert.Empty(state.SelectedNodes);
        Assert.Null(state.CurrentCell);
        state.BeginEdit(new(source.Rows[0].Id, new(2)), x => ((Model)x).Name, (_, _) => { });
        state.RemoveColumn(new(2));
        Assert.Null(state.EditSession);
        Assert.Null(state.CurrentCell);
    }

    [Fact]
    public void StateDisposalStopsNotificationsWithoutDisposingSource()
    {
        var items = new ObservableCollection<Model> { new("first") };
        using var source = new FlatTreeDataGridSource<Model>(items);
        var state = new TreeDataGridState(source);
        state.Select(source.Rows[0].Id);
        state.Changed += (_, _) => throw new InvalidOperationException("Disposed state was notified.");
        state.Dispose();
        items.Clear();
        Assert.Empty(source.Rows);
        Assert.Throws<ObjectDisposedException>(() => state.ClearSelection());
    }

    private sealed class Model(string name) : INotifyPropertyChanged
    {
        private string _name = name;
        public string Name { get => _name; set { _name = value; PropertyChanged?.Invoke(this, new(nameof(Name))); } }
        public ObservableCollection<Model> Children { get; } = [];
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
