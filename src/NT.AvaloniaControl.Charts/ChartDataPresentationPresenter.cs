using System.Runtime.ExceptionServices;
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

internal sealed record ChartDataPresentationSlot(object Context, Point Anchor, Rect Bounds, Geometry? Clip = null);
internal sealed record ChartDataPresentationReading(IReadOnlyList<ChartDataPresentationSlot> Slots, long Version, Rect Area, Func<bool> IsCurrent);

/// <summary>Bounded passive controls shared by permanent professional legends and labels.</summary>
internal sealed class ChartDataPresentationPresenter(ChartBase owner, bool legend, Func<IDataTemplate?> template,
    Func<ChartDataPresentationReading?> read, Action refresh)
{
    private PresentationHost? _host;
    private readonly List<Entry> _entries = [];
    private readonly List<Visual> _visibilityOwners = [];
    private IDataTemplate? _builtTemplate, _failedTemplate;
    private long _revision, _readingVersion = -1;
    private bool _busy, _clearing, _pending, _queued;
    private int _layoutDepth;
    internal bool IsBusy => _busy || _clearing || _layoutDepth != 0;

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
    { Unobserve(); _pending = false; _failedTemplate = null; Invalidate(release: true, queue: false); }

    internal void TemplateChanged()
    { _failedTemplate = null; Invalidate(release: true); }

    internal void Invalidate(bool release = false, bool queue = true)
    {
        ++_revision; _readingVersion = -1;
        if (release) Release();
        else
        {
            // Keep reusable controls, but release obsolete business objects immediately.
            foreach (var entry in _entries.ToArray())
                if (entry.Border is { } border) border.DataContext = null;
            if (_host is { } host) host.Slots.Clear();
        }
        if (_busy || _layoutDepth != 0) _pending = true;
        if (queue) Queue();
    }

    internal void RequestRefresh() => Queue();
    private void Queue()
    {
        _pending = false;
        if (_queued || template() is null) return;
        _queued = true;
        Dispatcher.UIThread.Post(() => { _queued = false; refresh(); }, DispatcherPriority.Loaded);
    }

    internal void Synchronize()
    {
        if (IsBusy) { _pending = true; return; }
        var currentTemplate = template(); var reading = currentTemplate is null ? null : read();
        if (currentTemplate is null || reading is null)
        { Invalidate(release: true, queue: false); return; }
        if (ReferenceEquals(_failedTemplate, currentTemplate)) return;
        if (ReferenceEquals(_builtTemplate, currentTemplate) && _readingVersion == reading.Version) return;
        var revision = ++_revision;
        _busy = true;
        try
        {
            if (!ReferenceEquals(_builtTemplate, currentTemplate)) Release();
            _builtTemplate = currentTemplate;
            EnsureHost();
            if (_host is not { } host) return;
            host.Area = reading.Area; host.Slots.Clear();
            for (var index = 0; index < reading.Slots.Count; index++)
            {
                var slot = reading.Slots[index];
                Entry entry;
                if (index < _entries.Count && _entries[index].Border is not null) entry = _entries[index];
                else
                {
                    var child = currentTemplate.Build(slot.Context);
                    if (!Current(revision, currentTemplate, reading)) { ReleaseUnparented(child); return; }
                    if (child is null) { entry = new(null, null); if (index < _entries.Count) _entries[index] = entry; else _entries.Add(entry); continue; }
                    if (((ILogical)child).LogicalParent is not null || child.GetVisualParent() is not null)
                        throw new InvalidOperationException("A chart presentation template must return an unparented control.");
                    var border = new Border
                    {
                        Name = legend ? "PART_ChartDataLegendItem" : "PART_ChartDataLabel", Child = child,
                        IsHitTestVisible = false, Focusable = false, ClipToBounds = true,
                    };
                    KeyboardNavigation.SetTabNavigation(border, KeyboardNavigationMode.None);
                    var layer = new ItemLayer(border) { IsHitTestVisible = false, Focusable = false };
                    entry = new(border, layer); if (index < _entries.Count) _entries[index] = entry; else _entries.Add(entry);
                    host.Children.Add(layer);
                    if (!Current(revision, currentTemplate, reading)) return;
                }
                if (entry.Border is not { } item) continue;
                item.DataContext = slot.Context;
                if (!Current(revision, currentTemplate, reading)) return;
                ApplyTextStyle(item);
                entry.Layer!.Clip = slot.Clip ?? new RectangleGeometry(reading.Area);
                host.Slots.Add((entry.Layer, slot));
            }
            while (_entries.Count > reading.Slots.Count)
            { var entry = _entries[^1]; _entries.RemoveAt(_entries.Count - 1); Discard(entry); }
            if (!Current(revision, currentTemplate, reading)) return;
            _readingVersion = reading.Version;
            EnsureMembership();
            if (!Current(revision, currentTemplate, reading)) return;
            host.InvalidateMeasure(); host.InvalidateArrange(); owner.InvalidateMeasure(); owner.InvalidateArrange(); owner.InvalidateVisual();
        }
        catch
        {
            if (revision == _revision) { Release(); _failedTemplate = currentTemplate; }
            throw;
        }
        finally
        {
            _busy = false;
            if (revision != _revision) { Release(); _pending = true; }
            if (_pending) Queue();
        }
    }

    private bool Current(long revision, IDataTemplate currentTemplate, ChartDataPresentationReading reading) =>
        revision == _revision && ReferenceEquals(currentTemplate, template()) && reading.IsCurrent();

    private static void ReleaseUnparented(Control? child)
    { if (child is not null && ((ILogical)child).LogicalParent is null && child.GetVisualParent() is null) child.DataContext = null; }

    private void EnsureHost()
    {
        if (_host is not null) return;
        _host = new PresentationHost(legend)
        {
            Name = legend ? "PART_ChartDataLegendOverlay" : "PART_ChartDataLabelsOverlay",
            IsHitTestVisible = false, Focusable = false, ClipToBounds = true, ZIndex = legend ? 25 : 20,
        };
        KeyboardNavigation.SetTabNavigation(_host, KeyboardNavigationMode.None);
    }

    internal void EnsureMembership()
    { if (_host is { } host) owner.AttachDataTooltipOverlay(host, () => ReferenceEquals(_host, host)); }

    private void ApplyTextStyle(Control item)
    {
        TextElement.SetForeground(item, owner.Foreground ?? Brushes.Gray);
        TextElement.SetFontFamily(item, owner.FontFamily); TextElement.SetFontSize(item, owner.FontSize);
        TextElement.SetFontStyle(item, owner.FontStyle); TextElement.SetFontWeight(item, owner.FontWeight);
    }

    private static void Discard(Entry entry)
    {
        if (entry.Border is not { } border) return;
        try
        {
            if (entry.Layer is { } layer && layer.GetVisualParent() is Panel parent) parent.Children.Remove(layer);
            if (border.GetVisualParent() is Panel container) container.Children.Remove(border);
        }
        finally { try { border.Child = null; } finally { border.DataContext = null; border.Clip = null; } }
    }

    private void Release()
    {
        if (_clearing) return;
        _clearing = true;
        var host = _host; var entries = _entries.ToArray();
        _host = null; _entries.Clear(); _builtTemplate = null; _readingVersion = -1;
        ExceptionDispatchInfo? failure = null;
        void Clean(Action action) { try { action(); } catch (Exception error) { failure ??= ExceptionDispatchInfo.Capture(error); } }
        try
        {
            if (host is not null) { host.Slots.Clear(); Clean(() => owner.DetachDataTooltipOverlay(host)); }
            foreach (var entry in entries) Clean(() => Discard(entry));
            if (host is not null) Clean(host.Children.Clear);
        }
        finally { _clearing = false; }
        failure?.Throw();
    }

    internal void Measure(Size size) => Layout(size, false);
    internal void Arrange(Size size) => Layout(size, true);
    private void Layout(Size size, bool arrange)
    {
        if (_host is not { } host) return;
        var revision = _revision; var currentTemplate = _builtTemplate;
        _layoutDepth++;
        try { EnsureMembership(); if (arrange) host.Arrange(new Rect(size)); else host.Measure(size); }
        catch
        {
            if (revision == _revision && ReferenceEquals(_host, host)) { Release(); _failedTemplate = currentTemplate; }
            throw;
        }
        finally { _layoutDepth--; if (_pending) Queue(); }
    }

    internal void RenderExport(DrawingContext context)
    {
        if (_host is not { Bounds.Width: > 0, Bounds.Height: > 0 } host) return;
        context.DrawRectangle(new VisualBrush(host) { Stretch = Stretch.Fill, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top }, null, host.Bounds);
    }

    private sealed record Entry(Border? Border, ItemLayer? Layer);
    private sealed class ItemLayer : Panel
    {
        private Rect _placement;
        public Rect Placement
        {
            get => _placement;
            set { if (_placement == value) return; _placement = value; InvalidateArrange(); }
        }
        internal ItemLayer(Border border) { Children.Add(border); }
        protected override Size MeasureOverride(Size availableSize)
        { if (Children.Count == 0) return default; var child = Children[0]; child.Measure(availableSize); return child.DesiredSize; }
        protected override Size ArrangeOverride(Size finalSize)
        { if (Children.Count > 0) Children[0].Arrange(Placement); return finalSize; }
    }
    private sealed class PresentationHost(bool legend) : Panel
    {
        public Rect Area { get; set; }
        public List<(ItemLayer Control, ChartDataPresentationSlot Slot)> Slots { get; } = [];
        protected override Size MeasureOverride(Size availableSize)
        {
            var area = Area.Intersect(new Rect(availableSize));
            foreach (var (control, slot) in Slots.ToArray())
            {
                var width = legend ? area.Width : slot.Bounds.Width > 0 ? Math.Min(area.Width, slot.Bounds.Width) : area.Width;
                control.Measure(new(Math.Max(0, width), Math.Max(0, area.Height)));
            }
            return default;
        }
        protected override Size ArrangeOverride(Size finalSize)
        {
            var area = Area.Intersect(new Rect(finalSize));
            var x = area.Left; var y = area.Top; var rowHeight = 0d;
            foreach (var (control, slot) in Slots.ToArray())
            {
                var width = Math.Min(area.Width, control.DesiredSize.Width); var height = Math.Min(area.Height, control.DesiredSize.Height);
                Rect bounds;
                if (legend)
                {
                    if (x > area.Left && x + width > area.Right) { x = area.Left; y += rowHeight + 4; rowHeight = 0; }
                    bounds = new(x, y, width, Math.Max(0, Math.Min(height, area.Bottom - y)));
                    x += width + 8; rowHeight = Math.Max(rowHeight, height);
                }
                else bounds = new(Math.Clamp(slot.Anchor.X - width / 2, area.Left, Math.Max(area.Left, area.Right - width)),
                    Math.Clamp(slot.Anchor.Y - height / 2, area.Top, Math.Max(area.Top, area.Bottom - height)), width, height);
                control.Placement = bounds; control.Arrange(new Rect(finalSize));
            }
            return finalSize;
        }
    }
}
