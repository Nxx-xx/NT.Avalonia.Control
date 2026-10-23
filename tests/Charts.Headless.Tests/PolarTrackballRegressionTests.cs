using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

/// <summary>New page layout must preserve the existing Trackball domain contract, including true subpixel misses.</summary>
public sealed class PolarTrackballRegressionTests
{
    [AvaloniaTheory]
    [InlineData(-.25, ChartTrackballDisplayMode.GroupAllPoints)]
    [InlineData(.25, ChartTrackballDisplayMode.GroupAllPoints)]
    [InlineData(-.25, ChartTrackballDisplayMode.FloatAllPoints)]
    [InlineData(.25, ChartTrackballDisplayMode.FloatAllPoints)]
    public void A_quarter_DIP_beyond_the_secondary_minimum_does_not_extrapolate_or_invent_a_card(
        double screenOffset, ChartTrackballDisplayMode displayMode)
    {
        var primary = LineInterpolationTests.CreateLine(PrimaryRows());
        var secondary = LineInterpolationTests.CreateLine(SecondaryRows());
        using var host = new SegmentTestHost(new(0, 10), new(0, 100), primary, secondary);
        Configure(host.Chart, displayMode);
        var query = host.Project(new(3, 40)) + new Vector(0, screenOffset);
        var rawY = 40 - screenOffset * 100 / host.Chart.PlotArea.Height;
        var insideSecondary = screenOffset < 0;
        host.Move(query);

        Assert.Equal(insideSecondary ? 2 : 1, host.Chart.TrackballReadings.Count);
        var primaryReading = Assert.Single(host.Chart.TrackballReadings, r => ReferenceEquals(r.Series, primary));
        AssertInterpolated(primaryReading, primary, new ChartPoint(1 + (rawY - 20) / 10, rawY));
        if (insideSecondary)
        {
            var secondaryReading = Assert.Single(host.Chart.TrackballReadings, r => ReferenceEquals(r.Series, secondary));
            AssertInterpolated(secondaryReading, secondary, new ChartPoint((80 - rawY) / 10, rawY));
        }
        else
        {
            Assert.DoesNotContain(host.Chart.TrackballReadings, r => ReferenceEquals(r.Series, secondary));
            Assert.DoesNotContain(host.Chart.HoveredPoints, p => ReferenceEquals(p.Series, secondary));
            Assert.DoesNotContain(host.Chart.HoveredSegments, p => ReferenceEquals(p.Series, secondary));
        }
        AssertCards(host.Chart, displayMode, insideSecondary ? 2 : 1);
    }

    [AvaloniaTheory]
    [InlineData(ChartTrackballDisplayMode.GroupAllPoints)]
    [InlineData(ChartTrackballDisplayMode.FloatAllPoints)]
    public void Interior_horizontal_readings_keep_both_original_endpoint_pairs_and_business_cards(
        ChartTrackballDisplayMode displayMode)
    {
        var primary = LineInterpolationTests.CreateLine(PrimaryRows());
        var secondary = LineInterpolationTests.CreateLine(SecondaryRows());
        using var host = new SegmentTestHost(new(0, 10), new(0, 100), primary, secondary);
        Configure(host.Chart, displayMode);
        host.Move(host.Project(new(3, 45)));

        Assert.Equal(2, host.Chart.TrackballReadings.Count);
        var primaryReading = Assert.Single(host.Chart.TrackballReadings, r => ReferenceEquals(r.Series, primary));
        var secondaryReading = Assert.Single(host.Chart.TrackballReadings, r => ReferenceEquals(r.Series, secondary));
        AssertInterpolated(primaryReading, primary, new ChartPoint(3.5, 45));
        AssertInterpolated(secondaryReading, secondary, new ChartPoint(3.5, 45));
        Assert.NotSame(primaryReading.Original.Item, secondaryReading.Original.Item);
        AssertCards(host.Chart, displayMode, 2);
    }

