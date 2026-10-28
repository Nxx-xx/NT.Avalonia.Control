using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class ProfessionalTrackballTests(ITestOutputHelper output)
{
    [AvaloniaFact]
    public void Professional_modes_and_appearance_share_Cartesian_properties_with_a_distinct_original_glyph_context()
    {
        Assert.Same(CartesianChart.TrackballModeProperty, EngineeringChartBase.TrackballModeProperty);
        Assert.Same(CartesianChart.TrackballModeProperty, AnalysisChartBase.TrackballModeProperty);
        Assert.Same(CartesianChart.TrackballContentTemplateProperty, AnalysisChartBase.TrackballContentTemplateProperty);
        Assert.Same(EngineeringChartBase.MaxTrackballReadingsProperty, AnalysisChartBase.MaxTrackballReadingsProperty);
        Assert.Same(EngineeringChartBase.TrackballFormatterProperty, AnalysisChartBase.TrackballFormatterProperty);
        var chart = new EngineeringProbe(); Assert.Equal(ChartTrackballMode.None, chart.TrackballMode); Assert.Equal(32, chart.MaxTrackballReadings);
        Assert.Throws<ArgumentException>(() => chart.MaxTrackballReadings = -1);
        Assert.Throws<ArgumentException>(() => chart.MaxTrackballReadings = 1025);
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void Vertical_horizontal_and_equal_distance_slices_return_all_real_visible_glyphs_in_paint_order(bool engineering)
    {
        var rows = Rows(); var chart = Create(engineering, rows); using var host = new Host(chart);
        var plot = Plot(chart); var business = new object(); chart.DataContext = business;
        Mode(chart, ChartTrackballMode.Vertical); Update(chart, new(plot.Left + 80, plot.Top + 58)); host.Drain();
        Assert.Equal(new[] { "r0", "r1" }, Readings(chart).Select(reading => reading.Members[0].StableId));
        Assert.All(Readings(chart), reading => { Assert.Same(business, reading.BusinessDataContext); Assert.Same(rows[reading.Members[0].SourceIndex], reading.Members[0].Item); Assert.True(reading.Bounds.Intersects(plot)); });
        if (engineering) { Assert.Equal(new[] { 0, 3 }, Readings(chart)[0].Members.Select(member => member.SourceIndex)); Assert.NotNull(Readings(chart)[0].EngineeringHit); Assert.Null(Readings(chart)[0].PointHit); }
        else { Assert.Single(Readings(chart)[0].Members); Assert.NotNull(Readings(chart)[0].PointHit); Assert.Null(Readings(chart)[0].EngineeringHit); }
        Mode(chart, ChartTrackballMode.Horizontal); Update(chart, new(plot.Left + 80, plot.Top + 58)); host.Drain();
        Assert.Equal(new[] { "r0", "r2" }, Readings(chart).Select(reading => reading.Members[0].StableId));
        Mode(chart, ChartTrackballMode.Vertical); Update(chart, new(plot.Left + 150, plot.Top + 58)); host.Drain();
        Assert.Equal(new[] { "r0", "r1", "r2" }, Readings(chart).Select(reading => reading.Members[0].StableId));
        Update(chart, new(-1, -1)); Assert.Empty(Readings(chart));
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void Budgets_disclose_total_and_truncation_without_copying_or_truncating_aggregate_members(bool engineering)
    {
        var chart = Create(engineering, Rows()); Template(chart, SimpleTemplate()); using var host = new Host(chart);
        Mode(chart, ChartTrackballMode.Vertical); Limit(chart, 1); Update(chart, Anchor(chart)); host.Drain();
        Assert.Single(Readings(chart)); Assert.Equal(2, Total(chart));
        var context = Assert.IsType<ChartDataTrackballContext>(Assert.Single(Cards(chart)).DataContext);
        Assert.Equal(2, context.TotalReadings); Assert.True(context.IsTruncated); Assert.Single(context.Readings);
        if (engineering) Assert.Equal(2, context.Readings[0].Members.Count);
        Limit(chart, 0); host.Drain(); Assert.Empty(Readings(chart)); Assert.Equal(2, Total(chart)); Assert.Empty(Cards(chart));
        Limit(chart, 4); host.Drain(); Assert.Equal(2, Readings(chart).Count); Assert.False(Assert.IsType<ChartDataTrackballContext>(Assert.Single(Cards(chart)).DataContext).IsTruncated);
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void Group_and_float_templates_are_real_passive_controls_and_reuse_on_pointer_business_and_style_changes(bool engineering)
    {
        var source = new CountingRows(Rows()); var chart = Create(engineering, source); var model = new Business(); chart.DataContext = model;
        var builds = 0; Template(chart, new FuncDataTemplate<ChartDataTrackballContext>((_, _) =>
        { builds++; var text = new TextBlock(); text.Bind(TextBlock.TextProperty, new Binding("BusinessDataContext.Title")); return text; }));
        using var host = new Host(chart); Mode(chart, ChartTrackballMode.Vertical); Update(chart, Anchor(chart)); host.Drain();
        var group = Assert.Single(Cards(chart)); var child = group.Child; var count = builds; var reads = source.Enumerations;
        Update(chart, Anchor(chart) + new Vector(1, 1)); host.Drain(); model.Title = "Updated business"; host.Drain();
        Assert.Same(child, Assert.Single(Cards(chart)).Child); Assert.Equal(count, builds); Assert.Equal(reads, source.Enumerations);
        Assert.Equal("Updated business", Assert.IsType<TextBlock>(child).Text);
        foreach (var overlay in chart.GetVisualChildren().OfType<Control>().Where(control => control.Name is "PART_ChartDataTrackballOverlay" or "PART_ChartDataTrackballLines"))
        { Assert.False(overlay.IsHitTestVisible); Assert.False(overlay.Focusable); Assert.Same(chart, ((ILogical)overlay).LogicalParent); Assert.Equal(new Rect(chart.Bounds.Size), overlay.Bounds); }
        Display(chart, ChartTrackballDisplayMode.FloatAllPoints); host.Drain(); Assert.Equal(2, Cards(chart).Length);
        Assert.All(Cards(chart), card => { Assert.Single(Assert.IsType<ChartDataTrackballContext>(card.DataContext).Readings); Assert.True(card.Bounds.Width <= chart.Bounds.Width); Assert.True(card.Bounds.Height <= chart.Bounds.Height); });
        Assert.Equal(reads, source.Enumerations);
        chart.HighContrastMode = ChartHighContrastMode.Dark; host.Drain(); Assert.All(Cards(chart), card => Assert.Same(Brushes.Black, card.Background));
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void Source_mutation_hide_disable_detach_clear_old_contexts_and_restore_only_current_readings(bool engineering)
    {
        var rows = Rows(); var chart = Create(engineering, rows); var container = new Border { Child = chart }; Template(chart, SimpleTemplate());
        using var host = new Host(chart, container); Mode(chart, ChartTrackballMode.Vertical); Update(chart, Anchor(chart)); host.Drain();
        var old = Assert.Single(Cards(chart)).Child!; rows[0] = new("new", "Replacement", 33);
        Assert.Null(old.DataContext); host.Drain(); Assert.Equal("new", Readings(chart)[0].Members[0].StableId);
        old = Assert.Single(Cards(chart)).Child!; Log("before ancestor hide", chart, container); container.IsVisible = false; Log("after ancestor hide immediate", chart, container); Assert.Null(old.DataContext); host.Drain(); Log("after ancestor hide drain", chart, container); Assert.Empty(Cards(chart));
        container.IsVisible = true; Log("after ancestor show immediate", chart, container); host.Drain(); Log("after ancestor show drain", chart, container); Update(chart, Anchor(chart)); host.Drain(); Log("after explicit reading update", chart, container); Assert.NotEmpty(Cards(chart));
        old = Assert.Single(Cards(chart)).Child!; chart.IsEnabled = false; host.Drain(); Assert.Empty(Cards(chart)); Assert.Null(old.DataContext);
        chart.IsEnabled = true; host.Drain(); Update(chart, Anchor(chart)); host.Drain(); old = Assert.Single(Cards(chart)).Child!;
        host.Detach(); Assert.Null(old.DataContext); Assert.Empty(Readings(chart)); Assert.Empty(Cards(chart));
        host.Attach(); Assert.Empty(Readings(chart)); Mode(chart, ChartTrackballMode.None); Update(chart, Anchor(chart)); Assert.Empty(Readings(chart));
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void Null_template_build_hides_cards_and_failed_templates_are_cleaned_and_quarantined_until_replacement(bool engineering)
    {
        var chart = Create(engineering, Rows()); using var host = new Host(chart); Mode(chart, ChartTrackballMode.Vertical);
        Template(chart, new FuncDataTemplate<ChartDataTrackballContext>((_, _) => null)); Update(chart, Anchor(chart)); host.Drain(); Assert.Empty(Cards(chart)); Assert.Equal(2, Readings(chart).Count);
        var attempts = 0; Template(chart, new FuncDataTemplate<ChartDataTrackballContext>((_, _) => { attempts++; throw new InvalidOperationException("Trackball template failed"); }));
        Assert.Throws<InvalidOperationException>(host.Drain); Assert.Empty(Cards(chart)); host.Drain(); Assert.Equal(1, attempts);
        Template(chart, SimpleTemplate()); host.Drain(); Assert.Single(Cards(chart));
        var parent = new Border(); var existing = new TextBlock { Text = "owned", DataContext = new object() }; parent.Child = existing; var context = existing.DataContext;
        Template(chart, new FuncDataTemplate<ChartDataTrackballContext>((_, _) => existing)); Assert.Throws<InvalidOperationException>(host.Drain);
        Assert.Same(existing, parent.Child); Assert.Same(context, existing.DataContext); Template(chart, SimpleTemplate()); host.Drain(); Assert.Single(Cards(chart));
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void Template_and_formatter_reentry_cannot_publish_removed_business_sources(bool engineering)
    {
        var chart = Create(engineering, Rows()); using var host = new Host(chart); Mode(chart, ChartTrackballMode.Vertical);
        var replaced = false; Template(chart, new FuncDataTemplate<ChartDataTrackballContext>((_, _) =>
        { if (!replaced) { replaced = true; Source(chart, new[] { new Row("latest", "Latest", 9) }); } return new TextBlock { Text = "reentrant" }; }));
        Update(chart, Anchor(chart)); host.Drain(); Assert.All(Readings(chart).SelectMany(reading => reading.Members), member => Assert.Equal("latest", member.StableId));
        var called = false; Formatter(chart, readings => { if (!called) { called = true; Clear(chart); } return "stale formatter"; });
        Update(chart, Anchor(chart)); host.Drain(); Assert.Empty(Readings(chart)); Assert.Empty(Cards(chart));
        Formatter(chart, _ => "recovered"); Update(chart, Anchor(chart)); host.Drain(); Assert.NotEmpty(Readings(chart));
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void Formatter_failure_is_quarantined_and_replacement_recovers_without_source_reenumeration(bool engineering)
    {
        var source = new CountingRows(Rows()); var chart = Create(engineering, source); using var host = new Host(chart); Mode(chart, ChartTrackballMode.Vertical);
        var attempts = 0; Formatter(chart, _ => { attempts++; throw new InvalidOperationException("formatter failed"); });
        Assert.Throws<InvalidOperationException>(() => Update(chart, Anchor(chart))); host.Drain(); Assert.Equal(1, attempts); Assert.Empty(Cards(chart));
        Formatter(chart, _ => "Formatter recovered"); Update(chart, Anchor(chart)); host.Drain(); var reads = source.Enumerations; Assert.Single(Cards(chart));
        Update(chart, Anchor(chart)); host.Drain(); Assert.Equal(reads, source.Enumerations);
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void Trackball_is_independent_of_tooltip_enabled_and_pointer_input_clears_on_leave(bool engineering)
    {
        var chart = Create(engineering, Rows()); Tooltip(chart, false); using var host = new Host(chart); Mode(chart, ChartTrackballMode.Vertical);
        host.Move(Anchor(chart)); Assert.NotEmpty(Readings(chart)); Assert.Single(Cards(chart));
        host.Move(new(-5, -5)); Assert.Empty(Readings(chart)); Assert.Empty(Cards(chart));
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void A_valid_formatter_survives_template_failure_and_self_replacing_throwing_formatter_does_not_poison_its_replacement(bool engineering)
    {
        var chart = Create(engineering, Rows()); using var host = new Host(chart); Mode(chart, ChartTrackballMode.Vertical);
        Formatter(chart, _ => "valid formatter"); Template(chart, new FuncDataTemplate<ChartDataTrackballContext>((_, _) => throw new InvalidOperationException("bad template")));
        Assert.Throws<InvalidOperationException>(() => Update(chart, Anchor(chart))); host.Drain();
        Template(chart, SimpleTemplate()); host.Drain(); Assert.Single(Cards(chart)); Assert.NotEmpty(Readings(chart));
        Formatter(chart, _ => { Formatter(chart, _ => "replacement formatter"); throw new InvalidOperationException("retired formatter"); });
        Assert.Throws<InvalidOperationException>(() => Update(chart, Anchor(chart))); host.Drain();
        Assert.Equal("replacement formatter", Assert.IsType<ChartDataTrackballContext>(Assert.Single(Cards(chart)).DataContext).Text);
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void Measured_float_cards_avoid_overlap_when_space_exists_and_follow_live_theme_variant(bool engineering)
    {
        var chart = Create(engineering, Rows()); var scope = new ThemeVariantScope { Child = chart, RequestedThemeVariant = ThemeVariant.Light };
        Template(chart, new FuncDataTemplate<ChartDataTrackballContext>((_, _) => new TextBlock { Text = "Tall float card", Width = 120, Height = 110 }));
        using var host = new Host(chart, scope); Mode(chart, ChartTrackballMode.Vertical); Display(chart, ChartTrackballDisplayMode.FloatAllPoints);
        Update(chart, Anchor(chart)); host.Drain(); var cards = Cards(chart); Assert.Equal(2, cards.Length); Assert.False(cards[0].Bounds.Intersects(cards[1].Bounds));
        var light = Assert.Single(Cards(chart).Select(card => card.Background).Distinct());
        scope.RequestedThemeVariant = ThemeVariant.Dark; host.Drain(); Assert.All(Cards(chart), card => Assert.Same(TooltipBackground(chart) ?? Brushes.Black, card.Background));
        var dark = Assert.Single(Cards(chart).Select(card => card.Background).Distinct()); Assert.NotEqual(light, dark);
        scope.RequestedThemeVariant = ThemeVariant.Light; host.Drain(); Assert.All(Cards(chart), card => Assert.Same(TooltipBackground(chart) ?? Brushes.White, card.Background));
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void A_stale_unparented_build_result_releases_its_explicit_old_reading_context(bool engineering)
    {
        var chart = Create(engineering, Rows()); chart.DataContext = new Business(); using var host = new Host(chart); Mode(chart, ChartTrackballMode.Vertical);
        TextBlock? abandoned = null; var latest = new Business();
        Template(chart, new FuncDataTemplate<ChartDataTrackballContext>((context, _) =>
        {
            if (abandoned is not null) return new TextBlock { Text = "latest" };
            abandoned = new TextBlock { DataContext = context, Text = "stale" };
            chart.DataContext = latest; Source(chart, new[] { new Row("latest", "Latest", 55) }); return abandoned;
        }));
        Update(chart, Anchor(chart)); host.Drain(); Assert.NotNull(abandoned); Assert.Null(abandoned.DataContext);
        Assert.Null(abandoned.GetVisualParent()); Assert.Null(((ILogical)abandoned).LogicalParent);
        var current = Assert.IsType<ChartDataTrackballContext>(Assert.Single(Cards(chart)).DataContext);
        Assert.Same(latest, current.BusinessDataContext); Assert.Equal("latest", Assert.Single(current.Readings).Members[0].StableId);
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void Null_formatter_output_hides_default_cards_but_custom_templates_still_receive_original_readings(bool engineering)
    {
        var chart = Create(engineering, Rows()); using var host = new Host(chart); Mode(chart, ChartTrackballMode.Vertical);
        Formatter(chart, _ => null); Update(chart, Anchor(chart)); host.Drain(); Assert.NotEmpty(Readings(chart)); Assert.Empty(Cards(chart));
        Assert.Contains(chart.GetVisualChildren().OfType<Control>(), control => control.Name == "PART_ChartDataTrackballLines");
        Template(chart, SimpleTemplate()); host.Drain(); var context = Assert.IsType<ChartDataTrackballContext>(Assert.Single(Cards(chart)).DataContext);
        Assert.Null(context.Text); Assert.Equal(2, context.Readings.Count); Assert.Equal("r0", context.Readings[0].Members[0].StableId);
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void Real_keyboard_navigation_updates_original_readings_and_Escape_clears_the_overlay(bool engineering)
    {
        var rows = Rows(); var chart = Create(engineering, rows); using var host = new Host(chart); Mode(chart, ChartTrackballMode.Horizontal);
        chart.Focus(); host.Key(Key.Home); Assert.Contains(Readings(chart), reading => ReferenceEquals(reading.Members[0].Item, rows[0]));
        host.Key(Key.Right); var reading = Assert.Single(Readings(chart)); Assert.Same(rows[1], reading.Members[0].Item); Assert.Equal("r1", reading.Members[0].StableId);
        Assert.Single(Cards(chart)); host.Key(Key.Escape); Assert.Empty(Readings(chart)); Assert.Empty(Cards(chart));
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void A_real_click_without_a_preceding_pointer_move_reads_and_selects_the_original_glyph(bool engineering)
    {
        var rows = Rows(); var chart = Create(engineering, rows); using var host = new Host(chart); Mode(chart, ChartTrackballMode.Horizontal);
        Assert.Empty(Readings(chart)); host.Click(Box(2, Plot(chart)).Center); Assert.Contains(Readings(chart), reading => ReferenceEquals(reading.Members[0].Item, rows[2]));
        if (chart is EngineeringChartBase e) Assert.Same(rows[2], Assert.Single(e.SelectedPoint!.Members).Item);
        else Assert.Same(rows[2], ((AnalysisChartBase)chart).SelectedPoint!.Item);
        Assert.NotEmpty(Cards(chart));
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void A_readings_cleared_notification_may_request_a_new_reading_without_splitting_the_published_total(bool engineering)
    {
        var chart = Create(engineering, Rows()); using var host = new Host(chart); Mode(chart, ChartTrackballMode.Vertical);
        Update(chart, Anchor(chart)); host.Drain(); Assert.Equal(2, Readings(chart).Count); Assert.Equal(2, Total(chart));
        var requested = false;
        chart.PropertyChanged += (_, change) =>
        {
            if (change.Property.Name != nameof(EngineeringChartBase.TrackballReadings) || requested || Readings(chart).Count != 0) return;
            requested = true; Update(chart, Anchor(chart));
        };
        Clear(chart); host.Drain(); Log("after reading-cleared callback requests new pointer", chart, chart); Assert.True(requested); Assert.Equal(2, Readings(chart).Count); Assert.Equal(2, Total(chart));
        var context = Assert.IsType<ChartDataTrackballContext>(Assert.Single(Cards(chart)).DataContext);
        Assert.Equal(Total(chart), context.TotalReadings); Assert.Equal(Readings(chart), context.Readings);
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void A_readings_cleared_listener_can_replace_mode_source_and_business_context_without_restoring_old_identity(bool engineering)
    {
        var oldRows = Rows(); var chart = Create(engineering, oldRows); chart.DataContext = new Business(); using var host = new Host(chart);
        Mode(chart, ChartTrackballMode.Vertical); Update(chart, Anchor(chart)); host.Drain();
        var rows = new[] { new Row("new0", "New first", 101), new Row("new1", "New second", 102), new Row("new2", "New third", 103) };
        var latestBusiness = new Business(); var requested = false;
        chart.PropertyChanged += (_, change) =>
        {
            if (change.Property.Name != nameof(EngineeringChartBase.TrackballReadings) || requested || Readings(chart).Count != 0) return;
            requested = true; Mode(chart, ChartTrackballMode.Horizontal); Source(chart, rows); chart.DataContext = latestBusiness; Update(chart, Anchor(chart));
        };
        Clear(chart); host.Drain(); Log("after reading-cleared callback replaces mode/source/VM", chart, chart); Assert.True(requested); Assert.Equal(2, Total(chart));
        Assert.Equal(new[] { "new0", "new2" }, Readings(chart).Select(reading => reading.Members[0].StableId));
        Assert.All(Readings(chart), reading => { Assert.Same(latestBusiness, reading.BusinessDataContext); Assert.Same(rows[reading.Members[0].SourceIndex], reading.Members[0].Item); });
        var context = Assert.IsType<ChartDataTrackballContext>(Assert.Single(Cards(chart)).DataContext);
        Assert.Same(latestBusiness, context.BusinessDataContext); Assert.Equal(Total(chart), context.TotalReadings);
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void A_template_child_context_cleared_listener_may_request_a_new_reading_after_the_whole_teardown(bool engineering)
    {
        var chart = Create(engineering, Rows()); Template(chart, SimpleTemplate()); using var host = new Host(chart); Mode(chart, ChartTrackballMode.Vertical);
        Update(chart, Anchor(chart)); host.Drain(); var old = Assert.Single(Cards(chart)).Child!; Assert.IsType<ChartDataTrackballContext>(old.DataContext);
        var requested = false;
        old.DataContextChanged += (_, _) =>
        {
            if (requested || old.DataContext is not null) return;
            requested = true; Update(chart, Anchor(chart));
        };
        Clear(chart); host.Drain(); Log("after child-context-cleared callback requests new pointer", chart, chart);
        Assert.True(requested); Assert.Equal(2, Readings(chart).Count); Assert.Equal(2, Total(chart));
        var context = Assert.IsType<ChartDataTrackballContext>(Assert.Single(Cards(chart)).DataContext);
        Assert.Equal(Total(chart), context.TotalReadings); Assert.Equal(Readings(chart), context.Readings);
    }

    private void Log(string stage, ChartBase chart, Control ancestor)
    {
        var type = chart is EngineeringChartBase ? typeof(EngineeringChartBase) : typeof(AnalysisChartBase);
        object? Read(string name) => type.GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(chart);
        output.WriteLine($"{stage}: ancestorVisible={ancestor.IsVisible}, effectiveVisible={chart.IsEffectivelyVisible}, readings={Readings(chart).Count}, total={Total(chart)}, cards={Cards(chart).Length}, sourceRevision={Read("_professionalTrackballSourceRevision")}, rootRevision={Read("_revision")}, version={Read("_professionalTrackballVersion")}, position={Read("_professionalTrackballPosition")}, frame={Read("_professionalTrackballFrame") is not null}");
    }
    private static ObservableCollection<Row> Rows() => [new("r0", "First", 10), new("r1", "Second", 20), new("r2", "Third", 30), new("r3", "Fourth", 40)];
    private static ChartBase Create(bool engineering, IEnumerable source) { ChartBase chart = engineering ? new EngineeringProbe() : new AnalysisProbe(); Source(chart, source); return chart; }
    private static void Source(ChartBase chart, IEnumerable source) { if (chart is EngineeringChartBase e) e.ItemsSource = source; else ((AnalysisChartBase)chart).ItemsSource = source; }
    private static void Mode(ChartBase chart, ChartTrackballMode mode) { if (chart is EngineeringChartBase e) e.TrackballMode = mode; else ((AnalysisChartBase)chart).TrackballMode = mode; }
    private static void Display(ChartBase chart, ChartTrackballDisplayMode mode) { if (chart is EngineeringChartBase e) e.TrackballDisplayMode = mode; else ((AnalysisChartBase)chart).TrackballDisplayMode = mode; }
    private static void Update(ChartBase chart, Point point) { if (chart is EngineeringChartBase e) e.UpdateTrackball(point); else ((AnalysisChartBase)chart).UpdateTrackball(point); }
    private static void Clear(ChartBase chart) { if (chart is EngineeringChartBase e) e.ClearTrackball(); else ((AnalysisChartBase)chart).ClearTrackball(); }
    private static void Limit(ChartBase chart, int count) { if (chart is EngineeringChartBase e) e.MaxTrackballReadings = count; else ((AnalysisChartBase)chart).MaxTrackballReadings = count; }
    private static void Template(ChartBase chart, IDataTemplate template) { if (chart is EngineeringChartBase e) e.TrackballContentTemplate = template; else ((AnalysisChartBase)chart).TrackballContentTemplate = template; }
    private static void Formatter(ChartBase chart, Func<IReadOnlyList<ChartDataTrackballReading>, string?> formatter) { if (chart is EngineeringChartBase e) e.TrackballFormatter = formatter; else ((AnalysisChartBase)chart).TrackballFormatter = formatter; }
    private static IBrush? TooltipBackground(ChartBase chart) => chart is EngineeringChartBase e ? e.TooltipBackground : ((AnalysisChartBase)chart).TooltipBackground;
    private static void Tooltip(ChartBase chart, bool enabled) { if (chart is EngineeringChartBase e) e.IsTooltipEnabled = enabled; else ((AnalysisChartBase)chart).IsTooltipEnabled = enabled; }
    private static Rect Plot(ChartBase chart) => chart is EngineeringChartBase e ? e.PlotArea : ((AnalysisChartBase)chart).PlotArea;
    private static Point Anchor(ChartBase chart) { var plot = Plot(chart); return new(plot.Left + 80, plot.Top + 58); }
    private static IReadOnlyList<ChartDataTrackballReading> Readings(ChartBase chart) => chart is EngineeringChartBase e ? e.TrackballReadings : ((AnalysisChartBase)chart).TrackballReadings;
    private static int Total(ChartBase chart) => chart is EngineeringChartBase e ? e.TrackballTotalReadings : ((AnalysisChartBase)chart).TrackballTotalReadings;
    private static Border[] Cards(ChartBase chart) => chart.GetVisualDescendants().OfType<Border>().Where(control => control.Name == "PART_ChartDataTrackballCard").ToArray();
    private static IDataTemplate SimpleTemplate() => new FuncDataTemplate<ChartDataTrackballContext>((_, _) => new TextBlock { Text = "Trackball only template" });
    private sealed record Row(string Id, string Label, double Value);
    private sealed class Business : System.ComponentModel.INotifyPropertyChanged
    { private string _title = "Business"; public string Title { get => _title; set { _title = value; PropertyChanged?.Invoke(this, new(nameof(Title))); } } public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged; }
    private sealed class EngineeringProbe : EngineeringChartBase
    {
        public EngineeringProbe() { IdPath = "Id"; LabelPath = "Label"; ObservePaths("Value"); ShowGrid = false; }
        protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot)
        {
            var results = new List<EngineeringMarkInfo>();
            for (var i = 0; i < data.Count; i++)
            {
                var bounds = Box(i, plot); var members = i == 0 && data.Count > 3 ? Array.AsReadOnly(new[] { data.Points[0], data.Points[3] }) : Array.AsReadOnly(new[] { data.Points[i] });
                results.Add(new(members, new RectangleGeometry(bounds), Brushes.Blue, bounds.Center) { Value = data.Points[i].Point.Y, TooltipText = $"original-{data.Points[i].StableId}" });
            }
            return results;
        }
    }
    private sealed class AnalysisProbe : AnalysisChartBase
    {
        public AnalysisProbe() { IdPath = "Id"; CategoryPath = "Label"; ValuePath = "Value"; }
        protected override IReadOnlyList<AnalysisMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot) => data.Points.Select((point, index) =>
        { var bounds = Box(index, plot); return new AnalysisMarkInfo(point, new RectangleGeometry(bounds), Brushes.Blue, bounds.Center) { TooltipText = $"original-{point.StableId}" }; }).ToArray();
    }
    private static Rect Box(int index, Rect plot) => index switch { 0 => new(plot.Left + 40, plot.Top + 40, 80, 35), 1 => new(plot.Left + 40, plot.Top + 110, 80, 35), 2 => new(plot.Left + 180, plot.Top + 40, 80, 35), _ => new(plot.Right + 10, plot.Top + 40, 80, 35) };
    private sealed class CountingRows(ObservableCollection<Row> rows) : IEnumerable, INotifyCollectionChanged
    { public int Enumerations { get; private set; } public IEnumerator GetEnumerator() { Enumerations++; return rows.GetEnumerator(); } public event NotifyCollectionChangedEventHandler? CollectionChanged { add => rows.CollectionChanged += value; remove => rows.CollectionChanged -= value; } }
    private sealed class Host : IDisposable
    {
        private readonly Window _window; private readonly ChartBase _chart; private readonly Control _content;
        internal Host(ChartBase chart, Control? content = null) { _chart = chart; _content = content ?? chart; _window = new Window { Content = _content, Width = 640, Height = 400 }; _window.Show(); Drain(); }
        internal void Drain() { _window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); _window.UpdateLayout(); }
        internal void Move(Point point) { _window.MouseMove(_chart.TranslatePoint(point, _window) ?? point); Drain(); }
        internal void Key(Key key) { _window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null); _window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null); Drain(); }
        internal void Click(Point point) { var target = _chart.TranslatePoint(point, _window) ?? point; _window.MouseDown(target, MouseButton.Left); _window.MouseUp(target, MouseButton.Left); Drain(); }
        internal void Detach() { _window.Content = null; Drain(); }
        internal void Attach() { _window.Content = _content; Drain(); }
        public void Dispose() { _window.Close(); Dispatcher.UIThread.RunJobs(); }
    }
}
