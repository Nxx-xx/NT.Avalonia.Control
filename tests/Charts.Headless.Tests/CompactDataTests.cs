using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Headless.XUnit;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

/// <summary>Public data guarantees that must survive an internal compact representation.</summary>
public sealed class CompactDataTests
{
    [AvaloniaFact]
    public void Direct_coordinate_memory_is_immutable_and_only_exposed_for_unmodified_point_gaps()
    {
        var values = new ChartPoint[] { new(1, 2), new(3, 4) };
        var series = new LineSeries { ItemsSource = values };
        using var owner = new SeriesOwner(series);
        var snapshot = series.CreateSnapshot();
        Assert.Equal(2, snapshot.Coordinates.Length);
        values[0] = new ChartPoint(90, 99);
        Assert.Equal(new ChartPoint(1, 2), snapshot.Coordinates.Span[0]);
        Assert.True(snapshot.WithValues([snapshot.GetValue(0), snapshot.GetValue(1)]).Coordinates.IsEmpty);
        series.EmptyPointMode = EmptyPointMode.Zero;
        Assert.True(series.CreateSnapshot().Coordinates.IsEmpty);
        series.ItemsSource = new[] { 1d, 2d };
        Assert.True(series.CreateSnapshot().Coordinates.IsEmpty);
    }

    [AvaloniaFact]
    public void Lightweight_values_and_single_candidate_lookup_do_not_expand_the_whole_source()
    {
        var source = new GenericOnlyPointSource(Enumerable.Range(0, 10_000).Select(index => new ChartPoint(index, index + 2)).ToArray());
        var series = new LineSeries { ItemsSource = source };
        using var owner = new SeriesOwner(series);
        var snapshot = series.CreateSnapshot();
        Assert.Equal(10_000, snapshot.Count);
        for (var index = 0; index < 32; index++) _ = snapshot.GetValue(index);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var sum = 0d;
        for (var index = 0; index < snapshot.Count; index++) sum += snapshot.GetValue(index).Point.Y;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(50_015_000, sum);
        Assert.True(allocated < 1024, $"Coordinate traversal allocated {allocated} bytes.");

        before = GC.GetAllocatedBytesForCurrentThread();
        var found = snapshot.TryGetPoint(9_999, out var candidate);
        var missing = snapshot.TryGetPoint(10_000, out _);
        allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(found);
        Assert.False(missing);
        Assert.Equal(new ChartPoint(9_999, 10_001), candidate.Point);
        Assert.True(allocated < 16_384, $"One candidate lookup expanded {allocated} bytes of metadata.");
        Assert.True(snapshot.TryFindIdentity(candidate.StableId, out var byIdentity));
        Assert.Equal(candidate.Point, byIdentity.Point);
        Assert.False(snapshot.TryFindIdentity(new object(), out _));
    }

    [AvaloniaFact]
    public void Typed_point_and_numeric_families_retain_the_original_scalar_items()
    {
        Array[] sources = [new Point[] { new(0, 2), new(1, 3) }, new double[] { 2, 3 }, new float[] { 2, 3 },
            new decimal[] { 2, 3 }, new int[] { 2, 3 }, new long[] { 2, 3 }, new short[] { 2, 3 }, new byte[] { 2, 3 },
            new sbyte[] { 2, 3 }, new uint[] { 2, 3 }, new ulong[] { 2, 3 }, new ushort[] { 2, 3 },
            new double?[] { 2, null }, new float?[] { 2, null }, new decimal?[] { 2, null }, new int?[] { 2, null },
            new long?[] { 2, null }, new short?[] { 2, null }, new byte?[] { 2, null }, new sbyte?[] { 2, null },
            new uint?[] { 2, null }, new ulong?[] { 2, null }, new ushort?[] { 2, null }];
        foreach (var source in sources)
        {
            var series = new LineSeries { ItemsSource = source };
            using var owner = new SeriesOwner(series);
            var snapshot = series.CreateSnapshot();
            Assert.Equal(new ChartPoint(0, 2), snapshot.GetValue(0).Point);
            Assert.True(source.GetValue(0)?.GetType() == snapshot.Points[0].Item?.GetType(),
                $"Source {source.GetType()}: expected Item {source.GetValue(0)?.GetType()}, actual {snapshot.Points[0].Item?.GetType()}.");
            Assert.Equal(source.GetValue(0), snapshot.Points[0].Item);
            Assert.Null(snapshot.Points[0].Value);
            Assert.Equal(source.GetValue(1) is null, snapshot.GetValue(1).IsEmpty);
        }
    }

