using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Runtime.ExceptionServices;

namespace NT.AvaloniaControl.Charts;

public sealed partial class CartesianChart
{
    private TrackballPresentationHost? _trackballPresentationHost;
    private Dictionary<TrackballCardKey, TrackballCard>? _trackballCards;
    private bool _synchronizingTrackballPresentation;
    private bool _clearingTrackballPresentation;
    private bool _pendingTrackballPresentation;
    private bool _queuedTrackballPresentation;
    private bool _failedTrackballPresentation;
    private IDataTemplate? _failedTrackballTemplate;
    private long _trackballPresentationRevision;

    private bool CanPresentTrackball => !_clearingTrackballPresentation && IsTrackballActive && IsVisible &&
        (_attached || _exportInProgress) && TrackballReadings.Count > 0 && _hoveredPoint is not null &&
        _panPointer is null && _selectionPointer is null;

    private void ResetTrackballPresentationFailure()
    { _failedTrackballPresentation = false; _failedTrackballTemplate = null; }

    private void SynchronizeTrackballPresentation()
    {
        if (_synchronizingTrackballPresentation) { _pendingTrackballPresentation = true; return; }
        if (!CanPresentTrackball) { ClearTrackballPresentation(); return; }
        var template = TrackballContentTemplate;
        if (_failedTrackballPresentation && ReferenceEquals(template, _failedTrackballTemplate)) return;
        var scene = _scene;
        if (scene is null) return;
        var readingRevision = _readingsRevision;
        var presentationRevision = ++_trackballPresentationRevision;
        var readings = TrackballReadings;
        bool Current() => presentationRevision == _trackballPresentationRevision && CanPresentTrackball &&
            ReferenceEquals(readings, TrackballReadings) && ReferenceEquals(template, TrackballContentTemplate) &&
            ReadingStateIsCurrent(readingRevision, scene);
        _synchronizingTrackballPresentation = true;
        try
        {
            var host = _trackballPresentationHost ??= new TrackballPresentationHost
            { Name = "PART_ChartTrackballOverlay", IsHitTestVisible = false, Focusable = false, ClipToBounds = true, ZIndex = 31 };
            KeyboardNavigation.SetTabNavigation(host, KeyboardNavigationMode.None);
            var cards = _trackballCards ??= [];
            var groups = TrackballDisplayMode == ChartTrackballDisplayMode.GroupAllPoints
                ? new[] { readings }
                : readings.Select(reading => (IReadOnlyList<ChartTrackballReading>)Array.AsReadOnly(new[] { reading })).ToArray();
            var retained = new HashSet<TrackballCardKey>();
            var order = new List<Border>();
            host.Anchors.Clear();
            foreach (var group in groups)
            {
                var grouped = TrackballDisplayMode == ChartTrackballDisplayMode.GroupAllPoints;
                var key = new TrackballCardKey(grouped, grouped ? null : group[0].Series);
                var primary = group.MinBy(reading => Square(reading.Position.X - _tooltipAnchor.X) + Square(reading.Position.Y - _tooltipAnchor.Y))!;
                var formatted = TrackballFormatter is { } formatter ? formatter(group) : string.Join("\n", group.Select(reading => FormatTrackballReading(scene, reading)));
                if (!Current()) return;
                if (formatted is null && template is null) continue;
                var hits = Array.AsReadOnly(group.Select(reading => reading.Original).ToArray());
                var segments = Array.AsReadOnly(group.Select(reading => reading.Segment).OfType<ChartSegmentHit>().ToArray());
                var anchor = grouped ? _tooltipAnchor : primary.Position;
                var context = new ChartTooltipContext(primary.Original, hits, formatted ?? string.Empty, anchor)
                { Readings = group, Segments = segments, PrimarySegment = primary.Segment };
                if (cards.TryGetValue(key, out var card) && !ReferenceEquals(card.Template, template))
                { ReleaseTrackballCard(key, card); card = null; }
                if (card is null)
                {
                    var child = template is null ? new TextBlock { TextWrapping = TextWrapping.Wrap } : template.Build(context);
                    if (!Current()) return;
                    if (child is not null && (((ILogical)child).LogicalParent is not null || child.GetVisualParent() is not null))
                        throw new InvalidOperationException("A trackball template must return an unparented control.");
                    var border = child is null ? null : new Border
                    {
                        Name = "PART_ChartTrackballCard", Padding = new Thickness(12, 9), BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(6), IsHitTestVisible = false, Focusable = false, ClipToBounds = true,
                    };
                    card = new(template, border);
                    cards.Add(key, card);
                    if (border is not null)
                    {
                        border.Child = child;
                        if (!Current()) { DiscardTrackballBorder(border); return; }
                        KeyboardNavigation.SetTabNavigation(border, KeyboardNavigationMode.None);
                    }
                }
                retained.Add(key);
                if (card.Border is not { } container) continue;
                container.DataContext = context;
                if (!Current()) return;
                if (template is null && container.Child is TextBlock text) text.Text = context.Text;
                if (!Current()) return;
                container.Background = TooltipBackground;
                if (!Current()) return;
                container.BorderBrush = TooltipBorderBrush;
                if (!Current()) return;
                TextElement.SetForeground(container, TooltipForeground ?? Foreground ?? Brushes.Black);
                if (!Current()) return;
                TextElement.SetFontFamily(container, FontFamily);
                if (!Current()) return;
                TextElement.SetFontSize(container, FontSize);
                if (!Current()) return;
                TextElement.SetFontStyle(container, FontStyle);
                if (!Current()) return;
                TextElement.SetFontWeight(container, FontWeight);
                if (!Current()) return;
                order.Add(container); host.Anchors[container] = anchor;
            }
            foreach (var pair in cards.ToArray())
                if (!retained.Contains(pair.Key)) ReleaseTrackballCard(pair.Key, pair.Value);
            if (!Current()) return;
            if (!host.Children.SequenceEqual(order))
            {
                host.Children.Clear();
                if (!Current()) return;
                foreach (var child in order)
                { host.Children.Add(child); if (!Current()) return; }
            }
            EnsureTrackballPresentationMembership();
            if (!Current()) return;
            host.InvalidateMeasure(); host.InvalidateArrange();
            InvalidateMeasure(); InvalidateArrange();
        }
        catch
        {
            var failed = Current();
            try { ReleaseTrackballPresentationVisual(); }
            finally
            {
                if (failed && Current()) { _failedTrackballPresentation = true; _failedTrackballTemplate = template; }
            }
            throw;
        }
        finally
        {
            _synchronizingTrackballPresentation = false;
            if (presentationRevision != _trackballPresentationRevision || readingRevision != _readingsRevision)
                ReleaseTrackballPresentationVisual();
            if (_pendingTrackballPresentation) QueueTrackballPresentation();
        }
    }

