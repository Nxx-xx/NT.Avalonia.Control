using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.Charts;

public abstract partial class HierarchyChartBase
{
    public static readonly StyledProperty<ChartAnimationTriggers> AnimationTriggersProperty = AvaloniaProperty.Register<HierarchyChartBase, ChartAnimationTriggers>(nameof(AnimationTriggers), validate: x => (x & ~ChartAnimationTriggers.All) == 0);
    public static readonly StyledProperty<TimeSpan> AnimationDurationProperty = AvaloniaProperty.Register<HierarchyChartBase, TimeSpan>(nameof(AnimationDuration), TimeSpan.FromMilliseconds(450), validate: x => x >= TimeSpan.Zero);
    public static readonly StyledProperty<TimeSpan> AnimationDelayProperty = AvaloniaProperty.Register<HierarchyChartBase, TimeSpan>(nameof(AnimationDelay), validate: x => x >= TimeSpan.Zero);
    public static readonly StyledProperty<IEasing?> AnimationEasingProperty = AvaloniaProperty.Register<HierarchyChartBase, IEasing?>(nameof(AnimationEasing), new CubicEaseOut());
    public static readonly StyledProperty<bool> IsAnimationPausedProperty = AvaloniaProperty.Register<HierarchyChartBase, bool>(nameof(IsAnimationPaused));
    public static readonly StyledProperty<bool> ReduceMotionProperty = AvaloniaProperty.Register<HierarchyChartBase, bool>(nameof(ReduceMotion));
    public static readonly StyledProperty<int> AnimationPointLimitProperty = AvaloniaProperty.Register<HierarchyChartBase, int>(nameof(AnimationPointLimit), 10_000, validate: x => x >= 0);
    public static readonly DirectProperty<HierarchyChartBase, double> AnimationProgressProperty = AvaloniaProperty.RegisterDirect<HierarchyChartBase, double>(nameof(AnimationProgress), c => c.AnimationProgress);
    public static readonly DirectProperty<HierarchyChartBase, ChartAnimationState> AnimationStateProperty = AvaloniaProperty.RegisterDirect<HierarchyChartBase, ChartAnimationState>(nameof(AnimationState), c => c.AnimationState);
    public ChartAnimationTriggers AnimationTriggers { get => GetValue(AnimationTriggersProperty); set => SetValue(AnimationTriggersProperty, value); }
    public TimeSpan AnimationDuration { get => GetValue(AnimationDurationProperty); set => SetValue(AnimationDurationProperty, value); }
    public TimeSpan AnimationDelay { get => GetValue(AnimationDelayProperty); set => SetValue(AnimationDelayProperty, value); }
    /// <summary>Null is linear. Invalid or throwing easing functions fall back to the linear timeline.</summary>
    public IEasing? AnimationEasing { get => GetValue(AnimationEasingProperty); set => SetValue(AnimationEasingProperty, value); }
    public bool IsAnimationPaused { get => GetValue(IsAnimationPausedProperty); set => SetValue(IsAnimationPausedProperty, value); }
    public bool ReduceMotion { get => GetValue(ReduceMotionProperty); set => SetValue(ReduceMotionProperty, value); }
    public int AnimationPointLimit { get => GetValue(AnimationPointLimitProperty); set => SetValue(AnimationPointLimitProperty, value); }
    public double AnimationProgress => _animationProgress;
    public ChartAnimationState AnimationState => _animationState;
    internal TimeProvider AnimationTimeProvider { get; set; } = TimeProvider.System;
    private double _animationProgress = 1;
    private ChartAnimationState _animationState;
    private IReadOnlyList<Glyph>? _animationFrom, _animationTarget;
    private long _animationStart, _animationGeneration, _frameRequestGeneration = -1;
    private TimeSpan _elapsed, _duration, _delay;
    private IEasing? _easing;
    private bool _entrancePending, _animationUpdating;
    private ChartAnimationTriggers _pendingTrigger;
    private (double Zoom, double X, double Y) _fromView, _targetView;
    private readonly List<Visual> _animationAncestors = [];
    private double _publishedProgress = 1;
    private ChartAnimationState _publishedState;
    private bool _statusQueued;
    private bool CanAnimate => _hierarchyAttached && IsEffectivelyVisible && IsEffectivelyEnabled && !ReduceMotion && AnimationDuration > TimeSpan.Zero && AnimationPointLimit > 0;
    private void ObserveAnimationAncestors()
    {
        foreach (var ancestor in _animationAncestors) ancestor.PropertyChanged -= AnimationAncestorChanged;
        _animationAncestors.Clear(); if (!_hierarchyAttached) return;
        foreach (var ancestor in this.GetVisualAncestors()) { _animationAncestors.Add(ancestor); ancestor.PropertyChanged += AnimationAncestorChanged; }
    }
    private void AnimationAncestorChanged(object? sender, AvaloniaPropertyChangedEventArgs change) { if (change.Property == IsVisibleProperty && sender is Visual { IsVisible: false }) CancelAnimations(); }
    private bool AnimationPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == AnimationProgressProperty || change.Property == AnimationStateProperty) return true;
        if (change.Property == IsAnimationPausedProperty)
        {
            if (_animationTarget is not null)
            {
                ++_animationGeneration;
                if (IsAnimationPaused) { _elapsed += AnimationTimeProvider.GetElapsedTime(_animationStart); SetAnimationStatus(ChartAnimationState.Paused, _animationProgress); }
                else { _animationStart = AnimationTimeProvider.GetTimestamp(); UpdateAnimationFrame(); QueueAnimationFrame(); }
            }
            return true;
        }
        if (change.Property == AnimationTriggersProperty || change.Property == AnimationDurationProperty || change.Property == AnimationDelayProperty || change.Property == AnimationEasingProperty || change.Property == ReduceMotionProperty || change.Property == AnimationPointLimitProperty)
        { CancelAnimations(); return true; }
        if (change.Property == IsVisibleProperty && !IsVisible || change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled) CancelAnimations();
        return false;
    }
    private static bool SameGlyph(Glyph a, Glyph b) => Identity(a.Entry).Equals(Identity(b.Entry)) && a.Kind == b.Kind && a.X == b.X && a.Y == b.Y && a.Width == b.Width && a.Height == b.Height && a.InnerRadius == b.InnerRadius && a.StartAngle == b.StartAngle && a.SweepAngle == b.SweepAngle;
    private void BeginTransition(IReadOnlyList<Glyph> target)
    {
        var entrance = _entrancePending; _entrancePending = false;
        var trigger = entrance ? ChartAnimationTriggers.Entrance : _pendingTrigger; _pendingTrigger = ChartAnimationTriggers.None;
        var view = (Zoom, PanX, PanY); var currentView = (_displayZoom, _displayPanX, _displayPanY);
        if (!CanAnimate || (AnimationTriggers & trigger) == 0 || target.Count + _frame.Count > AnimationPointLimit ||
            !entrance && view == currentView && target.Count == _frame.Count && target.Zip(_frame).All(pair => SameGlyph(pair.First, pair.Second)))
        {
            ReleaseAnimation(); _frame = target; (_displayZoom, _displayPanX, _displayPanY) = view; SetAnimationStatus(ChartAnimationState.Idle, 1); return;
        }
        var old = _frame.Where(g => !g.IsExiting).ToDictionary(g => Identity(g.Entry));
        var from = new List<Glyph>(); var next = new List<Glyph>(); var desired = new HashSet<NodeIdentity>();
        foreach (var glyph in target)
        {
            var key = Identity(glyph.Entry); desired.Add(key); next.Add(glyph);
            from.Add(!entrance && old.TryGetValue(key, out var previous) && previous.Kind == glyph.Kind ? previous : Shrink(glyph));
        }
        if (!entrance) foreach (var glyph in _frame.Where(g => !g.IsExiting && !desired.Contains(Identity(g.Entry))))
        { from.Add(glyph with { IsExiting = true }); next.Add(Shrink(glyph) with { IsExiting = true }); }
        _animationFrom = from.AsReadOnly(); _animationTarget = next.AsReadOnly();
        _fromView = currentView; _targetView = view;
        _animationStart = AnimationTimeProvider.GetTimestamp(); _elapsed = TimeSpan.Zero;
        _duration = AnimationDuration; _delay = AnimationDelay; _easing = AnimationEasing; ++_animationGeneration;
        ApplyAnimationFrame(0); SetAnimationStatus(IsAnimationPaused ? ChartAnimationState.Paused : _delay > TimeSpan.Zero ? ChartAnimationState.Delayed : ChartAnimationState.Running, 0); QueueAnimationFrame();
    }
    private static Glyph Shrink(Glyph glyph) => glyph.Kind switch
    {
        GlyphKind.Rectangle => glyph with { X = glyph.X + glyph.Width / 2, Y = glyph.Y + glyph.Height / 2, Width = 0, Height = 0 },
        GlyphKind.Sector => glyph with { InnerRadius = glyph.Width, SweepAngle = 0, StartAngle = glyph.StartAngle + glyph.SweepAngle / 2 },
        _ => glyph with { Width = 0, Height = 0 }
    };
    private static double Lerp(double a, double b, double t) => a * (1 - t) + b * t;
    private void ApplyAnimationFrame(double progress)
    {
        if (_animationFrom is not { } from || _animationTarget is not { } target) return;
        _frame = Array.AsReadOnly(target.Select((b, i) =>
        {
            var a = from[i]; return b with { X = Lerp(a.X, b.X, progress), Y = Lerp(a.Y, b.Y, progress), Width = Lerp(a.Width, b.Width, progress), Height = Lerp(a.Height, b.Height, progress),
                InnerRadius = Lerp(a.InnerRadius, b.InnerRadius, progress), StartAngle = Lerp(a.StartAngle, b.StartAngle, progress), SweepAngle = Lerp(a.SweepAngle, b.SweepAngle, progress) };
        }).ToArray());
        _displayZoom = Lerp(_fromView.Zoom, _targetView.Zoom, progress); _displayPanX = Lerp(_fromView.X, _targetView.X, progress); _displayPanY = Lerp(_fromView.Y, _targetView.Y, progress);
    }
    private void SetAnimationStatus(ChartAnimationState state, double progress)
    {
        // Geometry may be prepared inside Render. Publish notifications on the dispatcher to keep
        // application callbacks outside frame construction, while the getters expose that frame immediately.
        _animationProgress = progress; _animationState = state; QueueAnimationStatus();
    }
    private void QueueAnimationStatus()
    {
        if (_statusQueued || _publishedProgress == _animationProgress && _publishedState == _animationState) return;
        _statusQueued = true; Dispatcher.UIThread.Post(() =>
        {
            _statusQueued = false; var progress = _animationProgress; var state = _animationState;
            if (_publishedProgress != progress) { var old = _publishedProgress; _publishedProgress = progress; RaisePropertyChanged(AnimationProgressProperty, old, progress); }
            // Progress listeners may cancel or replace the transition. Publish only the currently displayed state.
            if (_animationState == state && _publishedState != state) { var old = _publishedState; _publishedState = state; RaisePropertyChanged(AnimationStateProperty, old, state); }
            QueueAnimationStatus();
        });
    }
    private void QueueAnimationFrame()
    {
        if (_frameRequestGeneration == _animationGeneration || IsAnimationPaused || _animationTarget is null || !_hierarchyAttached || TopLevel.GetTopLevel(this) is not { } top) return;
        var generation = _frameRequestGeneration = _animationGeneration; var weak = new WeakReference<HierarchyChartBase>(this);
        top.RequestAnimationFrame(_ =>
        {
            if (!weak.TryGetTarget(out var chart)) return;
            if (chart._frameRequestGeneration == generation) chart._frameRequestGeneration = -1;
            if (chart._animationGeneration == generation && chart._hierarchyAttached) chart.UpdateAnimationFrame();
        });
    }
    internal void UpdateAnimationFrame()
    {
        VerifyAccess(); if (_animationUpdating || IsAnimationPaused || _animationTarget is null) return;
        _animationUpdating = true;
        try
        {
            if (!CanAnimate) { CancelAnimations(); return; }
            var generation = _animationGeneration; var elapsed = _elapsed + AnimationTimeProvider.GetElapsedTime(_animationStart);
            var fraction = Math.Clamp((elapsed - _delay).TotalMilliseconds / _duration.TotalMilliseconds, 0, 1); var progress = fraction;
            try { if (_easing is { } easing) progress = easing.Ease(fraction); } catch { progress = fraction; }
            if (!double.IsFinite(progress)) progress = fraction; progress = fraction >= 1 ? 1 : Math.Clamp(progress, 0, 1);
            if (generation != _animationGeneration) return;
            ApplyAnimationFrame(progress);
            if (fraction >= 1) { _frame = Array.AsReadOnly(_animationTarget.Where(g => !g.IsExiting).ToArray()); ReleaseAnimation(); SetAnimationStatus(ChartAnimationState.Idle, 1); }
            else { SetAnimationStatus(elapsed < _delay ? ChartAnimationState.Delayed : ChartAnimationState.Running, progress); QueueAnimationFrame(); }
            InvalidateEngineering();
        }
        finally { _animationUpdating = false; }
    }
    private void ReleaseAnimation() { _animationFrom = _animationTarget = null; _easing = null; ++_animationGeneration; _frameRequestGeneration = -1; }
    public void CancelAnimations()
    {
        VerifyAccess();
        // Resolve pending source/view edits before settling; no previous target is republished.
        if (_dataDirty || _viewDirty) _ = GetMarks();
        if (_animationTarget is { } target) { _frame = Array.AsReadOnly(target.Where(g => !g.IsExiting).ToArray()); (_displayZoom, _displayPanX, _displayPanY) = _targetView; }
        ReleaseAnimation(); _pendingTrigger = ChartAnimationTriggers.None; _entrancePending = false; SetAnimationStatus(ChartAnimationState.Idle, 1); InvalidateEngineering();
    }
    public void PauseAnimations() => SetCurrentValue(IsAnimationPausedProperty, true);
    public void ResumeAnimations() => SetCurrentValue(IsAnimationPausedProperty, false);
    public void ReplayEntranceAnimation() { VerifyAccess(); CancelAnimations(); _entrancePending = true; _viewDirty = true; InvalidateEngineering(); }
}