    [AvaloniaFact]
    public void Primitive_array_signedness_extremes_and_non_numeric_items_keep_their_exact_type()
    {
        (Array Source, double Expected)[] numeric = [(new sbyte[] { sbyte.MinValue }, sbyte.MinValue),
            (new short[] { short.MinValue }, short.MinValue), (new int[] { int.MinValue }, int.MinValue),
            (new long[] { long.MinValue }, long.MinValue), (new byte[] { byte.MaxValue }, byte.MaxValue),
            (new ushort[] { ushort.MaxValue }, ushort.MaxValue), (new uint[] { uint.MaxValue }, uint.MaxValue),
            (new ulong[] { ulong.MaxValue }, ulong.MaxValue)];
        foreach (var item in numeric)
        {
            var snapshot = new LineSeries { ItemsSource = item.Source }.CreateSnapshot();
            var point = Assert.Single(snapshot.Points);
            Assert.True(item.Source.GetValue(0)?.GetType() == point.Item?.GetType(),
                $"Source {item.Source.GetType()}: expected Item {item.Source.GetValue(0)?.GetType()}, actual {point.Item?.GetType()}.");
            Assert.Equal(item.Source.GetValue(0), point.Item);
            Assert.Equal(item.Expected, point.Point.Y);
        }
        Array[] nonNumeric = [new[] { true }, new[] { char.MaxValue }, new[] { SignedExample.Negative }, new[] { UnsignedExample.Maximum }];
        foreach (var source in nonNumeric)
        {
            Assert.Null(CompactSeriesData.TryCreate(source));
            var point = Assert.Single(new LineSeries { ItemsSource = source }.CreateSnapshot().Points);
            Assert.Equal(source.GetValue(0)?.GetType(), point.Item?.GetType());
            Assert.Equal(source.GetValue(0), point.Item);
            if (source.GetValue(0) is bool or char) Assert.True(point.IsEmpty);
            else Assert.Equal(Convert.ToDouble(source.GetValue(0)), point.Point.Y);
        }
    }

    [AvaloniaFact]
    public void Compact_empty_policies_match_the_public_projection_without_changing_raw_items()
    {
        foreach (var mode in Enum.GetValues<EmptyPointMode>())
        {
            var series = new LineSeries { ItemsSource = new ChartPoint[] { new(0, 0), new(2, double.NaN), new(10, 10) }, EmptyPointMode = mode };
            using var owner = new SeriesOwner(series);
            var snapshot = series.CreateSnapshot();
            var value = snapshot.GetValue(1);
            var expected = mode switch { EmptyPointMode.Zero => 0, EmptyPointMode.Average => 5, EmptyPointMode.Interpolate => 2, _ => double.NaN };
            Assert.Equal(expected, value.Point.Y);
            Assert.True(value.WasEmpty);
            Assert.Equal(mode == EmptyPointMode.Gap, value.IsEmpty);
            Assert.Equal(value.Point, snapshot.Points[1].Point);
            Assert.True(double.IsNaN(Assert.IsType<ChartPoint>(snapshot.Points[1].Item).Y));
        }
        var invalidNumber = new LineSeries { ItemsSource = new[] { double.PositiveInfinity } }.CreateSnapshot();
        Assert.True(double.IsNaN(invalidNumber.GetValue(0).Point.Y));
    }

    [AvaloniaFact]
    public void Coordinate_overlays_keep_identity_metadata_and_lookup_the_projected_values()
    {
        var series = new LineSeries { ItemsSource = new ChartPoint[] { new(1, 2), new(3, 4) } };
        using var owner = new SeriesOwner(series);
        var original = series.CreateSnapshot();
        var overlay = original.WithValues([new(0, new ChartPoint(10, 20), false, false), new(1, new ChartPoint(30, double.NaN), true, false)]);
        var identity = original.Points[1].StableId;
        Assert.True(overlay.TryGetPoint(1, out var projected));
        Assert.True(overlay.TryFindIdentity(identity, out var byIdentity));
        Assert.Same(identity, projected.StableId);
        Assert.Equal(new ChartPoint(30, double.NaN), projected.Point);
        Assert.Equal(projected.Point, byIdentity.Point);
        Assert.True(projected.IsEmpty);
        Assert.False(projected.WasEmpty);
        Assert.Equal(new ChartPoint(3, 4), Assert.IsType<ChartPoint>(projected.Item));
        Assert.Equal(new ChartPoint(3, 4), original.GetValue(1).Point);
        Assert.Throws<ArgumentException>(() => original.WithValues([new(1, new ChartPoint(1, 2), false, false), new(0, new ChartPoint(3, 4), false, false)]));
        Assert.Throws<ArgumentException>(() => original.WithValues([]));
    }

    [Fact]
    public void Sparse_reordered_snapshots_lookup_source_indices_and_identity_comparers_explicitly()
    {
        var firstId = new string(['i', 'd']);
        var equalId = new string(['i', 'd']);
        var first = new ChartDataPoint(9, firstId, "item-nine", "category", new ChartPoint(1, 2), false) { Value = "raw-value" };
        var second = new ChartDataPoint(4, new object(), null, null, new ChartPoint(3, 4), false);
        var snapshot = new ChartDataSnapshot(1, [first, second]);
        Assert.True(snapshot.TryGetPoint(4, out var sourceFour));
        Assert.Equal(second, sourceFour);
        Assert.False(snapshot.TryGetPoint(0, out _));
        Assert.False(snapshot.TryGetPoint(-1, out _));
        Assert.False(snapshot.TryFindIdentity(equalId, out _));
        Assert.True(snapshot.TryFindIdentity(equalId, out var byValue, valueEquality: true));
        Assert.Equal(first, byValue);
        Assert.True(snapshot.TryFindIdentity(firstId, out var byReference));
        Assert.Equal(first, byReference);
        var overlay = snapshot.WithValues([new(9, new ChartPoint(10, 20), false, true), new(4, new ChartPoint(30, 40), false, false)]);
        Assert.True(overlay.TryGetPoint(9, out var remapped));
        Assert.Equal("raw-value", remapped.Value);
        Assert.Equal(new ChartPoint(10, 20), remapped.Point);
    }