    private void QueueTrackballPresentation()
    {
        _pendingTrackballPresentation = false;
        if (_queuedTrackballPresentation || !CanPresentTrackball) return;
        _queuedTrackballPresentation = true;
        Dispatcher.UIThread.Post(() =>
        {
            _queuedTrackballPresentation = false;
            if (CanPresentTrackball) { SynchronizeTrackballPresentation(); RequestRender(); }
        }, DispatcherPriority.Loaded);
    }

    private void EnsureTrackballPresentationMembership()
    {
        if (_trackballPresentationHost is not { } host) return;
        var logical = ((ILogical)host).LogicalParent; var visual = host.GetVisualParent();
        if (logical is not null && !ReferenceEquals(logical, this) || visual is not null && !ReferenceEquals(visual, this))
            throw new InvalidOperationException("The trackball presentation already has another parent.");
        if (logical is null) LogicalChildren.Add(host);
        if (!ReferenceEquals(host, _trackballPresentationHost)) return;
        if (visual is null) VisualChildren.Add(host);
    }

    private static void DiscardTrackballBorder(Border border)
    { try { border.Child = null; } finally { border.DataContext = null; } }

    private void ReleaseTrackballCard(TrackballCardKey key, TrackballCard card)
    {
        _trackballCards?.Remove(key);
        if (card.Border is not { } border) return;
        try { if (border.GetVisualParent() is Panel parent) parent.Children.Remove(border); }
        finally { DiscardTrackballBorder(border); }
    }

