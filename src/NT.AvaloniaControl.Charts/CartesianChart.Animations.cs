using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.Charts;

/// <summary>Changes that may animate. None preserves the immediate rendering path.</summary>
[Flags]
public enum ChartAnimationTriggers { None = 0, Entrance = 1, Data = 2, Viewport = 4, All = Entrance | Data | Viewport }

public enum ChartAnimationState { Idle, Delayed, Running, Paused }

public sealed partial class CartesianChart
{
    public static readonly StyledProperty<ChartAnimationTriggers> AnimationTriggersProperty =
        AvaloniaProperty.Register<CartesianChart, ChartAnimationTriggers>(nameof(AnimationTriggers),
            validate: value => (value & ~ChartAnimationTriggers.All) == 0);
    public static readonly StyledProperty<TimeSpan> AnimationDurationProperty =
        AvaloniaProperty.Register<CartesianChart, TimeSpan>(nameof(AnimationDuration), TimeSpan.FromMilliseconds(450),
            validate: value => value >= TimeSpan.Zero);
    public static readonly StyledProperty<TimeSpan> AnimationDelayProperty =
        AvaloniaProperty.Register<CartesianChart, TimeSpan>(nameof(AnimationDelay), validate: value => value >= TimeSpan.Zero);
    public static readonly StyledProperty<IEasing?> AnimationEasingProperty =
        AvaloniaProperty.Register<CartesianChart, IEasing?>(nameof(AnimationEasing), new CubicEaseOut());
    public static readonly StyledProperty<bool> IsAnimationPausedProperty =
        AvaloniaProperty.Register<CartesianChart, bool>(nameof(IsAnimationPaused));
    public static readonly StyledProperty<bool> ReduceMotionProperty =
        AvaloniaProperty.Register<CartesianChart, bool>(nameof(ReduceMotion));
    public static readonly StyledProperty<int> AnimationPointLimitProperty =
        AvaloniaProperty.Register<CartesianChart, int>(nameof(AnimationPointLimit), 10_000, validate: value => value >= 0);
    public static readonly DirectProperty<CartesianChart, double> AnimationProgressProperty =
        AvaloniaProperty.RegisterDirect<CartesianChart, double>(nameof(AnimationProgress), chart => chart.AnimationProgress);
    public static readonly DirectProperty<CartesianChart, ChartAnimationState> AnimationStateProperty =
        AvaloniaProperty.RegisterDirect<CartesianChart, ChartAnimationState>(nameof(AnimationState), chart => chart.AnimationState);

    /// <summary>Automatic transition triggers. Disabled by default, including for large data.</summary>
    public ChartAnimationTriggers AnimationTriggers { get => GetValue(AnimationTriggersProperty); set => SetValue(AnimationTriggersProperty, value); }
    public TimeSpan AnimationDuration { get => GetValue(AnimationDurationProperty); set => SetValue(AnimationDurationProperty, value); }
    public TimeSpan AnimationDelay { get => GetValue(AnimationDelayProperty); set => SetValue(AnimationDelayProperty, value); }
    /// <summary>Null is linear. Exceptions and nonfinite results fall back to linear; finite results are clamped to [0, 1].</summary>
    public IEasing? AnimationEasing { get => GetValue(AnimationEasingProperty); set => SetValue(AnimationEasingProperty, value); }
    /// <summary>Freezes the current frame and elapsed delay. Resuming continues the same transition.</summary>
    public bool IsAnimationPaused { get => GetValue(IsAnimationPausedProperty); set => SetValue(IsAnimationPausedProperty, value); }
    /// <summary>Application supplied reduced-motion preference. True immediately settles to the latest target.</summary>
    public bool ReduceMotion { get => GetValue(ReduceMotionProperty); set => SetValue(ReduceMotionProperty, value); }
    /// <summary>Maximum total visible source points allowed for a transition. Zero disables transitions.</summary>
    public int AnimationPointLimit { get => GetValue(AnimationPointLimitProperty); set => SetValue(AnimationPointLimitProperty, value); }
    /// <summary>Eased progress of the current frame; one when settled. Reading does not advance the clock.</summary>
    public double AnimationProgress => _animationProgress;
    public ChartAnimationState AnimationState => _animationState;

