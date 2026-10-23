using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using NT.AvaloniaControl.TreeDataGrid;

internal static class P6Profile
{
    private const int Seed = 1729;
    private const int RowCount = 100_000;
    private const int MutationBaseRows = 10_000;
    private const int MutationCount = 200;
    private static readonly Size Viewport = new(1200, 700);

    public static int Run(string[] args)
    {
        var samplesText = Option(args, "--samples");
        if (samplesText is not null && (!int.TryParse(samplesText, out var parsed) || parsed < 5))
        {
            Console.Error.WriteLine("P6 profiles require at least five samples per scenario.");
            return 2;
        }
        var sampleCount = samplesText is null ? 5 : int.Parse(samplesText);
        var outputPath = Path.GetFullPath(Option(args, "--output") ?? "artifacts/benchmarks/p6-profile.json");
        var scenarios = new List<ScenarioResult>();
        var random = new Random(Seed);
        var data = Enumerable.Range(0, RowCount).Select(index => new ProfileItem(index, random.Next())).ToArray();
        var columns = Enumerable.Range(0, 100).Select(index => (TreeDataGridColumn)new TreeDataGridTextColumn
            { Header = $"Column {index}", Width = new GridLength(120) }).ToArray();
        var wideRoot = new ProfileItem(-1, 0) { Children = data };
        var deepRoots = CreateDeepRoots(1000, 100);
        Warmup(data, columns);

        Add("flat-create-100000", () => new Fixture(), fixture => fixture.Source = new FlatTreeDataGridSource<ProfileItem>(data),
            fixture => Require(fixture.Source!.Rows.Count == RowCount, "Flat creation lost rows."));
        Add("flat-refresh-100000", FlatFixture, fixture => fixture.Source!.Refresh(),
            fixture => Require(fixture.Source!.Rows.Count == RowCount && fixture.Source!.Rows[0].Id == fixture.FirstId, "Refresh changed a retained identity."));
        Add("layout-100000-100-columns-mixed-height", FlatFixture, fixture =>
            fixture.Layout = new TreeDataGridLayout(fixture.Source!.Rows, columns, Viewport.Width, 28,
                row => ((ProfileItem)row.Model).Number % 5 == 0 ? 48 : 28, 2),
            fixture => Require(fixture.Layout!.ColumnCount == 100 && fixture.Layout!.RowCount == RowCount
                && fixture.Layout!.Extent == new Size(12000, 3_200_000), "Mixed geometry is inconsistent."));
        Add("geometry-findrow-hit-test-20000-scroll-positions", () =>
        {
            var fixture = FlatFixture();
            fixture.Layout = new TreeDataGridLayout(fixture.Source!.Rows, columns, Viewport.Width, 28,
                row => ((ProfileItem)row.Model).Number % 5 == 0 ? 48 : 28, 2);
            return fixture;
        }, fixture =>
        {
            var layout = fixture.Layout!;
            for (var index = 0; index < 20_000; ++index)
            {
                var y = (index * 137L) % (long)(layout.Extent.Height - Viewport.Height);
                var row = layout.FindRow(y);
                Require(row >= 0 && layout.GetRowTop(row) <= y && layout.GetRowTop(row) + layout.GetRowHeight(row) > y,
                    "FindRow did not contain the content coordinate.");
                Require(layout.HitTest(new Point(400, 200), new Vector(index % 2000, y), Viewport, out _, out _),
                    "A scrolling geometry hit was lost.");
            }
        }, _ => { });
        Add("wide-tree-create-100001-collapsed", () => new Fixture(), fixture =>
            fixture.Source = new HierarchicalTreeDataGridSource<ProfileItem>([wideRoot], item => item.Children),
            fixture => Require(fixture.Source!.Rows.Count == 1, "Wide tree did not start collapsed."));
        Add("wide-tree-expand-100000-children", () => new Fixture
            { Source = new HierarchicalTreeDataGridSource<ProfileItem>([wideRoot], item => item.Children) }, fixture =>
            Require(fixture.Source!.SetExpanded(fixture.Source!.Rows[0].Id, true), "Wide tree expansion failed."),
            fixture => Require(fixture.Source!.Rows.Count == RowCount + 1, "Wide expansion lost nodes."));
        Add("deep-tree-create-100000-depth-100", () => new Fixture(), fixture =>
            fixture.Source = new HierarchicalTreeDataGridSource<ProfileItem>(deepRoots, item => item.Children),
            fixture => Require(fixture.Source!.Rows.Count == 1000
                && fixture.Source!.TryGetNodeId(new IndexPath(Enumerable.Repeat(0, 100).ToArray()), out _), "Deep source path is missing."));
        Add("deep-tree-expand-all-100000-depth-100", () => new Fixture
            { Source = new HierarchicalTreeDataGridSource<ProfileItem>(deepRoots, item => item.Children) }, fixture => fixture.Source!.ExpandAll(),
            fixture => Require(fixture.Source!.Rows.Count == RowCount, "Deep expansion lost nodes."));
        Add("observable-add-200-base-10000", MutationFixture, fixture =>
        {
            foreach (var item in fixture.Additions!) fixture.Items!.Add(item);
        }, fixture => ValidateMutation(fixture, MutationCount));
        Add("defer-refresh-add-200-base-10000", MutationFixture, fixture =>
        {
            using (fixture.Source!.DeferRefresh())
                foreach (var item in fixture.Additions!) fixture.Items!.Add(item);
        }, fixture => ValidateMutation(fixture, 1));
        Add("row-move-collection-add-200-base-10000", RowMoveMutationFixture, fixture =>
        {
            foreach (var item in fixture.Additions!) fixture.MoveItems!.Add(item);
        }, fixture => ValidateMutation(fixture, MutationCount));
        Add("row-move-collection-addrange-200-base-10000", RowMoveMutationFixture, fixture =>
            fixture.MoveItems!.AddRange(fixture.Additions!), fixture => ValidateMutation(fixture, 1));

        using var process = Process.GetCurrentProcess();
        process.Refresh();
        var report = new
        {
            SchemaVersion = 1,
            Profile = "TreeDataGrid P6 data and geometry baseline",
            CreatedUtc = DateTimeOffset.UtcNow,
            Passed = scenarios.All(scenario => scenario.Samples.All(sample => sample.Passed)),
            SampleCount = sampleCount,
            Environment = new
            {
                MachineName = Environment.MachineName,
                Runtime = RuntimeInformation.FrameworkDescription,
                OperatingSystem = RuntimeInformation.OSDescription,
                Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                LogicalProcessors = Environment.ProcessorCount,
                Processor = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER"),
                Configuration = Configuration(),
                Seed,
                Viewport = new { Width = Viewport.Width, Height = Viewport.Height },
                Font = "Not used: data source and pixel geometry only; UI font belongs to the separate headless probe.",
                RenderBackend = "None: no UI renderer or native input in this executable.",
            },
            Interpretation = new
            {
                Timing = "Prepare, full GC before a sample, validation and cleanup are excluded. No elapsed-time pass budget is asserted.",
                Allocations = "GC.GetTotalAllocatedBytes deltas are cumulative allocations, including temporary objects; they are not retained or peak memory.",
                Memory = "PeakWorkingSetBytes is the cumulative process high-water mark, including setup and previous scenarios; it is not a scenario-specific peak.",
                Distribution = "Five or more repetitions; nearest-rank P95 equals max when only five samples are present.",
                Mutation = "Each mutation scenario starts with 10000 rows and appends 200 pre-created models. DeferRefresh merges publication and still performs structural rebuilds.",
                RangePair = "The two row-move-collection scenarios use the same collection type, source and data; only 200 Add calls versus one AddRange call differ. Their pair evaluates opt-in structural batch import, not a change to ObservableCollection or DeferRefresh.",
                Limitations = "Geometry scrolling is FindRow and HitTest only. This does not measure frame rate, drawing, actual input latency, IME or native DPI.",
            },
            PeakWorkingSetBytes = process.PeakWorkingSet64,
            Scenarios = scenarios,
        };
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"P6 profile: {outputPath}; passed={report.Passed}; samples={sampleCount}; scenarios={scenarios.Count}");
        return report.Passed ? 0 : 1;

