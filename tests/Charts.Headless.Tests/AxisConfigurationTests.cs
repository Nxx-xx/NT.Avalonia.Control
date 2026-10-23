using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class AxisConfigurationTests
{
    [AvaloniaFact]
    public void Axis_and_feature_styles_are_presentation_changes_and_keep_the_transform()
    {
        var axis = new NumericAxis { Minimum = 0, Maximum = 10 };
        var gap = new ChartScaleBreak { Start = 3, End = 5 };
        var band = new ChartPlotBand();
        axis.ScaleBreaks.Add(gap);
        axis.PlotBands.Add(band);
        var before = ChartScale.CreateForView(axis, 0, 10).Normalize(2);
        var changes = new List<ChartAxisChangedEventArgs>();
        EventHandler<ChartAxisChangedEventArgs> observer = (_, args) => changes.Add(args);
        axis.Changed += observer;
        try
        {
            axis.IsVisible = false;
            axis.AxisDashStyle = DashStyle.Dash;
            axis.GridLineDashStyle = DashStyle.Dot;
            axis.MinorGridLineDashStyle = DashStyle.DashDot;
            axis.AxisLineCap = PenLineCap.Round;
            axis.GridLineCap = PenLineCap.Square;
            axis.MinorGridLineCap = PenLineCap.Round;
            axis.AxisLineJoin = PenLineJoin.Round;
            axis.GridLineJoin = PenLineJoin.Bevel;
            axis.MinorGridLineJoin = PenLineJoin.Round;
            gap.Stroke = Brushes.Red;
            gap.StrokeThickness = 3;
            band.IsVisible = false;
            band.Opacity = .4;
            band.HorizontalTextAlignment = ChartBandTextAlignment.Start;
            band.VerticalTextAlignment = ChartBandTextAlignment.End;
            band.RepeatUntil = 30;

            Assert.NotEmpty(changes);
            Assert.All(changes, change => Assert.False(change.RangeChanged));
            Assert.Equal(before, ChartScale.CreateForView(axis, 0, 10).Normalize(2));
            Assert.Equal(0, axis.Minimum);
            Assert.Equal(10, axis.Maximum);
            changes.Clear();
            gap.End = 6;
            Assert.True(Assert.Single(changes).RangeChanged);
        }
        finally { axis.Changed -= observer; }
    }

    [AvaloniaFact]
    public void Bound_feature_sources_override_inline_items_and_observe_collection_and_child_changes()
    {
        var axis = new NumericAxis { DataContext = "model" };
        var inlineGap = new ChartScaleBreak { Start = 1, End = 2 };
        var inlineBand = new ChartPlotBand();
        axis.ScaleBreaks.Add(inlineGap);
        axis.PlotBands.Add(inlineBand);
        var firstGap = new ChartScaleBreak { Start = 4, End = 6 };
        var firstBand = new ChartPlotBand { Start = 7, End = 8 };
        var gaps = new TrackedCollection<ChartScaleBreak>(firstGap);
        var bands = new TrackedCollection<ChartPlotBand>(firstBand);
        axis.ScaleBreaksSource = gaps;
        axis.PlotBandsSource = bands;
        Assert.Null(((ILogical)inlineGap).LogicalParent);
        Assert.Null(((ILogical)inlineBand).LogicalParent);
        Assert.Equal("model", firstGap.DataContext);
        Assert.Equal("model", firstBand.DataContext);
        Assert.Equal(0, gaps.Subscribers);
        Assert.Equal(0, bands.Subscribers);

        var changes = new List<ChartAxisChangedEventArgs>();
        EventHandler<ChartAxisChangedEventArgs> observer = (_, args) => changes.Add(args);
        axis.Changed += observer;
        try
        {
            Assert.Equal(1, gaps.Subscribers);
            Assert.Equal(1, bands.Subscribers);
            Assert.False(axis.IsInScaleBreak(1.5));
            Assert.True(axis.IsInScaleBreak(5));
            var addedGap = new ChartScaleBreak { Start = 8, End = 9 };
            var addedBand = new ChartPlotBand();
            gaps.Add(addedGap);
            bands.Add(addedBand);
            Assert.Same(axis, ((ILogical)addedGap).LogicalParent);
            Assert.Same(axis, ((ILogical)addedBand).LogicalParent);
            Assert.Equal("model", addedBand.DataContext);
            Assert.True(axis.IsInScaleBreak(8.5));
            Assert.Contains(changes, change => change.RangeChanged);
            changes.Clear();
            addedGap.StrokeThickness = 2;
            addedBand.Opacity = .5;
            Assert.All(changes, change => Assert.False(change.RangeChanged));
            changes.Clear();
            addedGap.End = 10;
            Assert.True(Assert.Single(changes).RangeChanged);

            axis.ScaleBreaksSource = Array.Empty<ChartScaleBreak>();
            axis.PlotBandsSource = Array.Empty<ChartPlotBand>();
            Assert.Empty(axis.ActiveScaleBreaks);
            Assert.Empty(axis.ActivePlotBands);
            Assert.Null(((ILogical)firstGap).LogicalParent);
            Assert.Null(((ILogical)addedBand).LogicalParent);
            Assert.Equal(0, gaps.Subscribers);
            Assert.Equal(0, bands.Subscribers);
            changes.Clear();
            firstGap.End = 7;
            firstBand.Text = "detached";
            gaps.Add(new ChartScaleBreak { Start = 20, End = 21 });
            bands.Add(new ChartPlotBand());
            Assert.Empty(changes);

            axis.ScaleBreaksSource = null;
            axis.PlotBandsSource = null;
            Assert.Same(inlineGap, Assert.Single(axis.ActiveScaleBreaks));
            Assert.Same(inlineBand, Assert.Single(axis.ActivePlotBands));
            Assert.Same(axis, ((ILogical)inlineGap).LogicalParent);
            Assert.Same(axis, ((ILogical)inlineBand).LogicalParent);
            Assert.True(axis.IsInScaleBreak(1.5));
        }
        finally { axis.Changed -= observer; }
        Assert.Equal(0, gaps.Subscribers);
        Assert.Equal(0, bands.Subscribers);
    }

    [AvaloniaFact]
    public void Reattaching_an_owner_resynchronizes_source_items_changed_while_unobserved()
    {
        var oldGap = new ChartScaleBreak { Start = 1, End = 2 };
        var oldBand = new ChartPlotBand();
        var gaps = new TrackedCollection<ChartScaleBreak>(oldGap);
        var bands = new TrackedCollection<ChartPlotBand>(oldBand);
        var axis = new NumericAxis { ScaleBreaksSource = gaps, PlotBandsSource = bands, DataContext = "model" };
        var calls = 0;
        EventHandler<ChartAxisChangedEventArgs> observer = (_, _) => calls++;
        axis.Changed += observer;
        axis.Changed -= observer;
        Assert.Equal(0, gaps.Subscribers);
        Assert.Equal(0, bands.Subscribers);
        var newGap = new ChartScaleBreak { Start = 4, End = 6 };
        var newBand = new ChartPlotBand();
        gaps[0] = newGap;
        bands[0] = newBand;
        oldGap.End = 3;
        oldBand.Text = "not observed";
        Assert.Equal(0, calls);

        axis.Changed += observer;
        try
        {
            Assert.Same(axis, ((ILogical)newGap).LogicalParent);
            Assert.Same(axis, ((ILogical)newBand).LogicalParent);
            Assert.Null(((ILogical)oldGap).LogicalParent);
            Assert.Null(((ILogical)oldBand).LogicalParent);
            Assert.Equal("model", newGap.DataContext);
            Assert.True(axis.IsInScaleBreak(5));
            Assert.False(axis.IsInScaleBreak(1.5));
            calls = 0;
            oldGap.End = 3.5;
            oldBand.Text = "still detached";
            Assert.Equal(0, calls);
            newGap.End = 7;
            Assert.Equal(1, calls);
        }
        finally { axis.Changed -= observer; }
        Assert.Equal(0, gaps.Subscribers);
        Assert.Equal(0, bands.Subscribers);
    }

    [AvaloniaFact]
    public void Feature_source_bindings_follow_data_context_replacement_and_children_inherit_it()
    {
        var first = new FeatureModel("first");
        var second = new FeatureModel("second");
        var axis = new NumericAxis { DataContext = first };
        using var breakBinding = axis.Bind(ChartAxis.ScaleBreaksSourceProperty, new Binding(nameof(FeatureModel.Breaks)));
        using var bandBinding = axis.Bind(ChartAxis.PlotBandsSourceProperty, new Binding(nameof(FeatureModel.Bands)));
        using var textBinding = first.Bands[0].Bind(ChartPlotBand.TextProperty, new Binding(nameof(FeatureModel.Name)));
        Assert.Same(first.Breaks[0], Assert.Single(axis.ActiveScaleBreaks));
        Assert.Same(first, first.Bands[0].DataContext);
        Assert.Equal("first", first.Bands[0].Text);

        axis.DataContext = second;
        Assert.Same(second.Breaks[0], Assert.Single(axis.ActiveScaleBreaks));
        Assert.Same(second.Bands[0], Assert.Single(axis.ActivePlotBands));
        Assert.Same(second, second.Bands[0].DataContext);
        Assert.Null(((ILogical)first.Bands[0]).LogicalParent);
    }

    [AvaloniaFact]
    public void Band_full_range_defaults_and_style_validation_are_explicit()
    {
        var axis = new NumericAxis();
        var band = new ChartPlotBand();
        Assert.True(axis.IsVisible);
        Assert.True(band.IsVisible);
        Assert.True(double.IsNaN(band.Start));
        Assert.True(double.IsNaN(band.End));
        Assert.Equal(1, band.Opacity);
        Assert.Equal(ChartBandTextAlignment.Center, band.HorizontalTextAlignment);
        Assert.Equal(ChartBandTextAlignment.Center, band.VerticalTextAlignment);
        Assert.Null(band.RepeatUntil);
        Assert.Equal(1, band.RepeatCount);
        Assert.Throws<ArgumentException>(() => band.Start = double.NegativeInfinity);
        Assert.Throws<ArgumentException>(() => band.End = double.PositiveInfinity);
        Assert.Throws<ArgumentException>(() => band.Opacity = double.NaN);
        Assert.Throws<ArgumentException>(() => band.Opacity = 1.1);
        Assert.Throws<ArgumentException>(() => band.RepeatUntil = double.NaN);
        Assert.Throws<ArgumentException>(() => new ChartScaleBreak().StrokeThickness = -1);
        Assert.Throws<ArgumentException>(() => axis.AxisLineCap = (PenLineCap)100);
        Assert.Throws<ArgumentException>(() => band.VerticalTextAlignment = (ChartBandTextAlignment)100);
    }

    private sealed class FeatureModel(string name)
    {
        public string Name { get; } = name;
        public ObservableCollection<ChartScaleBreak> Breaks { get; } = [new() { Start = 1, End = 2 }];
        public ObservableCollection<ChartPlotBand> Bands { get; } = [new()];
    }

    private sealed class TrackedCollection<T>(params T[] items) : Collection<T>(items.ToList()), INotifyCollectionChanged
    {
        private NotifyCollectionChangedEventHandler? _changed;
        public int Subscribers { get; private set; }
        public event NotifyCollectionChangedEventHandler? CollectionChanged
        {
            add { _changed += value; Subscribers++; }
            remove { _changed -= value; Subscribers--; }
        }
        protected override void InsertItem(int index, T item)
        { base.InsertItem(index, item); _changed?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index)); }
        protected override void SetItem(int index, T item)
        { var old = this[index]; base.SetItem(index, item); _changed?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, item, old, index)); }
    }
}
