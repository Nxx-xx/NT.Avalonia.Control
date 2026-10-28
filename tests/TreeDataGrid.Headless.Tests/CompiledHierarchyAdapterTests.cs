using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class CompiledHierarchyAdapterTests
{
    [AvaloniaFact]
    public void Compiled_xaml_hierarchy_tracks_expansion_hint_children_replacement_null_and_detaches_old_graph()
    {
        var row = new A3Row(); var oldState = row.Hierarchy; var oldChild = new A3Row(); oldState.Children!.Add(oldChild);
        oldState.Expanded = true;
        var view = new A3BindingView(); var grid = view.FindControl<GridControl>("BindingGrid")!; grid.ItemsSource = new[] { row };
        var window = A1TestHost.Show(view, 700);
        try
        {
            var source = grid.Source!; var id = source.Rows[0].Id;
            Assert.Equal(2, source.Rows.Count); Assert.True(source.SetExpanded(id, false)); Assert.False(row.Hierarchy.Expanded);
            Assert.Single(source.Rows); row.Hierarchy.Expanded = true; Assert.Equal(2, source.Rows.Count);
            var replacementChild = new A3Row { Name = "replacement" };
            oldState.Children = [replacementChild]; window.UpdateLayout(); Assert.Equal(2, source.Rows.Count);
            Assert.Equal(0, oldChild.Subscribers); Assert.Same(replacementChild, source.Rows[1].Model);
            oldState.Children = null; window.UpdateLayout(); Assert.Single(source.Rows);
            oldState.HasChildren = true; Assert.True(source.Rows[0].HasChildren);
            oldState.HasChildren = false; Assert.False(source.Rows[0].HasChildren);
            row.Hierarchy = null!; window.UpdateLayout(); Assert.Single(source.Rows); Assert.Equal(0, oldState.Subscribers);
            row.Hierarchy = new A3Hierarchy { Expanded = true, Children = [new A3Row()] }; window.UpdateLayout();
            Assert.Equal(2, source.Rows.Count); Assert.Equal(id, source.Rows[0].Id);
            var version = source.Version; oldState.Children = [oldChild]; oldState.Expanded = true;
            Assert.Equal(version, source.Version); Assert.Equal(0, oldState.Subscribers);
        }
        finally { window.Close(); grid.ItemsSource = null; }
        Assert.Equal(0, row.Subscribers); Assert.Equal(0, row.Hierarchy.Subscribers); Assert.Equal(0, row.Hierarchy.Children![0].Subscribers);
    }

    [AvaloniaFact]
    public void Factory_hierarchy_adapters_track_nested_notifications_while_collapsed_and_filtered()
    {
        var root = new A3Row(); var child = new A3Row(); root.Hierarchy.Children!.Add(child);
        var tree = new TreeDataGridHierarchicalExpanderColumn
        {
            ChildrenBinding = TreeDataGridBinding.Create<A3Row, ObservableCollection<A3Row>?>(r => r.Hierarchy.Children),
            HasChildrenBinding = TreeDataGridBinding.Create<A3Row, bool>(r => r.Hierarchy.HasChildren),
            IsExpandedBinding = TreeDataGridBinding.Create<A3Row, bool>(r => r.Hierarchy.Expanded)
        };
        var grid = new GridControl(); grid.Columns.Add(tree); grid.ItemsSource = new[] { root };
        try
        {
            var source = (HierarchicalTreeDataGridSource<object>)grid.Source!; Assert.Single(source.Rows);
            var old = child.Hierarchy;
            child.Hierarchy = new A3Hierarchy { Expanded = true, Children = [new A3Row()] };
            Assert.Equal(0, old.Subscribers); Assert.True(child.Hierarchy.Subscribers > 0);
            root.Hierarchy.Expanded = true; Assert.Equal(3, source.Rows.Count);
            source.SetFilter(_ => false); Assert.Empty(source.Rows);
            child.Hierarchy.Children = [new A3Row(), new A3Row()]; source.SetFilter(null); Assert.Equal(4, source.Rows.Count);
        }
        finally { grid.ItemsSource = null; }
        Assert.Equal(0, root.Subscribers); Assert.Equal(0, child.Subscribers); Assert.Equal(0, child.Hierarchy.Subscribers);
    }

    [AvaloniaFact]
    public void Opaque_compiled_hierarchy_and_invalid_adapter_options_fail_without_replacing_existing_source()
    {
        var grid = new GridControl(); grid.ItemsSource = new[] { new A3Row() }; var previous = grid.Source;
        var tree = new TreeDataGridHierarchicalExpanderColumn();
        Assert.Throws<NotSupportedException>(() => tree.ChildrenBinding = CompiledBinding.Create<A3Row, ObservableCollection<A3Row>?>(r => r.Hierarchy.Children));
        var binding = TreeDataGridBinding.Create<A3Row, ObservableCollection<A3Row>?>(r => r.Hierarchy.Children); binding.Mode = BindingMode.TwoWay;
        Assert.Throws<NotSupportedException>(() => tree.ChildrenBinding = binding);
        Assert.Same(previous, grid.Source); Assert.Single(previous!.Rows); grid.ItemsSource = null;
    }
}