        Fixture FlatFixture()
        {
            var source = new FlatTreeDataGridSource<ProfileItem>(data);
            return new Fixture { Source = source, FirstId = source.Rows[0].Id };
        }

        Fixture MutationFixture()
        {
            var items = new ObservableCollection<ProfileItem>(data.Take(MutationBaseRows));
            var source = new FlatTreeDataGridSource<ProfileItem>(items);
            var fixture = new Fixture { Source = source, Items = items, FirstId = source.Rows[0].Id,
                Additions = Enumerable.Range(MutationBaseRows, MutationCount).Select(index => new ProfileItem(index, index)).ToArray() };
            source.ProjectionChanged += (_, _) => fixture.Notifications++;
            return fixture;
        }

        Fixture RowMoveMutationFixture()
        {
            var items = new TreeDataGridRowMoveCollection<ProfileItem>(data.Take(MutationBaseRows));
            var source = new FlatTreeDataGridSource<ProfileItem>(items);
            var fixture = new Fixture { Source = source, MoveItems = items, FirstId = source.Rows[0].Id,
                Additions = Enumerable.Range(MutationBaseRows, MutationCount).Select(index => new ProfileItem(index, index)).ToArray() };
            source.ProjectionChanged += (_, _) => fixture.Notifications++;
            return fixture;
        }

