using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.Charts;

public sealed partial class CircularChart
{
    public static readonly StyledProperty<ChartAnimationTriggers> AnimationTriggersProperty = AvaloniaProperty.Register<CircularChart, ChartAnimationTriggers>(nameof(AnimationTriggers), validate: v => (v & ~ChartAnimationTriggers.All) == 0);
    public static readonly StyledProperty<TimeSpan> AnimationDurationProperty = AvaloniaProperty.Register<CircularChart, TimeSpan>(nameof(AnimationDuration), TimeSpan.FromMilliseconds(450), validate: v => v >= TimeSpan.Zero);
    public static readonly StyledProperty<TimeSpan> AnimationDelayProperty = AvaloniaProperty.Register<CircularChart, TimeSpan>(nameof(AnimationDelay), validate: v => v >= TimeSpan.Zero);
    public static readonly StyledProperty<IEasing?> AnimationEasingProperty = AvaloniaProperty.Register<CircularChart, IEasing?>(nameof(AnimationEasing), new CubicEaseOut());
    public static readonly StyledProperty<bool> IsAnimationPausedProperty = AvaloniaProperty.Register<CircularChart, bool>(nameof(IsAnimationPaused));
    public static readonly StyledProperty<bool> ReduceMotionProperty = AvaloniaProperty.Register<CircularChart, bool>(nameof(ReduceMotion));
    public static readonly StyledProperty<int> AnimationPointLimitProperty = AvaloniaProperty.Register<CircularChart, int>(nameof(AnimationPointLimit), 10_000, validate: v => v >= 0);
    public static readonly DirectProperty<CircularChart, double> AnimationProgressProperty = AvaloniaProperty.RegisterDirect<CircularChart, double>(nameof(AnimationProgress), c => c.AnimationProgress);
    public static readonly DirectProperty<CircularChart, ChartAnimationState> AnimationStateProperty = AvaloniaProperty.RegisterDirect<CircularChart, ChartAnimationState>(nameof(AnimationState), c => c.AnimationState);
    public ChartAnimationTriggers AnimationTriggers { get => GetValue(AnimationTriggersProperty); set => SetValue(AnimationTriggersProperty, value); }
    public TimeSpan AnimationDuration { get => GetValue(AnimationDurationProperty); set => SetValue(AnimationDurationProperty, value); }
    public TimeSpan AnimationDelay { get => GetValue(AnimationDelayProperty); set => SetValue(AnimationDelayProperty, value); }
    public IEasing? AnimationEasing { get => GetValue(AnimationEasingProperty); set => SetValue(AnimationEasingProperty, value); }
    public bool IsAnimationPaused { get => GetValue(IsAnimationPausedProperty); set => SetValue(IsAnimationPausedProperty, value); }
    public bool ReduceMotion { get => GetValue(ReduceMotionProperty); set => SetValue(ReduceMotionProperty, value); }
    public int AnimationPointLimit { get => GetValue(AnimationPointLimitProperty); set => SetValue(AnimationPointLimitProperty, value); }
    public double AnimationProgress => _animationProgress;
    public ChartAnimationState AnimationState => _animationState;
    internal TimeProvider AnimationTimeProvider { get; set; } = TimeProvider.System;
    private double _animationProgress = 1;
    private double _displayProgress = 1;
    private ChartAnimationState _animationState;
    private IReadOnlyList<CircularSliceInfo>? _animationTarget, _animationFrom;
    private long _animationStart, _animationGeneration;
    private TimeSpan _elapsed;
    private TimeSpan _duration, _delay;
    private IEasing? _easing;
    private bool _entrancePending;
    private bool _updatingAnimationFrame;
    private long _frameRequestGeneration = -1;
    private readonly List<Visual> _animationAncestors = [];
    private void RefreshAnimationAncestors()
    {
        foreach (var visual in _animationAncestors) visual.PropertyChanged -= AnimationAncestorChanged;
        _animationAncestors.Clear();
        if (!_attached) return;
        foreach (var visual in this.GetVisualAncestors()) { _animationAncestors.Add(visual); visual.PropertyChanged += AnimationAncestorChanged; }
    }
    private void AnimationAncestorChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    { if (e.Property == IsVisibleProperty && sender is Visual { IsVisible: false }) CancelAnimations(); }
    private bool CanAnimate => _attached && IsEffectivelyVisible && IsEffectivelyEnabled && !ReduceMotion && AnimationDuration > TimeSpan.Zero && AnimationPointLimit > 0;
    private bool AnimationPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == IsAnimationPausedProperty)
        {
            if (_animationTarget is not null)
            {
                _animationGeneration++;
                if (IsAnimationPaused) { _elapsed += AnimationTimeProvider.GetElapsedTime(_animationStart); SetAnimationStatus(ChartAnimationState.Paused, _animationProgress); }
                else
                {
                    _animationStart = AnimationTimeProvider.GetTimestamp();
                    if (SetAnimationStatus(_elapsed < _delay ? ChartAnimationState.Delayed : ChartAnimationState.Running, _animationProgress))
                    { UpdateAnimationFrame(); QueueAnimationFrame(); }
                }
            }
            return true;
        }
        if (change.Property == ReduceMotionProperty || change.Property == AnimationTriggersProperty || change.Property == AnimationDurationProperty || change.Property == AnimationDelayProperty || change.Property == AnimationPointLimitProperty || change.Property == AnimationEasingProperty)
        { CancelAnimations(); return true; }
        return false;
    }
    private void BeginTransition(IReadOnlyList<CircularSliceInfo> previous, IReadOnlyList<CircularSliceInfo> target)
    {
        var entrance = _entrancePending;
        _entrancePending = entrance && _attached && !CanAnimate && (AnimationTriggers & ChartAnimationTriggers.Entrance) != 0;
        var trigger = entrance ? ChartAnimationTriggers.Entrance : _pendingTrigger;
        _pendingTrigger = ChartAnimationTriggers.None;
        if (!entrance && _animationTarget is { } active && SameGeometry(active, target))
        { _animationTarget = target; ApplyAnimationFrame(_displayProgress); return; }
        if (!CanAnimate || (AnimationTriggers & trigger) == 0 || target.Count + previous.Count > AnimationPointLimit ||
            !entrance && SameGeometry(previous, target))
        { _animationTarget = _animationFrom = null; _slices = target; SetAnimationStatus(ChartAnimationState.Idle, 1); return; }
        var from = new List<CircularSliceInfo>();
        foreach (var value in target)
        {
            var old = entrance ? null : previous.FirstOrDefault(p => SameIdentity(p.Hit, value.Hit) && !p.IsExiting);
            from.Add(old ?? value with { StartAngle = entrance ? (value.Series.StartAngle % 360 + 360) % 360 : value.StartAngle + value.SweepAngle / 2, SweepAngle = 0 });
        }
        var next = target.ToList();
        if (!entrance)
            foreach (var old in previous.Where(p => !p.IsExiting && !target.Any(t => SameIdentity(t.Hit, p.Hit))))
            {
                from.Add(old with { IsExiting = true });
                next.Add(old with { IsExiting = true, SweepAngle = 0, StartAngle = old.StartAngle + old.SweepAngle / 2 });
            }
        _animationFrom = from.AsReadOnly(); _animationTarget = next.AsReadOnly();
        _animationGeneration++;
        _animationStart = AnimationTimeProvider.GetTimestamp(); _elapsed = TimeSpan.Zero;
        _duration = AnimationDuration; _delay = AnimationDelay; _easing = AnimationEasing;
        ApplyAnimationFrame(0);
        if (!SetAnimationStatus(IsAnimationPaused ? ChartAnimationState.Paused : _delay > TimeSpan.Zero ? ChartAnimationState.Delayed : ChartAnimationState.Running, 0)) return;
        QueueAnimationFrame();
    }
    private static bool SameGeometry(IReadOnlyList<CircularSliceInfo> a, IReadOnlyList<CircularSliceInfo> b) => a.Count == b.Count && a.Zip(b).All(p =>
        SameIdentity(p.First.Hit, p.Second.Hit) && p.First.Center == p.Second.Center && p.First.StartAngle == p.Second.StartAngle && p.First.SweepAngle == p.Second.SweepAngle &&
        p.First.InnerRadius == p.Second.InnerRadius && p.First.OuterRadius == p.Second.OuterRadius && SameFill(p.First.Fill, p.Second.Fill) && p.First.IsVisible == p.Second.IsVisible);
    private static bool SameFill(IBrush? a, IBrush? b) => a is ISolidColorBrush ac && b is ISolidColorBrush bc ? ac.Color == bc.Color && ac.Opacity == bc.Opacity : Equals(a, b);
    private bool SetAnimationStatus(ChartAnimationState state, double progress)
    {
        var generation = _animationGeneration;
        if (_rendering)
        {
            var weak = new WeakReference<CircularChart>(this);
            Dispatcher.UIThread.Post(() => { if (weak.TryGetTarget(out var chart) && chart._animationGeneration == generation) chart.SetAnimationStatus(state, progress); });
            return generation == _animationGeneration;
        }
        SetAndRaise(AnimationProgressProperty, ref _animationProgress, progress);
        if (generation != _animationGeneration) return false;
        SetAndRaise(AnimationStateProperty, ref _animationState, state);
        return generation == _animationGeneration;
    }
    private void QueueAnimationFrame()
    {
        if (_frameRequestGeneration == _animationGeneration || IsAnimationPaused || _animationTarget is null || !_attached) return;
        if (TopLevel.GetTopLevel(this) is not { } top) return;
        var generation = _frameRequestGeneration = _animationGeneration;
        var weak = new WeakReference<CircularChart>(this);
        top.RequestAnimationFrame(_ =>
        {
            if (!weak.TryGetTarget(out var chart)) return;
            if (chart._frameRequestGeneration == generation) chart._frameRequestGeneration = -1;
            if (chart._animationGeneration == generation && chart._attached) chart.UpdateAnimationFrame();
        });
    }
    internal void UpdateAnimationFrame()
    {
        if (_updatingAnimationFrame) return;
        _updatingAnimationFrame = true;
        try { UpdateAnimationFrameCore(); }
        finally { _updatingAnimationFrame = false; }
    }
    private void UpdateAnimationFrameCore()
    {
        VerifyAccess();
        if (_animationTarget is null || IsAnimationPaused) return;
        if (!CanAnimate) { CancelAnimations(); return; }
        var generation = _animationGeneration;
        var elapsed = _elapsed + AnimationTimeProvider.GetElapsedTime(_animationStart);
        var fraction = Math.Clamp((elapsed - _delay).TotalMilliseconds / _duration.TotalMilliseconds, 0, 1);
        var progress = fraction;
        try { if (_easing is { } easing) progress = easing.Ease(fraction); } catch { progress = fraction; }
        if (!double.IsFinite(progress)) progress = fraction;
        progress = fraction >= 1 ? 1 : Math.Clamp(progress, 0, 1);
        if (generation != _animationGeneration) return;
        ApplyAnimationFrame(progress);
        if (fraction >= 1)
        {
            _slices = Array.AsReadOnly(_animationTarget.Where(s => !s.IsExiting).ToArray());
            _animationTarget = _animationFrom = null; _easing = null;
            if (!SetAnimationStatus(ChartAnimationState.Idle, 1)) return;
        }
        else { if (!SetAnimationStatus(elapsed < _delay ? ChartAnimationState.Delayed : ChartAnimationState.Running, progress)) return; QueueAnimationFrame(); }
        RequestCircularRender(); ArrangeCenter();
        ReconcileSelection(_slices);
    }
    private static double Lerp(double a, double b, double t) => a * (1 - t) + b * t;
    private void ApplyAnimationFrame(double progress)
    {
        _displayProgress = progress;
        if (_animationTarget is not { } target || _animationFrom is not { } from) return;
        var frame = new CircularSliceInfo[target.Count];
        for (var i = 0; i < frame.Length; i++)
        {
            var a = from[i]; var b = target[i];
            var fill = b.Fill;
            if (a.Fill is ISolidColorBrush ac && b.Fill is ISolidColorBrush bc)
            {
                byte Mix(byte x, byte y) => (byte)Math.Clamp(Math.Round(Lerp(x, y, progress)), 0, 255);
                fill = new SolidColorBrush(Color.FromArgb(Mix(ac.Color.A, bc.Color.A), Mix(ac.Color.R, bc.Color.R), Mix(ac.Color.G, bc.Color.G), Mix(ac.Color.B, bc.Color.B)), Lerp(ac.Opacity, bc.Opacity, progress));
            }
            frame[i] = b with { StartAngle = Lerp(a.StartAngle, b.StartAngle, progress), SweepAngle = Lerp(a.SweepAngle, b.SweepAngle, progress),
                Center = new(Lerp(a.Center.X, b.Center.X, progress), Lerp(a.Center.Y, b.Center.Y, progress)),
                InnerRadius = Lerp(a.InnerRadius, b.InnerRadius, progress), OuterRadius = Lerp(a.OuterRadius, b.OuterRadius, progress), Fill = fill };
        }
        _slices = Array.AsReadOnly(frame);
    }
    public void ReplayEntranceAnimation()
    {
        VerifyAccess(); if (_rendering) { Dispatcher.UIThread.Post(ReplayEntranceAnimation); return; }
        CancelAnimations(); _entrancePending = true; InvalidateCircular();
    }
    public void CancelAnimations()
    {
        VerifyAccess(); if (_rendering) { Dispatcher.UIThread.Post(CancelAnimations); return; }
        // A source change may be pending before its first draw. Cancellation must
        // settle that latest intent rather than briefly publishing an older target.
        if (_dirty && !_building) _ = GetSlices();
        VerifyAccess(); _animationGeneration++; _frameRequestGeneration = -1;
        if (_animationTarget is { } target) _slices = Array.AsReadOnly(target.Where(s => !s.IsExiting).ToArray());
        _animationTarget = _animationFrom = null; _easing = null; _entrancePending = false;
        if (SetAnimationStatus(ChartAnimationState.Idle, 1)) RequestCircularRender();
    }
    public void PauseAnimations() { VerifyAccess(); if (_rendering) Dispatcher.UIThread.Post(PauseAnimations); else SetCurrentValue(IsAnimationPausedProperty, true); }
    public void ResumeAnimations() { VerifyAccess(); if (_rendering) Dispatcher.UIThread.Post(ResumeAnimations); else SetCurrentValue(IsAnimationPausedProperty, false); }
}