    [AvaloniaFact]
    public void Failed_or_reentrant_typed_enumeration_cannot_publish_a_half_built_store()
    {
        var series = new LineSeries { ItemsSource = new ChartPoint[] { new(1, 2) } };
        using var owner = new SeriesOwner(series);
        var first = series.CreateSnapshot();
        IEnumerable<ChartPoint> Fails() { yield return new ChartPoint(5, 6); throw new InvalidOperationException("enumerator failed"); }
        Assert.Throws<InvalidOperationException>(() => series.ItemsSource = Fails());
        Assert.Throws<InvalidOperationException>(() => series.CreateSnapshot());
        var observable = new ObservableCollection<ChartPoint> { new(9, 10) };
        IEnumerable<ChartPoint> Reenters() { series.ItemsSource = observable; yield return new ChartPoint(7, 8); }
        Assert.ThrowsAny<InvalidOperationException>(() => series.ItemsSource = Reenters());
        var recovered = series.CreateSnapshot();
        Assert.Equal(new ChartPoint(9, 10), Assert.Single(recovered.Points).Point);
        Assert.True(recovered.Version > first.Version);
        Assert.Equal(new ChartPoint(1, 2), first.GetValue(0).Point);
        observable.Add(new ChartPoint(11, 12));
        Assert.Equal(2, series.CreateSnapshot().Count);
    }

    [Fact]
    public void Dead_weak_identity_entries_are_swept_instead_of_accumulating_with_a_live_source_epoch()
    {
        var registry = new CompactIdentityRegistry(4096);
        CreateUnheldIdentities(registry);
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        registry.PruneAfterCollectionChange();
        Assert.InRange(registry.WeakEntryCount, 0, 1);
        var identity = registry.GetToken(42);
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        registry.PruneAfterCollectionChange();
        Assert.Same(identity, registry.GetToken(42));
    }

    [AvaloniaFact]
    public void Typed_iterator_snapshots_freeze_values_before_any_public_point_is_read()
    {
        var source = new MutablePointSource(new ChartPoint(2, 3), new ChartPoint(4, 5));
        var series = new LineSeries { ItemsSource = source };
        using var owner = new SeriesOwner(series);
        var first = series.CreateSnapshot();
        source.Values[0] = new ChartPoint(20, 30);
        series.Refresh();
        var second = series.CreateSnapshot();

        Assert.Equal(new ChartPoint(2, 3), first.Points[0].Point);
        Assert.Equal(new ChartPoint(2, 3), Assert.IsType<ChartPoint>(first.Points[0].Item));
        Assert.Equal(new ChartPoint(20, 30), second.Points[0].Point);
        Assert.Equal(new ChartPoint(4, 5), first.Points[1].Point);
        Assert.True(second.Version > first.Version);
        Assert.Same(second, series.CreateSnapshot());
    }

    [AvaloniaFact]
    public void Numeric_array_snapshots_keep_old_values_after_refresh_and_source_replacement()
    {
        var source = new[] { 2d, 4d };
        var series = new LineSeries { ItemsSource = source };
        using var owner = new SeriesOwner(series);
        var first = series.CreateSnapshot();
        source[0] = 99;
        series.Refresh();
        var refreshed = series.CreateSnapshot();
        series.ItemsSource = new[] { -10d };

        Assert.Equal(new ChartPoint(0, 2), first.Points[0].Point);
        Assert.Equal(2d, Assert.IsType<double>(first.Points[0].Item));
        Assert.Equal(new ChartPoint(0, 99), refreshed.Points[0].Point);
        Assert.Equal(new ChartPoint(0, -10), Assert.Single(series.CreateSnapshot().Points).Point);
        Assert.Equal(2, first.Points.Count);
        Assert.Equal(2, refreshed.Points.Count);
    }

    [AvaloniaFact]
    public void Moving_before_the_first_public_identity_read_keeps_the_same_occurrence()
    {
        var source = new ObservableCollection<double> { 10, 20, 30 };
        var series = new LineSeries { ItemsSource = source };
        using var owner = new SeriesOwner(series);
        var before = series.CreateSnapshot();
        source.Move(0, 2);
        var afterMove = series.CreateSnapshot();
        source.Insert(0, 99);
        var afterInsert = series.CreateSnapshot();

        // No Points indexer was read before both mutations. A lazy identity must use
        // the captured occurrence, rather than the live collection's current index.
        var identity = before.Points[0].StableId;
        Assert.NotNull(identity);
        Assert.Same(identity, afterMove.Points[2].StableId);
        Assert.Same(identity, afterInsert.Points[3].StableId);
        Assert.Equal(new ChartPoint(0, 10), before.Points[0].Point);
        Assert.Equal(new ChartPoint(2, 10), afterMove.Points[2].Point);
        Assert.Equal(3, afterInsert.Points[3].SourceIndex);
    }