        void Add(string scenario, Func<Fixture> prepare, Action<Fixture> action, Action<Fixture> validate)
        {
            var samples = new List<Sample>();
            for (var repetition = 1; repetition <= sampleCount; ++repetition)
            {
                using var fixture = prepare();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                var collections = new[] { GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2) };
                var allocated = GC.GetTotalAllocatedBytes(true);
                var started = Stopwatch.GetTimestamp();
                string? error = null;
                try { action(fixture); }
                catch (Exception failure) { error = failure.ToString(); }
                var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                var allocatedBytes = GC.GetTotalAllocatedBytes(true) - allocated;
                var gcCounts = Enumerable.Range(0, 3).Select(generation => GC.CollectionCount(generation) - collections[generation]).ToArray();
                if (error is null)
                    try { validate(fixture); }
                    catch (Exception failure) { error = failure.ToString(); }
                using var sampleProcess = Process.GetCurrentProcess();
                sampleProcess.Refresh();
                samples.Add(new Sample(repetition, elapsed, allocatedBytes, gcCounts, GC.GetTotalMemory(false),
                    sampleProcess.WorkingSet64, sampleProcess.PeakWorkingSet64, fixture.Notifications, error is null, error));
                Console.WriteLine($"{scenario} {repetition}/{sampleCount}: {elapsed:F2} ms; allocated={allocatedBytes}; passed={error is null}");
            }
            scenarios.Add(new ScenarioResult(scenario, Summarize(samples.Select(sample => sample.ElapsedMilliseconds)),
                Summarize(samples.Select(sample => (double)sample.AllocatedBytes)), samples));
        }
    }

    private static void ValidateMutation(Fixture fixture, int expectedNotifications)
    {
        Require(fixture.Source!.Rows.Count == MutationBaseRows + MutationCount, "Mutation count is inconsistent.");
        Require(fixture.Source!.Rows[0].Id == fixture.FirstId, "Mutation changed the retained first NodeId.");
        Require(fixture.Notifications == expectedNotifications, "Projection publication count is inconsistent.");
    }

    private static void Warmup(ProfileItem[] data, TreeDataGridColumn[] columns)
    {
        using var source = new FlatTreeDataGridSource<ProfileItem>(data.Take(128));
        source.Refresh();
        var layout = new TreeDataGridLayout(source.Rows, columns, Viewport.Width, 28, null, 2);
        for (var index = 0; index < 100; ++index) layout.FindRow(index * 20);
        using var tree = new HierarchicalTreeDataGridSource<ProfileItem>([new ProfileItem(-1, 0) { Children = data.Take(16).ToArray() }], item => item.Children);
        tree.ExpandAll();
        var items = new ObservableCollection<ProfileItem>(data.Take(16));
        using var mutating = new FlatTreeDataGridSource<ProfileItem>(items);
        items.Add(data[16]);
        using (mutating.DeferRefresh()) items.Add(data[17]);
        var rangeItems = new TreeDataGridRowMoveCollection<ProfileItem>(data.Take(16));
        using var rangeSource = new FlatTreeDataGridSource<ProfileItem>(rangeItems);
        rangeItems.AddRange(data.Skip(16).Take(2));
    }

    private static ProfileItem[] CreateDeepRoots(int chains, int depth)
    {
        var roots = new ProfileItem[chains];
        for (var chain = 0; chain < chains; ++chain)
        {
            var current = new ProfileItem(chain * depth, 0);
            roots[chain] = current;
            for (var level = 1; level < depth; ++level)
            {
                var next = new ProfileItem(chain * depth + level, 0);
                current.Children = [next];
                current = next;
            }
        }
        return roots;
    }

    private static Distribution Summarize(IEnumerable<double> values)
    {
        var ordered = values.Order().ToArray();
        var median = ordered.Length % 2 == 0 ? (ordered[ordered.Length / 2 - 1] + ordered[ordered.Length / 2]) / 2 : ordered[ordered.Length / 2];
        return new(ordered[0], median, ordered[(int)Math.Ceiling(ordered.Length * 0.95) - 1], ordered[^1]);
    }

    private static string? Option(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static string Configuration()
    {
#if DEBUG
        return "Debug";
#else
        return "Release";
#endif
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class ProfileItem(int number, int value)
    {
        public int Number { get; } = number;
        public int Value { get; } = value;
        public IReadOnlyList<ProfileItem> Children { get; set; } = [];
    }

    private sealed class Fixture : IDisposable
    {
        public TreeDataGridSource? Source { get; set; }
        public TreeDataGridLayout? Layout { get; set; }
        public ObservableCollection<ProfileItem>? Items { get; set; }
        public TreeDataGridRowMoveCollection<ProfileItem>? MoveItems { get; set; }
        public ProfileItem[]? Additions { get; set; }
        public NodeId FirstId { get; set; }
        public int Notifications { get; set; }
        public void Dispose() => Source?.Dispose();
    }

    private sealed record Distribution(double Min, double Median, double P95, double Max);
    private sealed record Sample(int Repetition, double ElapsedMilliseconds, long AllocatedBytes, int[] CollectionsByGeneration,
        long ManagedBytesAfter, long WorkingSetBytesAfter, long CumulativeProcessPeakWorkingSetBytes, int ProjectionNotifications, bool Passed, string? Error);
    private sealed record ScenarioResult(string Scenario, Distribution ElapsedMilliseconds, Distribution AllocatedBytes, IReadOnlyList<Sample> Samples);
}