    private double _animationProgress = 1;
    private ChartAnimationState _animationState;
    private ChartScene? _animationTarget;
    private ChartScene? _animationFrom;
    private ChartAnimationTriggers _pendingAnimation;
    private ChartAnimationTriggers _activeAnimation;
    private bool _entranceAnimationPending;
    private bool _updatingAnimationFrame;
    private long _animationGeneration;
    private long _requestedAnimationGeneration = -1;
    private long _animationClockStart;
    private TimeSpan _animationAccumulated;
    private TimeSpan _activeAnimationDuration;
    private TimeSpan _activeAnimationDelay;
    private IEasing? _activeAnimationEasing;
    private List<Visual>? _animationVisibilityAncestors;

    // The scheduler and deterministic timeline share the same production entry point.
    internal TimeProvider AnimationTimeProvider { get; set; } = TimeProvider.System;
    internal bool AnimationsActive => _animationTarget is not null;

    private bool CanAnimate => _attached && IsVisible && IsEffectivelyVisible && IsEffectivelyEnabled && !ReduceMotion &&
        AnimationTriggers != ChartAnimationTriggers.None && AnimationDuration > TimeSpan.Zero && AnimationPointLimit > 0;

    /// <summary>Replays enabled entrance motion from the effective Y baseline. Does nothing while detached or reduced motion is active.</summary>
    public void ReplayEntranceAnimation()
    {
        VerifyAccess();
        if (!CanAnimate || (AnimationTriggers & ChartAnimationTriggers.Entrance) == 0) { CancelAnimations(); return; }
        _ = GetScene();
        _entranceAnimationPending = true;
        _pendingAnimation = ChartAnimationTriggers.Entrance;
        _animationGeneration++;
        _animationTarget = null;
        _animationFrom = null;
        _activeAnimationEasing = null;
        InvalidateScene();
        RequestRender();
        InvalidateMeasure();
    }

    /// <summary>Cancels delay or motion and displays the latest target, without changing source data or viewport intent.</summary>
    public void CancelAnimations()
    {
        VerifyAccess();
        var target = _animationTarget;
        var generation = ++_animationGeneration;
        ReleaseAnimationSession();
        _pendingAnimation = ChartAnimationTriggers.None;
        _entranceAnimationPending = false;
        if (target is not null) _scene = target;
        if (!SetAnimationStatus(ChartAnimationState.Idle, 1, generation)) return;
        if (target is not null) RefreshAnimationPresentation(target, generation);
    }

    private void ReleaseAnimationSession()
    {
        // Release application-owned easing closures together with scene inputs,
        // before publishing status: callbacks may create a newer session.
        _animationTarget = null;
        _animationFrom = null;
        _activeAnimationEasing = null;
        _activeAnimation = ChartAnimationTriggers.None;
    }

    private void AnimationAttached()
    {
        _entranceAnimationPending = (AnimationTriggers & ChartAnimationTriggers.Entrance) != 0;
        RefreshAnimationAncestors();
    }

    private void AnimationDetached()
    {
        CancelAnimations();
        RefreshAnimationAncestors();
    }

    private void RefreshAnimationAncestors()
    {
        if (_animationVisibilityAncestors is { } observed)
        {
            foreach (var ancestor in observed) ancestor.PropertyChanged -= AnimationAncestorChanged;
            observed.Clear();
        }
        if (!_attached || AnimationTriggers == ChartAnimationTriggers.None) return;
        var ancestors = _animationVisibilityAncestors ??= [];
        foreach (var ancestor in this.GetVisualAncestors())
        {
            ancestors.Add(ancestor);
            ancestor.PropertyChanged += AnimationAncestorChanged;
        }
    }

