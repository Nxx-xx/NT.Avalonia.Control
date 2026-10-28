using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.Charts;

public sealed partial class CartesianChart
{
    public static readonly StyledProperty<IDataTemplate?> TooltipContentTemplateProperty =
        AvaloniaProperty.Register<CartesianChart, IDataTemplate?>(nameof(TooltipContentTemplate));

    /// <summary>
    /// Optional passive tooltip content. Its DataContext is ChartTooltipContext; the
    /// chart supplies the outer tooltip chrome. A null Build result suppresses the box.
    /// Build errors propagate after cleanup, following Avalonia data-template behavior.
    /// </summary>
    public IDataTemplate? TooltipContentTemplate
    {
        get => GetValue(TooltipContentTemplateProperty);
        set => SetValue(TooltipContentTemplateProperty, value);
    }

    private TooltipPresentationHost? _tooltipPresentationHost;
    private Border? _tooltipPresentationBorder;
    private Control? _tooltipPresentationContent;
    private IDataTemplate? _tooltipPresentationTemplate;
    private IDataTemplate? _failedTooltipPresentationTemplate;
    private string[]? _tooltipPresentationFallbackLines;
    private long _tooltipPresentationFallbackRevision;
    private long _tooltipPresentationRevision;
    private bool _synchronizingTooltipPresentation;
    private bool _clearingTooltipPresentation;
    private bool _pendingTooltipPresentation;
    private bool _queuedTooltipPresentation;
    private bool _suppressDrawnTooltip;

    private bool SuppressDrawnTooltip => _suppressDrawnTooltip;