    [AvaloniaTheory]
    [InlineData(40, 2)]
    [InlineData(45, 1)]
    public void An_explicit_secondary_axis_uses_its_own_domain_at_the_same_screen_coordinate(
        double primaryY, int expectedReadings)
    {
        var primary = LineInterpolationTests.CreateLine(PrimaryRows());
        var secondary = LineInterpolationTests.CreateLine(SecondaryRows());
        using var host = new SegmentTestHost(new(0, 10), new(0, 100), primary, secondary);
        var extraAxis = new NumericAxis { Key = "secondary-200", Minimum = 0, Maximum = 200, Position = ChartAxisPosition.Far };
        host.Chart.Axes.Add(extraAxis); secondary.YAxisKey = extraAxis.Key;
        Configure(host.Chart, ChartTrackballDisplayMode.FloatAllPoints); host.Drain();
        host.Move(host.Project(new(3, primaryY)));

        Assert.Equal(expectedReadings, host.Chart.TrackballReadings.Count);
        AssertInterpolated(Assert.Single(host.Chart.TrackballReadings, r => ReferenceEquals(r.Series, primary)), primary,
            new ChartPoint(1 + (primaryY - 20) / 10, primaryY));
        if (primaryY == 40)
        {
            var reading = Assert.Single(host.Chart.TrackballReadings, r => ReferenceEquals(r.Series, secondary));
            Assert.False(reading.IsInterpolated); SegmentTestHost.AssertPoint(new(0, 80), reading.Point);
            Assert.Same(((SegmentRow[])secondary.ItemsSource!)[0], reading.Original.Item);
            Assert.Equal("track-b0", reading.Original.StableId);
        }
        else Assert.DoesNotContain(host.Chart.TrackballReadings, r => ReferenceEquals(r.Series, secondary));
        AssertCards(host.Chart, ChartTrackballDisplayMode.FloatAllPoints, expectedReadings);
    }

    private static void Configure(CartesianChart chart, ChartTrackballDisplayMode displayMode)
    {
        chart.IsTooltipEnabled = false;
        chart.TrackballMode = ChartTrackballMode.Horizontal;
        chart.TrackballSnapMode = ChartTrackballSnapMode.LinearInterpolation;
        chart.TrackballDisplayMode = displayMode;
        chart.TrackballContentTemplate = new FuncDataTemplate<ChartTooltipContext>((context, _) => new Border
        {
            Name = "DomainRegressionCard", MinWidth = 80, MinHeight = 24,
            Child = new TextBlock { Text = string.Join(",", context!.Readings.Select(r => r.Original.StableId)) },
        });
    }
    private static void AssertInterpolated(ChartTrackballReading reading, LineSeries series, ChartPoint expected)
    {
        Assert.True(reading.IsInterpolated); SegmentTestHost.AssertPoint(expected, reading.Point);
        var segment = Assert.IsType<ChartSegmentHit>(reading.Segment);
        var rows = (SegmentRow[])series.ItemsSource!;
        Assert.Same(rows[0], segment.Start.Item); Assert.Same(rows[1], segment.End.Item);
        Assert.Equal(rows[0].Id, segment.Start.StableId); Assert.Equal(rows[1].Id, segment.End.StableId);
        Assert.Equal(0, segment.Start.Index); Assert.Equal(1, segment.End.Index);
        Assert.Contains(reading.Original.Item, rows); Assert.NotEqual(reading.Original.Point, reading.Point);
    }
    private static void AssertCards(CartesianChart chart, ChartTrackballDisplayMode mode, int readingCount)
    {
        var cards = chart.GetVisualDescendants().OfType<Border>().Where(c => c.Name == "DomainRegressionCard").ToArray();
        Assert.Equal(mode == ChartTrackballDisplayMode.GroupAllPoints ? 1 : readingCount, cards.Length);
        foreach (var card in cards)
        {
            Assert.True(card.Bounds.Width >= 80); Assert.True(card.Bounds.Height >= 24);
            var context = Assert.IsType<ChartTooltipContext>(card.DataContext);
            Assert.Equal(mode == ChartTrackballDisplayMode.GroupAllPoints ? readingCount : 1, context.Readings.Count);
            Assert.All(context.Readings, r => Assert.Contains(r, chart.TrackballReadings));
        }
    }
    private static SegmentRow[] PrimaryRows() => [new("track-a0", 1, 20), new("track-a1", 5, 60), new("track-a2", 9, 35)];
    private static SegmentRow[] SecondaryRows() => [new("track-b0", 0, 80), new("track-b1", 4, 40), new("track-b2", 8, 70)];
}
