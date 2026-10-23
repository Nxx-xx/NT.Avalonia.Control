using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class PolarDataTests
{
    [AvaloniaFact]
    public void Angle_and_radius_paths_alias_existing_mapping_and_revisions_in_both_directions()
    {
        var series = new PolarLineSeries();
        Assert.Same(ChartSeries.CategoryPathProperty, PolarSeries.AnglePathProperty);
        Assert.Same(ChartSeries.ValuePathProperty, PolarSeries.RadiusPathProperty);
        series.AnglePath = "Angle"; series.RadiusPath = "Radius";
        Assert.Equal("Angle", series.CategoryPath); Assert.Equal("Radius", series.ValuePath);
        series.CategoryPath = "OtherAngle"; series.ValuePath = "OtherRadius";
        Assert.Equal("OtherAngle", series.AnglePath); Assert.Equal("OtherRadius", series.RadiusPath);
        series.ItemsSource = new[] { new { OtherAngle = 30d, OtherRadius = 40d } };
        Assert.Equal(new ChartPoint(30, 40), Assert.Single(series.CreateSnapshot().Points).Point);
    }

    [AvaloniaFact]
    public void Categorical_series_accept_arbitrary_keys_while_numeric_angles_reject_nonnumeric_labels()
    {
        var key = new Key("category"); var row = new Row("a", key, 25, "Red");
        var radar = Configure(new RadarSeries(), new[] { row });
        var point = Assert.Single(radar.CreateSnapshot().Points);
        Assert.Same(key, point.Category); Assert.False(point.IsEmpty); Assert.Equal(0, point.Point.X);
        var line = Configure(new PolarLineSeries(), new[] { row });
        Assert.True(Assert.Single(line.CreateSnapshot().Points).IsEmpty);
        row.Angle = "45";
        Assert.Equal(45, Assert.Single(line.CreateSnapshot().Points).Point.X);
        row.Angle = "not-an-angle";
        Assert.True(Assert.Single(line.CreateSnapshot().Points).IsEmpty);
    }

    [AvaloniaFact]
    public void Mapping_retains_original_values_items_ids_source_indices_and_direct_color_inputs()
    {
        var brush = new LinearGradientBrush();
        var rows = new[] { new Row("a", 90d, -25, Colors.Red), new Row("b", 180d, 0, brush), new Row("c", 270d, 75, "Blue") };
        var series = Configure(new PolarScatterSeries(), rows);
        var snapshot = series.CreateSnapshot();
        Assert.Equal(new[] { -25d, 0, 75 }, snapshot.Points.Select(p => p.Point.Y));
        Assert.All(snapshot.Points, p => Assert.False(p.IsEmpty));
        for (var i = 0; i < rows.Length; i++)
        {
            Assert.Same(rows[i], snapshot.Points[i].Item); Assert.Equal(rows[i].Id, snapshot.Points[i].StableId);
            Assert.Equal(i, snapshot.Points[i].SourceIndex); Assert.Equal(rows[i].Radius, snapshot.Points[i].Value);
        }
        Assert.Equal(Colors.Red, snapshot.Points[0].ColorValue); Assert.Same(brush, snapshot.Points[1].ColorValue);
        var scene = PolarChartGeometry.Build([(series, snapshot)], default, 100, radiusMaximum: 100);
        Assert.False(scene.Points[0].IsVisible); Assert.True(scene.Points[1].IsVisible);
        Assert.Same(brush, scene.Points[1].Fill);
        Assert.Equal(Colors.Blue, Assert.IsAssignableFrom<ISolidColorBrush>(scene.Points[2].Fill).Color);
    }

    [AvaloniaFact]
    public void Live_item_angle_radius_color_and_identity_changes_invalidate_the_snapshot()
    {
        var row = new Row("a", 0d, 25, "Red"); var series = Configure(new PolarLineSeries(), new[] { row });
        var changes = 0; EventHandler owner = (_, _) => changes++; series.Changed += owner;
        var snapshot = series.CreateSnapshot(); var revision = series.SourceVersion;
        row.Angle = 90d; row.Radius = 75; row.Tone = Colors.Blue; row.Id = "updated";
        var updated = series.CreateSnapshot();
        Assert.True(series.SourceVersion >= revision + 4); Assert.True(changes >= 4);
        Assert.NotSame(snapshot, updated); var point = Assert.Single(updated.Points);
        Assert.Equal(new ChartPoint(90, 75), point.Point); Assert.Equal("updated", point.StableId); Assert.Equal(Colors.Blue, point.ColorValue);
        series.Changed -= owner; Assert.Equal(0, row.ObserverCount);
    }

    [AvaloniaFact]
    public void Unowned_sources_reenumerate_without_keeping_collection_or_property_subscriptions()
    {
        var row = new Row("a", 0d, 25, null); var rows = new ObservableCollection<Row> { row };
        var series = Configure(new RadarSeries(), rows); Assert.Equal(0, row.ObserverCount);
        Assert.Single(series.CreateSnapshot().Points); Assert.Equal(0, row.ObserverCount);
        row.Radius = 80; rows.Add(new Row("b", "next", 50, null));
        var snapshot = series.CreateSnapshot(); Assert.Equal(2, snapshot.Points.Count); Assert.Equal(80, snapshot.Points[0].Point.Y);
        EventHandler owner = (_, _) => { }; series.Changed += owner;
        Assert.True(row.ObserverCount > 0); series.Changed -= owner; Assert.Equal(0, row.ObserverCount);
    }

    [AvaloniaFact]
    public void Collection_moves_replacements_and_removal_keep_source_indices_and_stable_ids_current()
    {
        var a = new Row("a", "A", 10, null); var b = new Row("b", "B", 20, null);
        var rows = new ObservableCollection<Row> { a, b }; var series = Configure(new RadarSeries(), rows);
        EventHandler owner = (_, _) => { }; series.Changed += owner;
        rows.Move(1, 0); var moved = series.CreateSnapshot();
        Assert.Equal(new[] { "b", "a" }, moved.Points.Select(p => p.StableId));
        Assert.Equal(new[] { 0, 1 }, moved.Points.Select(p => p.SourceIndex));
        rows[0] = new Row("c", "C", 30, null); Assert.Equal(0, b.ObserverCount);
        Assert.Equal("c", series.CreateSnapshot().Points[0].StableId);
        rows.RemoveAt(1); Assert.Equal(0, a.ObserverCount); Assert.Single(series.CreateSnapshot().Points);
        series.Changed -= owner; Assert.Equal(0, rows[0].ObserverCount);
    }

    [AvaloniaFact]
    public void Nested_mapping_replaces_observed_owners_and_releases_old_color_and_coordinate_sources()
    {
        var old = new Row("a", 30d, 25, "Red"); var current = new Row("b", 90d, 75, "Blue");
        var container = new Container(old);
        var series = new PolarScatterSeries { ItemsSource = new[] { container }, AnglePath = "Value.Angle", RadiusPath = "Value.Radius", IdPath = "Value.Id", ColorPath = "Value.Tone" };
        EventHandler owner = (_, _) => { }; series.Changed += owner;
        Assert.True(old.ObserverCount > 0); container.Value = current;
        Assert.Equal(0, old.ObserverCount); Assert.True(current.ObserverCount > 0);
        var point = Assert.Single(series.CreateSnapshot().Points); Assert.Equal(new ChartPoint(90, 75), point.Point); Assert.Equal("Blue", point.ColorValue);
        var version = series.SourceVersion; old.Radius = 100; Assert.Equal(version, series.SourceVersion);
        series.Changed -= owner; Assert.Equal(0, current.ObserverCount); Assert.Equal(0, container.ObserverCount);
    }

    [AvaloniaFact]
    public void Adding_color_mapping_materializes_a_compact_direct_point_source()
    {
        var source = new[] { new ChartPoint(30, 25), new ChartPoint(60, 50) };
        var series = new PolarScatterSeries { ItemsSource = source };
        EventHandler owner = (_, _) => { }; series.Changed += owner;
        Assert.Null(series.CreateSnapshot().Points[0].ColorValue);
        series.ColorPath = "X";
        var snapshot = series.CreateSnapshot(); Assert.Equal(30d, snapshot.Points[0].ColorValue); Assert.Equal(60d, snapshot.Points[1].ColorValue);
        Assert.Equal(source[0], snapshot.Points[0].Point);
        series.Changed -= owner;
    }

    [AvaloniaFact]
    public void Changing_alias_paths_and_color_path_refreshes_nested_property_observers()
    {
        var rows = new[] { new { Theta = 90d, Amount = 75d, Shade = Colors.Orange } };
        var series = new PolarScatterSeries { ItemsSource = rows };
        EventHandler owner = (_, _) => { }; series.Changed += owner; var before = series.SourceVersion;
        series.AnglePath = "Theta"; series.RadiusPath = "Amount"; series.ColorPath = "Shade";
        var point = Assert.Single(series.CreateSnapshot().Points);
        Assert.True(series.SourceVersion >= before + 3); Assert.Equal(new ChartPoint(90, 75), point.Point); Assert.Equal(Colors.Orange, point.ColorValue);
        series.Changed -= owner;
    }

    [AvaloniaFact]
    public void Version_checked_snapshot_publication_preserves_source_identity_and_rejects_stale_results()
    {
        var rows = new[] { new Row("a", 0d, 25, "Red") }; var series = Configure(new PolarLineSeries(), rows);
        EventHandler owner = (_, _) => { }; series.Changed += owner;
        var original = series.CreateSnapshot(); var point = original.Points[0];
        var computed = new ChartDataSnapshot(original.Version, new[] { point with { Point = new ChartPoint(90, 50) } });
        Assert.True(series.TryPublishSnapshot(computed, original.Version));
        Assert.Equal(new ChartPoint(90, 50), Assert.Single(series.CreateSnapshot().Points).Point);
        rows[0].Radius = 75; Assert.False(series.TryPublishSnapshot(computed, original.Version));
        Assert.Equal(75, Assert.Single(series.CreateSnapshot().Points).Point.Y);
        series.Changed -= owner; Assert.False(series.TryPublishSnapshot(series.CreateSnapshot(), series.SourceVersion));
    }

    private static T Configure<T>(T series, System.Collections.IEnumerable rows) where T : PolarSeries
    { series.ItemsSource = rows; series.AnglePath = "Angle"; series.RadiusPath = "Radius"; series.IdPath = "Id"; series.ColorPath = "Tone"; return series; }
    private sealed record Key(string Name);
    private sealed class Row(string id, object? angle, double radius, object? tone) : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _changed;
        public int ObserverCount { get; private set; }
        public event PropertyChangedEventHandler? PropertyChanged
        { add { _changed += value; ObserverCount++; } remove { _changed -= value; ObserverCount--; } }
        public string Id { get => id; set { id = value; _changed?.Invoke(this, new(nameof(Id))); } }
        public object? Angle { get => angle; set { angle = value; _changed?.Invoke(this, new(nameof(Angle))); } }
        public double Radius { get => radius; set { radius = value; _changed?.Invoke(this, new(nameof(Radius))); } }
        public object? Tone { get => tone; set { tone = value; _changed?.Invoke(this, new(nameof(Tone))); } }
    }
    private sealed class Container(Row value) : INotifyPropertyChanged
    {
        private Row _value = value;
        private PropertyChangedEventHandler? _changed;
        public int ObserverCount { get; private set; }
        public event PropertyChangedEventHandler? PropertyChanged
        { add { _changed += value; ObserverCount++; } remove { _changed -= value; ObserverCount--; } }
        public Row Value { get => _value; set { _value = value; _changed?.Invoke(this, new(nameof(Value))); } }
    }
}
