using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class A6LifecycleTests
{
    private static string? Output => Environment.GetEnvironmentVariable("TREE_DATAGRID_A6_OUTPUT_DIRECTORY");

    [AvaloniaFact]
    public void Repeated_auto_height_scroll_profiles_compare_reflection_controlled_events_and_custom_factory()
    {
        var samples = new List<object>(); var expanded = !string.IsNullOrWhiteSpace(Output);
        foreach (var mode in new[] { "reflection-default", "controlled-events-default", "controlled-events-factory" })
        {
            for (var round = 0; round < 5; round++)
            {
                var models = Enumerable.Range(0, expanded ? 10000 : 1000).Select(index => new A6Model(index % 11 == 0
                    ? string.Join(' ', Enumerable.Repeat("自动行高 wrap", 12)) : "row " + index)).ToArray();
                using var source = new FlatTreeDataGridSource<A6Model>(models);
                var factory = mode.EndsWith("-factory", StringComparison.Ordinal) ? new A6Factory() : null;
                var grid = A6Host.Create(source, expanded ? 100 : 24, factory, mode != "reflection-default"); grid.AutoRowHeight = true;
                foreach (var column in grid.Columns.Cast<TreeDataGridTextColumn>()) column.TextWrapping = TextWrapping.Wrap;
                var prepared = 0; var cleared = 0; var changed = 0;
                if (mode != "reflection-default")
                {
                    grid.CellPrepared += (_, _) => prepared++; grid.CellClearing += (_, _) => cleared++; grid.CellValueChanged += (_, _) => changed++;
                }
                var window = A1TestHost.Show(grid, 640, 360); var presenter = grid.RowsPresenter!;
                try
                {
                    var address = new CellAddress(source.Rows[0].Id, grid.Columns[0].Id);
                    Assert.True(grid.BeginEdit(address)); grid.EditSession!.Value = "cancelled"; grid.CancelEdit(); Assert.Equal(0, models[0].Writes);
                    for (var frame = 0; frame < (expanded ? 100 : 4); frame++) Scroll(frame);
                    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                    var allocated = GC.GetAllocatedBytesForCurrentThread(); var started = Stopwatch.GetTimestamp();
                    var peakRows = 0; var peakCells = 0; var frameTimes = new List<double>();
                    for (var frame = 0; frame < (expanded ? 80 : 4); frame++)
                    {
                        var frameStarted = Stopwatch.GetTimestamp(); Scroll(frame + 100);
                        frameTimes.Add(Stopwatch.GetElapsedTime(frameStarted).TotalMilliseconds);
                        peakRows = Math.Max(peakRows, presenter.RetainedRowCount); peakCells = Math.Max(peakCells, presenter.RealizedCellCount);
                    }
                    var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                    var bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
                    Assert.Equal(0, changed); Assert.All(models, model => Assert.Equal(0, model.Writes));
                    samples.Add(new { mode, round, elapsedMs = elapsed, allocatedBytes = bytes, frames = frameTimes.Count,
                        medianLayoutMs = frameTimes.Order().ElementAt(frameTimes.Count / 2), maxLayoutMs = frameTimes.Max(), peakRows, peakCells,
                        presenter.CreatedRowCount, presenter.CreatedCellCount, prepared, cleared, factoryCreated = factory?.Created, factoryReleased = factory?.Released, passed = true });
                }
                finally { window.Close(); }
                Assert.Equal(prepared, cleared);
                if (factory is not null) { Assert.Empty(factory.Active); Assert.Equal(0, factory.Subscriptions); Assert.Equal(factory.Created, factory.Released); }
                void Scroll(int frame) { presenter.Offset = new Vector(frame * 137 % (expanded ? 9000 : 1800), frame * 701 % (expanded ? 250000 : 20000)); window.UpdateLayout(); AssertBounded(grid); }
            }
        }
        Write("ui-profile.json", new { schemaVersion = 1, passed = true, sampleCount = 5, rows = expanded ? 10000 : 1000, columns = expanded ? 100 : 24,
            warmupFramesPerSample = expanded ? 100 : 4, environment = EnvironmentFacts(), samples,
            scope = "Same-process repeated Headless scroll/layout, wrapped auto-height, UI-thread allocation and bounded-container assertions. Preparations/JIT can affect early samples; not native FPS, physical DPI or an input-latency/product budget." });
    }

    [AvaloniaFact]
    public void Factory_events_bindings_batch_moves_and_source_replacement_release_retired_graphs_after_render_drain()
    {
        var run = Exercise(); Drain(null); GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var retained = run.references.GroupBy(item => item.kind).Select(group => new { kind = group.Key, tracked = group.Count(), retained = group.Count(item => item.reference.TryGetTarget(out _)) }).ToArray();
        Assert.All(retained, item => Assert.Equal(0, item.retained));
        Write("durability.json", new { schemaVersion = 1, passed = true, cycles = run.cycles, environment = EnvironmentFacts(), retained, trends = run.trends,
            scope = "Weak references collected after no-inline exercise returns and dispatcher/layout/headless render drain. Tracks retired models/sources/factories/contexts/rows/cells/headers/parts/logical peers; this is finite Headless lifecycle evidence, not long native endurance." });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (int cycles, List<(string kind, WeakReference<object> reference)> references, List<object> trends) Exercise()
    {
        var cycles = string.IsNullOrWhiteSpace(Output) ? 6 : 40;
        var references = new List<(string kind, WeakReference<object> reference)>(); var trends = new List<object>();
        using var initial = new FlatTreeDataGridSource<A6Model>(Array.Empty<A6Model>());
        var grid = A6Host.Create(initial, string.IsNullOrWhiteSpace(Output) ? 24 : 100); grid.SelectionUnit = TreeDataGridSelectionUnit.Row;
        var active = new HashSet<TreeDataGridCellPresenter>(); var prepared = 0; var cleared = 0; var commits = 0;
        grid.CellPrepared += (_, args) => { Assert.True(active.Add(args.Container)); prepared++; };
        grid.CellClearing += (_, args) => { Assert.True(active.Remove(args.Container)); cleared++; };
        grid.CellValueChanged += (_, args) => { if (args.Reason == TreeDataGridCellValueChangeReason.EditCommit) commits++; };
        var window = A1TestHost.Show(grid, 640, 360);
        try
        {
            for (var cycle = 0; cycle < cycles; cycle++)
            {
                ExerciseCycle(grid, window, cycle, references);
                Assert.Empty(active); Assert.Equal(prepared, cleared); Assert.Equal(cycle + 1, commits);
                Drain(window);
                if (cycle % 5 == 4) { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); trends.Add(new { cycle = cycle + 1, managedBytes = GC.GetTotalMemory(false), tracked = references.Count, prepared, cleared, commits }); }
            }
        }
        finally { grid.ElementFactory = null; grid.Source = null; window.Content = null; window.Close(); }
        return (cycles, references, trends);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ExerciseCycle(GridControl grid, Window window, int cycle, List<(string kind, WeakReference<object> reference)> references)
    {
        var items = new TreeDataGridRowMoveCollection<A6Model>(Enumerable.Range(0, 300).Select(index => new A6Model($"cycle {cycle} row {index}")));
        using var source = new FlatTreeDataGridSource<A6Model>(items);
        var factory = new A6Factory { Prepared = context => Track("contexts", context) };
        grid.Source = source; grid.ElementFactory = factory; grid.ShowColumnHeaders = true; window.UpdateLayout();
        var peer = A6Host.Peer(grid).GetCellPeer(source.Rows[0].Id, grid.Columns[0].Id)!; Track("logicalPeers", peer);
        var address = new CellAddress(source.Rows[0].Id, grid.Columns[0].Id);
        grid.State!.Select(address.Node); grid.State.Select(source.Rows[1].Id, true);
        Assert.True(grid.BeginEdit(address)); var edit = grid.EditSession!; edit.Value = "committed " + cycle;
        window.RequestedThemeVariant = cycle % 2 == 0 ? ThemeVariant.Dark : ThemeVariant.Light;
        grid.ShowColumnHeaders = false; grid.Template = A1TestHost.Template(); window.UpdateLayout();
        Assert.Same(edit, grid.EditSession); Assert.Equal(0, items[0].Writes); Assert.True(grid.CommitEdit()); Assert.Equal(1, items[0].Writes);
        Assert.True(grid.TryMoveRows([address.Node, source.Rows[1].Id], source.Rows[^1].Id, TreeDataGridRowDropPosition.After).IsCommitted);
        for (var step = 0; step < 3; step++) { grid.RowsPresenter!.Offset = new Vector(step * 400, step * 1200); window.UpdateLayout(); AssertBounded(grid); }
        Track("sources", source); Track("factories", factory); Track("parts", grid.RowsPresenter!);
        foreach (var model in items) Track("models", model);
        foreach (var row in grid.RowsPresenter!.RealizedRows) { Track("rows", row); foreach (var cell in row.RealizedCells) Track("cells", cell); }
        grid.ShowColumnHeaders = true; window.UpdateLayout();
        foreach (var control in Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(grid).OfType<TreeDataGridColumnHeader>()) Track("headers", control);
        grid.ElementFactory = null; grid.Source = null; grid.Template = A1TestHost.Template(); window.UpdateLayout(); source.Dispose();
        Assert.Empty(factory.Active); Assert.Equal(0, factory.Subscriptions); Assert.Equal(factory.Created, factory.Released);
        Assert.All(items, model => Assert.Equal(0, model.Subscribers));
        void Track(string kind, object item) => references.Add((kind, new WeakReference<object>(item)));
    }

    private static void AssertBounded(GridControl grid)
    {
        var presenter = grid.RowsPresenter!; var rowBudget = (int)Math.Ceiling(presenter.Viewport.Height / grid.RowHeight) + 4;
        var columnBudget = (int)Math.Ceiling(presenter.Viewport.Width / 110) + grid.FrozenColumnCount + 4;
        Assert.InRange(presenter.RetainedRowCount, 1, rowBudget); Assert.InRange(presenter.RealizedCellCount, 1, rowBudget * columnBudget);
        Assert.All(presenter.RealizedRows, row => { Assert.Same(grid.Source!.Rows[row.RowIndex].Model, row.DataContext); Assert.All(row.RealizedCells, cell => Assert.Same(presenter.Layout!.Columns[cell.ColumnIndex], cell.Column)); });
    }
    private static void Drain(Window? window)
    {
        for (var pass = 0; pass < 3; pass++) { Dispatcher.UIThread.RunJobs(); window?.UpdateLayout(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(3); }
        Dispatcher.UIThread.RunJobs();
    }
    private static object EnvironmentFacts() => new { runtime = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(), processors = Environment.ProcessorCount, backend = "Avalonia.Headless", viewport = "640x360 DIP" };
    private static void Write(string name, object value)
    {
        if (string.IsNullOrWhiteSpace(Output)) return;
        Directory.CreateDirectory(Output); File.WriteAllText(Path.Combine(Output, name), JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
    }
}
