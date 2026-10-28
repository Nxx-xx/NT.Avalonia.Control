using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.Charts;

internal sealed record ChartDataTooltipReading(ChartDataTooltipContext Context, object Identity, long Version, Func<bool> IsCurrent);

/// <summary>Passive bounded live control overlay shared by the professional chart families.</summary>
internal sealed class ChartDataTooltipPresenter
{
    private readonly ChartBase owner;
    private readonly Func<IDataTemplate?> template;
    private readonly Func<ChartDataTooltipReading?> read;
    private readonly Action refresh;
    private readonly Func<(IBrush? Background, IBrush? Foreground, IBrush? Border)> brushes;
    private TooltipHost? _host;
    private Border? _border;
    private Control? _child;
    private IDataTemplate? _builtTemplate, _failedTemplate;
    private long _revision;
    private object? _readingIdentity, _readingBusinessContext;
    private long _readingVersion;
    private int _layoutDepth;
    private readonly List<Visual> _visibilityOwners = [];
    private bool _busy, _clearing, _pending, _queued;
    internal bool IsBusy => _busy || _clearing || _layoutDepth != 0;
    internal bool SuppressLegacy { get; private set; }

    internal ChartDataTooltipPresenter(ChartBase owner, Func<IDataTemplate?> template, Func<ChartDataTooltipReading?> read,
        Action refresh, Func<(IBrush? Background, IBrush? Foreground, IBrush? Border)> brushes)
    {
        this.owner = owner; this.template = template; this.read = read; this.refresh = refresh; this.brushes = brushes;
        if (owner.IsAttachedToVisualTree()) Attached();
    }

    internal void Attached()
    {
        UnobserveVisibility();
        foreach (var visual in owner.GetVisualAncestors().Prepend(owner))
        { _visibilityOwners.Add(visual); visual.PropertyChanged += VisibilityChanged; }
        Queue();
    }

