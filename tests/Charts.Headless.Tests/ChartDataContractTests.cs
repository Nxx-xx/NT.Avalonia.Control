using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class ChartDataContractTests
{
    [AvaloniaFact]
    public void Categorical_value_mapping_preserves_the_raw_value_and_reads_its_getter_once()
    {
        var reads = 0;
        var row = new CategoricalValueModel(() => { reads++; return "High"; });
        var series = new LineSeries { ItemsSource = new[] { row }, ValuePath = nameof(CategoricalValueModel.Value) };
        var point = Assert.Single(series.CreateSnapshot().Points);
        Assert.Equal("High", point.Value);
        Assert.True(double.IsNaN(point.Point.Y));
        Assert.True(point.IsEmpty);
        Assert.Equal(1, reads);

        series.ValuePath = null;
        series.ItemsSource = new[] { "Low" };
        Assert.Equal("Low", Assert.Single(series.CreateSnapshot().Points).Value);
        series.ItemsSource = new[] { 3d };
        Assert.Null(Assert.Single(series.CreateSnapshot().Points).Value);
        series.ItemsSource = new[] { new ChartPoint(1, 2) };
        Assert.Null(Assert.Single(series.CreateSnapshot().Points).Value);
    }

    [AvaloniaFact]
    public void Numeric_and_point_sources_keep_original_indices_and_items()
    {
        var items = new object?[] { 4, 5.5m, null, new ChartPoint(20, 9), double.NaN };
        var series = new LineSeries { ItemsSource = items };
        var snapshot = series.CreateSnapshot();
        Assert.Equal(5, snapshot.Points.Count);
        Assert.Equal(new ChartPoint(0, 4), snapshot.Points[0].Point);
        Assert.Equal(new ChartPoint(1, 5.5), snapshot.Points[1].Point);
        Assert.True(snapshot.Points[2].IsEmpty);
        Assert.Equal(new ChartPoint(20, 9), snapshot.Points[3].Point);
        Assert.Same(items[3], snapshot.Points[3].Item);
        Assert.Equal(Enumerable.Range(0, 5), snapshot.Points.Select(point => point.SourceIndex));
        Assert.True(snapshot.Points[4].IsEmpty);
    }

    [AvaloniaFact]
    public void Property_paths_preserve_categories_ids_range_weight_and_date_coordinates()
    {
        var date = new DateTime(2026, 10, 15, 3, 0, 0, DateTimeKind.Unspecified);
        var row = new DataRow("row-1", date, new ValueModel(12), 20, 5, 2);
        var series = new ScatterSeries
        {
            ItemsSource = new[] { row }, CategoryPath = nameof(DataRow.Category), ValuePath = "Metric.Value",
            IdPath = nameof(DataRow.Id), HighPath = nameof(DataRow.High), LowPath = nameof(DataRow.Low), WeightPath = nameof(DataRow.Weight)
        };
        var point = Assert.Single(series.CreateSnapshot().Points);
        Assert.Equal(new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Utc)).ToUnixTimeMilliseconds(), point.Point.X);
        Assert.Equal(12, point.Point.Y);
        Assert.Equal(date, point.Category);
        Assert.Equal("row-1", point.StableId);
        Assert.Equal(20, point.High);
        Assert.Equal(5, point.Low);
        Assert.Equal(2, point.Weight);
        series.CategoryPath = nameof(DataRow.Id);
        Assert.Equal("row-1", series.CreateSnapshot().Points[0].Category);
        Assert.Equal(0, series.CreateSnapshot().Points[0].Point.X);
    }

    [AvaloniaFact]
    public void Cached_snapshots_are_immutable_and_nested_item_changes_invalidate_the_version()
    {
        var metric = new ValueModel(3);
        var row = new DataRow("a", "January", metric);
        var series = new LineSeries { ItemsSource = new[] { row }, CategoryPath = "Category", ValuePath = "Metric.Value" };
        EventHandler owner = (_, _) => { };
        series.Changed += owner;
        try
        {
            var first = series.CreateSnapshot();
            Assert.Same(first, series.CreateSnapshot());
            Assert.Equal(1, row.SubscriberCount);
            Assert.Equal(1, metric.SubscriberCount);
            metric.Value = 7;
            var second = series.CreateSnapshot();
            Assert.True(second.Version > first.Version);
            Assert.Equal(3, first.Points[0].Point.Y);
            Assert.Equal(7, second.Points[0].Point.Y);
            var replacement = new ValueModel(11);
            row.Metric = replacement;
            Assert.Equal(0, metric.SubscriberCount);
            Assert.Equal(1, replacement.SubscriberCount);
            Assert.Equal(11, series.CreateSnapshot().Points[0].Point.Y);
        }
        finally { series.Changed -= owner; }
        Assert.Equal(0, row.SubscriberCount);
        Assert.Equal(0, row.Metric.SubscriberCount);
    }

    [AvaloniaFact]
    public void Value_type_identity_tokens_follow_collection_moves_and_original_indices_update()
    {
        var values = new ObservableCollection<double> { 1, 2, 3 };
        var series = new LineSeries { ItemsSource = values };
        EventHandler owner = (_, _) => { };
        series.Changed += owner;
        try
        {
            var initial = series.CreateSnapshot();
            var identity = initial.Points[0].StableId;
            values.Move(0, 2);
            var moved = series.CreateSnapshot();
            Assert.Same(identity, moved.Points[2].StableId);
            Assert.Equal(2, moved.Points[2].SourceIndex);
            Assert.Equal(1, moved.Points[2].Point.Y);
            values.Insert(0, 99);
            Assert.Same(identity, series.CreateSnapshot().Points[3].StableId);
            values.RemoveAt(0);
            values[2] = 10;
            Assert.NotSame(identity, series.CreateSnapshot().Points[2].StableId);
        }
        finally { series.Changed -= owner; }
    }

    [AvaloniaTheory]
    [InlineData(EmptyPointMode.Gap, double.NaN)]
    [InlineData(EmptyPointMode.Zero, 0)]
    [InlineData(EmptyPointMode.Average, 5)]
    [InlineData(EmptyPointMode.Interpolate, 2)]
    public void Empty_point_modes_preserve_the_source_and_use_data_coordinates_for_interpolation(EmptyPointMode mode, double expected)
    {
        var source = new[] { new NullableValue(0, 0), new NullableValue(2, null), new NullableValue(10, 10) };
        var series = new LineSeries { ItemsSource = source, CategoryPath = "X", ValuePath = "Y", EmptyPointMode = mode };
        var value = series.CreateSnapshot().Points[1];
        Assert.True(value.WasEmpty);
        Assert.Equal(mode == EmptyPointMode.Gap, value.IsEmpty);
        Assert.Equal(expected, value.Point.Y);
        Assert.Null(source[1].Y);
    }

    [AvaloniaFact]
    public void Nested_update_scopes_coalesce_collection_and_item_notifications()
    {
        var row = new DataRow("a", "A", new ValueModel(2));
        var values = new ObservableCollection<DataRow> { row };
        var series = new LineSeries { ItemsSource = values, ValuePath = "Metric.Value" };
        var calls = 0;
        EventHandler owner = (_, _) => calls++;
        series.Changed += owner;
        calls = 0;
        try
        {
            using (series.DeferUpdates())
            {
                values.Add(new DataRow("b", "B", new ValueModel(4)));
                using (series.DeferUpdates()) row.Metric.Value = 6;
                Assert.True(series.IsUpdateSuspended);
                Assert.Equal(0, calls);
            }
            Assert.Equal(1, calls);
            Assert.Equal(new[] { 6d, 4d }, series.CreateSnapshot().Points.Select(point => point.Point.Y));
            Assert.Throws<InvalidOperationException>(series.ResumeUpdates);
        }
        finally { series.Changed -= owner; }
    }

    [AvaloniaFact]
    public void Mapping_errors_are_bounded_and_duplicate_explicit_ids_are_reported()
    {
        var series = new LineSeries { ItemsSource = Enumerable.Range(0, 150).Select(index => new { Id = index }).ToArray(), ValuePath = "Missing" };
        var errors = new List<ChartDataIssue>();
        series.DataError += (_, args) => errors.Add(args.Issue);
        var snapshot = series.CreateSnapshot();
        Assert.Equal(100, errors.Count);
        Assert.Equal(100, snapshot.Issues.Count);
        Assert.All(snapshot.Points, point => Assert.True(point.IsEmpty));
        series.ItemsSource = new[] { new { Id = "same", Value = 1 }, new { Id = "same", Value = 2 } };
        series.ValuePath = "Value";
        series.IdPath = "Id";
        Assert.Contains("duplicate", Assert.Throws<InvalidOperationException>(() => series.CreateSnapshot()).Message);
        Assert.Contains(errors, error => error.Path == "Id");
    }

    [AvaloniaFact]
    public void Reentrant_source_replacement_cannot_publish_a_snapshot_of_the_wrong_version()
    {
        var series = new LineSeries();
        var row = new GetterModel(() => { series.ItemsSource = new[] { 99d }; return 2; });
        series.ItemsSource = new[] { row };
        series.ValuePath = "Value";
        var error = Assert.ThrowsAny<InvalidOperationException>(() => series.CreateSnapshot());
        Assert.Contains("source changed while mapping", error.Message);
        series.ValuePath = null;
        Assert.Equal(99, Assert.Single(series.CreateSnapshot().Points).Point.Y);

        var recursive = new GetterModel(() => series.CreateSnapshot().Points.Count);
        series.ItemsSource = new[] { recursive };
        series.ValuePath = "Value";
        Assert.Contains("Reentrant", Assert.ThrowsAny<InvalidOperationException>(() => series.CreateSnapshot()).Message);
        series.ItemsSource = new[] { 1d };
        series.ValuePath = null;
        Assert.Equal(1, Assert.Single(series.CreateSnapshot().Points).Point.Y);
    }

    [AvaloniaFact]
    public void Reentrant_changed_notifications_coalesce_without_recursive_callbacks()
    {
        var series = new LineSeries { ItemsSource = new[] { 1d } };
        var depth = 0; var maximumDepth = 0; var calls = 0;
        EventHandler owner = (_, _) =>
        {
            depth++; maximumDepth = Math.Max(maximumDepth, depth); calls++;
            if (calls == 1) series.Title = "changed inside callback";
            depth--;
        };
        series.Changed += owner;
        try { Assert.Equal(2, calls); Assert.Equal(1, maximumDepth); }
        finally { series.Changed -= owner; }
    }

    [AvaloniaFact]
    public void Unowned_snapshot_publication_is_rejected_and_unloading_drops_published_state()
    {
        var series = new LineSeries { ItemsSource = new[] { 1d } };
        var unowned = series.CreateSnapshot();
        Assert.False(series.TryPublishSnapshot(unowned, unowned.Version));
        EventHandler owner = (_, _) => { };
        series.Changed += owner;
        var current = series.CreateSnapshot();
        var replacement = new ChartDataSnapshot(current.Version, current.Points.Select(point => point with { Point = point.Point with { Y = 99 } }).ToArray());
        Assert.True(series.TryPublishSnapshot(replacement, current.Version));
        Assert.Same(replacement, series.CreateSnapshot());
        series.Changed -= owner;
        Assert.Equal(1, series.CreateSnapshot().Points[0].Point.Y);
        Assert.False(series.TryPublishSnapshot(replacement, replacement.Version));
    }

    [AvaloniaFact]
    public async Task Background_publication_dispatches_to_ui_and_rejects_stale_cancelled_or_malformed_snapshots()
    {
        var values = new ObservableCollection<double> { 1, 2 };
        var series = new LineSeries { ItemsSource = values };
        EventHandler owner = (_, _) => { };
        series.Changed += owner;
        try
        {
            var old = series.CreateSnapshot();
            values.Add(3);
            Assert.False(series.TryPublishSnapshot(old, old.Version));
            var current = series.CreateSnapshot();
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            Assert.False(await series.PublishSnapshotAsync(current, current.Version, cancelled.Token));
            var duplicates = new ChartDataSnapshot(current.Version, new[] { current.Points[0], current.Points[0], current.Points[2] });
            Assert.False(series.TryPublishSnapshot(duplicates, current.Version));
            var updated = new ChartDataSnapshot(current.Version, current.Points.Select(point => point with { Point = point.Point with { Y = point.Point.Y + 10 } }).ToArray());
            Task<bool>? pending = null;
            await Task.Run(() => { pending = series.PublishSnapshotAsync(updated, current.Version); });
            Dispatcher.UIThread.RunJobs();
            Assert.True(await pending!);
            Assert.Same(updated, series.CreateSnapshot());
            Assert.Equal(11, series.CreateSnapshot().Points[0].Point.Y);
            await Task.Run(() => Assert.Throws<InvalidOperationException>(series.Refresh));
        }
        finally { series.Changed -= owner; }
    }

    private sealed record NullableValue(double X, double? Y);
    private sealed class CategoricalValueModel(Func<string> getter) { public string Value => getter(); }
    private sealed class GetterModel(Func<double> getter) { public double Value => getter(); }
    private sealed class ValueModel(double initial) : INotifyPropertyChanged
    {
        private double _value = initial;
        private PropertyChangedEventHandler? _changed;
        public double Value { get => _value; set { _value = value; _changed?.Invoke(this, new PropertyChangedEventArgs(nameof(Value))); } }
        public int SubscriberCount => _changed?.GetInvocationList().Length ?? 0;
        public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
    }
    private sealed class DataRow(string id, object category, ValueModel metric, double high = 0, double low = 0, double weight = 0) : INotifyPropertyChanged
    {
        private ValueModel _metric = metric;
        private PropertyChangedEventHandler? _changed;
        public string Id => id;
        public object Category => category;
        public double High => high;
        public double Low => low;
        public double Weight => weight;
        public ValueModel Metric { get => _metric; set { _metric = value; _changed?.Invoke(this, new PropertyChangedEventArgs(nameof(Metric))); } }
        public int SubscriberCount => _changed?.GetInvocationList().Length ?? 0;
        public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
    }
}
