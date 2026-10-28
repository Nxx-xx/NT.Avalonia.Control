using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using NT.AvaloniaControl.TreeDataGrid;
using Xunit;

namespace TreeDataGrid.Core.Tests;

public sealed class ProjectionRebuildContractsTests
{
    [Fact]
    public void Unchanged_sequence_keeps_duplicate_identities_and_still_publishes_model_changes()
    {
        var repeated = new Model("before");
        using var source = new FlatTreeDataGridSource<Model>([repeated, repeated]);
        var oldRows = source.Rows; var ids = oldRows.Select(row => row.Id).ToArray(); var version = source.Version;
        var changes = new List<ProjectionChangedEventArgs>(); source.ProjectionChanged += (_, e) => changes.Add(e);
        repeated.Name = "after";
        Assert.Equal(version + 1, source.Version);
        Assert.Equal(ids, source.Rows.Select(row => row.Id)); Assert.NotEqual(ids[0], ids[1]);
        Assert.NotSame(oldRows, source.Rows); Assert.Equal(ids, oldRows.Select(row => row.Id));
        Assert.Equal(ProjectionChangeReason.ModelChanged, Assert.Single(changes).Reason); Assert.Empty(changes[0].RemovedNodes);
        source.Refresh();
        Assert.Equal(version + 2, source.Version); Assert.Equal(ids, source.Rows.Select(row => row.Id));
        Assert.Equal(ProjectionChangeReason.Refresh, changes[1].Reason); Assert.False(source.IsRefreshDeferred);
    }

    [Fact]
    public void Indexed_move_and_reset_override_an_identical_reference_sequence()
    {
        var repeated = new Model("same"); var models = new ResetCollection<Model> { repeated, repeated };
        using var source = new FlatTreeDataGridSource<Model>(models);
        var first = source.Rows[0].Id; var second = source.Rows[1].Id;
        models.Move(0, 1);
        Assert.Equal(new[] { second, first }, source.Rows.Select(row => row.Id));
        Assert.Equal(1, source.GetViewIndex(first)); Assert.Equal(new IndexPath(1), source.Rows[1].ModelIndex);
        models.Reset();
        Assert.DoesNotContain(first, source.Rows.Select(row => row.Id)); Assert.DoesNotContain(second, source.Rows.Select(row => row.Id));
        Assert.False(source.TryGetRow(first, out _)); Assert.False(source.TryGetRow(second, out _));
    }

    [Fact]
    public void Silent_same_length_reorders_and_replacements_fall_back_to_occurrence_order()
    {
        var a = new Model("a"); var b = new Model("b"); var models = new List<Model> { a, b, a };
        using var source = new FlatTreeDataGridSource<Model>(models);
        var ids = source.Rows.Select(row => row.Id).ToArray();
        models[1] = a; models[2] = b; source.Refresh();
        Assert.Equal(new[] { ids[0], ids[2], ids[1] }, source.Rows.Select(row => row.Id));
        models[0] = b; models[1] = new Model("new"); models[2] = a;
        ProjectionChangedEventArgs? last = null; source.ProjectionChanged += (_, e) => last = e;
        source.Refresh();
        Assert.Equal(ids[1], source.Rows[0].Id); Assert.Equal(ids[0], source.Rows[2].Id);
        Assert.DoesNotContain(source.Rows[1].Id, ids); Assert.Equal(ids[2], Assert.Single(last!.RemovedNodes));
        Assert.Equal(new IndexPath(2), source.Rows[2].ModelIndex);
    }

    [Fact]
    public void Projection_capacity_count_preserves_hidden_maps_filter_calls_and_old_snapshots()
    {
        var root = new Model("root");
        for (var i = 0; i < 2000; i++) root.Children.Add(new Model(i.ToString()));
        using var source = new HierarchicalTreeDataGridSource<Model>([root], model => model.Children);
        var rootId = Assert.Single(source.Rows).Id; Assert.True(source.TryGetNodeId(new IndexPath(0, 1999), out var hidden));
        Assert.Equal(-1, source.GetViewIndex(hidden)); Assert.True(source.TryGetRow(hidden, out _));
        var calls = 0; source.SetFilter(model => { calls++; return ReferenceEquals(model, root) || int.Parse(((Model)model).Name) % 2 == 0; });
        Assert.Equal(2001, calls); Assert.Single(source.Rows); calls = 0;
        Assert.True(source.SetExpanded(rootId, true)); Assert.Equal(2001, calls); Assert.Equal(1001, source.Rows.Count);
        Assert.Equal(-1, source.GetViewIndex(hidden)); Assert.True(source.TryGetRow(hidden, out _));
        source.SetFilter(null); Assert.Equal(2001, source.Rows.Count); Assert.Equal(2000, source.GetViewIndex(hidden));
        var expanded = source.Rows; Assert.True(source.SetExpanded(rootId, false)); source.Refresh();
        Assert.Single(source.Rows); Assert.Equal(2001, expanded.Count); Assert.Equal(hidden, expanded[^1].Id);
        root.Children.Clear();
        Assert.Single(source.Rows); Assert.Equal(rootId, source.Rows[0].Id); Assert.False(source.TryGetRow(hidden, out _));
        Assert.Equal(2001, expanded.Count);
    }

    private sealed class Model(string name) : INotifyPropertyChanged
    {
        private string _name = name;
        public string Name { get => _name; set { _name = value; PropertyChanged?.Invoke(this, new(nameof(Name))); } }
        public ObservableCollection<Model> Children { get; } = [];
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    private sealed class ResetCollection<T> : ObservableCollection<T>
    {
        internal void Reset() => OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
