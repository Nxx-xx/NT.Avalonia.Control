using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class BufferResourceBudgetTests
{
    [AvaloniaFact]
    public void Explicit_packing_reduces_backing_pages_and_preserves_each_existing_point_identity_and_snapshot()
    {
        var buffer = new ChartDataBuffer<ChartPoint>(600);
        for (var index = 0; index < 600; index++) buffer.AppendRange([new(index, index + 2)]);
        Assert.Equal(600, buffer.StoragePageCount);
        var series = new LineSeries { ItemsSource = buffer }; using var owner = new Owner(series);
        var previous = series.CreateSnapshot(); var original = previous.Points.ToArray();
        var events = 0;
        buffer.CollectionChanged += (_, args) => { events++; Assert.Equal(NotifyCollectionChangedAction.Reset, args.Action); };
        var version = buffer.Version; var sourceVersion = series.SourceVersion;
        var copies = ChartDataBufferMetrics.CopiedItemCount; var mapped = ChartSeries.BufferedMappedRowCount;
        Assert.True(buffer.Compact()); Assert.Equal(3, buffer.StoragePageCount);
        Assert.Equal(version + 1, buffer.Version); Assert.Equal(sourceVersion + 1, series.SourceVersion); Assert.Equal(1, events);
        Assert.Equal(600, ChartDataBufferMetrics.CopiedItemCount - copies); Assert.Equal(mapped, ChartSeries.BufferedMappedRowCount);
        var packed = series.CreateSnapshot();
        for (var index = 0; index < original.Length; index++)
        {
            Assert.Equal(original[index].Point, packed.Points[index].Point); Assert.Equal(original[index].Item, packed.Points[index].Item);
            Assert.Same(original[index].StableId, packed.Points[index].StableId);
            Assert.Equal(original[index], previous.Points[index]);
        }
        Assert.False(buffer.Compact()); Assert.Equal(1, events);
        Assert.Equal(600, packed.Count); Assert.Equal(600, previous.Count);
    }

    [AvaloniaFact]
    public void Compaction_transfers_cached_object_mapping_without_reading_mutable_getters()
    {
        var buffer = new ChartDataBuffer<MutableRow>();
        var rows = Enumerable.Range(0, 600).Select(index => new MutableRow(index, index * 2)).ToArray();
        foreach (var row in rows) buffer.AppendRange([row]);
        var series = new LineSeries { ItemsSource = buffer, CategoryPath = nameof(MutableRow.X), ValuePath = nameof(MutableRow.Y), IdPath = nameof(MutableRow.Id) };
        using var owner = new Owner(series); var previous = series.CreateSnapshot(); var ids = previous.Points.Select(point => point.StableId).ToArray();
        foreach (var row in rows) { row.Value = -99; row.ReadCount = 0; }
        var mapped = ChartSeries.BufferedMappedRowCount; var transfers = ChartDataBufferMetrics.MappedCompactionCopyCount;
        Assert.True(buffer.Compact()); var packed = series.CreateSnapshot();
        Assert.Equal(0, rows.Sum(row => row.ReadCount)); Assert.Equal(mapped, ChartSeries.BufferedMappedRowCount);
        Assert.Equal(600, ChartDataBufferMetrics.MappedCompactionCopyCount - transfers);
        for (var index = 0; index < packed.Count; index++)
        {
            Assert.Equal(index * 2, packed.Points[index].Point.Y); Assert.Same(rows[index], packed.Points[index].Item);
            Assert.Same(ids[index], packed.Points[index].StableId); Assert.Equal(index * 2, previous.Points[index].Point.Y);
        }
    }

    [AvaloniaFact]
    public void Mapped_and_unmapped_rows_can_share_a_compacted_page_without_skipping_holes()
    {
        var buffer = new ChartDataBuffer<MutableRow>();
        var oldRows = Enumerable.Range(0, 512).Select(index => new MutableRow(index, index)).ToArray();
        buffer.AppendRange(oldRows);
        var series = new LineSeries { ItemsSource = buffer, CategoryPath = nameof(MutableRow.X), ValuePath = nameof(MutableRow.Y), IdPath = nameof(MutableRow.Id) };
        using var owner = new Owner(series); var previous = series.CreateSnapshot();
        var added = new[] { new MutableRow(-1, 91), new MutableRow(-2, 92) };
        buffer.InsertRange(100, added);
        foreach (var row in oldRows.Concat(added)) row.ReadCount = 0;
        var before = ChartSeries.BufferedMappedRowCount;
        Assert.True(buffer.Compact()); var next = series.CreateSnapshot();
        Assert.Equal(2, ChartSeries.BufferedMappedRowCount - before); Assert.Equal(0, oldRows.Sum(row => row.ReadCount));
        Assert.Equal(2, added.Sum(row => row.ReadCount));
        Assert.Equal(new ChartPoint(-1, 91), next.Points[100].Point); Assert.Equal(new ChartPoint(-2, 92), next.Points[101].Point);
        Assert.Equal(previous.Points[100].Point, next.Points[102].Point); Assert.Same(previous.Points[100].StableId, next.Points[102].StableId);
    }

    [AvaloniaFact]
    public void Compacted_non_monotonic_creation_ids_still_resolve_every_retained_original_point()
    {
        var buffer = new ChartDataBuffer<ChartPoint>(); buffer.AppendRange(Enumerable.Range(0, 100).Select(index => new ChartPoint(index, index)));
        var series = new LineSeries { ItemsSource = buffer }; using var owner = new Owner(series);
        var old = series.CreateSnapshot(); var original = old.Points.ToArray();
        buffer.InsertRange(5, [new(900, 901)]); buffer.InsertRange(3, [new(800, 801)]); buffer.RemoveRange(20, 3);
        var before = series.CreateSnapshot().Points.ToArray(); Assert.True(buffer.Compact()); var after = series.CreateSnapshot();
        for (var index = 0; index < before.Length; index++)
        {
            Assert.True(after.TryFindIdentity(before[index].StableId, out var found));
            Assert.Equal(index, found.SourceIndex); Assert.Equal(before[index].Point, found.Point); Assert.Same(before[index].StableId, found.StableId);
        }
        foreach (var removed in original.Where(point => point.Point.X is >= 18 and <= 20))
            Assert.False(after.TryFindIdentity(removed.StableId, out _));
    }

    [AvaloniaFact]
    public void Packing_multiple_slices_of_one_old_page_copies_current_order_once_and_clears_only_last_operation_counts()
    {
        var buffer = new ChartDataBuffer<int>(256); buffer.AppendRange(Enumerable.Range(0, 256));
        buffer.RemoveRange(20, 20); buffer.RemoveRange(100, 10);
        var expected = Enumerable.Range(0, 256).Where(value => value is not (>= 20 and < 40) && value is not (>= 120 and < 130)).ToArray();
        Assert.Equal(1, buffer.StoragePageCount);
        Assert.Equal(3, ((IChartDataBufferSource)buffer).Capture().Slices.Length);
        var removed = buffer.RemovedCount; var evicted = buffer.EvictedCount;
        Assert.True(buffer.Compact()); Assert.Equal(expected, buffer.ToArray());
        Assert.Single(((IChartDataBufferSource)buffer).Capture().Slices); Assert.Equal(1, buffer.StoragePageCount);
        Assert.Equal(removed, buffer.RemovedCount); Assert.Equal(evicted, buffer.EvictedCount);
        Assert.Equal(0, buffer.LastMutationRemovedCount); Assert.Equal(0, buffer.LastMutationEvictedCount);
    }

    [AvaloniaFact]
    public void Implicit_scalar_x_category_and_surviving_identity_remain_current_ordinals_after_packing()
    {
        var buffer = new ChartDataBuffer<int>(300);
        for (var index = 0; index < 400; index++) buffer.AppendRange([index]);
        var series = new LineSeries { ItemsSource = buffer }; using var owner = new Owner(series);
        var previous = series.CreateSnapshot(); var retained = previous.Points[100];
        buffer.RemoveRange(20, 7); buffer.InsertRange(5, [900, 901]); Assert.True(buffer.Compact());
        var next = series.CreateSnapshot(); Assert.Equal(295, next.Count);
        for (var index = 0; index < next.Count; index++)
        {
            Assert.Equal(index, next.Points[index].SourceIndex); Assert.Equal(index, next.Points[index].Category);
            Assert.Equal(new ChartPoint(index, buffer[index]), next.Points[index].Point);
        }
        Assert.True(next.TryFindIdentity(retained.StableId, out var moved)); Assert.Equal(95, moved.SourceIndex);
        Assert.Equal(new ChartPoint(100, 200), previous.Points[100].Point);
    }

    [AvaloniaFact]
    public void Existing_duplicate_id_failure_can_recover_after_compaction_and_removal()
    {
        var buffer = new ChartDataBuffer<MutableRow>(); buffer.AppendRange([new(1, 10)]); buffer.AppendRange([new(1, 20)]);
        var series = new LineSeries { ItemsSource = buffer, CategoryPath = nameof(MutableRow.X), ValuePath = nameof(MutableRow.Y), IdPath = nameof(MutableRow.Id) };
        using var owner = new Owner(series); Assert.Throws<InvalidOperationException>(() => series.CreateSnapshot());
        Assert.True(buffer.Compact()); Assert.Throws<InvalidOperationException>(() => series.CreateSnapshot());
        buffer.RemoveRange(0, 1); var next = series.CreateSnapshot();
        Assert.Single(next.Points); Assert.Equal(new ChartPoint(1, 20), next.Points[0].Point);
    }

    [AvaloniaFact]
    public void Live_chart_selection_and_original_hit_survive_packing_and_later_capacity_eviction()
    {
        var buffer = new ChartDataBuffer<MutableRow>(300);
        for (var index = 0; index < 300; index++) buffer.AppendRange([new(index, index % 13)]);
        var series = new LineSeries { ItemsSource = buffer, CategoryPath = nameof(MutableRow.X), ValuePath = nameof(MutableRow.Y), IdPath = nameof(MutableRow.Id), MarkerSize = 0 };
        var chart = new CartesianChart { Width = 720, Height = 360, ShowLegend = false,
            XAxis = new NumericAxis { Minimum = 0, Maximum = 400 }, YAxis = new NumericAxis { Minimum = -1, Maximum = 20 } };
        chart.Series.Add(series);
        var window = new Window { Width = 720, Height = 360, Content = chart };
        try
        {
            window.Show(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
            var original = series.CreateSnapshot().Points[180]; chart.SelectedPoint = ChartPointHit.FromData(series, original, default);
            Assert.True(buffer.Compact()); _ = chart.VisibleRange;
            Assert.NotNull(chart.SelectedPoint); Assert.Equal(180, chart.SelectedPoint.Index);
            Assert.Same(original.Item, chart.SelectedPoint.Item); Assert.Same(original.StableId, chart.SelectedPoint.StableId);
            buffer.AppendRange(Enumerable.Range(300, 20).Select(index => new MutableRow(index, index % 13)));
            _ = chart.VisibleRange; Assert.NotNull(chart.SelectedPoint); Assert.Equal(160, chart.SelectedPoint.Index);
            Assert.Same(original.StableId, chart.SelectedPoint.StableId); Assert.Same(original.Item, chart.SelectedPoint.Item);
            var selected = chart.SelectedPoint;
            var hit = chart.HitTestPoint(selected.Position, 0.01); Assert.NotNull(hit);
            Assert.Equal(selected.Index, hit.Index); Assert.Same(original.Item, hit.Item);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Warm_append_does_not_automatically_pack_or_copy_retained_rows_after_explicit_compaction()
    {
        var buffer = new ChartDataBuffer<MutableRow>(2_000);
        for (var index = 0; index < 2_000; index++) buffer.AppendRange([new(index, index)]);
        var series = new LineSeries { ItemsSource = buffer, CategoryPath = nameof(MutableRow.X), ValuePath = nameof(MutableRow.Y), IdPath = nameof(MutableRow.Id) };
        using var owner = new Owner(series); _ = series.CreateSnapshot(); Assert.True(buffer.Compact()); _ = series.CreateSnapshot();
        for (var revision = 0; revision < 6; revision++)
        {
            var copies = ChartDataBufferMetrics.CopiedItemCount; var mapped = ChartSeries.BufferedMappedRowCount; var compactions = ChartDataBufferMetrics.CompactionCount;
            buffer.AppendRange(Enumerable.Range(2_000 + revision * 125, 125).Select(index => new MutableRow(index, index)));
            _ = series.CreateSnapshot();
            Assert.Equal(125, ChartDataBufferMetrics.CopiedItemCount - copies); Assert.Equal(125, ChartSeries.BufferedMappedRowCount - mapped);
            Assert.Equal(compactions, ChartDataBufferMetrics.CompactionCount);
        }
    }

    [AvaloniaFact]
    public async Task Empty_and_packed_buffers_are_noops_but_worker_and_reentrant_compaction_are_rejected()
    {
        var buffer = new ChartDataBuffer<int>(); Assert.False(buffer.Compact());
        buffer.AppendRange(Enumerable.Range(0, 256)); var version = buffer.Version;
        Assert.False(buffer.Compact()); Assert.Equal(version, buffer.Version);
        var exception = await Task.Run(() => Record.Exception(() => buffer.Compact())); Assert.IsType<InvalidOperationException>(exception);
        buffer.CollectionChanged += (_, _) => Assert.Throws<InvalidOperationException>(() => buffer.Compact());
        buffer.InsertRange(5, [257]); Assert.True(buffer.Compact());
        Assert.False(buffer.Compact());
    }

    [AvaloniaFact]
    public void Old_snapshots_retain_removed_items_until_released_but_compacted_active_pages_do_not()
    {
        var fixture = MakeRetentionFixture();
        Collect(); Assert.True(IsAlive(fixture.Removed));
        fixture.Snapshot = null;
        Collect(); Assert.False(IsAlive(fixture.Removed));
        GC.KeepAlive(fixture.Buffer);
        static void Collect() { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool IsAlive(WeakReference<MutableRow> reference) => reference.TryGetTarget(out _);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static RetentionFixture MakeRetentionFixture()
    {
        var removed = new MutableRow(0, 10);
        var buffer = new ChartDataBuffer<MutableRow>(); buffer.AppendRange([removed, new(1, 11)]);
        var series = new LineSeries { ItemsSource = buffer, CategoryPath = nameof(MutableRow.X), ValuePath = nameof(MutableRow.Y) };
        using var owner = new Owner(series); var old = series.CreateSnapshot();
        buffer.RemoveRange(0, 1); Assert.True(buffer.Compact()); _ = series.CreateSnapshot();
        return new(buffer, old, new(removed));
    }

    private sealed class RetentionFixture(ChartDataBuffer<MutableRow> buffer, ChartDataSnapshot snapshot, WeakReference<MutableRow> removed)
    {
        public ChartDataBuffer<MutableRow> Buffer { get; } = buffer;
        public ChartDataSnapshot? Snapshot = snapshot;
        public WeakReference<MutableRow> Removed { get; } = removed;
    }
    private sealed class MutableRow(int x, double value)
    {
        public int Id => x;
        public int X => x;
        public double Value = value;
        public int ReadCount;
        public double Y { get { ReadCount++; return Value; } }
    }
    private sealed class Owner : IDisposable
    {
        private readonly ChartSeries _series;
        public Owner(ChartSeries series) { _series = series; series.Changed += Changed; }
        private void Changed(object? sender, EventArgs args) { }
        public void Dispose() => _series.Changed -= Changed;
    }
}