    [AvaloniaFact]
    public void Duplicate_values_have_distinct_occurrences_and_replace_and_reset_create_new_ones()
    {
        var source = new ResettableCollection<double>([7d, 7d]);
        var series = new LineSeries { ItemsSource = source };
        using var owner = new SeriesOwner(series);
        var first = series.CreateSnapshot();
        var firstIdentity = first.Points[0].StableId;
        var secondIdentity = first.Points[1].StableId;
        Assert.NotSame(firstIdentity, secondIdentity);

        source.Move(0, 1);
        var moved = series.CreateSnapshot();
        Assert.Same(secondIdentity, moved.Points[0].StableId);
        Assert.Same(firstIdentity, moved.Points[1].StableId);
        source[1] = 7;
        var replaced = series.CreateSnapshot();
        Assert.NotSame(firstIdentity, replaced.Points[1].StableId);
        Assert.Same(secondIdentity, replaced.Points[0].StableId);

        source.ResetTo(7, 7);
        var reset = series.CreateSnapshot();
        Assert.NotSame(secondIdentity, reset.Points[0].StableId);
        Assert.NotSame(replaced.Points[1].StableId, reset.Points[1].StableId);
        Assert.Same(firstIdentity, first.Points[0].StableId);
        Assert.Equal(new ChartPoint(0, 7), first.Points[0].Point);
    }

    [AvaloniaFact]
    public void An_externally_held_identity_survives_gc_and_collection_movement()
    {
        var source = new ObservableCollection<ChartPoint> { new(1, 2), new(3, 4) };
        var series = new LineSeries { ItemsSource = source };
        using var owner = new SeriesOwner(series);
        var identity = ReadFirstIdentity(series);
        source.Move(0, 1);
        // Avalonia finalizers can need the UI dispatcher. Tokens have no finalizer;
        // do not wait for unrelated finalizers while running on this UI thread.
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);