    private void VisibilityChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property != Visual.IsVisibleProperty) return;
        InvalidateReading(queue: false);
        if (owner.IsEffectivelyVisible && owner.IsEffectivelyEnabled && _visibilityOwners.All(visual => visual.IsVisible)) Queue();
    }

    private void UnobserveVisibility()
    {
        foreach (var visual in _visibilityOwners) visual.PropertyChanged -= VisibilityChanged;
        _visibilityOwners.Clear();
    }

    internal void TemplateChanged()
    {
        _failedTemplate = null; InvalidateReading(queue: false); Synchronize();
        // A first template may be assigned after an unpresented frame was dirtied.
        // Equal analytical hits need not notify ActivePoint again after rebuilding.
        Queue();
    }

    internal void InvalidateReading(bool queue = true)
    {
        ++_revision; SuppressLegacy = false;
        Release();
        if (_busy) _pending = true;
        if (queue) Queue();
    }

    internal void Detached()
    {
        UnobserveVisibility(); _pending = false; _failedTemplate = null; InvalidateReading(queue: false);
    }

    internal void Synchronize()
    {
        if (IsBusy) { ++_revision; _pending = true; return; }
        var currentTemplate = template(); var reading = currentTemplate is null ? null : read();
        if (currentTemplate is null || reading is null)
        {
            InvalidateReading(queue: false); return;
        }
        if (ReferenceEquals(_failedTemplate, currentTemplate)) { SuppressLegacy = false; return; }
        if (ReferenceEquals(_builtTemplate, currentTemplate) && SuppressLegacy && ReferenceEquals(_readingIdentity, reading.Identity) &&
            _readingVersion == reading.Version && ReferenceEquals(_readingBusinessContext, reading.Context.BusinessDataContext)) return;
        var version = ++_revision;
        _busy = true;
        try
        {
            if (ReferenceEquals(_builtTemplate, currentTemplate) && _child is { } existing && _border is { } existingBorder)
            {
                existingBorder.DataContext = reading.Context;
                if (!Current(version, currentTemplate, reading)) { DiscardCurrent(existingBorder, existing); return; }
                _host!.SetAnchor(reading.Context.Anchor); SuppressLegacy = true;
                Remember(reading);
                owner.InvalidateMeasure(); owner.InvalidateArrange(); return;
            }
            var child = currentTemplate.Build(reading.Context);
            if (!Current(version, currentTemplate, reading)) return;
            if (child is null)
            {
                Release();
                if (!Current(version, currentTemplate, reading)) return;
                _builtTemplate = currentTemplate; SuppressLegacy = true; Remember(reading); owner.InvalidateVisual(); return;
            }
            if (((ILogical)child).LogicalParent is not null || child.GetVisualParent() is not null)
                throw new InvalidOperationException("A chart tooltip template must return an unparented control.");
            EnsureHost();
            if (!Current(version, currentTemplate, reading) || _border is not { } border || _host is not { } host) return;
            _child = child; _builtTemplate = currentTemplate; SuppressLegacy = true;
            // Finish Child before giving the subtree a reading context or attaching it.
            // Inherited-context callbacks must not reset Decorator.Child mid-setter.
            border.Child = child;
            if (!Current(version, currentTemplate, reading)) { DiscardCurrent(border, child); return; }
            border.DataContext = reading.Context;
            if (!Current(version, currentTemplate, reading)) { DiscardCurrent(border, child); return; }
            owner.AttachDataTooltipOverlay(host, () => ReferenceEquals(_host, host));
            if (!Current(version, currentTemplate, reading)) { DiscardCurrent(border, child); return; }
            host.SetAnchor(reading.Context.Anchor);
            Remember(reading);
            owner.InvalidateMeasure(); owner.InvalidateArrange(); owner.InvalidateVisual();
        }
        catch
        {
            if (version == _revision)
            {
                Release(); _failedTemplate = currentTemplate; SuppressLegacy = false;
            }
            throw;
        }
        finally { _busy = false; if (_pending) Queue(); }
    }

    private bool Current(long version, IDataTemplate currentTemplate, ChartDataTooltipReading reading) =>
        version == _revision && ReferenceEquals(currentTemplate, template()) && reading.IsCurrent();

    private void Remember(ChartDataTooltipReading reading)
    { _readingIdentity = reading.Identity; _readingVersion = reading.Version; _readingBusinessContext = reading.Context.BusinessDataContext; }

    internal void RequestRefresh() => Queue();

    private void Queue()
    {
        _pending = false;
        if (_queued || template() is null) return;
        _queued = true;
        Dispatcher.UIThread.Post(() => { _queued = false; refresh(); }, DispatcherPriority.Loaded);
    }

    private static void Discard(Border border, Control child)
    {
        try { if (ReferenceEquals(border.Child, child)) border.Child = null; }
        finally { border.DataContext = null; }
    }

    private void DiscardCurrent(Border border, Control child)
    {
        try { Discard(border, child); }
        finally
        {
            if (ReferenceEquals(_border, border) && ReferenceEquals(_child, child))
            { SuppressLegacy = false; Release(); }
        }
    }

    private void Release()
    {
        if (_clearing) return;
        _clearing = true;
        var host = _host; var border = _border;
        _host = null; _border = null; _child = null; _builtTemplate = null;
        _readingIdentity = null; _readingBusinessContext = null; _readingVersion = 0;
        try
        {
            try { if (border is not null) border.Child = null; }
            finally
            {
                try { if (border is not null) border.DataContext = null; }
                finally
                {
                    if (host is not null)
                    {
                        try { owner.DetachDataTooltipOverlay(host); }
                        finally { host.Children.Clear(); }
                    }
                }
            }
        }
        finally { _clearing = false; if (_pending && !_busy && _layoutDepth == 0) Queue(); }
    }

    private void EnsureHost()
    {
        if (_host is not null) return;
        var host = new TooltipHost { Name = "PART_ChartDataTooltipOverlay", IsHitTestVisible = false, Focusable = false, ClipToBounds = true, ZIndex = 30 };
        KeyboardNavigation.SetTabNavigation(host, KeyboardNavigationMode.None);
        var border = new Border
        {
            Name = "PART_ChartDataTooltip", Padding = new Thickness(10, 8), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5), ClipToBounds = true, IsHitTestVisible = false, Focusable = false,
        };
        host.Children.Add(border); _host = host; _border = border; UpdateAppearance();
    }

    internal void UpdateAppearance()
    {
        if (_border is not { } border) return;
        var appearance = brushes();
        var dark = owner.HighContrastMode == ChartHighContrastMode.Dark || owner.HighContrastMode == ChartHighContrastMode.None && owner.ActualThemeVariant == ThemeVariant.Dark;
        border.Background = appearance.Background ?? (dark ? Brushes.Black : Brushes.White);
        border.BorderBrush = appearance.Border ?? owner.Foreground ?? (dark ? Brushes.White : Brushes.Black);
        TextElement.SetForeground(border, appearance.Foreground ?? owner.Foreground ?? (dark ? Brushes.White : Brushes.Black));
        TextElement.SetFontFamily(border, owner.FontFamily); TextElement.SetFontSize(border, owner.FontSize);
        TextElement.SetFontStyle(border, owner.FontStyle); TextElement.SetFontWeight(border, owner.FontWeight);
        owner.InvalidateMeasure(); owner.InvalidateVisual();
    }

    internal void EnsureMembership()
    {
        if (_host is { } host) owner.AttachDataTooltipOverlay(host, () => ReferenceEquals(_host, host));
    }
    internal void Measure(Size size)
    {
        if (_host is not { } host) return;
        var version = _revision; var currentTemplate = _builtTemplate;
        _layoutDepth++;
        try { EnsureMembership(); host.Measure(size); }
        catch
        {
            if (version == _revision && ReferenceEquals(_host, host)) { _failedTemplate = currentTemplate; SuppressLegacy = false; Release(); }
            throw;
        }
        finally { _layoutDepth--; if (_pending) Queue(); }
    }
    internal void Arrange(Size size)
    {
        if (_host is not { } host) return;
        var version = _revision; var currentTemplate = _builtTemplate;
        _layoutDepth++;
        try { EnsureMembership(); host.Arrange(new Rect(size)); }
        catch
        {
            if (version == _revision && ReferenceEquals(_host, host)) { _failedTemplate = currentTemplate; SuppressLegacy = false; Release(); }
            throw;
        }
        finally { _layoutDepth--; if (_pending) Queue(); }
    }

    private sealed class TooltipHost : Panel
    {
        private Point _anchor;
        internal void SetAnchor(Point anchor) { if (_anchor == anchor) return; _anchor = anchor; InvalidateArrange(); }
        protected override Size MeasureOverride(Size availableSize)
        {
            if (Children.Count > 0) Children[0].Measure(new(Math.Max(0, availableSize.Width - 16), Math.Max(0, availableSize.Height - 16)));
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
            x = Math.Clamp(x, 8, Math.Max(8, finalSize.Width - width - 8)); y = Math.Clamp(y, 8, Math.Max(8, finalSize.Height - height - 8));
            child.Arrange(new(x, y, width, height)); return finalSize;
        }
    }
}