    private void ReleaseTrackballPresentationVisual()
    {
        var host = _trackballPresentationHost;
        var cards = _trackballCards;
        _trackballPresentationHost = null; _trackballCards = null;
        ExceptionDispatchInfo? failure = null;
        void Clean(Action action)
        { try { action(); } catch (Exception error) { failure ??= ExceptionDispatchInfo.Capture(error); } }
        if (cards is not null)
            foreach (var card in cards.Values.ToArray())
                if (card.Border is { } border) Clean(() => DiscardTrackballBorder(border));
        if (host is not null)
        {
            Clean(() => { if (ReferenceEquals(host.GetVisualParent(), this)) VisualChildren.Remove(host); });
            Clean(() => { if (ReferenceEquals(((ILogical)host).LogicalParent, this)) LogicalChildren.Remove(host); });
            Clean(host.Children.Clear); host.Anchors.Clear();
        }
        failure?.Throw();
    }

    private void ClearTrackballPresentation()
    {
        if (_clearingTrackballPresentation) return;
        if (_trackballPresentationHost is null && _trackballCards is null && !_synchronizingTrackballPresentation) return;
        _clearingTrackballPresentation = true;
        try
        {
            _trackballPresentationRevision++;
            _pendingTrackballPresentation = false;
            ReleaseTrackballPresentationVisual();
        }
        finally { _clearingTrackballPresentation = false; }
    }

    private void MeasureTrackballPresentation(Size size)
    {
        if (_trackballPresentationHost is not { } host) return;
        EnsureTrackballPresentationMembership();
        host.Measure(size);
    }
    private void ArrangeTrackballPresentation(Size size)
    {
        if (_trackballPresentationHost is not { } host) return;
        EnsureTrackballPresentationMembership();
        host.Measure(size); host.Arrange(new Rect(size));
    }
    private void RenderTrackballPresentationForExport(DrawingContext drawing)
    {
        if (_trackballPresentationHost is not { } host || host.Bounds.Width <= 0 || host.Bounds.Height <= 0) return;
        drawing.DrawRectangle(new VisualBrush(host) { Stretch = Stretch.Fill, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top }, null, host.Bounds);
    }

    private readonly record struct TrackballCardKey(bool Grouped, ChartSeries? Series);
    private sealed record TrackballCard(IDataTemplate? Template, Border? Border);
    private sealed class TrackballPresentationHost : Panel
    {
        public Dictionary<Control, Point> Anchors { get; } = [];
        protected override Size MeasureOverride(Size availableSize)
        {
            foreach (var child in Children) child.Measure(new Size(Math.Max(0, Math.Min(320, availableSize.Width - 16)), Math.Max(0, availableSize.Height - 16)));
            return default;
        }
        protected override Size ArrangeOverride(Size finalSize)
        {
            var occupied = new List<Rect>();
            foreach (var child in Children)
            {
                if (!Anchors.TryGetValue(child, out var anchor)) continue;
                var width = Math.Min(child.DesiredSize.Width, Math.Max(0, finalSize.Width - 16));
                var height = Math.Min(child.DesiredSize.Height, Math.Max(0, finalSize.Height - 16));
                var x = anchor.X + 14; var y = anchor.Y + 16;
                if (x + width > finalSize.Width - 8) x = anchor.X - width - 14;
                if (y + height > finalSize.Height - 8) y = anchor.Y - height - 12;
                x = Math.Clamp(x, 8, Math.Max(8, finalSize.Width - width - 8));
                y = Math.Clamp(y, 8, Math.Max(8, finalSize.Height - height - 8));
                var bounds = new Rect(x, y, width, height);
                // Float cards prefer distinct rows without moving their readout/line.
                foreach (var previous in occupied)
                {
                    if (!previous.Intersects(bounds)) continue;
                    var below = previous.Bottom + 4;
                    if (below + height <= finalSize.Height - 8) bounds = new Rect(x, below, width, height);
                    else if (previous.Top - height - 4 >= 8) bounds = new Rect(x, previous.Top - height - 4, width, height);
                }
                occupied.Add(bounds); child.Arrange(bounds);
            }
            return finalSize;
        }
    }
}
