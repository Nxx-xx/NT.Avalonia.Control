using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia.Threading;

namespace NT.AvaloniaControl.Charts;

/// <summary>Owns bounded reusable passive cards. The per-card presenter handles attachment, visibility,
/// failed-template quarantine and layout cleanup using the same lifecycle as professional tooltips.</summary>
internal sealed class ChartDataTrackballPresenter(ChartBase owner, Func<IDataTemplate?> template,
    Func<IReadOnlyList<ChartDataTrackballPresentationReading>> read, Action refresh,
    Func<(IBrush? Background, IBrush? Foreground, IBrush? Border)> brushes, Action<DrawingContext> draw, Func<bool> hasReadings)
{
    private static readonly IDataTemplate DefaultTemplate = new FuncDataTemplate<ChartDataTrackballContext>((context, _) =>
    {
        if (context?.Text is null) return null;
        var text = new TextBlock { TextWrapping = TextWrapping.Wrap, MaxWidth = 360 };
        text.Bind(TextBlock.TextProperty, new Binding(nameof(ChartDataTrackballContext.Text)));
        return text;
    });
    private readonly List<ChartDataTrackballCardPresenter> _cards = [];
    private readonly List<Visual> _visibilityOwners = [];
    private TrackballOverlay? _overlay;
    private IReadOnlyList<ChartDataTrackballPresentationReading> _readings = [];
    private bool _synchronizing, _pending;
    private bool _attached, _queued;
    internal bool IsBusy => _synchronizing || _cards.Any(card => card.IsBusy);
    internal bool IsOwnerVisible => owner.IsEffectivelyVisible && _visibilityOwners.All(visual => visual.IsVisible);
    private IDataTemplate CurrentTemplate() => template() ?? DefaultTemplate;

    internal void Synchronize()
    {
        if (_synchronizing) { _pending = true; return; }
        _synchronizing = true;
        try
        {
            _readings = read();
            if (hasReadings())
            {
                _overlay ??= new(draw) { Name = "PART_ChartDataTrackballLines", IsHitTestVisible = false, Focusable = false, ZIndex = 35 };
                var overlay = _overlay; owner.AttachDataTooltipOverlay(overlay, () => ReferenceEquals(_overlay, overlay));
                _overlay.InvalidateVisual();
            }
            else ReleaseOverlay();
            while (_cards.Count > _readings.Count) { var card = _cards[^1]; _cards.RemoveAt(_cards.Count - 1); card.Detached(); }
            while (_cards.Count < _readings.Count)
            {
                var index = _cards.Count;
                _cards.Add(new(owner, CurrentTemplate, () => index < _readings.Count ? _readings[index] : null, refresh, brushes));
            }
            foreach (var card in _cards.ToArray()) card.Synchronize();
        }
        finally { _synchronizing = false; if (_pending) { _pending = false; RequestRefresh(); } }
    }

    internal void Attached()
    {
        Unobserve();
        _attached = true;
        owner.ActualThemeVariantChanged += ActualThemeVariantChanged;
        foreach (var visual in owner.GetVisualAncestors().Prepend(owner))
        { _visibilityOwners.Add(visual); visual.PropertyChanged += VisibilityChanged; }
        foreach (var card in _cards.ToArray()) card.Attached(); RequestRefresh();
    }
    private void VisibilityChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property != Visual.IsVisibleProperty) return;
        // IsVisible can notify before the owner's inherited effective visibility has settled.
        // Clear immediately and also refresh after that propagation completes.
        refresh(); RequestRefresh();
    }
    private void ActualThemeVariantChanged(object? sender, EventArgs args) => UpdateAppearance();
    private void Unobserve()
    { owner.ActualThemeVariantChanged -= ActualThemeVariantChanged; foreach (var visual in _visibilityOwners) visual.PropertyChanged -= VisibilityChanged; _visibilityOwners.Clear(); }
    internal void Detached() { _attached = false; Unobserve(); _readings = []; foreach (var card in _cards.ToArray()) card.Detached(); _cards.Clear(); ReleaseOverlay(); }
    internal void TemplateChanged() { foreach (var card in _cards.ToArray()) card.TemplateChanged(); RequestRefresh(); }
    internal void InvalidateReading() { _readings = []; foreach (var card in _cards.ToArray()) card.InvalidateReading(queue: false); }
    internal void UpdateAppearance() { foreach (var card in _cards.ToArray()) card.UpdateAppearance(); InvalidateVisual(); }
    internal void EnsureMembership() { if (_overlay is { } overlay) owner.AttachDataTooltipOverlay(overlay, () => ReferenceEquals(_overlay, overlay)); foreach (var card in _cards.ToArray()) card.EnsureMembership(); }
    internal void Measure(Size size) { _overlay?.Measure(size); foreach (var card in _cards.ToArray()) card.Measure(size); }
    internal void Arrange(Size size)
    {
        _overlay?.Arrange(new Rect(size)); var occupied = new List<Rect>();
        foreach (var card in _cards.ToArray()) { card.Arrange(size, occupied); if (card.CardBounds.Width > 0 && card.CardBounds.Height > 0) occupied.Add(card.CardBounds); }
    }
    internal void InvalidateVisual() => _overlay?.InvalidateVisual();
    private void ReleaseOverlay() { if (_overlay is not { } overlay) return; _overlay = null; owner.DetachDataTooltipOverlay(overlay); }
    internal void RequestRefresh()
    {
        if (_queued || !_attached) return;
        _queued = true;
        Dispatcher.UIThread.Post(() => { _queued = false; if (_attached) refresh(); }, DispatcherPriority.Loaded);
    }

    private sealed class TrackballOverlay(Action<DrawingContext> draw) : Control
    {
        public override void Render(DrawingContext context) { base.Render(context); draw(context); }
    }
}
