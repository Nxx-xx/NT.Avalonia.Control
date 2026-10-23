using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class P6DurabilityTests
{
    [AvaloniaFact]
    public void Repeated_scroll_visibility_template_theme_and_source_changes_release_retired_models()
    {
        var run = ExerciseDurability();
        // Collect only after the exercise's stack frame has returned; its last presenter local must
        // not keep a retired template part alive during the final retention assertion.
        Drain(null);
        DrainHeadlessRenderer(null);
        Collect();
        Assert.Equal(0, Retained(run.Models));
        foreach (var references in run.Objects.Values)
            Assert.Equal(0, RetainedObjectsCount(references));
        var output = Environment.GetEnvironmentVariable("TREE_DATAGRID_P6_DURABILITY_OUTPUT");
        if (!string.IsNullOrWhiteSpace(output)) Write(output, new
        {
            Passed = true, Cycles = 20, RetiredModels = run.Models.Count, RetainedRetiredModels = Retained(run.Models), Trends = run.Trends,
            RetiredObjects = RetainedObjects(run.Objects),
            Interpretation = "Headless functional lifecycle retention evidence with retired source, rows presenter, row, cell and scroll viewer weak references. Full-GC observations before/after dispatcher/layout and explicit headless render timer ticks distinguish queued work from retained visuals. Final assertions run after the no-inline exercise frame returns and require all tracked retired models, sources and visuals to be collectible. This does not assert a native endurance or frame-rate budget.",
        });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static DurabilityRun ExerciseDurability()
    {
        var grid = CreateGrid(1000);
        var window = new Window { Content = grid, Width = 640, Height = 360 };
        window.Show();
        window.UpdateLayout();
        var retired = new List<WeakReference<ProbeModel>>();
        var retiredObjects = new Dictionary<string, List<WeakReference<object>>>(StringComparer.Ordinal);
        var trends = new List<object>();
        try
        {
            for (var cycle = 0; cycle < 20; ++cycle)
            {
                var presenter = grid.RowsPresenter!;
                presenter.Offset = new Vector(cycle * 150 % 7000, cycle * 700 % 25000);
                grid.Columns[cycle % grid.Columns.Count].IsVisible = false;
                window.RequestedThemeVariant = cycle % 2 == 0 ? ThemeVariant.Dark : ThemeVariant.Light;
                window.UpdateLayout();
                AssertBounded(grid);
                TrackRetiredVisuals(grid, retiredObjects);
                grid.Columns[cycle % grid.Columns.Count].IsVisible = true;
                grid.Template = Template();
                grid.ApplyTemplate();
                window.UpdateLayout();
                AssertBounded(grid);
                retired.AddRange(ReplaceSource(grid, cycle, retiredObjects));
                window.UpdateLayout();
                AssertBounded(grid);
                if (cycle % 5 == 4)
                {
                    Collect();
                    var beforeDrain = GC.GetTotalMemory(false);
                    var objectsBeforeDrain = RetainedObjects(retiredObjects);
                    Drain(window);
                    Collect();
                    var afterDispatcher = GC.GetTotalMemory(false);
                    var objectsAfterDispatcher = RetainedObjects(retiredObjects);
                    DrainHeadlessRenderer(window);
                    Collect();
                    trends.Add(new { Cycle = cycle + 1, ManagedBytesBeforeDispatcherDrain = beforeDrain,
                        ManagedBytesAfterDispatcherDrain = afterDispatcher, ManagedBytes = GC.GetTotalMemory(false),
                        RetiredModels = retired.Count, WeakModelListCapacity = retired.Capacity,
                        RetainedRetiredModels = Retained(retired), ObjectsBeforeDispatcherDrain = objectsBeforeDrain,
                        ObjectsAfterDispatcherDrain = objectsAfterDispatcher, ObjectsAfterHeadlessRenderDrain = RetainedObjects(retiredObjects) });
                }
            }
        }
        finally
        {
            window.Close();
            ReleaseSource(grid);
        }
        return new(retired, retiredObjects, trends);
    }

    [AvaloniaFact]
    public void Auto_height_and_sustained_headless_scroll_have_five_bounded_probe_samples()
    {
        var output = Environment.GetEnvironmentVariable("TREE_DATAGRID_P6_UI_PROFILE_OUTPUT");
        var scrollSteps = string.IsNullOrWhiteSpace(output) ? 12 : 100;
        var samples = new List<object>();
        string? font = null;
        Size viewport = default;
        var fontSize = 0d;
        for (var repetition = 1; repetition <= 5; ++repetition)
        {
            var grid = CreateGrid(10_000);
            var window = new Window { Content = grid, Width = 640, Height = 360 };
            window.Show();
            window.UpdateLayout(); // Initial window/font/template warmup is excluded from this sample.
            try
            {
                var presenter = grid.RowsPresenter!;
                font = grid.FontFamily.ToString();
                fontSize = grid.FontSize;
                viewport = presenter.Viewport;
                Assert.True(presenter.Layout!.GetRowHeight(0) > grid.RowHeight);
                Collect();
                var collections = new[] { GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2) };
                var allocation = GC.GetTotalAllocatedBytes(true);
                var started = Stopwatch.GetTimestamp();
                for (var step = 1; step <= scrollSteps; ++step)
                {
                    presenter.Offset = new Vector(step * 137 % 6000, step * 701 % 250000);
                    window.UpdateLayout();
                    AssertBounded(grid);
                }
                var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                var allocated = GC.GetTotalAllocatedBytes(true) - allocation;
                using var process = Process.GetCurrentProcess();
                process.Refresh();
                samples.Add(new
                {
                    Repetition = repetition, ScrollSteps = scrollSteps, ElapsedMilliseconds = elapsed, AllocatedBytes = allocated,
                    CollectionsByGeneration = Enumerable.Range(0, 3).Select(generation => GC.CollectionCount(generation) - collections[generation]).ToArray(),
                    ManagedBytesAfter = GC.GetTotalMemory(false), WorkingSetBytesAfter = process.WorkingSet64,
                    CumulativeProcessPeakWorkingSetBytes = process.PeakWorkingSet64,
                    presenter.CreatedRowCount, presenter.CreatedCellCount, presenter.RecycledRowCount, presenter.RecycledCellCount,
                    Passed = true,
                });
            }
            finally { window.Close(); ReleaseSource(grid); }
        }
        if (!string.IsNullOrWhiteSpace(output)) Write(output, new
        {
            SchemaVersion = 1, Passed = true, SampleCount = 5, Rows = 10000, Columns = 100, FrozenColumns = 2,
            Environment = new
            {
                MachineName = Environment.MachineName, Runtime = RuntimeInformation.FrameworkDescription,
                OperatingSystem = RuntimeInformation.OSDescription, Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                RenderBackend = "Avalonia.Headless: functional control template with Fluent text controls", Font = font, FontSize = fontSize,
                Viewport = new { Width = viewport.Width, Height = viewport.Height },
            },
            Interpretation = "Includes synchronous headless layout and invariant assertions for sustained programmatic scrolling with wrapped auto row heights. It does not measure native frame rate, actual input latency, IME or DPI. Peak working set is cumulative for the entire test process.",
            Samples = samples,
        });
    }

    private static GridControl CreateGrid(int rows)
    {
        var models = Enumerable.Range(0, rows).Select(index => new ProbeModel(index % 11 == 0
            ? string.Join(' ', Enumerable.Repeat("wrap", 20)) : "row " + index)).ToArray();
        var grid = new GridControl { Source = new FlatTreeDataGridSource<ProbeModel>(models), AutoRowHeight = true,
            FrozenColumnCount = 2, Template = Template() };
        for (var column = 0; column < 100; ++column)
            grid.Columns.Add(new TreeDataGridTextColumn { Width = new GridLength(80),
                Binding = new ReflectionBinding(nameof(ProbeModel.Name)), TextWrapping = TextWrapping.Wrap });
        return grid;
    }

    private static FuncControlTemplate<GridControl> Template() => new((_, scope) =>
    {
        var rows = new TreeDataGridRowsPresenter();
        var scroll = new ScrollViewer { Content = rows, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        scope.Register("PART_RowsPresenter", rows);
        scope.Register("PART_ScrollViewer", scroll);
        return scroll;
    });

    private static void AssertBounded(GridControl grid)
    {
        var presenter = grid.RowsPresenter!;
        Assert.InRange(presenter.RealizedRowCount, 1, 20);
        Assert.InRange(presenter.RetainedRowCount, 1, 22);
        Assert.True(presenter.RealizedCellCount <= presenter.RealizedRowCount * 12);
        Assert.All(presenter.RealizedRows, row =>
        {
            Assert.Same(grid.Source!.Rows[row.RowIndex].Model, row.DataContext);
            Assert.All(row.RealizedCells, cell => Assert.Same(presenter.Layout!.Columns[cell.ColumnIndex], cell.Column));
        });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<ProbeModel>[] ReplaceSource(GridControl grid, int cycle, Dictionary<string, List<WeakReference<object>>> objects)
    {
        var old = grid.Source!;
        Track(objects, "Sources", old);
        var models = old.Rows.Select(row => (ProbeModel)row.Model).ToArray();
        var references = models.Select(model => new WeakReference<ProbeModel>(model)).ToArray();
        grid.Source = new FlatTreeDataGridSource<ProbeModel>(Enumerable.Range(0, 1000).Select(index => new ProbeModel($"cycle {cycle} row {index}")));
        old.Dispose();
        Assert.All(models, model => Assert.Equal(0, model.SubscriptionCount));
        return references;
    }

    private static void ReleaseSource(GridControl grid)
    {
        var source = grid.Source;
        grid.Source = null;
        source?.Dispose();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Retained(IEnumerable<WeakReference<ProbeModel>> references) => references.Count(reference => reference.TryGetTarget(out _));

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void TrackRetiredVisuals(GridControl grid, Dictionary<string, List<WeakReference<object>>> objects)
    {
        if (grid.RowsPresenter is not { } presenter) return;
        Track(objects, "RowsPresenters", presenter);
        foreach (var row in presenter.RealizedRows)
        {
            Track(objects, "RowPresenters", row);
            foreach (var cell in row.RealizedCells) Track(objects, "CellPresenters", cell);
        }
        foreach (var scroll in grid.GetVisualDescendants().OfType<ScrollViewer>()) Track(objects, "ScrollViewers", scroll);
    }

    private static void Track(Dictionary<string, List<WeakReference<object>>> objects, string kind, object value)
    {
        if (!objects.TryGetValue(kind, out var references)) objects.Add(kind, references = []);
        references.Add(new WeakReference<object>(value));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Dictionary<string, object> RetainedObjects(Dictionary<string, List<WeakReference<object>>> objects) => objects
        .ToDictionary(pair => pair.Key, pair => (object)new { Retired = pair.Value.Count, Retained = RetainedObjectsCount(pair.Value) });

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int RetainedObjectsCount(IEnumerable<WeakReference<object>> references) => references.Count(reference => reference.TryGetTarget(out _));

    private static void Drain(Window? window)
    {
        for (var pass = 0; pass < 3; ++pass)
        {
            Dispatcher.UIThread.RunJobs();
            window?.UpdateLayout();
        }
    }

    private static void DrainHeadlessRenderer(Window? window)
    {
        // The headless render timer is explicit. Layout updates alone do not consume renderer work.
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(3);
        Drain(window);
    }

    private static void Collect() { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }

    private static void Write(string path, object report)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    }

    public sealed class ProbeModel(string name) : System.ComponentModel.INotifyPropertyChanged
    {
        private System.ComponentModel.PropertyChangedEventHandler? _changed;
        public string Name { get; } = name;
        public int SubscriptionCount { get; private set; }
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; SubscriptionCount++; }
            remove { _changed -= value; SubscriptionCount--; }
        }
    }

    private sealed record DurabilityRun(List<WeakReference<ProbeModel>> Models,
        Dictionary<string, List<WeakReference<object>>> Objects, List<object> Trends);
}
