using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class SharedTooltipPresentationTests
{
    [AvaloniaFact]
    public void Professional_roots_share_template_property_identity_without_changing_the_Cartesian_contract()
    {
        Assert.Same(EngineeringChartBase.TooltipContentTemplateProperty, AnalysisChartBase.TooltipContentTemplateProperty);
        Assert.Same(EngineeringChartBase.TooltipBackgroundProperty, AnalysisChartBase.TooltipBackgroundProperty);
        Assert.NotSame(CartesianChart.TooltipContentTemplateProperty, EngineeringChartBase.TooltipContentTemplateProperty);
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Templates_reuse_controls_with_original_members_business_context_and_bounded_passive_layout(bool engineering)
    {
        var model = new SharedTooltipPresentationModel(); var source = new CountingRows(model.Records);
        var chart = Create(engineering, source); chart.DataContext = model;
        var builds = 0; var child = new TextBlock { Text = "custom", Width = 4000, Height = 4000 };
        SetTemplate(chart, new FuncDataTemplate<ChartDataTooltipContext>((_, _) => { builds++; return child; }));
        using var host = new Host(chart); host.Move(0);
        var first = Assert.IsType<ChartDataTooltipContext>(child.DataContext);
        Assert.Same(model, first.BusinessDataContext);
        if (engineering)
        {
            Assert.NotNull(first.EngineeringHit); Assert.Null(first.PointHit);
            Assert.Same(first.EngineeringHit!.Members, first.Members);
            Assert.Equal(new[] { 0, 2 }, first.Members.Select(member => member.SourceIndex));
            Assert.Same(model.Records[0], first.Members[0].Item); Assert.Same(model.Records[2], first.Members[1].Item);
            Assert.Equal(new object?[] { "a", "c" }, first.Members.Select(member => member.StableId));
        }
        else
        {
            Assert.Null(first.EngineeringHit); Assert.NotNull(first.PointHit);
            Assert.Same(model.Records[0], Assert.Single(first.Members).Item); Assert.Same(first.PointHit!.Item, first.Members[0].Item);
        }
        var overlay = Assert.IsAssignableFrom<Control>(Overlay(chart));
        Assert.False(overlay.IsHitTestVisible); Assert.False(overlay.Focusable);
        Assert.Equal(KeyboardNavigationMode.None, KeyboardNavigation.GetTabNavigation(overlay));
        Assert.Equal(new Rect(chart.Bounds.Size), overlay.Bounds);
        var border = Assert.IsType<Border>(child.GetVisualParent()); Assert.True(border.ClipToBounds);
        Assert.True(border.Bounds.Left >= 8 && border.Bounds.Top >= 8);
        Assert.True(border.Bounds.Right <= chart.Bounds.Width - 8 && border.Bounds.Bottom <= chart.Bounds.Height - 8);
        var reads = source.Enumerations; host.Move(1);
        var second = Assert.IsType<ChartDataTooltipContext>(child.DataContext);
        Assert.NotSame(first, second); Assert.Same(model.Records[1], Assert.Single(second.Members).Item);
        Assert.Equal(1, builds); Assert.Same(overlay, Overlay(chart)); Assert.Equal(reads, source.Enumerations);
        _ = Record(chart); _ = Record(chart); Assert.Equal(1, builds); Assert.Equal(reads, source.Enumerations);
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Source_INPC_replacement_disable_reset_and_detach_release_obsolete_template_contexts(bool engineering)
    {
        var rows = new SharedTooltipPresentationModel().Records; var chart = Create(engineering, rows);
        var created = new List<TextBlock>(); SetTemplate(chart, new FuncDataTemplate<ChartDataTooltipContext>((_, _) => { var item = new TextBlock(); created.Add(item); return item; }));
        using var host = new Host(chart); host.Move(0); var old = created[^1];
        rows[0].Value = 17; Assert.Null(old.DataContext); Assert.Null(old.GetVisualParent()); Assert.Null(((ILogical)old).LogicalParent);
        host.Drain(); Assert.NotNull(Overlay(chart)); Assert.Contains(Assert.IsType<ChartDataTooltipContext>(created[^1].DataContext).Members, member => ReferenceEquals(member.Item, rows[0]) && member.Point.Y == 17);
        old = created[^1]; Replace(chart, new[] { new SharedTooltipRow("new", "New", 11, "NewR", "NewC") });
        Assert.Null(old.DataContext); host.Drain(); Assert.Null(Overlay(chart));
        host.Move(0); old = created[^1]; Disable(chart, true); Assert.Null(old.DataContext); Assert.Null(Overlay(chart));
        Disable(chart, false); host.Move(0); old = created[^1]; chart.ResetViewport(); host.Drain(); Assert.Null(old.DataContext); Assert.Null(Overlay(chart));
        host.Move(0); old = created[^1]; host.Detach(); Assert.Null(old.DataContext); Assert.Null(Overlay(chart));
        host.Attach(); host.Move(0); Assert.NotNull(Overlay(chart)); Assert.Equal("new", Assert.Single(Assert.IsType<ChartDataTooltipContext>(created[^1].DataContext).Members).StableId);
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Null_build_suppresses_legacy_readout_and_build_failure_cleans_up_once_until_replacement(bool engineering)
    {
        var chart = Create(engineering, new[] { new SharedTooltipRow("a", "LegacyReadout", 2, "LegacyReadout", "C") }); HideLabels(chart);
        using var host = new Host(chart); host.Move(0); Assert.Contains("LegacyReadout", Text(Record(chart)), StringComparison.Ordinal);
        SetTemplate(chart, new FuncDataTemplate<ChartDataTooltipContext>((_, _) => null)); host.Drain();
        Assert.Null(Overlay(chart)); Assert.DoesNotContain("LegacyReadout", Text(Record(chart)), StringComparison.Ordinal);
        var attempts = 0;
        Assert.Throws<InvalidOperationException>(() => SetTemplate(chart, new FuncDataTemplate<ChartDataTooltipContext>((_, _) => { attempts++; throw new InvalidOperationException("business template failed"); })));
        Assert.Null(Overlay(chart)); Assert.Contains("LegacyReadout", Text(Record(chart)), StringComparison.Ordinal);
        _ = Record(chart); host.Move(0); Assert.Equal(1, attempts);
        var recovered = new TextBlock(); SetTemplate(chart, new FuncDataTemplate<ChartDataTooltipContext>((_, _) => recovered)); host.Drain();
        Assert.Single(Assert.IsType<ChartDataTooltipContext>(recovered.DataContext).Members);
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void First_template_on_an_already_dirty_hovered_frame_appears_without_another_pointer_move(bool engineering)
    {
        var rows = new SharedTooltipPresentationModel().Records; var chart = Create(engineering, rows);
        using var host = new Host(chart); host.Move(0);
        if (chart is ComparisonChartBase comparison) comparison.ShowValues = false; else ((AnalysisChartBase)chart).ShowValues = false;
        var child = new TextBlock(); SetTemplate(chart, new FuncDataTemplate<ChartDataTooltipContext>((_, _) => child));
        host.Drain(); Assert.NotNull(Overlay(chart));
        Assert.Contains(Assert.IsType<ChartDataTooltipContext>(child.DataContext).Members, member => ReferenceEquals(member.Item, rows[0]));
    }

    [AvaloniaTheory]
    [InlineData(false, "disable")] [InlineData(true, "disable")]
    [InlineData(false, "source")] [InlineData(true, "source")]
    [InlineData(false, "template")] [InlineData(true, "template")]
    [InlineData(false, "reset")] [InlineData(true, "reset")]
    [InlineData(false, "detach")] [InlineData(true, "detach")]
    public void Build_reentry_does_not_publish_a_stale_control_or_reading(bool engineering, string action)
    {
        var chart = Create(engineering, new SharedTooltipPresentationModel().Records); using var host = new Host(chart); host.Move(0);
        var stale = new TextBlock(); var current = new TextBlock();
        SetTemplate(chart, new FuncDataTemplate<ChartDataTooltipContext>((_, _) =>
        {
            if (action == "disable") Disable(chart, true);
            else if (action == "source") Replace(chart, new[] { new SharedTooltipRow("new", "New", 1, "NewR", "NewC") });
            else if (action == "template") SetTemplate(chart, new FuncDataTemplate<ChartDataTooltipContext>((_, _) => current));
            else if (action == "reset") chart.ResetViewport();
            else host.Detach();
            return stale;
        }));
        host.Drain(); Assert.Null(stale.GetVisualParent()); Assert.Null(((ILogical)stale).LogicalParent); Assert.Null(stale.DataContext);
        if (action == "template") Assert.NotNull(current.GetVisualParent()); else Assert.Null(Overlay(chart));
    }

    [AvaloniaTheory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public void Inherited_context_and_attachment_callbacks_cannot_leave_orphaned_controls(bool engineering, bool attachment)
    {
        var chart = Create(engineering, new SharedTooltipPresentationModel().Records); using var host = new Host(chart); host.Move(0);
        var child = new TextBlock();
        if (attachment) child.AttachedToVisualTree += (_, _) => Disable(chart, true);
        else child.DataContextChanged += (_, _) => { if (child.DataContext is ChartDataTooltipContext) Disable(chart, true); };
        SetTemplate(chart, new FuncDataTemplate<ChartDataTooltipContext>((_, _) => child)); host.Drain();
        Assert.Null(Overlay(chart)); Assert.Null(child.GetVisualParent()); Assert.Null(((ILogical)child).LogicalParent); Assert.Null(child.DataContext);
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Parented_template_controls_keep_the_application_parent_and_context(bool engineering)
    {
        var chart = Create(engineering, new SharedTooltipPresentationModel().Records); using var host = new Host(chart); host.Move(0);
        var business = new object(); var child = new TextBlock(); var original = new Border { DataContext = business, Child = child };
        Assert.Throws<InvalidOperationException>(() => SetTemplate(chart, new FuncDataTemplate<ChartDataTooltipContext>((_, _) => child)));
        Assert.Same(child, original.Child); Assert.Same(original, child.GetVisualParent()); Assert.Same(business, child.DataContext); Assert.Null(Overlay(chart));
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Reapplying_or_clearing_the_control_template_preserves_overlay_membership(bool engineering)
    {
        var chart = Create(engineering, new SharedTooltipPresentationModel().Records); SetTemplate(chart, new FuncDataTemplate<ChartDataTooltipContext>((_, _) => new TextBlock { Text = "Template" }));
        using var host = new Host(chart); host.Move(0);
        chart.Template = new FuncControlTemplate<ChartBase>((_, _) => new Border()); host.Drain();
        chart.Template = null; host.Drain(); var overlay = Assert.IsAssignableFrom<Control>(Overlay(chart));
        Assert.Same(chart, overlay.GetVisualParent()); Assert.Same(chart, ((ILogical)overlay).LogicalParent); Assert.Equal(new Rect(chart.Bounds.Size), overlay.Bounds);
    }

    [AvaloniaFact]
    public void Compiled_templates_bind_business_VM_and_all_raw_members_inherit_resources_and_clear_after_VM_replacement()
    {
        var first = new SharedTooltipPresentationModel(); var view = new SharedTooltipPresentationView { DataContext = first };
        var engineering = view.FindControl<MosaicChart>("Engineering")!; var analysis = view.FindControl<FunnelChart>("Analysis")!;
        using var host = new Host(engineering, view); host.Move(0);
        Assert.Equal(first.Title, Find<TextBlock>(engineering, "SharedBusinessTitle").Text);
        Assert.Equal(new[] { "A", "C" }, engineering.GetVisualDescendants().OfType<TextBlock>().Where(item => item.Classes.Contains("OriginalMember")).Select(item => item.Text));
        var old = Find<Border>(engineering, "SharedCompiledContent");
        Assert.Same(first, Assert.IsType<ChartDataTooltipContext>(old.DataContext).BusinessDataContext);
        view.Resources["SharedTooltipBrush"] = Brushes.Orange; host.Drain(); Assert.Same(Brushes.Orange, old.Background);
        host.MoveChart(analysis, 0); Assert.Equal(first.Title, Find<TextBlock>(analysis, "SharedBusinessTitle").Text);
        var analyticalOld = Find<Border>(analysis, "SharedCompiledContent");
        var second = new SharedTooltipPresentationModel { Title = "Replacement VM", Records = [new("new", "Fresh", 7, "N", "C")] };
        view.DataContext = second; host.Drain(); Assert.Null(old.DataContext); Assert.Null(analyticalOld.DataContext);
        host.Move(0); Assert.Equal(second.Title, Find<TextBlock>(engineering, "SharedBusinessTitle").Text);
        Assert.Same(second.Records[0], Assert.Single(Assert.IsType<ChartDataTooltipContext>(Find<Border>(engineering, "SharedCompiledContent").DataContext).Members).Item);
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Theme_switches_and_local_chrome_bindings_update_the_live_control(bool engineering)
    {
        var chart = Create(engineering, new SharedTooltipPresentationModel().Records); var child = new TextBlock { Text = "Template" };
        SetTemplate(chart, new FuncDataTemplate<ChartDataTooltipContext>((_, _) => child)); using var host = new Host(chart); host.Move(0);
        chart.HighContrastMode = ChartHighContrastMode.Dark; host.Drain(); host.Move(0);
        var chrome = Find<Border>(chart, "PART_ChartDataTooltip"); Assert.True(IsColor(chrome.Background, Colors.Black));
        if (chart is EngineeringChartBase e) e.TooltipBackground = Brushes.Pink; else ((AnalysisChartBase)chart).TooltipBackground = Brushes.Pink;
        Assert.Same(Brushes.Pink, chrome.Background);
        chart.HighContrastMode = ChartHighContrastMode.Light; host.Drain(); host.Move(0);
        Assert.Same(Brushes.Pink, Find<Border>(chart, "PART_ChartDataTooltip").Background);
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Optional_theme_resources_and_contrast_modes_style_all_live_professional_tooltip_chrome(bool engineering)
    {
        var chart = Create(engineering, new SharedTooltipPresentationModel().Records);
        SetTemplate(chart, new FuncDataTemplate<ChartDataTooltipContext>((_, _) => new TextBlock { Name = "StyledTooltipText", Text = "reading" }));
        using var host = new Host(chart);
        foreach (var dark in new[] { false, true })
        {
            host.SetTheme(dark ? ThemeVariant.Dark : ThemeVariant.Light); host.Move(0);
            void AssertChrome(string background, string foreground, string border)
            {
                var chrome = Find<Border>(chart, "PART_ChartDataTooltip");
                Assert.True(IsColor(chrome.Background, Color.Parse(background))); Assert.True(IsColor(chrome.BorderBrush, Color.Parse(border)));
                Assert.True(IsColor(Find<TextBlock>(chart, "StyledTooltipText").Foreground, Color.Parse(foreground)));
                Assert.True(IsColor(chart.GetValue(EngineeringChartBase.TooltipBackgroundProperty), Color.Parse(background)));
                Assert.True(IsColor(chart.GetValue(EngineeringChartBase.TooltipForegroundProperty), Color.Parse(foreground)));
                Assert.True(IsColor(chart.GetValue(EngineeringChartBase.TooltipBorderBrushProperty), Color.Parse(border)));
            }
            AssertChrome(dark ? "#101B2B" : "#FFFFFF", dark ? "#F2F4F7" : "#243247", dark ? "#53657C" : "#BAC8DB");
            foreach (var mode in new[] { ChartHighContrastMode.Light, ChartHighContrastMode.Dark })
            {
                chart.HighContrastMode = mode; host.Drain(); host.Move(0);
                AssertChrome(mode == ChartHighContrastMode.Dark ? "#000000" : "#FFFFFF",
                    mode == ChartHighContrastMode.Dark ? "#FFFFFF" : "#000000", mode == ChartHighContrastMode.Dark ? "#FFFFFF" : "#000000");
            }
            chart.HighContrastMode = ChartHighContrastMode.None; host.Drain(); host.Move(0);
            AssertChrome(dark ? "#101B2B" : "#FFFFFF", dark ? "#F2F4F7" : "#243247", dark ? "#53657C" : "#BAC8DB");
        }
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Live_local_tooltip_brush_bindings_win_over_both_normal_and_contrast_styles(bool engineering)
    {
        var chart = Create(engineering, new SharedTooltipPresentationModel().Records); var model = new TooltipBrushModel();
        using var backgroundBinding = chart.Bind(EngineeringChartBase.TooltipBackgroundProperty, new Binding(nameof(TooltipBrushModel.Background)) { Source = model });
        using var foregroundBinding = chart.Bind(EngineeringChartBase.TooltipForegroundProperty, new Binding(nameof(TooltipBrushModel.Foreground)) { Source = model });
        using var borderBinding = chart.Bind(EngineeringChartBase.TooltipBorderBrushProperty, new Binding(nameof(TooltipBrushModel.Border)) { Source = model });
        SetTemplate(chart, new FuncDataTemplate<ChartDataTooltipContext>((_, _) => new TextBlock { Name = "BoundTooltipText", Text = "bound" }));
        using var host = new Host(chart); host.Move(0);
        foreach (var mode in new[] { ChartHighContrastMode.Light, ChartHighContrastMode.Dark, ChartHighContrastMode.None })
        {
            chart.HighContrastMode = mode; host.SetTheme(mode == ChartHighContrastMode.Light ? ThemeVariant.Dark : ThemeVariant.Light); host.Move(0);
            var chrome = Find<Border>(chart, "PART_ChartDataTooltip");
            Assert.Same(model.Background, chrome.Background); Assert.Same(model.Border, chrome.BorderBrush);
            Assert.Same(model.Foreground, Find<TextBlock>(chart, "BoundTooltipText").Foreground);
            model.Background = ReferenceEquals(model.Background, Brushes.Pink) ? Brushes.Orange : Brushes.Pink;
            model.Foreground = ReferenceEquals(model.Foreground, Brushes.Purple) ? Brushes.Lime : Brushes.Purple;
            model.Border = ReferenceEquals(model.Border, Brushes.Blue) ? Brushes.Red : Brushes.Blue;
            host.Drain(); Assert.Same(chrome, Find<Border>(chart, "PART_ChartDataTooltip"));
            Assert.Same(model.Background, chrome.Background); Assert.Same(model.Border, chrome.BorderBrush);
            Assert.Same(model.Foreground, Find<TextBlock>(chart, "BoundTooltipText").Foreground);
        }
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Raster_capture_excludes_legacy_and_template_transient_tooltips_and_rejects_build_reentry(bool engineering)
    {
        var chart = Create(engineering, new[] { new SharedTooltipRow("a", "TransientOnly", 2, "TransientOnly", "C") }); HideLabels(chart);
        using var host = new Host(chart); host.Move(0);
        Assert.Contains("TransientOnly", Text(Record(chart)), StringComparison.Ordinal);
        Assert.DoesNotContain("TransientOnly", Text(RecordExport(chart)), StringComparison.Ordinal);
        var child = new TextBlock { Text = "Custom transient" }; var rejected = false;
        SetTemplate(chart, new FuncDataTemplate<ChartDataTooltipContext>((_, _) =>
        {
            Assert.Throws<InvalidOperationException>(() => RecordExport(chart)); rejected = true; return child;
        }));
        host.Drain(); Assert.True(rejected); Assert.NotNull(Overlay(chart));
        Assert.DoesNotContain("TransientOnly", Text(RecordExport(chart)), StringComparison.Ordinal);
        Assert.DoesNotContain("Custom transient", Text(RecordExport(chart)), StringComparison.Ordinal);
        Assert.NotNull(child.DataContext);
    }

    [AvaloniaFact]
    public void Disposing_a_professional_map_immediately_releases_its_active_template_context()
    {
        using var map = new SeatMap
        {
            ShowLegend = false, ShowLabels = false,
            FeaturesSource = [new MapFeature("region", [new MapPolygon([new(0, 0), new(10, 0), new(10, 10), new(0, 10), new(0, 0)])], "Region")],
        };
        var source = new MapValue("region", 5); map.Layers.Add(new ShapeLayer { ItemsSource = new[] { source } });
        var child = new TextBlock(); map.TooltipContentTemplate = new FuncDataTemplate<ChartDataTooltipContext>((_, _) => child);
        using var host = new Host(map); host.Move(0);
        Assert.Same(source, Assert.Single(Assert.IsType<ChartDataTooltipContext>(child.DataContext).Members).Item);
        map.Dispose(); Assert.Null(child.DataContext); Assert.Null(child.GetVisualParent()); Assert.Null(Overlay(map));
        host.Drain(); Assert.Empty(map.GetMarks());
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Layout_failure_releases_the_failed_subtree_and_a_new_template_recovers(bool engineering)
    {
        var chart = Create(engineering, new SharedTooltipPresentationModel().Records); using var host = new Host(chart); host.Move(0);
        var child = new CallbackMeasureControl(() => throw new InvalidOperationException("template measure failed"));
        SetTemplate(chart, new FuncDataTemplate<ChartDataTooltipContext>((_, _) => child));
        Assert.Throws<InvalidOperationException>(() => chart.Measure(new Size(541, 381)));
        Assert.Null(Overlay(chart)); Assert.Null(child.DataContext); Assert.Null(child.GetVisualParent());
        var recovered = new TextBlock { Text = "Recovered" }; SetTemplate(chart, new FuncDataTemplate<ChartDataTooltipContext>((_, _) => recovered));
        host.Drain(); Assert.NotNull(Overlay(chart)); Assert.NotNull(recovered.DataContext);
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Reentrant_raster_capture_during_template_measure_is_rejected_before_drawing(bool engineering)
    {
        var chart = Create(engineering, new SharedTooltipPresentationModel().Records); using var host = new Host(chart); host.Move(0);
        var attempted = false;
        var child = new CallbackMeasureControl(() => { Assert.Throws<InvalidOperationException>(() => RecordExport(chart)); attempted = true; });
        SetTemplate(chart, new FuncDataTemplate<ChartDataTooltipContext>((_, _) => child)); host.Drain();
        Assert.True(attempted); Assert.NotNull(Overlay(chart)); Assert.NotNull(child.DataContext);
    }

    [AvaloniaTheory]
    [InlineData(false, "chart-visible")] [InlineData(true, "chart-visible")]
    [InlineData(false, "ancestor-visible")] [InlineData(true, "ancestor-visible")]
    [InlineData(false, "chart-enabled")] [InlineData(true, "chart-enabled")]
    [InlineData(false, "ancestor-enabled")] [InlineData(true, "ancestor-enabled")]
    public void Hidden_or_disabled_owners_release_the_reading_and_never_build_hidden_content(bool engineering, string state)
    {
        var model = new SharedTooltipPresentationModel(); var chart = Create(engineering, model.Records); var container = new Border { Child = chart };
        var controls = new List<TextBlock>(); SetTemplate(chart, new FuncDataTemplate<ChartDataTooltipContext>((_, _) => { var child = new TextBlock(); controls.Add(child); return child; }));
        using var host = new Host(chart, container); host.Move(0); var old = controls[^1]; var builds = controls.Count;
        void SetState(bool value)
        {
            if (state == "chart-visible") chart.IsVisible = value;
            else if (state == "ancestor-visible") container.IsVisible = value;
            else if (state == "chart-enabled") chart.IsEnabled = value;
            else container.IsEnabled = value;
        }
        SetState(false); Assert.Null(old.DataContext); Assert.Null(Overlay(chart));
        model.Records[0].Value = 31; host.Drain(); Assert.Equal(builds, controls.Count); Assert.Null(Overlay(chart));
        SetState(true); host.Drain(); host.Move(0); Assert.NotNull(Overlay(chart));
        Assert.Contains(Assert.IsType<ChartDataTooltipContext>(controls[^1].DataContext).Members, member => ReferenceEquals(member.Item, model.Records[0]) && member.Point.Y == 31);
    }

    [AvaloniaTheory]
    [InlineData(false, false)] [InlineData(true, false)] [InlineData(false, true)] [InlineData(true, true)]
    public void An_old_layout_failure_does_not_quarantine_a_reentrant_replacement_template(bool engineering, bool arrange)
    {
        var chart = Create(engineering, new SharedTooltipPresentationModel().Records); using var host = new Host(chart); host.Move(0);
        var replacement = new TextBlock { Text = "replacement" };
        void ReplaceThenThrow()
        {
            SetTemplate(chart, new FuncDataTemplate<ChartDataTooltipContext>((_, _) => replacement));
            throw new InvalidOperationException("obsolete layout failure");
        }
        Control old = arrange ? new CallbackArrangeControl(ReplaceThenThrow) : new CallbackMeasureControl(ReplaceThenThrow);
        SetTemplate(chart, new FuncDataTemplate<ChartDataTooltipContext>((_, _) => old));
        if (arrange)
        {
            chart.Measure(chart.Bounds.Size); chart.InvalidateArrange();
            Assert.Throws<InvalidOperationException>(() => chart.Arrange(new Rect(chart.Bounds.Size)));
        }
        else Assert.Throws<InvalidOperationException>(() => chart.Measure(new Size(541, 381)));
        Assert.Null(old.DataContext); host.Drain(); Assert.NotNull(replacement.DataContext); Assert.NotNull(Overlay(chart));
    }

    [AvaloniaTheory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public void Default_render_and_data_build_callbacks_cannot_export_an_empty_successful_capture(bool engineering, bool building)
    {
        var (chart, probe) = CreateProbe(engineering); using var host = new Host(chart);
        Assert.Null(chart.GetValue(EngineeringChartBase.TooltipContentTemplateProperty)); var attempted = false;
        void CaptureReentrantly() { attempted = true; Assert.Throws<InvalidOperationException>(() => RecordExport(chart)); }
        if (building)
        {
            probe.DuringBuild = CaptureReentrantly;
            Replace(chart, new[] { new SharedTooltipRow("new", "New", 17, "R", "C") });
            if (chart is EngineeringChartBase e) _ = e.GetMarks(); else _ = ((AnalysisChartBase)chart).GetMarks();
            probe.DuringBuild = null;
        }
        else { probe.DuringRender = CaptureReentrantly; _ = Record(chart); probe.DuringRender = null; }
        Assert.True(attempted);
        Assert.Contains(Shapes(RecordExport(chart)), shape => IsColor(shape.Brush, Colors.Blue));
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Ancestor_enabled_changes_invalidate_default_readouts_without_new_pointer_input(bool engineering)
    {
        var (chart, probe) = CreateProbe(engineering); var container = new Border { Child = chart, Focusable = true };
        using var host = new Host(chart, container); container.Focus();
        chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Home });
        Assert.Contains("DefaultTransient", Text(Record(chart)), StringComparison.Ordinal);
        host.RenderFrames(); var enabledPasses = probe.RenderPasses; Assert.True(enabledPasses > 0);
        container.IsEnabled = false; host.RenderFrames();
        Assert.True(probe.RenderPasses > enabledPasses, "Ancestor disabling must invalidate the chart's cached default tooltip drawing without new pointer input.");
        Assert.DoesNotContain("DefaultTransient", Text(Record(chart)), StringComparison.Ordinal);
        host.RenderFrames(); var disabledPasses = probe.RenderPasses;
        container.IsEnabled = true; host.RenderFrames();
        Assert.True(probe.RenderPasses > disabledPasses, "Ancestor enabling must invalidate the cached drawing as well.");
        Assert.Contains("DefaultTransient", Text(Record(chart)), StringComparison.Ordinal);
    }

    private static ChartBase Create(bool engineering, IEnumerable source) => engineering
        ? new MosaicChart { ItemsSource = source }
        : new FunnelChart { ItemsSource = source, CategoryPath = nameof(SharedTooltipRow.Label), ValuePath = nameof(SharedTooltipRow.Value), IdPath = nameof(SharedTooltipRow.Id) };
    private static void SetTemplate(ChartBase chart, IDataTemplate? template) { if (chart is EngineeringChartBase e) e.TooltipContentTemplate = template; else ((AnalysisChartBase)chart).TooltipContentTemplate = template; }
    private static void Replace(ChartBase chart, IEnumerable source) { if (chart is EngineeringChartBase e) e.ItemsSource = source; else ((AnalysisChartBase)chart).ItemsSource = source; }
    private static void Disable(ChartBase chart, bool disabled) { if (chart is EngineeringChartBase e) e.IsTooltipEnabled = !disabled; else ((AnalysisChartBase)chart).IsTooltipEnabled = !disabled; }
    private static void HideLabels(ChartBase chart) { if (chart is ComparisonChartBase e) { e.ShowLabels = false; e.ShowValues = false; } else if (chart is AnalysisChartBase a) { a.ShowLabels = false; a.ShowValues = false; } }
    private static Point Position(ChartBase chart, int index) => chart is EngineeringChartBase e ? e.GetMarks()[index].Position : ((AnalysisChartBase)chart).GetMarks()[index].LabelPosition;
    private static T Find<T>(ChartBase chart, string name) where T : Control => Assert.Single(chart.GetVisualDescendants().OfType<T>(), item => item.Name == name);
    private static Control? Overlay(ChartBase chart) => chart.GetVisualDescendants().OfType<Control>().SingleOrDefault(item => item.Name == "PART_ChartDataTooltipOverlay");
    private static DrawingGroup Record(ChartBase chart) { var group = new DrawingGroup(); using var context = group.Open(); chart.Render(context); return group; }
    private static DrawingGroup RecordExport(ChartBase chart)
    {
        var group = new DrawingGroup(); using var context = group.Open();
        if (chart is EngineeringChartBase e) e.RenderExport(context, e.GetExportLayout(null), null); else { var a = (AnalysisChartBase)chart; a.RenderExport(context, a.GetExportLayout(null), null); }
        return group;
    }
    private static string Text(Drawing drawing) => string.Concat(Glyphs(drawing).Select(glyph => glyph.GlyphRun is { } run ? new string(run.Characters.Span) : ""));
    private static IEnumerable<GlyphRunDrawing> Glyphs(Drawing drawing) { if (drawing is DrawingGroup group) foreach (var child in group.Children) foreach (var glyph in Glyphs(child)) yield return glyph; else if (drawing is GlyphRunDrawing glyph) yield return glyph; }
    private static bool IsColor(IBrush? brush, Color color) => brush is ISolidColorBrush solid && solid.Color == color;
    private static IEnumerable<GeometryDrawing> Shapes(Drawing drawing)
    { if (drawing is DrawingGroup group) foreach (var child in group.Children) foreach (var shape in Shapes(child)) yield return shape; else if (drawing is GeometryDrawing shape) yield return shape; }
    private static (ChartBase Chart, IRenderProbe Probe) CreateProbe(bool engineering)
    {
        ChartBase chart = engineering ? new EngineeringRenderProbe() : new AnalysisRenderProbe();
        Replace(chart, new[] { new SharedTooltipRow("old", "Old", 3, "R", "C") }); return (chart, (IRenderProbe)chart);
    }
    private interface IRenderProbe
    {
        Action? DuringBuild { get; set; } Action? DuringRender { get; set; } int RenderPasses { get; }
    }
    private sealed class EngineeringRenderProbe : EngineeringChartBase, IRenderProbe
    {
        public EngineeringRenderProbe() { IdPath = nameof(SharedTooltipRow.Id); LabelPath = nameof(SharedTooltipRow.Label); ObservePaths(nameof(SharedTooltipRow.Value)); ShowGrid = false; }
        public Action? DuringBuild { get; set; } public Action? DuringRender { get; set; } public int RenderPasses { get; private set; }
        protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot)
        {
            DuringBuild?.Invoke();
            return data.Count == 0 ? [] : [new(Array.AsReadOnly(new[] { data.Points[0] }), new RectangleGeometry(plot), Brushes.Blue, plot.Center) { TooltipText = "DefaultTransient" }];
        }
        protected override void RenderAnnotations(DrawingContext context, Rect plot) { RenderPasses++; DuringRender?.Invoke(); }
    }
    private sealed class AnalysisRenderProbe : AnalysisChartBase, IRenderProbe
    {
        public AnalysisRenderProbe() { IdPath = nameof(SharedTooltipRow.Id); CategoryPath = nameof(SharedTooltipRow.Label); ValuePath = nameof(SharedTooltipRow.Value); ShowLabels = ShowValues = false; }
        public Action? DuringBuild { get; set; } public Action? DuringRender { get; set; } public int RenderPasses { get; private set; }
        protected override IReadOnlyList<AnalysisMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot)
        {
            DuringBuild?.Invoke();
            return data.Count == 0 ? [] : [new(data.Points[0], new RectangleGeometry(plot), Brushes.Blue, plot.Center) { TooltipText = "DefaultTransient" }];
        }
        protected override void DrawDecorations(DrawingContext context, IReadOnlyList<AnalysisMarkInfo> marks) { RenderPasses++; DuringRender?.Invoke(); }
    }
    private sealed class CountingRows(ObservableCollection<SharedTooltipRow> rows) : IEnumerable, INotifyCollectionChanged
    {
        public int Enumerations { get; private set; }
        public IEnumerator GetEnumerator() { Enumerations++; return rows.GetEnumerator(); }
        public event NotifyCollectionChangedEventHandler? CollectionChanged { add => rows.CollectionChanged += value; remove => rows.CollectionChanged -= value; }
    }
    private sealed class CallbackMeasureControl(Action callback) : Control
    {
        protected override Size MeasureOverride(Size availableSize) { callback(); return new(40, 20); }
    }
    private sealed class CallbackArrangeControl(Action callback) : Control
    {
        protected override Size MeasureOverride(Size availableSize) => new(40, 20);
        protected override Size ArrangeOverride(Size finalSize) { callback(); return finalSize; }
    }
    private sealed class TooltipBrushModel : INotifyPropertyChanged
    {
        private IBrush _background = Brushes.Pink, _foreground = Brushes.Purple, _border = Brushes.Blue;
        public IBrush Background { get => _background; set { _background = value; PropertyChanged?.Invoke(this, new(nameof(Background))); } }
        public IBrush Foreground { get => _foreground; set { _foreground = value; PropertyChanged?.Invoke(this, new(nameof(Foreground))); } }
        public IBrush Border { get => _border; set { _border = value; PropertyChanged?.Invoke(this, new(nameof(Border))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    private sealed class Host : IDisposable
    {
        private readonly Window _window; private readonly ChartBase _chart; private readonly Control _content;
        public Host(ChartBase chart, Control? content = null) { _chart = chart; _content = content ?? chart; _window = new Window { Content = _content, Width = content is null ? 540 : 1000, Height = 380 }; _window.Show(); Drain(); }
        public void Drain() { _window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); _window.UpdateLayout(); }
        public void SetTheme(ThemeVariant variant) { _window.RequestedThemeVariant = variant; Drain(); }
        public void RenderFrames() { Drain(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(2); Drain(); }
        public void Move(int index) => MoveChart(_chart, index);
        public void MoveChart(ChartBase chart, int index) { var point = Position(chart, index); _window.MouseMove(chart.TranslatePoint(point, _window) ?? point); Drain(); }
        public void Detach() { _window.Content = null; Drain(); }
        public void Attach() { _window.Content = _content; Drain(); }
        public void Dispose() { _window.Close(); Dispatcher.UIThread.RunJobs(); }
    }
}
