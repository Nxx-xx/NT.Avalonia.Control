using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class ElementFactoryTests
{
    [AvaloniaFact]
    public void Factory_lifetimes_surround_A2_events_and_recycling_sees_cleared_detached_containers()
    {
        var model = new A5Model("row"); using var source = new FlatTreeDataGridSource<A5Model>([model]);
        var factory = new A5RecordingFactory(); var grid = Create(source, factory); var order = factory.Order;
        grid.CellPrepared += (_, e) => { order.Add("event-prepared"); Assert.Equal("factory-prepare:Cell", order[^2]); Assert.NotNull(e.Container.Content); };
        grid.CellClearing += (_, e) => { order.Add("event-clearing"); Assert.NotNull(e.Container.Content); Assert.Same(model, e.Container.DataContext); Assert.NotNull(e.Container.Parent); };
        factory.Clearing = context => { if (context.Kind == TreeDataGridElementKind.Cell) Assert.Equal("event-clearing", order[^2]); };
        var window = A1TestHost.Show(grid);
        window.Content = null; window.Close();
        Assert.Empty(factory.Active); Assert.Empty(factory.Subscriptions); Assert.Equal(factory.Created.Count, factory.Released.Count);
        Assert.Equal(1, model.Subscribers); Assert.Contains("factory-recycle:Cell", order);
    }

    [AvaloniaFact]
    public void Switching_factories_rebuilds_visible_containers_and_preserves_the_same_draft_selection_and_offset()
    {
        var models = Enumerable.Range(0, 100).Select(index => new A5Model("row " + index)).ToArray();
        using var source = new FlatTreeDataGridSource<A5Model>(models); var first = new A5RecordingFactory { Name = "first" };
        var second = new A5RecordingFactory { Name = "second" }; var grid = Create(source, first); var window = A1TestHost.Show(grid);
        try
        {
            grid.RowsPresenter!.Offset = new(0, 800); window.UpdateLayout(); var row = grid.RowsPresenter.RealizedRows[2].Row!;
            var address = new CellAddress(row.Id, grid.Columns[0].Id); grid.State!.Select(row.Id); Assert.True(grid.BeginEdit(address));
            var draft = grid.EditSession!; draft.Value = "new name"; window.UpdateLayout(); var offset = grid.RowsPresenter.Offset;
            grid.ElementFactory = second; window.UpdateLayout();
            Assert.Same(draft, grid.EditSession); Assert.Equal(offset, grid.RowsPresenter.Offset); Assert.Contains(row.Id, grid.State.SelectedNodes);
            Assert.Empty(first.Active); Assert.Empty(first.Subscriptions); Assert.Equal(first.Created.Count, first.Released.Count);
            Assert.All(grid.RowsPresenter.RealizedRows.SelectMany(item => item.RealizedCells), cell => Assert.Equal("second:Cell", cell.Tag));
            Assert.All(second.Created, element => Assert.DoesNotContain(element, first.Created));
            Assert.True(grid.CommitEdit()); Assert.Equal("new name", ((A5Model)row.Model).Name); Assert.Equal(1, ((A5Model)row.Model).Writes);
            grid.ElementFactory = null; window.UpdateLayout(); Assert.Empty(second.Active); Assert.Equal(second.Created.Count, second.Released.Count);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Both_axes_source_and_template_changes_pair_hooks_and_keep_retained_elements_viewport_bounded()
    {
        var models = Enumerable.Range(0, 2_000).Select(index => new A5Model("row " + index)).ToArray();
        using var source = new FlatTreeDataGridSource<A5Model>(models); var factory = new A5RecordingFactory(); var grid = Create(source, factory, 60);
        grid.FrozenColumnCount = 1; var window = A1TestHost.Show(grid, 480, 260);
        try
        {
            for (var step = 0; step < 12; step++)
            {
                grid.RowsPresenter!.Offset = new(step % 2 * 1_500, step * 1_000); window.UpdateLayout();
                Assert.InRange(factory.Created.Count - factory.Released.Count, 1, 160);
                Assert.InRange(grid.RowsPresenter.RetainedRowCount, 1, 15);
                Assert.InRange(factory.Active.Count, 1, 150);
            }
            Assert.True(factory.Recycled > 0); Assert.True(factory.Created.Count < 2_000);
            grid.Template = A1TestHost.Template(); window.UpdateLayout();
            using var replacement = new FlatTreeDataGridSource<A5Model>([new("replacement")]); grid.Source = replacement; window.UpdateLayout();
            Assert.All(factory.Active.Values, context => Assert.Same(replacement, context.Source));
            Assert.All(models, model => Assert.Equal(1, model.Subscribers));
            grid.Source = null; window.UpdateLayout();
            Assert.All(factory.Active.Values, context => Assert.Equal(TreeDataGridElementKind.ColumnHeader, context.Kind));
        }
        finally { window.Close(); }
        Assert.Empty(factory.Active); Assert.Empty(factory.Subscriptions); Assert.Equal(factory.Created.Count, factory.Released.Count);
    }

    [AvaloniaTheory]
    [InlineData(TreeDataGridElementKind.Row, true)]
    [InlineData(TreeDataGridElementKind.Cell, true)]
    [InlineData(TreeDataGridElementKind.ColumnHeader, true)]
    [InlineData(TreeDataGridElementKind.Row, false)]
    [InlineData(TreeDataGridElementKind.Cell, false)]
    [InlineData(TreeDataGridElementKind.ColumnHeader, false)]
    public void Invalid_factory_returns_have_a_role_diagnostic_and_can_recover(TreeDataGridElementKind kind, bool nullValue)
    {
        using var source = new FlatTreeDataGridSource<A5Model>([new("row")]); var grid = Create(source, null); var window = A1TestHost.Show(grid);
        try
        {
            var factory = new A5RecordingFactory { Creating = (owner, role) => role == kind ? nullValue ? null : new Button() : new TreeDataGridElementFactory().CreateElement(owner, role) };
            grid.ElementFactory = factory;
            Control target = kind == TreeDataGridElementKind.ColumnHeader ? grid.GetVisualDescendants().OfType<TreeDataGridColumnHeadersPresenter>().Single() : grid.RowsPresenter!;
            var error = Assert.Throws<InvalidOperationException>(() => target.Measure(new(491, 233)));
            Assert.Contains(kind.ToString(), error.Message); Assert.Contains("ElementFactory", error.Message);
            grid.ElementFactory = null; window.UpdateLayout(); Assert.Single(grid.RowsPresenter!.RealizedRows);
            Assert.Empty(factory.Active); Assert.Empty(factory.Subscriptions);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(TreeDataGridElementKind.Row, "source")]
    [InlineData(TreeDataGridElementKind.Cell, "source")]
    [InlineData(TreeDataGridElementKind.ColumnHeader, "source")]
    [InlineData(TreeDataGridElementKind.Row, "factory")]
    [InlineData(TreeDataGridElementKind.Cell, "factory")]
    [InlineData(TreeDataGridElementKind.ColumnHeader, "factory")]
    public void Reentrant_prepare_finishes_its_late_subscriptions_before_old_element_cleanup(TreeDataGridElementKind kind, string mutation)
    {
        var model = new A5Model("old"); using var source = new FlatTreeDataGridSource<A5Model>([model]);
        using var replacement = new FlatTreeDataGridSource<A5Model>([new("replacement")]);
        var first = new A5RecordingFactory(); var second = new A5RecordingFactory { Name = "second" }; var changed = false;
        var grid = Create(source, first);
        first.Preparing = context =>
        {
            if (changed || context.Kind != kind) return; changed = true;
            if (mutation == "source") context.Owner.Source = replacement; else context.Owner.ElementFactory = second;
            // RecordingFactory deliberately installs its cell model subscription after this callback returns.
        };
        var window = A1TestHost.Show(grid);
        try
        {
            Assert.True(changed); window.UpdateLayout();
            if (mutation == "factory") { Assert.Empty(first.Active); Assert.Empty(first.Subscriptions); Assert.Equal(first.Created.Count, first.Released.Count); }
            else
            {
                Assert.Same(replacement, grid.Source); Assert.Equal(1, model.Subscribers);
                Assert.All(first.Active.Values, context => Assert.Same(replacement, context.Source));
            }
        }
        finally { window.Close(); }
        Assert.Empty(first.Active); Assert.Empty(first.Subscriptions); Assert.Equal(first.Created.Count, first.Released.Count);
        Assert.Empty(second.Active); Assert.Empty(second.Subscriptions);
    }

    [AvaloniaTheory]
    [InlineData("prepare")]
    [InlineData("clear")]
    [InlineData("recycle")]
    [InlineData("release")]
    public void Hook_errors_still_clear_bindings_and_release_other_containers_then_allow_recovery(string phase)
    {
        var model = new A5Model("old"); using var source = new FlatTreeDataGridSource<A5Model>([model]);
        var factory = new A5RecordingFactory(); var next = new A5RecordingFactory(); var grid = Create(source, factory, 2); var window = A1TestHost.Show(grid);
        var thrown = false;
        void Fail(TreeDataGridElementContext context) { if (!thrown && context.Kind == TreeDataGridElementKind.Cell) { thrown = true; throw new InvalidOperationException("factory-" + phase); } }
        try
        {
            if (phase == "prepare")
            {
                factory.Preparing = Fail;
                source.Refresh(); Assert.ThrowsAny<Exception>(() => grid.RowsPresenter!.Measure(new(491, 233)));
                factory.Preparing = null;
                grid.ElementFactory = next;
            }
            else
            {
                if (phase == "clear") factory.Clearing = Fail;
                if (phase == "recycle") factory.Recycling = Fail;
                if (phase == "release") factory.Releasing = (_, _, _) => { if (!thrown) { thrown = true; throw new InvalidOperationException("release"); } };
                Assert.ThrowsAny<Exception>(() => grid.ElementFactory = next);
            }
            window.UpdateLayout(); Assert.True(thrown); Assert.Empty(factory.Active); Assert.Empty(factory.Subscriptions);
            Assert.Equal(factory.Created.Count, factory.Released.Count); Assert.Equal(2, grid.RowsPresenter!.RealizedCellCount);
        }
        finally { window.Close(); }
        Assert.Equal(1, model.Subscribers);
    }

    [AvaloniaFact]
    public void Factory_can_decline_pooling_and_a_factory_instance_can_be_shared_without_sharing_elements()
    {
        using var source = new FlatTreeDataGridSource<A5Model>(Enumerable.Range(0, 80).Select(i => new A5Model("row" + i)));
        var factory = new A5RecordingFactory { AllowRecycle = false }; var first = Create(source, factory); var second = Create(source, factory);
        var panel = new StackPanel { Children = { first, second } }; first.Height = second.Height = 180; var window = A1TestHost.Show(panel, height: 420);
        try
        {
            Assert.Equal(2, factory.Active.Values.Select(context => context.Owner).Distinct().Count());
            var created = factory.Created.Count; first.RowsPresenter!.Offset = new(0, 1_500); window.UpdateLayout(); Assert.True(factory.Created.Count > created);
            Assert.NotEmpty(factory.Released); Assert.Equal(first.RowsPresenter.RealizedRowCount, first.RowsPresenter.RetainedRowCount);
        }
        finally { window.Close(); }
        Assert.Empty(factory.Active); Assert.Equal(factory.Created.Count, factory.Released.Count);
    }

    [AvaloniaFact]
    public void Already_issued_or_parented_containers_are_rejected_without_stealing_the_original()
    {
        using var source = new FlatTreeDataGridSource<A5Model>([new("row")]); var first = Create(source, null); var second = Create(source, null);
        first.Height = second.Height = 130; var window = A1TestHost.Show(new StackPanel { Children = { first, second } }, height: 320);
        try
        {
            var existing = first.RowsPresenter!.RealizedRows[0].RealizedCells[0]; var originalParent = existing.Parent;
            second.ElementFactory = new A5RecordingFactory { Creating = (owner, kind) => kind == TreeDataGridElementKind.Cell ? existing : new TreeDataGridElementFactory().CreateElement(owner, kind) };
            var error = Assert.Throws<InvalidOperationException>(() => second.RowsPresenter!.Measure(new(491, 233))); Assert.Contains("already issued", error.Message);
            Assert.Same(originalParent, existing.Parent); Assert.NotNull(existing.Row); second.ElementFactory = null;
            var parented = new TreeDataGridCellPresenter(); var holder = new StackPanel { Children = { parented } };
            second.ElementFactory = new A5RecordingFactory { Creating = (owner, kind) => kind == TreeDataGridElementKind.Cell ? parented : new TreeDataGridElementFactory().CreateElement(owner, kind) };
            Assert.Contains("unparented", Assert.Throws<InvalidOperationException>(() => second.RowsPresenter!.Measure(new(492, 233))).Message);
            Assert.Same(holder, parented.Parent); second.ElementFactory = null; window.UpdateLayout();
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void CellPrepared_callback_can_change_factory_without_unmatched_A2_or_factory_lifetimes()
    {
        using var source = new FlatTreeDataGridSource<A5Model>([new("row")]); var first = new A5RecordingFactory(); var next = new A5RecordingFactory();
        var grid = Create(source, first); var active = new HashSet<Control>(); var changed = false;
        grid.CellPrepared += (_, e) => { Assert.True(active.Add(e.Container)); if (!changed) { changed = true; grid.ElementFactory = next; } };
        grid.CellClearing += (_, e) => Assert.True(active.Remove(e.Container));
        var window = A1TestHost.Show(grid);
        window.Close(); Assert.Empty(active); Assert.Empty(first.Active); Assert.Empty(next.Active);
        Assert.Equal(first.Created.Count, first.Released.Count); Assert.Equal(next.Created.Count, next.Released.Count);
    }

    [AvaloniaFact]
    public void Compiled_XAML_can_construct_a_custom_factory_and_reset_to_the_default()
    {
        var view = new A5FactoryView(); var grid = view.FindControl<GridControl>("FactoryGrid")!;
        var factory = Assert.IsType<A5RecordingFactory>(grid.ElementFactory); using var source = new FlatTreeDataGridSource<A5Model>([new("XAML")]);
        grid.Template = A1TestHost.Template(); grid.Source = source; var window = A1TestHost.Show(view);
        try { Assert.NotEmpty(factory.Active); Assert.Equal("xaml:Cell", A1TestHost.Cell(grid).Tag); grid.ElementFactory = null; window.UpdateLayout(); Assert.Empty(factory.Active); }
        finally { window.Close(); }
    }

    private static GridControl Create(TreeDataGridSource source, TreeDataGridElementFactory? factory, int columns = 1)
    {
        var grid = new GridControl { Source = source, ElementFactory = factory, Template = A1TestHost.Template() };
        for (var index = 0; index < columns; index++) grid.Columns.Add(new TreeDataGridTextColumn { Header = "Name", Width = new GridLength(100), Binding = new ReflectionBinding(nameof(A5Model.Name)) });
        return grid;
    }
}

public sealed partial class A5FactoryView : UserControl { public A5FactoryView() => AvaloniaXamlLoader.Load(this); }
public sealed class A5Model(string name) : INotifyPropertyChanged
{
    private PropertyChangedEventHandler? _changed;
    private string _name = name;
    public string Name { get => _name; set { _name = value; Writes++; _changed?.Invoke(this, new(nameof(Name))); } }
    public int Writes { get; private set; }
    public int Subscribers => _changed?.GetInvocationList().Length ?? 0;
    public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
}

public sealed class A5RecordingFactory : TreeDataGridElementFactory
{
    public string Name { get; set; } = "factory";
    public bool AllowRecycle { get; set; } = true;
    public readonly HashSet<Control> Created = [], Released = [];
    public readonly Dictionary<Control, TreeDataGridElementContext> Active = [];
    public readonly Dictionary<Control, (A5Model Model, PropertyChangedEventHandler Handler)> Subscriptions = [];
    public readonly List<string> Order = [];
    public int Recycled { get; private set; }
    public Func<GridControl, TreeDataGridElementKind, Control?>? Creating { get; set; }
    public Action<TreeDataGridElementContext>? Preparing { get; set; }
    public Action<TreeDataGridElementContext>? Clearing { get; set; }
    public Action<TreeDataGridElementContext>? Recycling { get; set; }
    public Action<GridControl, TreeDataGridElementKind, Control>? Releasing { get; set; }
    public override Control CreateElement(GridControl owner, TreeDataGridElementKind kind)
    {
        var element = Creating is null ? base.CreateElement(owner, kind) : Creating(owner, kind)!;
        if (element is not null) Created.Add(element);
        return element!;
    }
    public override void PrepareElement(TreeDataGridElementContext context)
    {
        Assert.True(Active.TryAdd(context.Element, context)); context.Element.Tag = Name + ":" + context.Kind;
        Order.Add("factory-prepare:" + context.Kind); Preparing?.Invoke(context);
        if (context.Kind == TreeDataGridElementKind.Cell && context.Row!.Model is A5Model model)
        {
            PropertyChangedEventHandler handler = (_, _) => { }; Subscriptions.Add(context.Element, (model, handler)); model.PropertyChanged += handler;
        }
    }
    public override void ClearElement(TreeDataGridElementContext context)
    {
        Assert.True(Active.Remove(context.Element)); Order.Add("factory-clear:" + context.Kind);
        if (Subscriptions.Remove(context.Element, out var subscription)) subscription.Model.PropertyChanged -= subscription.Handler;
        Clearing?.Invoke(context);
    }
    public override bool RecycleElement(TreeDataGridElementContext context)
    {
        Assert.Null(context.Element.Parent); Assert.Null(context.Element.DataContext);
        if (context.Element is TreeDataGridCellPresenter cell) { Assert.Null(cell.Row); Assert.Null(cell.Column); Assert.Null(cell.Content); }
        if (context.Element is TreeDataGridRowPresenter row) Assert.Null(row.Row);
        if (context.Element is TreeDataGridColumnHeader header) Assert.Null(header.Column);
        Recycled++; Order.Add("factory-recycle:" + context.Kind); Recycling?.Invoke(context); return AllowRecycle;
    }
    public override void ReleaseElement(GridControl owner, TreeDataGridElementKind kind, Control element)
    {
        Assert.True(Released.Add(element)); Assert.DoesNotContain(element, Active.Keys); Assert.Null(element.DataContext); Assert.Null(element.Parent);
        Order.Add("factory-release:" + kind); Releasing?.Invoke(owner, kind, element);
    }
}
