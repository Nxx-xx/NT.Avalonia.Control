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

internal sealed record ChartDataPlotAreaReading(ChartDataPlotAreaContext Context, long Version, Func<bool> IsCurrent);

/// <summary>One reusable passive plot-content control, laid out before rendering and included in permanent exports.</summary>
internal sealed class ChartDataPlotAreaPresenter(ChartBase owner, Func<object?> content, Func<IDataTemplate?> template,
    Func<ChartDataPlotAreaReading?> read, Action refresh)
{
    private PlotHost? _host;
    private Border? _border;
    private Control? _child;
    private object? _builtContent, _failedContent;
    private IDataTemplate? _builtTemplate, _failedTemplate;
    private readonly List<Visual> _visibilityOwners = [];
    private long _revision, _readingVersion = -1;
    private bool _busy, _clearing, _queued, _pending, _failed;
    private int _layoutDepth;
    internal bool IsBusy => _busy || _clearing || _layoutDepth != 0;
    internal bool HasPresentation => content() is Control || content() is not null && template() is not null;

    internal void Attached()
    {
        Unobserve();
        foreach (var visual in owner.GetVisualAncestors().Prepend(owner))
        { _visibilityOwners.Add(visual); visual.PropertyChanged += VisibilityChanged; }
        Queue();
    }
    private void VisibilityChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property != Visual.IsVisibleProperty) return;
        if (!owner.IsEffectivelyVisible || _visibilityOwners.Any(visual => !visual.IsVisible)) Invalidate(release: true, queue: false);
        else Queue();
    }
    private void Unobserve()
    { foreach (var visual in _visibilityOwners) visual.PropertyChanged -= VisibilityChanged; _visibilityOwners.Clear(); }
    internal void Detached()
    { Unobserve(); _pending = false; ResetFailure(); Invalidate(release: true, queue: false); }
    internal void ContentChanged() { ResetFailure(); Invalidate(release: true); }
    private void ResetFailure() { _failed = false; _failedContent = null; _failedTemplate = null; }
    internal void Invalidate(bool release = false, bool queue = true)
    {
        ++_revision; _readingVersion = -1;
        if (release) Release();
        else
        {
            if (_border is { } border) border.DataContext = null;
            if (_host is { } host) host.IsVisible = false;
        }
        if (IsBusy) _pending = true;
        if (queue) Queue();
    }
    internal void RequestRefresh() => Queue();
    private void Queue()
    {
        _pending = false;
        if (_queued || !HasPresentation) return;
        _queued = true;
        Dispatcher.UIThread.Post(() => { _queued = false; refresh(); }, DispatcherPriority.Loaded);
    }

    internal void Synchronize()
    {
        if (IsBusy) { _pending = true; return; }
        var currentContent = content(); var currentTemplate = template();
        if (currentContent is null || currentTemplate is null && currentContent is not Control)
        { Invalidate(release: true, queue: false); return; }
        if (_failed && ReferenceEquals(_failedContent, currentContent) && ReferenceEquals(_failedTemplate, currentTemplate)) return;
        var revision = ++_revision;
        _busy = true;
        Control? builtChild = null;
        var readCompleted = false;
        try
        {
            var reading = read();
            readCompleted = true;
            if (reading is null) { Release(); return; }
            if (!Current(revision, currentContent, currentTemplate, reading)) return;
            var same = ReferenceEquals(_builtContent, currentContent) && ReferenceEquals(_builtTemplate, currentTemplate);
            if (same && _readingVersion == reading.Version) return;
            // A null Build is retained for this reading only; a new frame can make a conditional template visible.
            if (!same || _child is null)
            {
                Release();
                builtChild = currentTemplate is null ? (Control)currentContent : currentTemplate.Build(reading.Context);
                if (!Current(revision, currentContent, currentTemplate, reading))
                { if (currentTemplate is not null) ReleaseUnparented(builtChild); return; }
                _builtContent = currentContent; _builtTemplate = currentTemplate;
                if (builtChild is null) { _readingVersion = reading.Version; return; }
                if (((ILogical)builtChild).LogicalParent is not null || builtChild.GetVisualParent() is not null)
                    throw new InvalidOperationException("Plot area content must be an unparented control.");
                var host = new PlotHost
                {
                    Name = "PART_ChartDataPlotAreaContentOverlay", IsHitTestVisible = false, Focusable = false,
                    ClipToBounds = true, UseLayoutRounding = false, ZIndex = 10,
                };
                KeyboardNavigation.SetTabNavigation(host, KeyboardNavigationMode.None);
                var border = new Border
                {
                    Name = "PART_ChartDataPlotAreaContent", IsHitTestVisible = false, Focusable = false,
                    ClipToBounds = true, UseLayoutRounding = false,
                };
                KeyboardNavigation.SetTabNavigation(border, KeyboardNavigationMode.None);
                host.Children.Add(border); _host = host; _border = border; _child = builtChild;
                border.Child = builtChild;
                if (!Current(revision, currentContent, currentTemplate, reading)) return;
            }
            if (_host is not { } existingHost || _border is not { } existingBorder) return;
            if (currentTemplate is not null) existingBorder.DataContext = reading.Context;
            else existingBorder.ClearValue(StyledElement.DataContextProperty);
            if (!Current(revision, currentContent, currentTemplate, reading)) return;
            ApplyTextStyle(existingBorder);
            existingHost.SetPlot(reading.Context.PlotArea);
            EnsureMembership();
            if (!Current(revision, currentContent, currentTemplate, reading)) return;
            existingHost.IsVisible = true; _readingVersion = reading.Version;
            owner.InvalidateMeasure(); owner.InvalidateArrange(); owner.InvalidateVisual();
        }
        catch
        {
            if (revision == _revision)
            {
                Release();
                // Source/projection failures belong to the chart's data lifecycle. Only presentation failures quarantine this slot.
                if (readCompleted) { _failed = true; _failedContent = currentContent; _failedTemplate = currentTemplate; }
            }
            throw;
        }
        finally
        {
            _busy = false;
            if (revision != _revision) { Release(); _pending = true; }
            if (_pending) Queue();
        }
    }
    private bool Current(long revision, object currentContent, IDataTemplate? currentTemplate, ChartDataPlotAreaReading reading) =>
        revision == _revision && ReferenceEquals(currentContent, content()) && ReferenceEquals(currentTemplate, template()) && reading.IsCurrent();
    private static void ReleaseUnparented(Control? child)
    { if (child is not null && ((ILogical)child).LogicalParent is null && child.GetVisualParent() is null) child.ClearValue(StyledElement.DataContextProperty); }
    private void ApplyTextStyle(Control item)
    {
        TextElement.SetForeground(item, owner.Foreground ?? Brushes.Gray);
        TextElement.SetFontFamily(item, owner.FontFamily); TextElement.SetFontSize(item, owner.FontSize);
        TextElement.SetFontStyle(item, owner.FontStyle); TextElement.SetFontWeight(item, owner.FontWeight);
    }
    internal void EnsureMembership()
    { if (_host is { } host) owner.AttachDataTooltipOverlay(host, () => ReferenceEquals(_host, host)); }
    private void Release()
    {
        if (_clearing) return;
        _clearing = true;
        var host = _host; var border = _border;
        _host = null; _border = null; _child = null; _builtContent = null; _builtTemplate = null; _readingVersion = -1;
        try
        {
            try { if (border is not null) border.Child = null; }
            finally
            {
                try { border?.ClearValue(StyledElement.DataContextProperty); }
                finally
                {
                    try { if (host is not null) owner.DetachDataTooltipOverlay(host); }
                    finally { host?.Children.Clear(); }
                }
            }
        }
        finally { _clearing = false; }
    }
    private void Layout(Action<PlotHost> action)
    {
        if (_host is not { IsVisible: true } host) return;
        if (IsBusy) { _pending = true; return; }
        var revision = _revision; _layoutDepth++;
        try { action(host); }
        catch
        {
            if (revision == _revision)
            { var c = _builtContent; var t = _builtTemplate; Release(); _failed = true; _failedContent = c; _failedTemplate = t; }
            throw;
        }
        finally
        {
            _layoutDepth--;
            if (revision != _revision) { Release(); _pending = true; }
            if (_pending) Queue();
        }
    }
    internal void Measure(Size size) => Layout(host => host.Measure(size));
    internal void Arrange(Size size) => Layout(host => { EnsureMembership(); if (ReferenceEquals(host, _host)) host.Arrange(new Rect(size)); });
    internal void RenderExport(DrawingContext context) => Layout(host =>
    {
        if (host.Bounds.Width <= 0 || host.Bounds.Height <= 0) return;
        var brush = new VisualBrush(host) { Stretch = Stretch.Fill, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top };
        context.DrawRectangle(brush, null, host.Bounds);
    });
    private sealed class PlotHost : Panel
    {
        private Rect _plot;
        internal void SetPlot(Rect plot)
        { if (_plot == plot) return; _plot = plot; InvalidateMeasure(); InvalidateArrange(); }
        protected override Size MeasureOverride(Size availableSize)
        { if (Children.Count > 0) Children[0].Measure(_plot.Size); return default; }
        protected override Size ArrangeOverride(Size finalSize)
        { if (Children.Count > 0) Children[0].Arrange(_plot); return finalSize; }
    }
}