    private void AnimationAncestorChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == IsVisibleProperty && sender is Visual { IsVisible: false }) CancelAnimations();
    }

    private bool AnimationPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == AnimationProgressProperty || change.Property == AnimationStateProperty) return true;
        if (change.Property == IsAnimationPausedProperty)
        {
            if (_animationTarget is not null)
            {
                var generation = ++_animationGeneration;
                if (IsAnimationPaused)
                {
                    UpdateAnimationFrameCore(allowPaused: true);
                    if (generation != _animationGeneration || _animationTarget is null) return true;
                    _animationAccumulated += AnimationTimeProvider.GetElapsedTime(_animationClockStart, AnimationTimeProvider.GetTimestamp());
                    if (!SetAnimationStatus(ChartAnimationState.Paused, AnimationProgress, generation)) return true;
                    if (_scene is { } frame) RefreshAnimationPresentation(frame, generation);
                }
                else
                {
                    _animationClockStart = AnimationTimeProvider.GetTimestamp();
                    UpdateAnimationFrame();
                    RequestAnimationFrame();
                }
            }
            return true;
        }
        if (change.Property == AnimationTriggersProperty || change.Property == AnimationDurationProperty ||
            change.Property == AnimationDelayProperty || change.Property == AnimationEasingProperty ||
            change.Property == ReduceMotionProperty || change.Property == AnimationPointLimitProperty)
        {
            // Configuration changes settle an existing transition; the next change uses the new settings.
            CancelAnimations();
            if (change.Property == AnimationTriggersProperty) RefreshAnimationAncestors();
            return true;
        }
        if (change.Property == IsVisibleProperty && !IsVisible ||
            change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled ||
            change.Property == BoundsProperty && !_entranceAnimationPending &&
            (_animationTarget is not null || _pendingAnimation != ChartAnimationTriggers.None))
            CancelAnimations();
        return false;
    }

    private void PrepareAnimationChange(ChartAnimationTriggers trigger)
    {
        if (!CanAnimate || (AnimationTriggers & trigger) == 0)
        {
            if (_animationTarget is not null || _pendingAnimation != ChartAnimationTriggers.None) CancelAnimations();
            return;
        }
        // Multiple changes before layout form one target; a later change during motion starts at the presented frame.
        _animationFrom = _scene ?? _animationFrom;
        _animationTarget = null;
        _activeAnimationEasing = null;
        _pendingAnimation |= trigger;
        _animationGeneration++;
        InvalidateMeasure(); InvalidateArrange();
    }

    private void AnimationSceneInvalidated()
    {
        if (_pendingAnimation != ChartAnimationTriggers.None || _entranceAnimationPending) return;
        if (_animationTarget is not null) CancelAnimations();
    }

    private void CompleteAnimationScene()
    {
        if (_scene is not { } target) return;
        var trigger = _entranceAnimationPending ? ChartAnimationTriggers.Entrance : _pendingAnimation;
        _pendingAnimation = ChartAnimationTriggers.None;
        _entranceAnimationPending = false;
        if (!CanAnimate || (trigger & AnimationTriggers) == 0 ||
            target.Snapshots.Sum(snapshot => (long)snapshot.Data.Count) > AnimationPointLimit ||
            _animationFrom is { } previousScene && previousScene.Snapshots.Sum(snapshot => (long)snapshot.Data.Count) > AnimationPointLimit ||
            trigger != ChartAnimationTriggers.Entrance && _animationFrom is null)
        {
            ReleaseAnimationSession();
            SetAnimationStatus(ChartAnimationState.Idle, 1, _animationGeneration);
            return;
        }
        _activeAnimation = trigger;
        _animationTarget = target;
        _activeAnimationDuration = AnimationDuration;
        _activeAnimationDelay = AnimationDelay;
        _activeAnimationEasing = AnimationEasing;
        _animationAccumulated = TimeSpan.Zero;
        _animationClockStart = AnimationTimeProvider.GetTimestamp();
        var generation = ++_animationGeneration;
        var initial = CreateAnimationFrame(target, 0);
        if (generation != _animationGeneration || !ReferenceEquals(target, _animationTarget)) return;
        _scene = initial;
        SetAnimationStatus(IsAnimationPaused ? ChartAnimationState.Paused :
            _activeAnimationDelay > TimeSpan.Zero ? ChartAnimationState.Delayed : ChartAnimationState.Running, 0, generation);
        if (generation == _animationGeneration) RequestAnimationFrame();
    }

    private void PrepareAnimationForLayout()
    {
        if (_entranceAnimationPending || _pendingAnimation != ChartAnimationTriggers.None) _ = GetScene();
    }

    private void RequestAnimationFrame()
    {
        if (_animationTarget is null || IsAnimationPaused || !_attached || _requestedAnimationGeneration == _animationGeneration) return;
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;
        var generation = _requestedAnimationGeneration = _animationGeneration;
        var weakChart = new WeakReference<CartesianChart>(this);
        topLevel.RequestAnimationFrame(_ =>
        {
            if (!weakChart.TryGetTarget(out var chart)) return;
            if (chart._requestedAnimationGeneration == generation) chart._requestedAnimationGeneration = -1;
            if (chart._animationGeneration != generation || !chart._attached) return;
            chart.UpdateAnimationFrame();
            chart.RequestAnimationFrame();
        });
    }

    internal void UpdateAnimationFrame()
        => UpdateAnimationFrameCore(allowPaused: false);

    private void UpdateAnimationFrameCore(bool allowPaused)
    {
        VerifyAccess();
        if (_updatingAnimationFrame || _animationTarget is not { } target || IsAnimationPaused && !allowPaused) return;
        if (!CanAnimate) { CancelAnimations(); return; }
        _updatingAnimationFrame = true;
        try
        {
            var generation = _animationGeneration;
            var elapsed = _animationAccumulated + AnimationTimeProvider.GetElapsedTime(_animationClockStart, AnimationTimeProvider.GetTimestamp());
            var raw = Math.Clamp((elapsed.TotalSeconds - _activeAnimationDelay.TotalSeconds) / _activeAnimationDuration.TotalSeconds, 0, 1);
            var progress = raw;
            if (raw is > 0 and < 1 && _activeAnimationEasing is { } easing)
            {
                try { var eased = easing.Ease(raw); if (double.IsFinite(eased)) progress = Math.Clamp(eased, 0, 1); }
                catch (Exception) { /* An application easing cannot strand the timeline. */ }
            }
            if (generation != _animationGeneration || !ReferenceEquals(target, _animationTarget)) return;
            if (raw < 1 && progress == _animationProgress)
            {
                SetAnimationStatus(IsAnimationPaused ? ChartAnimationState.Paused : elapsed < _activeAnimationDelay ?
                    ChartAnimationState.Delayed : ChartAnimationState.Running, progress, generation);
                return;
            }
            var frame = raw >= 1 ? target : CreateAnimationFrame(target, progress);
            if (generation != _animationGeneration || !ReferenceEquals(target, _animationTarget)) return;
            _scene = frame;
            if (raw >= 1)
            {
                ReleaseAnimationSession();
            }
            if (!SetAnimationStatus(raw >= 1 ? ChartAnimationState.Idle : IsAnimationPaused ? ChartAnimationState.Paused : elapsed < _activeAnimationDelay ?
                ChartAnimationState.Delayed : ChartAnimationState.Running, raw >= 1 ? 1 : progress, generation)) return;
            if (_scene is { } presented) RefreshAnimationPresentation(presented, generation);
        }
        finally { _updatingAnimationFrame = false; }
    }

    private bool SetAnimationStatus(ChartAnimationState state, double progress, long generation)
    {
        if (_rendering)
        {
            // A first scene can be requested by a drawing-only chart. As with
            // VisibleRange publication, application bindings run after Render.
            var weakChart = new WeakReference<CartesianChart>(this);
            Dispatcher.UIThread.Post(() =>
            {
                if (weakChart.TryGetTarget(out var chart) && generation == chart._animationGeneration)
                    chart.SetAnimationStatus(state, progress, generation);
            });
            return generation == _animationGeneration;
        }
        SetAndRaise(AnimationProgressProperty, ref _animationProgress, progress);
        if (generation != _animationGeneration) return false;
        SetAndRaise(AnimationStateProperty, ref _animationState, state);
        return generation == _animationGeneration;
    }

    private void RefreshAnimationPresentation(ChartScene frame, long generation)
    {
        InvalidatePlotAreaContent();
        InvalidateLabelPresentation();
        ClearHover();
        if (generation != _animationGeneration || !ReferenceEquals(frame, _scene)) return;
        PublishSceneState(frame);
        if (generation != _animationGeneration || !ReferenceEquals(frame, _scene)) return;
        RequestRender();
    }

    private ChartScene CreateAnimationFrame(ChartScene target, double progress)
    {
        var entrance = (_activeAnimation & ChartAnimationTriggers.Entrance) != 0;
        var dataMotion = entrance || (_activeAnimation & ChartAnimationTriggers.Data) != 0;
        var from = _animationFrom;
        var scales = new Dictionary<ChartAxis, ChartScale>();
        foreach (var pair in target.Scales)
        {
            var axis = pair.Key; var scale = pair.Value;
            if (!entrance && from?.Scales.TryGetValue(axis, out var previous) == true)
            {
                var minimum = InterpolateAnimationValue(scale, previous.Minimum, scale.Minimum, progress);
                var maximum = InterpolateAnimationValue(scale, previous.Maximum, scale.Maximum, progress);
                scales[axis] = double.IsFinite(minimum) && double.IsFinite(maximum) && minimum < maximum ?
                    CreateViewScale(axis, minimum, maximum, scale.CategoryLabels) : scale;
            }
            else scales[axis] = scale;
        }
        var x = scales[XAxis]; var y = scales[YAxis];
        var layouts = new Dictionary<ChartAxis, ChartAxisLayout>();
        foreach (var pair in target.Layouts)
        {
            var horizontal = ReferenceEquals(pair.Key, XAxis) || target.ExtraXAxes.Contains(pair.Key);
            var current = MeasureAxis(pair.Key, scales[pair.Key], horizontal ? target.Plot.Width : target.Plot.Height, horizontal);
            layouts[pair.Key] = current with { LabelWidth = pair.Value.LabelWidth, LabelHeight = pair.Value.LabelHeight, Bounds = pair.Value.Bounds };
        }
        var snapshots = new SeriesSnapshot[target.Snapshots.Length];
        Dictionary<ChartSeries, SeriesSnapshot>? fromSnapshots = null;
        if (from is not null)
        {
            fromSnapshots = new(ReferenceEqualityComparer.Instance);
            foreach (var snapshot in from.Snapshots) fromSnapshots.TryAdd(snapshot.Series, snapshot);
        }
        for (var seriesIndex = 0; seriesIndex < snapshots.Length; seriesIndex++)
        {
            var end = target.Snapshots[seriesIndex];
            SeriesSnapshot? previous = null;
            if (fromSnapshots?.TryGetValue(end.Series, out var candidate) == true &&
                ReferenceEquals(candidate.XAxis, end.XAxis) && ReferenceEquals(candidate.YAxis, end.YAxis)) previous = candidate;
            var values = end.Data;
            if (dataMotion)
            {
                var overlay = new ChartDataValue[values.Count];
                var bandOverlay = values.Count > 0 && values.GetBandValue(0) is not null ? new ChartBandValue[values.Count] : null;
                var pointOverlay = values.Count > 0 && values.GetPointStyle(0) is not null ? new ChartPointStyle[values.Count] : null;
                var xScale = target.Scales[end.XAxis]; var yScale = target.Scales[end.YAxis];
                var horizontalComparison = HorizontalComparison(end.Series);
                var valueScale = horizontalComparison ? xScale : yScale;
                var requestedBaseline = end.Series is IChartComparisonSeries comparison ? comparison.Baseline : 0;
                var baseline = valueScale.IsValidValue(requestedBaseline) ? requestedBaseline : valueScale.Minimum;
                for (var index = 0; index < overlay.Length; index++)
                {
                    var value = values.GetValue(index);
                    var destination = value.Point;
                    var origin = horizontalComparison ? new ChartPoint(baseline, destination.Y) : new ChartPoint(destination.X, baseline);
                    var endBand = values.GetBandValue(index);
                    var originLower = baseline;
                    var originCumulative = 0d;
                    var endStyle = values.GetPointStyle(index);
                    var originDiameter = 0d;
                    var originFill = endStyle?.Fill;
                    if (!entrance && previous is not null)
                    {
                        var item = values.Points[index];
                        var valueIdentity = end.Series.UsesValueIdentity;
                        if (previous.Data.TryFindIdentity(item.StableId, out var matched, valueIdentity) ||
                            !valueIdentity && previous.Data.TryGetPoint(item.SourceIndex, out matched))
                        {
                            origin = matched.Point; originLower = matched.BaseValue ?? baseline; originCumulative = matched.CumulativeValue ?? 0;
                            originDiameter = matched.MarkerDiameter is { } diameter && double.IsFinite(diameter) ? diameter : 0;
                            originFill = matched.PointFill ?? originFill;
                        }
                    }
                    if (IsValid(destination) && IsValid(origin))
                    {
                        destination = new(InterpolateAnimationValue(xScale, origin.X, destination.X, progress),
                            InterpolateAnimationValue(yScale, origin.Y, destination.Y, progress));
                        if (!end.XAxis.IsValidValue(destination.X) || !end.YAxis.IsValidValue(destination.Y) ||
                            end.XAxis.IsInScaleBreak(destination.X) || end.YAxis.IsInScaleBreak(destination.Y))
                            destination = horizontalComparison ? destination with { X = double.NaN } : destination with { Y = double.NaN };
                    }
                    overlay[index] = value with { Point = destination, IsEmpty = value.IsEmpty || !IsValid(destination) };
                    if (bandOverlay is not null && endBand is { } band)
                        bandOverlay[index] = band with { Lower = InterpolateAnimationValue(valueScale, originLower, band.Lower, progress),
                            CumulativeValue = band.CumulativeValue is { } cumulative ? originCumulative * (1 - progress) + cumulative * progress : null };
                    if (pointOverlay is not null && endStyle is { } style)
                        pointOverlay[index] = new(originDiameter * (1 - progress) + style.Diameter * progress,
                            InterpolatePointFill(originFill, style.Fill, progress));
                }
                values = bandOverlay is not null ? values.WithBandValues(overlay, bandOverlay) : values.WithValues(overlay);
                if (pointOverlay is not null) values = values.WithPointStyles(pointOverlay);
            }
            var context = end.Context!;
            var primaryClip = CreateBreakClip(target.Plot, scales[end.XAxis], scales[end.YAxis]);
            snapshots[seriesIndex] = new SeriesSnapshot(end.Series, values, end.Brush, end.Index, end.XAxis, end.YAxis)
            {
                SecondaryAxis = end.SecondaryAxis,
                Context = context with { Data = values, Projection = new CartesianProjection(target.Plot, scales[end.XAxis], scales[end.YAxis], values), PrimaryBreakClip = primaryClip,
                    PointHitRegions = EncodedPoints(end.Series) ? CreatePointHitRegions(target.Plot, scales[end.XAxis], scales[end.YAxis]) : null },
                BreakClip = primaryClip,
            };
        }
        PrepareStackPaths(snapshots);
        LayoutCategoryGroups(snapshots, scales, target.Plot);
        foreach (var snapshot in snapshots) SetSecondaryContext(snapshot, target.Plot, scales);
        return target with
        {
            XScale = x, YScale = y, Scales = scales, Layouts = layouts, Snapshots = snapshots,
            Range = new ChartViewport(x.Minimum, x.Maximum, y.Minimum, y.Maximum),
        };
    }

    private static double InterpolateAnimationValue(ChartScale scale, double from, double to, double progress)
    {
        if (progress <= 0) return from;
        if (progress >= 1) return to;
        var first = scale.Transform(from); var last = scale.Transform(to);
        if (!double.IsFinite(first) || !double.IsFinite(last)) return to;
        // The weighted form does not overflow merely because the endpoints have opposite signs.
        var interpolated = first * (1 - progress) + last * progress;
        var result = scale.Inverse(interpolated);
        return double.IsFinite(result) ? result : to;
    }
}