    private bool PresentationPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == TooltipContentTemplateProperty)
        {
            var revision = ++_tooltipPresentationRevision;
            _failedTooltipPresentationTemplate = null;
            _tooltipPresentationFallbackLines = null;
            _suppressDrawnTooltip = false;
            ReleaseTooltipPresentationVisual();
            if (_tooltipPresentationRevision == revision) SynchronizeTooltipPresentation();
            RequestRender();
            return true;
        }
        if (change.Property == TooltipFormatterProperty && TooltipContentTemplate is not null)
        {
            SynchronizeTooltipPresentation();
            RequestRender();
            return true;
        }
        if (_tooltipPresentationHost is null) return false;
        if (change.Property != TooltipBackgroundProperty && change.Property != TooltipForegroundProperty &&
            change.Property != TooltipBorderBrushProperty) return false;
        UpdateTooltipPresentationAppearance();
        InvalidateMeasure();
        RequestRender();
        return true;
    }

    private bool CanPresentTooltip() => !_clearingTooltipPresentation && !IsTrackballActive && _attached && IsTooltipEnabled && HoveredPoint is not null &&
        HoveredPoints.Count > 0 && _panPointer is null && _selectionPointer is null;

    private void SynchronizeTooltipPresentation()
    {
        if (_synchronizingTooltipPresentation)
        {
            _tooltipPresentationRevision++;
            _pendingTooltipPresentation = true;
            return;
        }
        if (TooltipContentTemplate is not { } template || !CanPresentTooltip())
        {
            ClearTooltipPresentation();
            return;
        }
        var revision = ++_tooltipPresentationRevision;
        var primary = HoveredPoint!;
        var hits = HoveredPoints;
        var anchor = _tooltipAnchor;
        _synchronizingTooltipPresentation = true;
        try
        {
            var scene = GetScene();
            if (scene is null || !scene.Snapshots.Any(item => ReferenceEquals(item.Series, primary.Series))) return;
            var lines = TooltipLines(scene);
            if (!IsTooltipPresentationCurrent(revision, template, primary, hits, anchor)) return;
            // Only independent strings are cached for a failed-template fallback:
            // never retain an obsolete scene, hit list, or application Item here.
            _tooltipPresentationFallbackLines = lines;
            _tooltipPresentationFallbackRevision = revision;
            var content = new ChartTooltipContext(primary, hits, string.Join("\n", lines), anchor)
            { Readings = TrackballReadings, Segments = HoveredSegments, PrimarySegment = PrimarySegment };
            if (ReferenceEquals(_failedTooltipPresentationTemplate, template))
            {
                _suppressDrawnTooltip = false;
                return;
            }
            if (ReferenceEquals(_tooltipPresentationTemplate, template) && _tooltipPresentationContent is not null)
            {
                var existingBorder = _tooltipPresentationBorder!;
                var existingChild = _tooltipPresentationContent;
                existingBorder.DataContext = content;
                if (!IsTooltipPresentationCurrent(revision, template, primary, hits, anchor))
                {
                    DiscardStaleTooltipContent(existingBorder, existingChild);
                    return;
                }
                _tooltipPresentationHost!.SetAnchor(anchor);
                _suppressDrawnTooltip = true;
                InvalidateMeasure();
                InvalidateArrange();
                return;
            }
            // Application code may replace a source/template, disable readings, or
            // request export inside Build. Do not parent or publish an obsolete result.
            var child = template.Build(content);
            if (!IsTooltipPresentationCurrent(revision, template, primary, hits, anchor)) return;
            if (child is null)
            {
                ReleaseTooltipPresentationVisual();
                if (!IsTooltipPresentationCurrent(revision, template, primary, hits, anchor)) return;
                _tooltipPresentationTemplate = template;
                _suppressDrawnTooltip = true;
                RequestRender();
                return;
            }
            var logicalParent = ((ILogical)child).LogicalParent;
            var visualParent = child.GetVisualParent();
            if ((logicalParent is not null || visualParent is not null) &&
                !ReferenceEquals(child, _tooltipPresentationContent))
                throw new InvalidOperationException("A tooltip template must return an unparented control.");
            EnsureTooltipPresentationHost();
            if (!IsTooltipPresentationCurrent(revision, template, primary, hits, anchor) ||
                _tooltipPresentationBorder is not { } border || _tooltipPresentationHost is not { } host) return;
            _tooltipPresentationTemplate = template;
            _tooltipPresentationContent = child;
            _suppressDrawnTooltip = true;
            // Finish Decorator.Child's setter before giving the subtree a reading
            // context or attaching it. Inherited-context callbacks can otherwise
            // clear Child midway through that setter, which then adds an orphan
            // visual after its property has already been reset to null.
            border.Child = child;
            if (!IsTooltipPresentationCurrent(revision, template, primary, hits, anchor))
            {
                DiscardStaleTooltipContent(border, child);
                return;
            }
            border.DataContext = content;
            if (!IsTooltipPresentationCurrent(revision, template, primary, hits, anchor))
            {
                DiscardStaleTooltipContent(border, child);
                return;
            }
            EnsureTooltipPresentationMembership();
            if (!IsTooltipPresentationCurrent(revision, template, primary, hits, anchor))
            {
                DiscardStaleTooltipContent(border, child);
                return;
            }
            host.SetAnchor(anchor);
            InvalidateMeasure();
            InvalidateArrange();
            RequestRender();
        }
        catch
        {
            if (_tooltipPresentationRevision == revision)
            {
                ReleaseTooltipPresentationVisual();
                _failedTooltipPresentationTemplate = template;
                _suppressDrawnTooltip = false;
            }
            throw;
        }
        finally
        {
            _synchronizingTooltipPresentation = false;
            if (_pendingTooltipPresentation) QueueTooltipPresentation();
        }
    }

    private bool IsTooltipPresentationCurrent(long revision, IDataTemplate template, ChartPointHit primary,
        IReadOnlyList<ChartPointHit> hits, Point anchor) =>
        revision == _tooltipPresentationRevision && ReferenceEquals(template, TooltipContentTemplate) &&
        ReferenceEquals(primary, HoveredPoint) && ReferenceEquals(hits, HoveredPoints) &&
        anchor == _tooltipAnchor && CanPresentTooltip();

    private static void DiscardStaleTooltipContent(Border border, Control child)
    {
        // Inherited DataContext/attachment notifications can reenter halfway through
        // Decorator.Child's setter. Clean the local obsolete container as well.
        try { if (ReferenceEquals(border.Child, child)) border.Child = null; }
        finally { border.DataContext = null; }
    }

    private void QueueTooltipPresentation()
    {
        _pendingTooltipPresentation = false;
        if (_queuedTooltipPresentation || !CanPresentTooltip() || TooltipContentTemplate is null) return;
        _queuedTooltipPresentation = true;
        Dispatcher.UIThread.Post(() =>
        {
            _queuedTooltipPresentation = false;
            if (CanPresentTooltip()) SynchronizeTooltipPresentation();
        }, DispatcherPriority.Loaded);
    }

    private void ClearTooltipPresentation()
    {
        if (_clearingTooltipPresentation) return;
        if (_tooltipPresentationHost is null && _tooltipPresentationTemplate is null &&
            _failedTooltipPresentationTemplate is null && _tooltipPresentationFallbackLines is null &&
            !_synchronizingTooltipPresentation && !_suppressDrawnTooltip) return;
        _clearingTooltipPresentation = true;
        try
        {
            _tooltipPresentationRevision++;
            _pendingTooltipPresentation = false;
            _tooltipPresentationFallbackLines = null;
            _failedTooltipPresentationTemplate = null;
            _suppressDrawnTooltip = false;
            ReleaseTooltipPresentationVisual();
        }
        finally { _clearingTooltipPresentation = false; }
    }

    private void ReleaseTooltipPresentationVisual()
    {
        var host = _tooltipPresentationHost;
        var border = _tooltipPresentationBorder;
        _tooltipPresentationHost = null;
        _tooltipPresentationBorder = null;
        _tooltipPresentationContent = null;
        _tooltipPresentationTemplate = null;
        try
        {
            if (border is not null) border.Child = null;
        }
        finally
        {
            try { if (border is not null) border.DataContext = null; }
            finally
            {
                if (host is not null)
                {
                    try { if (ReferenceEquals(host.GetVisualParent(), this)) VisualChildren.Remove(host); }
                    finally
                    {
                        try { if (ReferenceEquals(((ILogical)host).LogicalParent, this)) LogicalChildren.Remove(host); }
                        finally { host.Children.Clear(); }
                    }
                }
            }
        }
    }

    private void EnsureTooltipPresentationHost()
    {
        if (_tooltipPresentationHost is null)
        {
            var host = new TooltipPresentationHost
            {
                Name = "PART_ChartTooltipOverlay", IsHitTestVisible = false,
                Focusable = false, ClipToBounds = true, ZIndex = 30,
            };
            KeyboardNavigation.SetTabNavigation(host, KeyboardNavigationMode.None);
            var border = new Border
            {
                Name = "PART_ChartTooltip", Padding = new Thickness(12, 9),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
                ClipToBounds = true, IsHitTestVisible = false, Focusable = false,
            };
            host.Children.Add(border);
            _tooltipPresentationHost = host;
            _tooltipPresentationBorder = border;
            UpdateTooltipPresentationAppearance();
        }
    }

    private void EnsureTooltipPresentationMembership()
    {
        if (_tooltipPresentationHost is not { } host) return;
        var logicalParent = ((ILogical)host).LogicalParent;
        var visualParent = host.GetVisualParent();
        if (logicalParent is not null && !ReferenceEquals(logicalParent, this) ||
            visualParent is not null && !ReferenceEquals(visualParent, this))
            throw new InvalidOperationException("The tooltip overlay already has another parent.");
        if (logicalParent is null) LogicalChildren.Add(host);
        if (!ReferenceEquals(_tooltipPresentationHost, host)) return;
        if (visualParent is null) VisualChildren.Add(host);
    }

    private void UpdateTooltipPresentationAppearance()
    {
        if (_tooltipPresentationBorder is not { } border) return;
        border.Background = TooltipBackground;
        border.BorderBrush = TooltipBorderBrush;
        TextElement.SetForeground(border, TooltipForeground ?? Foreground ?? Brushes.Black);
        TextElement.SetFontFamily(border, FontFamily);
        TextElement.SetFontSize(border, FontSize);
        TextElement.SetFontStyle(border, FontStyle);
        TextElement.SetFontWeight(border, FontWeight);
    }

    private void PresentationAttached()
    {
        PlotAreaContentAttached();
        if (TooltipContentTemplate is not null && CanPresentTooltip()) SynchronizeTooltipPresentation();
    }

    private void PresentationDetached() { PlotAreaContentDetached(); ClearTooltipPresentation(); ClearTrackballPresentation(); }
    private void PresentationTemplateApplied() { EnsurePlotAreaContentMembership(); EnsureTooltipPresentationMembership(); EnsureTrackballPresentationMembership(); }

    private void MeasurePresentation(Size availableChartSize)
    {
        MeasurePlotAreaContent(availableChartSize);
        if (_tooltipPresentationHost is not { } host) return;
        try
        {
            EnsureTooltipPresentationMembership();
            host.Measure(availableChartSize);
        }
        catch
        {
            _failedTooltipPresentationTemplate = TooltipContentTemplate;
            _suppressDrawnTooltip = false;
            ReleaseTooltipPresentationVisual();
            throw;
        }
    }

    private void ArrangePresentation(Size finalSize)
    {
        ArrangePlotAreaContent(finalSize);
        if (_tooltipPresentationHost is not { } host) return;
        EnsureTooltipPresentationMembership();
        host.Arrange(new Rect(finalSize));
    }

    private string[] TooltipFallbackLines(ChartScene scene)
    {
        if (TooltipContentTemplate is null) return TooltipLines(scene);
        return _tooltipPresentationFallbackRevision == _tooltipPresentationRevision
            ? _tooltipPresentationFallbackLines ?? Array.Empty<string>() : Array.Empty<string>();
    }

    private void PreparePresentationForExport(Size chartSize)
    {
        if (ExportPrepareOverride is { } prepare) { prepare(); return; }
        if (_synchronizingTooltipPresentation || _synchronizingLabelTemplates || IsSynchronizingLegendPresentation || _drawingForegroundTooltip || _synchronizingTrackballPresentation || _synchronizingPlotAreaContent)
            throw new InvalidOperationException("A chart cannot be exported while a presentation template or formatter is updating.");
        PrepareLegendPresentationForExport(chartSize);
        MeasureLabelPresentation(chartSize);
        ArrangeLabelPresentation(chartSize);
        ArrangeForegroundPresentation(chartSize);
        if (TooltipContentTemplate is not null) SynchronizeTooltipPresentation();
        MeasurePresentation(chartSize);
        ArrangePresentation(chartSize);
        if (IsTrackballActive) SynchronizeTrackballPresentation();
        MeasureTrackballPresentation(chartSize);
        ArrangeTrackballPresentation(chartSize);
    }

    private void RenderPresentationForExport(DrawingContext context)
    {
        RenderPlotAreaContentForExport(context);
        RenderLabelPresentationForExport(context);
        RenderLegendPresentationForExport(context);
        RenderForegroundPresentationForExport(context);
        RenderTrackballPresentationForExport(context);
        if (_tooltipPresentationHost is not { } host || !_suppressDrawnTooltip ||
            host.Bounds.Width <= 0 || host.Bounds.Height <= 0) return;
        // VisualBrush is public; Avalonia's internal renderer traverses this exact
        // subtree in root-local coordinates. Never draw the chart a second time.
        var brush = new VisualBrush(host) { Stretch = Stretch.Fill, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top };
        context.DrawRectangle(brush, null, host.Bounds);
    }

    private sealed class TooltipPresentationHost : Panel
    {
        private Point _anchor;
        public void SetAnchor(Point anchor)
        {
            if (_anchor == anchor) return;
            _anchor = anchor;
            InvalidateArrange();
        }
        protected override Size MeasureOverride(Size availableSize)
        {
            if (Children.Count != 0)
                Children[0].Measure(new Size(Math.Max(0, availableSize.Width - 16), Math.Max(0, availableSize.Height - 16)));
            return default;
        }
        protected override Size ArrangeOverride(Size finalSize)
        {
            if (Children.Count == 0) return finalSize;
            var child = Children[0];
            var width = Math.Min(child.DesiredSize.Width, Math.Max(0, finalSize.Width - 16));
            var height = Math.Min(child.DesiredSize.Height, Math.Max(0, finalSize.Height - 16));
            var x = _anchor.X + 14; var y = _anchor.Y + 16;
            if (x + width > finalSize.Width - 8) x = _anchor.X - width - 14;
            if (y + height > finalSize.Height - 8) y = _anchor.Y - height - 12;
            x = Math.Clamp(x, 8, Math.Max(8, finalSize.Width - width - 8));
            y = Math.Clamp(y, 8, Math.Max(8, finalSize.Height - height - 8));
            child.Arrange(new Rect(x, y, width, height));
            return finalSize;
        }
    }
}
