using System.Diagnostics;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.Charts;

public enum GaugeLiquidShape { Circle, Rectangle }

/// <summary>A clipped liquid level. Motion is opt-in, pauses in place, and releases its timer on detachment.</summary>
public sealed class LiquidFillGaugeChart : GaugeBase
{
    public static readonly StyledProperty<GaugeLiquidShape> ShapeProperty = AvaloniaProperty.Register<LiquidFillGaugeChart, GaugeLiquidShape>(nameof(Shape), validate: Enum.IsDefined);
    public static readonly StyledProperty<double> WaveAmplitudeProperty = AvaloniaProperty.Register<LiquidFillGaugeChart, double>(nameof(WaveAmplitude), 5, validate: v => double.IsFinite(v) && v >= 0 && v <= 4096);
    public static readonly StyledProperty<double> WaveFrequencyProperty = AvaloniaProperty.Register<LiquidFillGaugeChart, double>(nameof(WaveFrequency), 2, validate: v => double.IsFinite(v) && v > 0 && v <= 100);
    public static readonly StyledProperty<double> WavePhaseProperty = AvaloniaProperty.Register<LiquidFillGaugeChart, double>(nameof(WavePhase), validate: double.IsFinite);
    public static readonly StyledProperty<bool> IsWaveAnimationEnabledProperty = AvaloniaProperty.Register<LiquidFillGaugeChart, bool>(nameof(IsWaveAnimationEnabled));
    public static readonly StyledProperty<bool> ReduceMotionProperty = AvaloniaProperty.Register<LiquidFillGaugeChart, bool>(nameof(ReduceMotion));
    public static readonly StyledProperty<bool> IsPausedProperty = AvaloniaProperty.Register<LiquidFillGaugeChart, bool>(nameof(IsPaused));
    public static readonly StyledProperty<bool> ShowPercentageProperty = AvaloniaProperty.Register<LiquidFillGaugeChart, bool>(nameof(ShowPercentage), true);
    private DispatcherTimer? _waveTimer;
    private bool _waveAttached;
    private long _waveAttachmentRevision;
    private long _lastTick;
    public GaugeLiquidShape Shape { get => GetValue(ShapeProperty); set => SetValue(ShapeProperty, value); }
    public double WaveAmplitude { get => GetValue(WaveAmplitudeProperty); set => SetValue(WaveAmplitudeProperty, value); }
    public double WaveFrequency { get => GetValue(WaveFrequencyProperty); set => SetValue(WaveFrequencyProperty, value); }
    /// <summary>Deterministic wave phase in radians. Pausing or reducing motion preserves the current phase.</summary>
    public double WavePhase { get => GetValue(WavePhaseProperty); set => SetValue(WavePhaseProperty, value); }
    public bool IsWaveAnimationEnabled { get => GetValue(IsWaveAnimationEnabledProperty); set => SetValue(IsWaveAnimationEnabledProperty, value); }
    public bool ReduceMotion { get => GetValue(ReduceMotionProperty); set => SetValue(ReduceMotionProperty, value); }
    public bool IsPaused { get => GetValue(IsPausedProperty); set => SetValue(IsPausedProperty, value); }
    public bool ShowPercentage { get => GetValue(ShowPercentageProperty); set => SetValue(ShowPercentageProperty, value); }
    internal bool IsWaveTimerRunning => _waveTimer?.IsEnabled == true;
    internal Rect LiquidBounds
    {
        get
        {
            var content = GaugeContentBounds;
            if (Shape == GaugeLiquidShape.Circle)
            {
                var radius = Math.Max(0, Math.Min(content.Width, content.Height) / 2 - 1);
                return new Rect(content.Center - new Vector(radius, radius), new Size(radius * 2, radius * 2));
            }
            return new Rect(content.X + 1, content.Y + 1, Math.Max(0, content.Width - 2), Math.Max(0, content.Height - 2));
        }
    }
    internal double SurfaceAt(double normalizedX)
    {
        var bounds = LiquidBounds; var fraction = NormalizedValue ?? 0;
        if (fraction <= 0) return bounds.Bottom; if (fraction >= 1) return bounds.Top;
        var amplitude = Math.Min(WaveAmplitude, bounds.Height * .1);
        return bounds.Bottom - bounds.Height * fraction + Math.Sin(normalizedX * Math.PI * 2 * WaveFrequency + WavePhase % (Math.PI * 2)) * amplitude;
    }
    internal Geometry LiquidGeometry()
    {
        var geometry = new StreamGeometry(); var bounds = LiquidBounds;
        if (NormalizedValue is not > 0 || bounds.Width <= 0 || bounds.Height <= 0) return geometry;
        using var path = geometry.Open();
        path.BeginFigure(new Point(bounds.Left, SurfaceAt(0)), true);
        for (var index = 1; index <= 128; index++) path.LineTo(new Point(bounds.Left + bounds.Width * index / 128, SurfaceAt((double)index / 128)));
        path.LineTo(bounds.BottomRight); path.LineTo(bounds.BottomLeft); path.EndFigure(true); return geometry;
    }
    private protected override void RenderGauge(DrawingContext context, GaugeLayout layout)
    {
        var bounds = LiquidBounds; if (bounds.Width <= 0 || bounds.Height <= 0) return;
        Geometry shape = Shape == GaugeLiquidShape.Circle ? new EllipseGeometry(bounds) : new RectangleGeometry(bounds);
        context.DrawGeometry(TrackBrush, null, shape);
        using (context.PushGeometryClip(shape))
        {
            context.DrawGeometry(ValueBrush, null, LiquidGeometry());
            if (NormalizedTarget is { } target)
            {
                var y = bounds.Bottom - bounds.Height * target;
                context.DrawLine(new Pen(NeedleBrush, 3), new Point(bounds.Left, y), new Point(bounds.Right, y));
            }
        }
        context.DrawGeometry(null, new Pen(AxisBrush, 1), shape);
        if (ShowValue) DrawText(context, ShowPercentage && NormalizedValue is { } fraction ? $"{fraction * 100:0.#}%" : CurrentText, bounds.Center, sizeScale: 1.5);
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        var revision = ++_waveAttachmentRevision;
        base.OnAttachedToVisualTree(e);
        if (revision != _waveAttachmentRevision) return;
        _waveAttached = this.IsAttachedToVisualTree(); UpdateTimer();
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    { ++_waveAttachmentRevision; _waveAttached = false; UpdateTimer(); base.OnDetachedFromVisualTree(e); }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsWaveAnimationEnabledProperty || change.Property == ReduceMotionProperty || change.Property == IsPausedProperty) UpdateTimer();
    }
    private void UpdateTimer()
    {
        var run = _waveAttached && this.IsAttachedToVisualTree() && IsWaveAnimationEnabled && !ReduceMotion && !IsPaused;
        if (!run)
        {
            if (_waveTimer is not null) { _waveTimer.Stop(); _waveTimer.Tick -= WaveTick; _waveTimer = null; }
            return;
        }
        if (_waveTimer is not null) return;
        _lastTick = Stopwatch.GetTimestamp(); _waveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) }; _waveTimer.Tick += WaveTick; _waveTimer.Start();
    }
    private void WaveTick(object? sender, EventArgs e)
    {
        var now = Stopwatch.GetTimestamp(); var elapsed = Stopwatch.GetElapsedTime(_lastTick, now); _lastTick = now; AdvanceWave(elapsed);
    }
    internal void AdvanceWave(TimeSpan elapsed)
    {
        if (!_waveAttached || !this.IsAttachedToVisualTree() || !IsWaveAnimationEnabled || ReduceMotion || IsPaused || elapsed <= TimeSpan.Zero) return;
        SetCurrentValue(WavePhaseProperty, (WavePhase % (Math.PI * 2) + elapsed.TotalSeconds * Math.PI) % (Math.PI * 2));
    }
}
