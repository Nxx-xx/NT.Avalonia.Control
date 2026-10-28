using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Headless.XUnit;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class IncrementalDataBufferTests
{
    [AvaloniaFact]
    public void Mutations_match_an_independent_list_and_report_capacity_evictions_separately()
    {
        var source = new ChartDataBuffer<int>(7);
        var expected = new List<int>();
        var events = 0;
        source.CollectionChanged += (_, args) => { Assert.Equal(NotifyCollectionChangedAction.Reset, args.Action); events++; };
        void Check() => Assert.Equal(expected, source.ToArray());
        void Trim() { if (expected.Count > 7) expected.RemoveRange(0, expected.Count - 7); }
        source.AppendRange([0, 1, 2, 3]); expected.AddRange([0, 1, 2, 3]); Check();
        source.InsertRange(2, [20, 21]); expected.InsertRange(2, [20, 21]); Check();
        source.ReplaceRange(1, 3, [30, 31, 32, 33, 34]); expected.RemoveRange(1, 3); expected.InsertRange(1, [30, 31, 32, 33, 34]); Trim(); Check();
        Assert.Equal(1, source.LastMutationEvictedCount); Assert.Equal(3, source.LastMutationRemovedCount);
        source.RemoveRange(1, 2); expected.RemoveRange(1, 2); Check();
        source.AppendRange(Enumerable.Range(50, 20)); expected.AddRange(Enumerable.Range(50, 20)); Trim(); Check();
        Assert.Equal(18, source.LastMutationEvictedCount); Assert.Equal(19, source.EvictedCount);
        Assert.Equal(0, source.LastMutationRemovedCount); Assert.Equal(5, source.RemovedCount);
        source.Clear(); expected.Clear(); Check();
        Assert.Equal(12, source.RemovedCount); Assert.Equal(19, source.EvictedCount);
        Assert.Equal(6, source.Version); Assert.Equal(6, events);
        source.Clear(); source.AppendRange([]); source.RemoveRange(0, 0);
        Assert.Equal(6, events); Assert.Equal(6, source.Version);
    }

    [AvaloniaFact]
    public void Invalid_ranges_failed_iterators_and_reentrant_writes_leave_a_complete_version()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChartDataBuffer<int>(0));
        var source = new ChartDataBuffer<int>(); source.AppendRange([1, 2, 3]);
        Assert.Throws<ArgumentOutOfRangeException>(() => source.InsertRange(4, [1]));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.RemoveRange(2, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.ReplaceRange(-1, 0, [1]));
        Assert.Throws<InvalidOperationException>(() => source.AppendRange(Broken()));
        Assert.Equal([1, 2, 3], source.ToArray()); Assert.Equal(1, source.Version);
        source.CollectionChanged += (_, _) => Assert.Throws<InvalidOperationException>(() => source.AppendRange([9]));
        source.AppendRange([4]); Assert.Equal([1, 2, 3, 4], source.ToArray()); Assert.Equal(2, source.Version);
        static IEnumerable<int> Broken() { yield return 7; throw new InvalidOperationException("fixture iterator"); }
    }

    [AvaloniaFact]
    public async Task Mutating_from_a_worker_is_rejected()
    {
        var source = new ChartDataBuffer<int>();
        var exception = await Task.Run(() => Record.Exception(() => source.AppendRange([1])));
        Assert.IsType<InvalidOperationException>(exception); Assert.Empty(source); Assert.Equal(0, source.Version);
    }

    [AvaloniaFact]
    public void Point_and_scalar_snapshots_share_retained_identity_without_mutating_old_coordinates()
    {
        var source = new ChartDataBuffer<ChartPoint>(4); source.AppendRange([new(0, 10), new(1, 11), new(2, 12)]);
        var series = new LineSeries { ItemsSource = source }; using var owner = new Owner(series);
        var initial = series.CreateSnapshot(); var retained = initial.Points[2];
        var copied = ChartDataBufferMetrics.CopiedItemCount; var mapped = ChartSeries.BufferedMappedRowCount;
        source.AppendRange([new(3, 13), new(4, 14)]); var next = series.CreateSnapshot();
        Assert.Equal(2, ChartDataBufferMetrics.CopiedItemCount - copied); Assert.Equal(0, ChartSeries.BufferedMappedRowCount - mapped);
        Assert.Equal(4, next.Count); Assert.Same(retained.StableId, next.Points[1].StableId);
        Assert.Equal(1, next.Points[1].SourceIndex); Assert.Equal(retained.Item, next.Points[1].Item);
        Assert.Equal(new ChartPoint(0, 10), initial.Points[0].Point); Assert.Equal(3, initial.Count);
        source.ReplaceRange(1, 1, [new(2, 12)]); var replaced = series.CreateSnapshot();
        Assert.NotSame(retained.StableId, replaced.Points[1].StableId);
        Assert.True(next.TryFindIdentity(retained.StableId, out var found)); Assert.Equal(1, found.SourceIndex);
        Assert.False(replaced.TryFindIdentity(retained.StableId, out _));

        var scalars = new ChartDataBuffer<int>(3); scalars.AppendRange([10, 20, 30]);
        series.ItemsSource = scalars; var scalarOld = series.CreateSnapshot(); var scalarId = scalarOld.Points[2].StableId;
        scalars.AppendRange([40, 50]); var scalarNew = series.CreateSnapshot();
        Assert.Equal(new ChartPoint(0, 30), scalarNew.Points[0].Point); Assert.Equal(0, scalarNew.Points[0].Category);
        Assert.Same(scalarId, scalarNew.Points[0].StableId); Assert.IsType<int>(scalarNew.Points[0].Item);
        Assert.Equal(new ChartPoint(2, 30), scalarOld.Points[2].Point);
    }

    [AvaloniaFact]
    public void Compact_numeric_traversal_does_not_box_or_expand_metadata()
    {
        var source = new ChartDataBuffer<int?>(); source.AppendRange(Enumerable.Range(0, 10_000).Select(index => (int?)index));
        var series = new LineSeries { ItemsSource = source }; using var owner = new Owner(series);
        var data = series.CreateSnapshot();
        for (var i = 0; i < 32; i++) _ = data.GetValue(i);
        var before = GC.GetAllocatedBytesForCurrentThread(); var total = 0d;
        for (var i = 0; i < data.Count; i++) total += data.GetValue(i).Point.Y;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(49_995_000, total);
        Assert.True(allocated < 1_024, $"Compact numeric traversal allocated {allocated} bytes; expected fewer than 1,024 bytes.");
        var points = new ChartDataBuffer<Point>(); points.AppendRange([new(1, 2), new(3, 4)]);
        series.ItemsSource = points; var point = series.CreateSnapshot().Points[1];
        Assert.IsType<Point>(point.Item); Assert.Equal(new ChartPoint(3, 4), point.Point);
    }

    [AvaloniaFact]
    public void Typed_families_and_non_numeric_items_match_the_existing_mapping_contract()
    {
        Check<double>([2, double.NaN]); Check<float>([2, float.NaN]); Check<decimal>([2, 3]);
        Check<int>([2, 3]); Check<long>([2, 3]); Check<short>([2, 3]); Check<byte>([2, 3]);
        Check<sbyte>([2, 3]); Check<uint>([2, 3]); Check<ulong>([2, 3]); Check<ushort>([2, 3]);
        Check<double?>([2, null]); Check<float?>([2, null]); Check<decimal?>([2, null]);
        Check<int?>([2, null]); Check<long?>([2, null]); Check<short?>([2, null]); Check<byte?>([2, null]);
        Check<sbyte?>([2, null]); Check<uint?>([2, null]); Check<ulong?>([2, null]); Check<ushort?>([2, null]);
        Check<bool>([true, false]); Check<string>(["2", "missing"]); Check<char>(['2', '3']);
        static void Check<T>(T[] values)
        {
            var buffer = new ChartDataBuffer<T>(); buffer.AppendRange(values);
            var actual = new LineSeries { ItemsSource = buffer }.CreateSnapshot();
            var expected = new LineSeries { ItemsSource = values }.CreateSnapshot();
            Assert.Equal(expected.Points.Select(point => (point.Point, point.IsEmpty, point.Category, point.Item, point.Value)),
                actual.Points.Select(point => (point.Point, point.IsEmpty, point.Category, point.Item, point.Value)));
        }
    }

    [AvaloniaFact]
    public void Immutable_objects_only_map_new_rows_and_keep_ordinal_category_and_ids_after_sliding()
    {
        var source = new ChartDataBuffer<Row>(10_000);
        source.AppendRange(Enumerable.Range(0, 10_000).Select(index => new Row(index, index + 7)));
        var series = new LineSeries { ItemsSource = source, ValuePath = nameof(Row.Y), IdPath = nameof(Row.Id) };
        using var owner = new Owner(series); var old = series.CreateSnapshot(); var survivor = old.Points[100];
        var mapped = ChartSeries.BufferedMappedRowCount; var copied = ChartDataBufferMetrics.CopiedItemCount;
        source.AppendRange(Enumerable.Range(10_000, 100).Select(index => new Row(index, index + 7)));
        var next = series.CreateSnapshot();
        Assert.Equal(100, ChartSeries.BufferedMappedRowCount - mapped); Assert.Equal(100, ChartDataBufferMetrics.CopiedItemCount - copied);
        Assert.Equal(new ChartPoint(0, 107), next.Points[0].Point); Assert.Equal(0, next.Points[0].Category);
        Assert.Equal(survivor.StableId, next.Points[0].StableId); Assert.Same(survivor.Item, next.Points[0].Item);
        Assert.Equal(new ChartPoint(100, 107), old.Points[100].Point);
        var before = ChartSeries.BufferedMappedRowCount; Assert.Same(next, series.CreateSnapshot());
        Assert.Equal(before, ChartSeries.BufferedMappedRowCount);
        source.RemoveRange(2, 5); var removed = series.CreateSnapshot();
        Assert.Equal(before, ChartSeries.BufferedMappedRowCount); Assert.Equal(7 + 107, removed.Points[2].Point.Y);
        Assert.Equal(2, removed.Points[2].SourceIndex);
    }

    [AvaloniaFact]
    public void Duplicate_and_null_id_validation_recovers_after_removal_without_remapping_retained_rows()
    {
        var source = new ChartDataBuffer<Row>(3); source.AppendRange([new(1, 2), new(2, 3)]);
        var series = new LineSeries { ItemsSource = source, CategoryPath = nameof(Row.Id), ValuePath = nameof(Row.Y), IdPath = nameof(Row.Id) };
        using var owner = new Owner(series); _ = series.CreateSnapshot();
        source.AppendRange([new(2, 9)]); Assert.Throws<InvalidOperationException>(() => series.CreateSnapshot());
        source.RemoveRange(1, 1); Assert.Equal(2, series.CreateSnapshot().Count);
        source.AppendRange([new(5, 3), new(6, 4)]); Assert.Equal(3, series.CreateSnapshot().Count);
        Assert.Equal([2, 5, 6], series.CreateSnapshot().Points.Select(point => (int)point.StableId!).ToArray());
        series.IdPath = "Missing"; Assert.Throws<InvalidOperationException>(() => series.CreateSnapshot());
        series.IdPath = nameof(Row.Id); Assert.Equal(3, series.CreateSnapshot().Count);
    }

    [AvaloniaFact]
    public void Insert_replace_and_remove_preserve_all_unchanged_object_values_and_old_snapshots()
    {
        var source = new ChartDataBuffer<Row>(); source.AppendRange(Enumerable.Range(0, 600).Select(index => new Row(index, index * 2)));
        var series = new LineSeries { ItemsSource = source, CategoryPath = nameof(Row.Id), ValuePath = nameof(Row.Y) };
        using var owner = new Owner(series); var old = series.CreateSnapshot(); var retained = old.Points[300];
        var before = ChartSeries.BufferedMappedRowCount;
        source.InsertRange(255, [new(-1, -2), new(-2, -4)]); source.ReplaceRange(500, 3, [new(-3, -6)]); source.RemoveRange(20, 5);
        var next = series.CreateSnapshot(); Assert.Equal(3, ChartSeries.BufferedMappedRowCount - before);
        Assert.Same(retained.Item, next.Points[297].Item); Assert.Same(retained.StableId, next.Points[297].StableId);
        Assert.Equal(new ChartPoint(300, 600), next.Points[297].Point); Assert.Equal(300, old.Points[300].SourceIndex);
        Assert.Equal(600, old.Count); Assert.Equal(595, next.Count);
    }

    [AvaloniaFact]
    public void Property_observers_follow_evicted_removed_replaced_and_unowned_rows()
    {
        var a = new MutableRow(1, 10); var b = new MutableRow(2, 20); var c = new MutableRow(3, 30);
        var source = new ChartDataBuffer<MutableRow>(2); source.AppendRange([a, b]);
        var series = new LineSeries { ItemsSource = source, CategoryPath = nameof(MutableRow.X), ValuePath = nameof(MutableRow.Y) };
        using (var owner = new Owner(series))
        {
            var old = series.CreateSnapshot(); Assert.Equal(1, a.Subscribers); Assert.Equal(1, b.Subscribers);
            a.Y = 11; Assert.Equal(11, series.CreateSnapshot().Points[0].Point.Y); Assert.Equal(10, old.Points[0].Point.Y);
            source.AppendRange([c]); Assert.Equal(0, a.Subscribers); Assert.Equal(1, c.Subscribers);
            source.RemoveRange(0, 1); Assert.Equal(0, b.Subscribers);
            source.ReplaceRange(0, 1, [a]); Assert.Equal(0, c.Subscribers); Assert.Equal(1, a.Subscribers);
        }
        Assert.Equal(0, a.Subscribers); a.Y = 40; Assert.Equal(40, series.CreateSnapshot().Points[0].Point.Y);
        a.Y = 50; Assert.Equal(50, series.CreateSnapshot().Points[0].Point.Y); Assert.Equal(0, a.Subscribers);
    }

    [AvaloniaFact]
    public void Nested_and_duplicate_object_observers_have_balanced_subscriptions()
    {
        var nested = new MutableRow(1, 2); var item = new Nested(nested);
        var source = new ChartDataBuffer<Nested>(); source.AppendRange([item, item]);
        var series = new LineSeries { ItemsSource = source, CategoryPath = "Value.X", ValuePath = "Value.Y" };
        using (var owner = new Owner(series))
        {
            _ = series.CreateSnapshot(); Assert.Equal(1, nested.Subscribers);
            source.RemoveRange(0, 1); Assert.Equal(1, nested.Subscribers);
            nested.Y = 7; Assert.Equal(7, series.CreateSnapshot().Points[0].Point.Y);
            source.Clear(); Assert.Equal(0, nested.Subscribers);
        }
    }

    [AvaloniaFact]
    public void Background_snapshot_publication_validates_buffer_item_and_stable_identity()
    {
        var source = new ChartDataBuffer<ChartPoint>(3); source.AppendRange([new(0, 2), new(1, 3)]);
        var series = new LineSeries { ItemsSource = source }; using var owner = new Owner(series);
        var snapshot = series.CreateSnapshot(); var points = snapshot.Points.Select(point => point with { Point = point.Point with { Y = point.Point.Y + 10 } }).ToArray();
        Assert.True(series.TryPublishSnapshot(new(series.SourceVersion, points), series.SourceVersion));
        Assert.Equal(12, series.CreateSnapshot().Points[0].Point.Y);
        points[0] = points[0] with { Item = new ChartPoint(90, 90) };
        Assert.False(series.TryPublishSnapshot(new(series.SourceVersion, points), series.SourceVersion));
        source.AppendRange([new(2, 4)]); Assert.False(series.TryPublishSnapshot(snapshot, snapshot.Version));
    }

    [AvaloniaFact]
    public void Empty_policies_and_range_zero_repair_match_existing_sources_across_page_boundaries()
    {
        var points = Enumerable.Range(0, 520).Select(index => new ChartPoint(index, index is >= 254 and <= 258 ? double.NaN : index)).ToArray();
        var buffer = new ChartDataBuffer<ChartPoint>(); buffer.AppendRange(points);
        foreach (var mode in Enum.GetValues<EmptyPointMode>())
        {
            var expected = new LineSeries { ItemsSource = points, EmptyPointMode = mode }.CreateSnapshot();
            var actual = new LineSeries { ItemsSource = buffer, EmptyPointMode = mode }.CreateSnapshot();
            Assert.Equal(expected.Points.Select(point => (point.Point, point.IsEmpty, point.WasEmpty)), actual.Points.Select(point => (point.Point, point.IsEmpty, point.WasEmpty)));
        }
        var rows = new[] { new Band(0, 2, 1), new Band(1, null, 3), new Band(2, 6, 4) };
        var rangeBuffer = new ChartDataBuffer<Band>(); rangeBuffer.AppendRange(rows);
        var range = new RangeAreaSeries { ItemsSource = rangeBuffer, CategoryPath = nameof(Band.X), HighPath = nameof(Band.High), LowPath = nameof(Band.Low), EmptyPointMode = EmptyPointMode.Zero };
        var rangeExpected = new RangeAreaSeries { ItemsSource = rows, CategoryPath = nameof(Band.X), HighPath = nameof(Band.High), LowPath = nameof(Band.Low), EmptyPointMode = EmptyPointMode.Zero };
        Assert.Equal(rangeExpected.CreateSnapshot().Points.Select(point => (point.Point, point.High, point.Low, point.IsEmpty)),
            range.CreateSnapshot().Points.Select(point => (point.Point, point.High, point.Low, point.IsEmpty)));
    }

    [AvaloniaFact]
    public void Mapping_callbacks_cannot_publish_old_rows_or_pollute_the_replacement_identity_registry()
    {
        var oldSource = new ChartDataBuffer<Row>(); oldSource.AppendRange([new(1, 2)]);
        var replacement = new ChartDataBuffer<Row>(); replacement.AppendRange([new(1, 9)]);
        var series = new LineSeries { ItemsSource = oldSource, CategoryPath = nameof(Row.Id), ValuePath = "Missing", IdPath = nameof(Row.Id) };
        using var owner = new Owner(series);
        EventHandler<ChartDataErrorEventArgs> replace = (_, _) => { series.ItemsSource = replacement; series.ValuePath = nameof(Row.Y); };
        series.DataError += replace;
        Assert.Throws<ChartDataMappingException>(() => series.CreateSnapshot());
        series.DataError -= replace;
        var current = series.CreateSnapshot(); Assert.Single(current.Points); Assert.Equal(new ChartPoint(1, 9), current.Points[0].Point);
        replacement.AppendRange([new(2, 7)]); Assert.Equal(2, series.CreateSnapshot().Count);

        var mutating = new ChartDataBuffer<GetterRow>();
        var once = true; var armed = false;
        var row = new GetterRow(() => { if (armed && once) { once = false; series.ValuePath = nameof(GetterRow.Actual); } });
        mutating.AppendRange([row]);
        series.CategoryPath = nameof(GetterRow.Id); series.IdPath = nameof(GetterRow.Id); series.ValuePath = nameof(GetterRow.Value); series.ItemsSource = mutating;
        armed = true;
        Assert.Throws<ChartDataMappingException>(() => series.CreateSnapshot());
        current = series.CreateSnapshot(); Assert.Equal(new ChartPoint(1, 12), current.Points[0].Point);
    }

    private sealed record Row(int Id, double Y);
    private sealed record Nested(MutableRow Value);
    private sealed record Band(double X, double? High, double? Low);
    private sealed class GetterRow(Action changed)
    {
        public int Id => 1;
        public double Value { get { changed(); return 3; } }
        public double Actual => 12;
    }
    private sealed class MutableRow(double x, double y) : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _changed;
        public int Subscribers { get; private set; }
        public double X { get; } = x;
        public double Y { get => y; set { y = value; _changed?.Invoke(this, new(nameof(Y))); } }
        public event PropertyChangedEventHandler? PropertyChanged { add { _changed += value; Subscribers++; } remove { _changed -= value; Subscribers--; } }
    }
    private sealed class Owner : IDisposable
    {
        private readonly ChartSeries _series;
        public Owner(ChartSeries series) { _series = series; _series.Changed += OnChanged; }
        private void OnChanged(object? sender, EventArgs args) { }
        public void Dispose() => _series.Changed -= OnChanged;
    }
}