        Assert.Same(identity, series.CreateSnapshot().Points[1].StableId);
        GC.KeepAlive(identity);
    }

    [AvaloniaFact]
    public void Public_snapshot_constructor_defensively_copies_points_and_issues()
    {
        var identity = new object();
        var original = new ChartDataPoint(5, identity, "source", "X", new ChartPoint(1, 2), false) { Value = "Y" };
        var points = new[] { original };
        var issue = new ChartDataIssue(5, "Value", "missing");
        var issues = new[] { issue };
        var snapshot = new ChartDataSnapshot(3, points, issues);
        points[0] = original with { Point = new ChartPoint(99, 100), Value = "changed" };
        issues[0] = new ChartDataIssue(0, null, "changed");

        Assert.Equal(original, Assert.Single(snapshot.Points));
        Assert.Same(identity, snapshot.Points[0].StableId);
        Assert.Equal("Y", snapshot.Points[0].Value);
        Assert.Same(issue, Assert.Single(snapshot.Issues));
    }

    [AvaloniaFact]
    public void Published_snapshot_source_indices_are_independent_of_storage_order()
    {
        var source = new[] { new ChartPoint(2, 20), new ChartPoint(4, 40), new ChartPoint(6, 60) };
        var series = new LineSeries { ItemsSource = source };
        using var owner = new SeriesOwner(series);
        var current = series.CreateSnapshot();
        var reversed = new ChartDataSnapshot(current.Version, current.Points.Reverse().ToArray());
        Assert.True(series.TryPublishSnapshot(reversed, current.Version));
        var published = series.CreateSnapshot();

        Assert.Same(reversed, published);
        Assert.Equal(new[] { 2, 1, 0 }, published.Points.Select(point => point.SourceIndex));
        var sourceZero = published.Points.Single(point => point.SourceIndex == 0);
        Assert.Equal(source[0], sourceZero.Point);
        Assert.Same(current.Points[0].StableId, sourceZero.StableId);
        Assert.Equal(source[2], published.Points[0].Point);
    }

    [AvaloniaFact]
    public void Small_unknown_length_sources_do_not_reserve_a_large_data_buffer()
    {
        var source = new GenericOnlyPointSource(Enumerable.Range(0, 1000).Select(index => new ChartPoint(index, index)).ToArray());
        // Warm generic dispatch and subscription code before measuring a realistic small update.
        var warmup = new LineSeries { ItemsSource = source };
        using var warmOwner = new SeriesOwner(warmup);
        var series = new LineSeries();
        using var owner = new SeriesOwner(series);
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        series.ItemsSource = source;
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Assert.True(allocated < 128 * 1024, $"A 1,000-point update allocated {allocated} bytes.");
        Assert.Equal(new ChartPoint(999, 999), series.CreateSnapshot().GetValue(999).Point);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(32)]
    [InlineData(4095)]
    [InlineData(4096)]
    [InlineData(4097)]
    [InlineData(8193)]
    public void Unknown_length_chunk_boundaries_keep_an_independently_owned_snapshot(int count)
    {
        var values = Enumerable.Range(0, count).Select(index => new ChartPoint(index, index + 17)).ToArray();
        var source = new CountingSource<ChartPoint>(values);
        var data = CompactSeriesData.TryCreate(source);
        Assert.NotNull(data);
        var snapshot = data.CreateSnapshot(1, EmptyPointMode.Gap);
        Assert.Equal(1, source.EnumerationCount);
        Assert.Equal(count + 1, source.MoveNextCount);
        Assert.Equal(1, source.DisposeCount);
        Assert.Equal(count, snapshot.Count);

        // Subsequent sources may reuse staging arrays, never this published buffer.
        if (count > 0) values[0] = new ChartPoint(-100, -200);
        _ = CompactSeriesData.TryCreate(new CountingSource<ChartPoint>(
            Enumerable.Range(0, 12_289).Select(index => new ChartPoint(-index, -index - 90)).ToArray()));
        for (var index = 0; index < count; index++)
        {
            Assert.Equal(new ChartPoint(index, index + 17), snapshot.Coordinates.Span[index]);
            Assert.Equal(new ChartPoint(index, index + 17), snapshot.GetValue(index).Point);
        }
        if (count > 0)
        {
            Assert.Equal(new ChartPoint(0, 17), Assert.IsType<ChartPoint>(snapshot.Points[0].Item));
            Assert.Equal(count - 1, snapshot.Points[count - 1].SourceIndex);
        }
        AssertReturnedStagingIsClearAndBounded();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void An_exception_after_borrowing_chunks_does_not_publish_or_corrupt_recovery(bool throwFromDispose)
    {
        var series = new LineSeries { ItemsSource = new ChartPoint[] { new(3, 7) } };
        using var owner = new SeriesOwner(series);
        var previous = series.CreateSnapshot();
        var failure = new InvalidOperationException(throwFromDispose ? "dispose failure" : "move failure");
        var source = new CountingSource<ChartPoint>(
            Enumerable.Range(0, 12_289).Select(index => new ChartPoint(index, index + 1)).ToArray())
        {
            MoveNextFailureIndex = throwFromDispose ? null : 8_193,
            DisposeFailure = throwFromDispose ? failure : null,
            MoveNextFailure = throwFromDispose ? null : failure,
        };
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => series.ItemsSource = source));
        Assert.Equal(1, source.EnumerationCount);
        Assert.Equal(1, source.DisposeCount);
        AssertReturnedStagingIsClearAndBounded();

        var recoveredSource = new CountingSource<ChartPoint>(
            Enumerable.Range(0, 8_193).Select(index => new ChartPoint(index, -index - 20)).ToArray());
        series.ItemsSource = recoveredSource;
        var recovered = series.CreateSnapshot();
        Assert.Equal(1, recoveredSource.EnumerationCount);
        Assert.Equal(1, recoveredSource.DisposeCount);
        Assert.Equal(new ChartPoint(8_192, -8_212), recovered.GetValue(8_192).Point);
        Assert.Equal(new ChartPoint(3, 7), Assert.Single(previous.Points).Point);
        Assert.True(recovered.Version > previous.Version);
        AssertReturnedStagingIsClearAndBounded();
    }

    [AvaloniaFact]
    public void Replacing_the_source_after_borrowing_chunks_cannot_publish_the_abandoned_values()
    {
        var series = new LineSeries { ItemsSource = new ChartPoint[] { new(2, 4) } };
        using var owner = new SeriesOwner(series);
        var previous = series.CreateSnapshot();
        var replacement = new ObservableCollection<ChartPoint> { new(30, 40) };
        var source = new CountingSource<ChartPoint>(
            Enumerable.Range(0, 12_289).Select(index => new ChartPoint(index, index)).ToArray())
        {
            BeforeMoveNext = index => { if (index == 8_193) series.ItemsSource = replacement; },
        };
        Assert.ThrowsAny<InvalidOperationException>(() => series.ItemsSource = source);
        Assert.Equal(1, source.EnumerationCount);
        Assert.Equal(1, source.DisposeCount);
        AssertReturnedStagingIsClearAndBounded();

        var recovered = series.CreateSnapshot();
        Assert.Equal(new ChartPoint(30, 40), Assert.Single(recovered.Points).Point);
        replacement.Add(new ChartPoint(50, 60));
        Assert.Equal(2, series.CreateSnapshot().Count);
        Assert.Equal(new ChartPoint(2, 4), Assert.Single(previous.Points).Point);
        Assert.True(recovered.Version > previous.Version);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Signed_and_unsigned_unknown_length_sources_keep_their_real_scalar_types(int family)
    {
        switch (family)
        {
            case 0: AssertPooledScalarPair(int.MinValue, uint.MaxValue); break;
            case 1: AssertPooledScalarPair(long.MinValue, ulong.MaxValue); break;
            case 2: AssertPooledScalarPair(short.MinValue, ushort.MaxValue); break;
            default: AssertPooledScalarPair(sbyte.MinValue, byte.MaxValue); break;
        }
        AssertReturnedStagingIsClearAndBounded();
    }

    [Fact]
    public void Nullable_decimal_chunks_preserve_nulls_raw_items_and_previous_values()
    {
        var values = Enumerable.Range(0, 131_073).Select(index => index % 3 == 0 ? (decimal?)null : index + .25m).ToArray();
        var source = new CountingSource<decimal?>(values);
        var data = CompactSeriesData.TryCreate(source);
        Assert.NotNull(data);
        var snapshot = data.CreateSnapshot(3, EmptyPointMode.Gap);
        _ = CompactSeriesData.TryCreate(new CountingSource<decimal?>(Enumerable.Repeat<decimal?>(999, 131_073).ToArray()));

        Assert.Equal(1, source.EnumerationCount);
        Assert.Equal(1, source.DisposeCount);
        foreach (var index in new[] { 0, 1, 4_095, 4_096, 4_097, 8_192, 12_288 })
        {
            Assert.Equal(values[index] is null, snapshot.GetValue(index).IsEmpty);
            Assert.Equal(values[index], snapshot.Points[index].Item);
            if (values[index] is { } number)
                Assert.Equal((double)number, snapshot.GetValue(index).Point.Y);
            else Assert.True(double.IsNaN(snapshot.GetValue(index).Point.Y));
        }
        // Nullable decimal's larger element layout reaches the payload budget
        // before ChartPoint. Values beyond both budgets still belong to this snapshot.
        Assert.Equal(131_073, snapshot.Count);
        Assert.Equal(131_074, source.MoveNextCount);
        Assert.Equal(100_000.25m, Assert.IsType<decimal>(snapshot.Points[100_000].Item));
        Assert.True(snapshot.GetValue(100_002).IsEmpty);
        Assert.Equal(new ChartPoint(131_072, 131_072.25), snapshot.GetValue(131_072).Point);
        AssertReturnedStagingIsClearAndBounded();
    }

    [Fact]
    public void Mixed_value_types_share_one_cleared_bounded_staging_pool()
    {
        // Each source needs more chunks than the retention limit. Inspect the
        // actual arrays, rather than trusting a separate diagnostic byte counter.
        FillStagingPool(new ChartPoint(3, 4));
        FillStagingPool(17);
        FillStagingPool(uint.MaxValue);
        FillStagingPool<decimal?>(7.25m);
        FillStagingPool<double?>(null);
        FillStagingPool(new Point(5, 6));
        Assert.NotEmpty(AssertReturnedStagingIsClearAndBounded());
    }

    [Fact]
    public async Task Concurrent_collectors_never_share_published_arrays_or_lose_values()
    {
        var captures = await Task.WhenAll(Enumerable.Range(0, 8).Select(worker => Task.Run(() =>
        {
            var source = new CountingSource<ChartPoint>(Enumerable.Range(0, 8_193)
                .Select(index => new ChartPoint(index, worker * 100_000 + index)).ToArray());
            var data = CompactSeriesData.TryCreate(source);
            Assert.NotNull(data);
            var snapshot = data.CreateSnapshot(worker, EmptyPointMode.Gap);
            Assert.Equal(1, source.EnumerationCount);
            Assert.Equal(1, source.DisposeCount);
            return (Worker: worker, Snapshot: snapshot);
        })));

        var publishedArrays = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (var capture in captures)
        {
            Assert.True(MemoryMarshal.TryGetArray(capture.Snapshot.Coordinates, out var segment));
            Assert.NotNull(segment.Array);
            Assert.True(publishedArrays.Add(segment.Array));
        }
        FillStagingPool(new ChartPoint(-1, -2));
        foreach (var capture in captures)
            for (var index = 0; index < capture.Snapshot.Count; index++)
                Assert.Equal(new ChartPoint(index, capture.Worker * 100_000 + index), capture.Snapshot.GetValue(index).Point);
        AssertReturnedStagingIsClearAndBounded();
    }

    [Fact]
    public void Collection_bulk_copy_stays_single_copy_and_owns_the_published_values()
    {
        var values = Enumerable.Range(0, 4_096).Select(index => new ChartPoint(index, index + 8)).ToArray();
        var source = new CopyOnlyCollection<ChartPoint>(values);
        var data = CompactSeriesData.TryCreate(source);
        Assert.NotNull(data);
        var snapshot = data.CreateSnapshot(1, EmptyPointMode.Gap);
        Assert.Equal(1, source.CopyCount);
        values[0] = new ChartPoint(90, 99);
        FillStagingPool(new ChartPoint(-1, -1));
        Assert.Equal(new ChartPoint(0, 8), snapshot.GetValue(0).Point);
        Assert.Equal(new ChartPoint(4_095, 4_103), snapshot.GetValue(4_095).Point);
    }

    [Fact]
    public void An_exact_known_count_chunk_remains_owned_after_unknown_length_reuse()
    {
        var source = Enumerable.Repeat(new ChartPoint(11, 13), 4_096);
        Assert.True(source.TryGetNonEnumeratedCount(out var count));
        Assert.Equal(4_096, count);
        var data = CompactSeriesData.TryCreate(source);
        Assert.NotNull(data);
        var snapshot = data.CreateSnapshot(2, EmptyPointMode.Gap);
        FillStagingPool(new ChartPoint(70, 80));
        Assert.All(snapshot.Points, point => Assert.Equal(new ChartPoint(11, 13), point.Point));
        AssertReturnedStagingIsClearAndBounded();
    }

    [Fact]
    public void A_source_larger_than_the_staging_budget_preserves_all_owned_coordinates_after_reuse()
    {
        const int count = 131_073;
        var values = Enumerable.Range(0, count).Select(index => new ChartPoint(index, index * 2 + 1)).ToArray();
        var source = new CountingSource<ChartPoint>(values);
        var data = CompactSeriesData.TryCreate(source);
        Assert.NotNull(data);
        var original = data.CreateSnapshot(1, EmptyPointMode.Gap);
        var identity = original.Points[100_001].StableId;
        Assert.Equal(1, source.EnumerationCount);
        Assert.Equal(count + 1, source.MoveNextCount);
        Assert.Equal(1, source.DisposeCount);
        Assert.Equal(count, original.Count);
        Assert.True(MemoryMarshal.TryGetArray(original.Coordinates, out var originalBuffer));

        Array.Clear(values);
        var nextSource = new CountingSource<ChartPoint>(Enumerable.Range(0, count)
            .Select(index => new ChartPoint(index, -index * 3 - 5)).ToArray());
        var nextData = CompactSeriesData.TryCreate(nextSource);
        Assert.NotNull(nextData);
        var next = nextData.CreateSnapshot(2, EmptyPointMode.Gap);
        Assert.Equal(1, nextSource.EnumerationCount);
        Assert.Equal(count + 1, nextSource.MoveNextCount);
        Assert.Equal(1, nextSource.DisposeCount);
        Assert.True(MemoryMarshal.TryGetArray(next.Coordinates, out var nextBuffer));
        Assert.NotSame(originalBuffer.Array, nextBuffer.Array);
        for (var index = 0; index < count; index++)
        {
            Assert.Equal(new ChartPoint(index, index * 2 + 1), original.Coordinates.Span[index]);
            Assert.Equal(new ChartPoint(index, -index * 3 - 5), next.Coordinates.Span[index]);
        }
        Assert.Equal(new ChartPoint(100_001, 200_003), Assert.IsType<ChartPoint>(original.Points[100_001].Item));
        Assert.Same(identity, original.Points[100_001].StableId);
        Assert.Equal(100_001, original.Points[100_001].SourceIndex);
        var returned = AssertReturnedStagingIsClearAndBounded();
        Assert.DoesNotContain(returned, array => ReferenceEquals(array, originalBuffer.Array));
        Assert.DoesNotContain(returned, array => ReferenceEquals(array, nextBuffer.Array));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Exceptions_after_more_than_one_hundred_thousand_items_leave_recovery_and_staging_valid(bool throwFromDispose)
    {
        const int count = 131_073;
        var series = new LineSeries { ItemsSource = new ChartPoint[] { new(5, 9) } };
        using var owner = new SeriesOwner(series);
        var previous = series.CreateSnapshot();
        var failure = new InvalidOperationException(throwFromDispose ? "large dispose failure" : "large move failure");
        var source = new CountingSource<ChartPoint>(Enumerable.Range(0, count)
            .Select(index => new ChartPoint(index, index + 7)).ToArray())
        {
            MoveNextFailureIndex = throwFromDispose ? null : 100_001,
            MoveNextFailure = throwFromDispose ? null : failure,
            DisposeFailure = throwFromDispose ? failure : null,
        };
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => series.ItemsSource = source));
        Assert.Equal(1, source.EnumerationCount);
        Assert.Equal(throwFromDispose ? count + 1 : 100_002, source.MoveNextCount);
        Assert.Equal(1, source.DisposeCount);
        AssertReturnedStagingIsClearAndBounded();

        var replacement = new CountingSource<ChartPoint>(Enumerable.Range(0, count)
            .Select(index => new ChartPoint(index, -index - 9)).ToArray());
        series.ItemsSource = replacement;
        var recovered = series.CreateSnapshot();
        Assert.Equal(1, replacement.EnumerationCount);
        Assert.Equal(count + 1, replacement.MoveNextCount);
        Assert.Equal(1, replacement.DisposeCount);
        Assert.Equal(count, recovered.Count);
        foreach (var index in new[] { 0, 4_096, 65_535, 65_536, 100_001, 131_072 })
        {
            Assert.Equal(new ChartPoint(index, -index - 9), recovered.Coordinates.Span[index]);
            Assert.Equal(new ChartPoint(index, -index - 9), Assert.IsType<ChartPoint>(recovered.Points[index].Item));
            Assert.Equal(index, recovered.Points[index].SourceIndex);
        }
        Assert.Equal(new ChartPoint(5, 9), Assert.Single(previous.Points).Point);
        Assert.True(recovered.Version > previous.Version);
        AssertReturnedStagingIsClearAndBounded();
    }

    private static void AssertPooledScalarPair<TSigned, TUnsigned>(TSigned signed, TUnsigned unsigned)
    {
        ChartDataSnapshot Capture<T>(T value)
        {
            var source = new CountingSource<T>(Enumerable.Repeat(value, 12_289).ToArray());
            var data = CompactSeriesData.TryCreate(source);
            Assert.NotNull(data);
            Assert.Equal(1, source.EnumerationCount);
            Assert.Equal(1, source.DisposeCount);
            return data.CreateSnapshot(1, EmptyPointMode.Gap);
        }
        var before = Capture(signed);
        var after = Capture(unsigned);
        var again = Capture(signed);
        foreach (var index in new[] { 0, 4_095, 4_096, 8_192, 12_288 })
        {
            Assert.Equal(signed, Assert.IsType<TSigned>(before.Points[index].Item));
            Assert.Equal(unsigned, Assert.IsType<TUnsigned>(after.Points[index].Item));
            Assert.Equal(signed, Assert.IsType<TSigned>(again.Points[index].Item));
            Assert.Equal(Convert.ToDouble(signed), before.GetValue(index).Point.Y);
            Assert.Equal(Convert.ToDouble(unsigned), after.GetValue(index).Point.Y);
        }
    }

    private static void FillStagingPool<T>(T value)
    {
        var source = new CountingSource<T>(Enumerable.Repeat(value, 73_729).ToArray());
        Assert.NotNull(CompactSeriesData.TryCreate(source));
        Assert.Equal(1, source.EnumerationCount);
        Assert.Equal(1, source.DisposeCount);
        AssertReturnedStagingIsClearAndBounded();
    }

    private static Array[] AssertReturnedStagingIsClearAndBounded()
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        var pool = typeof(CompactSeriesData).GetNestedType("StagingBufferPool", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The owned-data staging pool was not found.");
        var gate = pool.GetField("Gate", flags)?.GetValue(null)
            ?? throw new InvalidOperationException("The staging pool lock was not found.");
        lock (gate)
        {
            var slots = Assert.IsType<Array[]>(pool.GetField("Slots", flags)?.GetValue(null));
            var retained = slots.Where(array => array is not null).ToArray();
            Assert.InRange(retained.Length, 0, 16);
            Assert.Equal(retained.Length, retained.Distinct(ReferenceEqualityComparer.Instance).Count());
            long payloadBytes = 0;
            foreach (var array in retained)
            {
                Assert.Equal(4_096, array.Length);
                var elementType = array.GetType().GetElementType()!;
                var facts = ((int Size, bool ContainsReferences))typeof(CompactDataTests)
                    .GetMethod(nameof(StagingElementFacts), BindingFlags.Static | BindingFlags.NonPublic)!
                    .MakeGenericMethod(elementType).Invoke(null, null)!;
                Assert.False(facts.ContainsReferences);
                payloadBytes += (long)array.Length * facts.Size;
                var empty = Activator.CreateInstance(elementType);
                for (var index = 0; index < array.Length; index++) Assert.Equal(empty, array.GetValue(index));
            }
            Assert.InRange(payloadBytes, 0, 1_048_576);
            return retained;
        }
    }

    private static (int Size, bool ContainsReferences) StagingElementFacts<T>() =>
        (Unsafe.SizeOf<T>(), RuntimeHelpers.IsReferenceOrContainsReferences<T>());

    private sealed class CountingSource<T>(T[] values) : IEnumerable<T>
    {
        private readonly T[] _values = values;
        public int EnumerationCount { get; private set; }
        public int MoveNextCount { get; private set; }
        public int DisposeCount { get; private set; }
        public int? MoveNextFailureIndex { get; init; }
        public Exception? MoveNextFailure { get; init; }
        public Exception? DisposeFailure { get; init; }
        public Action<int>? BeforeMoveNext { get; init; }
        public IEnumerator<T> GetEnumerator() { EnumerationCount++; return new Enumerator(this); }
        IEnumerator IEnumerable.GetEnumerator() => throw new InvalidOperationException("The generic enumerator is required.");
        private sealed class Enumerator(CountingSource<T> source) : IEnumerator<T>
        {
            private int _index = -1;
            public T Current => source._values[_index];
            object? IEnumerator.Current => Current;
            public bool MoveNext()
            {
                source.MoveNextCount++;
                var next = _index + 1;
                source.BeforeMoveNext?.Invoke(next);
                if (next == source.MoveNextFailureIndex) throw source.MoveNextFailure!;
                _index = next;
                return _index < source._values.Length;
            }
            public void Reset() => throw new NotSupportedException();
            public void Dispose()
            {
                source.DisposeCount++;
                if (source.DisposeFailure is { } failure) throw failure;
            }
        }
    }

    private sealed class CopyOnlyCollection<T>(T[] values) : ICollection<T>
    {
        public int CopyCount { get; private set; }
        public int Count => values.Length;
        public bool IsReadOnly => true;
        public void CopyTo(T[] array, int arrayIndex) { CopyCount++; Array.Copy(values, 0, array, arrayIndex, values.Length); }
        public IEnumerator<T> GetEnumerator() => throw new InvalidOperationException("Bulk copy must not enumerate.");
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public void Add(T item) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
        public bool Contains(T item) => values.Contains(item);
        public bool Remove(T item) => throw new NotSupportedException();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static object ReadFirstIdentity(ChartSeries series) => series.CreateSnapshot().Points[0].StableId
        ?? throw new InvalidOperationException("A value occurrence must have an identity.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CreateUnheldIdentities(CompactIdentityRegistry registry)
    { for (var index = 1; index <= 4096; index++) _ = registry.GetToken(index); }

    private sealed class GenericOnlyPointSource(ChartPoint[] values) : IEnumerable<ChartPoint>
    {
        public IEnumerator<ChartPoint> GetEnumerator()
        { foreach (var value in values) yield return value; }
        IEnumerator IEnumerable.GetEnumerator() => throw new InvalidOperationException("A typed point source must use its generic enumerator.");
    }
    private enum SignedExample : sbyte { Negative = -100 }
    private enum UnsignedExample : ulong { Maximum = ulong.MaxValue }

    private sealed class MutablePointSource(params ChartPoint[] values) : IEnumerable<ChartPoint>
    {
        public ChartPoint[] Values { get; } = values;
        public IEnumerator<ChartPoint> GetEnumerator()
        {
            foreach (var value in Values) yield return value;
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class ResettableCollection<T>(IEnumerable<T> items) : ObservableCollection<T>(items)
    {
        public void ResetTo(params T[] items)
        {
            Items.Clear();
            foreach (var item in items) Items.Add(item);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

    private sealed class SeriesOwner : IDisposable
    {
        private readonly ChartSeries _series;
        private readonly EventHandler _changed = (_, _) => { };
        public SeriesOwner(ChartSeries series) { _series = series; _series.Changed += _changed; }
        public void Dispose() => _series.Changed -= _changed;
    }
}
