using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Headless.XUnit;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class MappedSourceOptimizationTests
{
    [AvaloniaFact]
    public void Immutable_bound_rows_do_not_allocate_per_path_observer_walks_or_full_point_records()
    {
        var rows = Enumerable.Range(0, 10_000).Select(index => new NumericRow(index, index, index + 3d)).ToArray();
        var series = new LineSeries { ItemsSource = new[] { rows[0] }, CategoryPath = "X", ValuePath = "Y", IdPath = "Id" };
        EventHandler owner = (_, _) => { };
        series.Changed += owner;
        try
        {
            _ = series.CreateSnapshot();
            var before = GC.GetAllocatedBytesForCurrentThread();
            series.ItemsSource = rows;
            var sourceBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            before = GC.GetAllocatedBytesForCurrentThread();
            var snapshot = series.CreateSnapshot();
            var mappingBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(sourceBytes < 1_000_000, $"Source replacement allocated {sourceBytes} bytes for 10K immutable rows.");
            Assert.True(mappingBytes < 2_000_000, $"Snapshot preparation allocated {mappingBytes} bytes for 10K bound rows.");
            Assert.Equal(rows.Length, snapshot.Count);
            Assert.Equal(rows.Length, snapshot.Coordinates.Length);
            var point = snapshot.Points[9_999];
            Assert.Same(rows[9_999], point.Item);
            Assert.Equal(9_999, point.StableId);
            Assert.IsType<double>(point.Category);
            Assert.IsType<double>(point.Value);
            Assert.Equal(new ChartPoint(9_999, 10_002), point.Point);
            Assert.False(snapshot.TryFindExistingIdentity(9_999, out _, valueEquality: true));
            Assert.True(snapshot.TryFindIdentity(9_999, out var byId, valueEquality: true));
            Assert.Equal(point, byId);
            Assert.True(snapshot.TryFindExistingIdentity(9_999, out var existingId, valueEquality: true));
            Assert.Equal(point, existingId);
        }
        finally { series.Changed -= owner; }
    }

    [AvaloniaFact]
    public void Lightweight_bound_snapshots_retain_raw_metadata_gaps_and_immutable_values()
    {
        var first = new MutableNumericRow(1, 4);
        var rows = new object?[] { first, new { X = 2d, Y = double.PositiveInfinity }, null, new { X = "category", Y = "unknown" } };
        var series = new ScatterSeries { ItemsSource = rows, CategoryPath = "X", ValuePath = "Y" };
        EventHandler owner = (_, _) => { };
        series.Changed += owner;
        try
        {
            var snapshot = series.CreateSnapshot();
            var point = snapshot.Points[0];
            Assert.Same(first, point.Item);
            Assert.Same(first, point.StableId);
            Assert.Equal(4d, point.Value);
            Assert.Equal(double.PositiveInfinity, snapshot.Points[1].Value);
            Assert.True(snapshot.Points[1].WasEmpty);
            Assert.True(double.IsNaN(snapshot.Points[1].Point.Y));
            Assert.True(snapshot.Points[2].IsEmpty);
            Assert.Equal("category", snapshot.Points[3].Category);
            Assert.Equal("unknown", snapshot.Points[3].Value);
            Assert.Equal(3, snapshot.Points[3].Point.X);
            first.Y = 9;
            Assert.Equal(new ChartPoint(1, 4), snapshot.Points[0].Point);
            Assert.Equal(new ChartPoint(1, 9), series.CreateSnapshot().Points[0].Point);
            Assert.True(snapshot.TryFindIdentity(first, out var byReference));
            Assert.Equal(point, byReference);
        }
        finally { series.Changed -= owner; }
    }

    [AvaloniaFact]
    public void Bound_reader_plans_preserve_mixed_nested_runtime_types_and_generic_mapping_results()
    {
        var rows = new[]
        {
            new MixedRow("first", new { X = (object)2d, Y = (object)7d }),
            new MixedRow("second", new AlternatePayload("label", 3m)),
            new MixedRow("null", null),
            new MixedRow("missing", new { X = 3d }),
            new MixedRow("infinite", new AlternatePayload(5d, double.NegativeInfinity))
        };
        var series = new LineSeries { ItemsSource = rows, CategoryPath = "Payload.X", ValuePath = "Payload.Y", IdPath = "Id" };
        var packed = series.CreateSnapshot();
        // A supplied (null) weight uses the full general mapper with otherwise
        // identical source paths, providing a public-contract comparison.
        series.WeightPath = "Weight";
        var general = series.CreateSnapshot();
        Assert.Equal(general.Points, packed.Points);
        Assert.Equal(general.Issues, packed.Issues);
        Assert.Equal(new ChartPoint(1, 3), packed.Points[1].Point);
        Assert.IsType<decimal>(packed.Points[1].Value);
        Assert.Equal("label", packed.Points[1].Category);
        Assert.True(packed.Points[2].IsEmpty);
        Assert.Contains(packed.Issues, issue => issue.SourceIndex == 3 && issue.Path == "Payload.Y");
    }

    [AvaloniaFact]
    public void Observer_plans_keep_object_typed_children_and_rewire_replacements()
    {
        var metric = new MutableNumericRow(0, 2);
        var holder = new DynamicHolder(metric);
        var series = new LineSeries { ItemsSource = new[] { holder }, ValuePath = "Metric.Y" };
        EventHandler owner = (_, _) => { };
        series.Changed += owner;
        try
        {
            var first = series.CreateSnapshot();
            Assert.Equal(1, holder.Subscribers);
            Assert.Equal(1, metric.Subscribers);
            metric.Y = 5;
            Assert.Equal(5, series.CreateSnapshot().Points[0].Point.Y);
            var replacement = new MutableNumericRow(0, 8);
            holder.Metric = replacement;
            Assert.Equal(0, metric.Subscribers);
            Assert.Equal(1, replacement.Subscribers);
            Assert.Equal(8, series.CreateSnapshot().Points[0].Point.Y);
            Assert.Equal(2, first.Points[0].Point.Y);
        }
        finally { series.Changed -= owner; }
        Assert.Equal(0, holder.Subscribers);
        Assert.Equal(0, ((MutableNumericRow)holder.Metric).Subscribers);
    }

    [AvaloniaFact]
    public void Observer_plans_follow_notifying_children_inside_value_type_rows()
    {
        var metric = new MutableNumericRow(0, 2);
        var series = new LineSeries { ItemsSource = new[] { new NullableContainer(metric) }, ValuePath = "Metric.Y" };
        EventHandler owner = (_, _) => { };
        series.Changed += owner;
        try
        {
            Assert.Equal(1, metric.Subscribers);
            var first = series.CreateSnapshot();
            metric.Y = 8;
            Assert.Equal(2, first.Points[0].Point.Y);
            Assert.Equal(8, series.CreateSnapshot().Points[0].Point.Y);
        }
        finally { series.Changed -= owner; }
        Assert.Equal(0, metric.Subscribers);
    }

    [AvaloniaFact]
    public void Observer_plans_follow_the_runtime_boxing_type_of_nullable_struct_paths()
    {
        var metric = new MutableNumericRow(0, 2);
        var holder = new NullableHolder(new NullableContainer(metric));
        var series = new LineSeries { ItemsSource = new[] { holder }, ValuePath = "Container.Metric.Y" };
        EventHandler owner = (_, _) => { };
        series.Changed += owner;
        try
        {
            Assert.Equal(1, metric.Subscribers);
            var first = series.CreateSnapshot();
            metric.Y = 8;
            Assert.Equal(2, first.Points[0].Point.Y);
            Assert.Equal(8, series.CreateSnapshot().Points[0].Point.Y);
        }
        finally { series.Changed -= owner; }
        Assert.Equal(0, metric.Subscribers);
    }

    [AvaloniaFact]
    public void Bound_value_type_tokens_still_follow_moves_and_replacements()
    {
        var rows = new ObservableCollection<ValueRow> { new(1, 2), new(3, 4) };
        var series = new LineSeries { ItemsSource = rows, CategoryPath = "X", ValuePath = "Y" };
        EventHandler owner = (_, _) => { };
        series.Changed += owner;
        try
        {
            var initial = series.CreateSnapshot();
            var id = initial.Points[0].StableId;
            rows.Move(0, 1);
            Assert.Same(id, series.CreateSnapshot().Points[1].StableId);
            rows[1] = new(5, 6);
            Assert.NotSame(id, series.CreateSnapshot().Points[1].StableId);
            Assert.Equal(new ChartPoint(1, 2), initial.Points[0].Point);
        }
        finally { series.Changed -= owner; }
    }

    private sealed record NumericRow(int Id, double X, double Y);
    private sealed record MixedRow(string Id, object? Payload) { public double? Weight => null; }
    private sealed record AlternatePayload(object X, object Y);
    private sealed record NullableHolder(NullableContainer? Container);
    private readonly record struct NullableContainer(MutableNumericRow Metric);
    private readonly record struct ValueRow(double X, double Y);
    private sealed class MutableNumericRow(double x, double y) : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _changed;
        public int Subscribers { get; private set; }
        public double X => x;
        public double Y { get => y; set { y = value; _changed?.Invoke(this, new(nameof(Y))); } }
        public event PropertyChangedEventHandler? PropertyChanged
        { add { _changed += value; Subscribers++; } remove { _changed -= value; Subscribers--; } }
    }
    private sealed class DynamicHolder(object metric) : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _changed;
        public int Subscribers { get; private set; }
        public object Metric { get => metric; set { metric = value; _changed?.Invoke(this, new(nameof(Metric))); } }
        public event PropertyChangedEventHandler? PropertyChanged
        { add { _changed += value; Subscribers++; } remove { _changed -= value; Subscribers--; } }
    }
}
