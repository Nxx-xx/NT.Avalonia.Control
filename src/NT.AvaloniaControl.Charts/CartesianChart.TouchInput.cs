using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.Charts;

public sealed partial class CartesianChart
{
    public static readonly StyledProperty<bool> IsTouchEnabledProperty =
        AvaloniaProperty.Register<CartesianChart, bool>(nameof(IsTouchEnabled), true);
    public static readonly StyledProperty<double> TouchPanThresholdProperty =
        AvaloniaProperty.Register<CartesianChart, double>(nameof(TouchPanThreshold), 8,
            validate: value => double.IsFinite(value) && value >= 0);

    /// <summary>Enables chart touch taps, single-finger pan and two-finger pinch. Pan and zoom still require their respective switches.</summary>
    public bool IsTouchEnabled { get => GetValue(IsTouchEnabledProperty); set => SetValue(IsTouchEnabledProperty, value); }
    /// <summary>Minimum accumulated finger movement or pinch-distance change, in device-independent pixels, before a gesture is claimed.</summary>
    public double TouchPanThreshold { get => GetValue(TouchPanThresholdProperty); set => SetValue(TouchPanThresholdProperty, value); }

    private sealed class TouchContact(IPointer pointer, Point position, ChartSeries? legendSeries)
    {
        public IPointer Pointer { get; } = pointer;
        public Point Origin { get; set; } = position;
        public Point Position { get; set; } = position;
        public ChartSeries? LegendSeries { get; } = legendSeries;
    }

    private readonly List<TouchContact> _touchContacts = [];
    private readonly HashSet<IPointer> _delegatedTouchPointers = [];
    private readonly List<Visual> _touchInputAncestors = [];
    private ChartScene? _touchScene;
    private Point _touchAnchor;
    private double _touchDistance;
    private bool _touchClaimed;
    private bool _touchHadMultiple;
    private bool _touchMoved;
    private bool _releasingTouchCapture;
    private long _touchRevision;
    private ChartScene? _nativePinchScene;
    private Point _nativePinchAnchor;
    private Window? _touchInputWindow;
    private bool IsTouchInputActive => _touchContacts.Count != 0 || _nativePinchScene is not null;

    private void InitializeTouchInput()
    {
        AddHandler(PinchEvent, HandleNativePinch, RoutingStrategies.Bubble);
        AddHandler(PinchEndedEvent, HandleNativePinchEnded, RoutingStrategies.Bubble);
    }

    private void HandleNativePinch(object? sender, PinchEventArgs args)
    {
        // Do not install a recognizer: platforms/hosts that already supply this routed
        // gesture may use it, while our own two contact stream must never zoom twice.
        if (args.Handled || _touchContacts.Count != 0 || !IsTouchEnabled || !IsZoomEnabled || !_attached ||
            !IsEffectivelyEnabled || !IsEffectivelyVisible || InteractionAxes == ChartInteractionAxes.None ||
            InputRoutingMode == ChartInputRoutingMode.ScrollViewer) return;
        var origin = args.ScaleOrigin;
        if (args.Source is Visual source && !ReferenceEquals(source, this))
        {
            if (source.TranslatePoint(origin, this) is not { } translated) { InvalidateInputTransform(); return; }
            origin = translated;
        }
        if (!double.IsFinite(args.Scale) || args.Scale <= 0 || !FiniteTouchPosition(origin))
        { InvalidateInputTransform(); return; }
        if (_nativePinchScene is null)
        {
            var scene = GetScene();
            if (scene is null || !scene.HasPoints || !scene.Plot.Contains(origin) ||
                IsLegendFloating && LegendPanelContains(scene, origin)) return;
            var expectedInputRevision = unchecked(_inputTransformRevision + 1);
            InvalidateInputTransform();
            if (_inputTransformRevision != expectedInputRevision) return;
            var startRevision = _touchRevision;
            StopRectangle(); StopPan();
            ClearHover();
            if (expectedInputRevision != _inputTransformRevision || startRevision != _touchRevision || !_attached || !IsTouchEnabled || !IsZoomEnabled ||
                !IsEffectivelyEnabled || !IsEffectivelyVisible || !ReferenceEquals(scene, _scene)) return;
            _nativePinchScene = scene;
            _nativePinchAnchor = origin;
        }
        var revision = _touchRevision;
        if (TryApplyInputTransform(_nativePinchScene, _nativePinchAnchor,
                IsPanEnabled ? origin : _nativePinchAnchor, 1 / Math.Clamp(args.Scale, 1e-300, 1e300), InteractionAxes) && revision == _touchRevision)
        { ClearHover(); args.Handled = true; }
    }

    private void HandleNativePinchEnded(object? sender, PinchEndedEventArgs args)
    {
        if (_nativePinchScene is not null) InvalidateInputTransform();
    }

    private bool HandleTouchPressed(PointerPressedEventArgs args)
    {
        if (args.Pointer.Type != PointerType.Touch) return false;
        if (_touchContacts.Count != 0 && !_touchContacts.Any(contact => ReferenceEquals(contact.Pointer, args.Pointer)))
        { _touchHadMultiple = true; _touchMoved = true; }
        // A touch stream must never enter the mouse press path, whose selection is immediate.
        if (!IsTouchEnabled || !_attached || !IsEffectivelyEnabled || !IsEffectivelyVisible ||
            _delegatedTouchPointers.Count != 0) return true;
        var position = args.GetPosition(this);
        var scene = GetScene();
        if (!FiniteTouchPosition(position) || scene is null) return true;
        var legend = (IsLegendInteractive || IsLegendFloating) ? LegendItemAt(scene, position)?.Series : null;
        if (legend is null && (!scene.HasPoints || !scene.Plot.Contains(position) ||
            IsLegendFloating && LegendPanelContains(scene, position))) return true;
        if (_touchContacts.Any(contact => ReferenceEquals(contact.Pointer, args.Pointer))) return true;
        var focusRevision = _touchRevision;
        if (IsKeyboardNavigationEnabled) Focus();
        if (focusRevision != _touchRevision || !_attached || !IsEffectivelyEnabled || GetScene() is not { } focusedScene) return true;
        scene = focusedScene;
        position = args.GetPosition(this);
        if (!FiniteTouchPosition(position)) return true;
        legend = (IsLegendInteractive || IsLegendFloating) ? LegendItemAt(scene, position)?.Series : null;
        if (legend is null && (!scene.HasPoints || !scene.Plot.Contains(position) ||
            IsLegendFloating && LegendPanelContains(scene, position))) return true;
        var startRevision = _touchRevision;
        var startInputRevision = _inputTransformRevision;
        StopRectangle(); StopPan();
        ClearHover();
        if (startInputRevision != _inputTransformRevision || startRevision != _touchRevision || !_attached || !IsTouchEnabled || !IsEffectivelyEnabled ||
            !IsEffectivelyVisible || !ReferenceEquals(scene, _scene)) return true;
        _touchContacts.Add(new(args.Pointer, position, legend));
        if (_touchContacts.Count == 1)
        {
            _nativePinchScene = null;
            _touchMoved = false;
            _touchHadMultiple = false;
        }
        else
        {
            _touchHadMultiple = true;
            _touchMoved = true;
        }
        RebaseTouchGesture(scene);
        if (_touchClaimed)
        {
            args.Pointer.Capture(this);
            args.PreventGestureRecognition();
            args.Handled = true;
        }
        return true;
    }

    private bool HandleTouchMoved(PointerEventArgs args)
    {
        if (args.Pointer.Type != PointerType.Touch) return false;
        var contact = _touchContacts.FirstOrDefault(item => ReferenceEquals(item.Pointer, args.Pointer));
        if (contact is null || _touchScene is not { } initial) return true;
        var position = args.GetPosition(this);
        if (!FiniteTouchPosition(position)) { InvalidateInputTransform(); return true; }
        contact.Position = position;
        if (_touchContacts.Any(item => item.LegendSeries is not null))
        {
            _touchMoved |= _touchContacts.Any(item => TouchDifference(item.Position, item.Origin).Length > TouchPanThreshold);
            return true;
        }
        var anchor = TouchCentroid();
        var delta = TouchDifference(anchor, _touchAnchor);
        var distance = TouchDistance();
        var multiple = _touchContacts.Count >= 2;
        var crossed = delta.Length > TouchPanThreshold ||
            multiple && Math.Abs(distance - _touchDistance) > TouchPanThreshold;
        if (!_touchClaimed && !crossed) return true;
        _touchMoved = true;
        if (!_touchClaimed && InputRoutingMode == ChartInputRoutingMode.ScrollViewer)
        {
            foreach (var item in _touchContacts) _delegatedTouchPointers.Add(item.Pointer);
            InvalidateInputTransform(preserveTouch: true);
            ClearTouchGesture(releaseCapture: false);
            return true;
        }
        if (!multiple && !_touchClaimed && InputRoutingMode == ChartInputRoutingMode.Auto)
        {
            var dominant = Math.Abs(delta.X) >= Math.Abs(delta.Y) ? new Vector(delta.X, 0) : new Vector(0, delta.Y);
            if (CanInputScrollAncestorMove(dominant))
            {
                // Once scrolling wins, do not steal the same finger halfway through its drag.
                foreach (var item in _touchContacts) _delegatedTouchPointers.Add(item.Pointer);
                InvalidateInputTransform(preserveTouch: true);
                ClearTouchGesture(releaseCapture: false);
                return true;
            }
        }
        var factor = multiple && IsZoomEnabled && _touchDistance > 1e-6 && distance > 1e-6
            ? _touchDistance / distance : 1;
        var currentAnchor = IsPanEnabled ? anchor : _touchAnchor;
        if (InteractionAxes == ChartInteractionAxes.None || !IsPanEnabled && factor == 1)
        {
            if (_touchClaimed) args.Handled = true;
            return true;
        }
        var revision = _touchRevision;
        var changed = TryApplyInputTransform(initial, _touchAnchor, currentAnchor, factor, InteractionAxes);
        // Two-way bindings and viewport callbacks may replace data, disable, detach or cancel.
        if (revision != _touchRevision || _touchContacts.Count == 0) return true;
        if (!changed && !_touchClaimed) return true;
        if (!_touchClaimed)
        {
            _touchClaimed = true;
            foreach (var item in _touchContacts.ToArray())
            {
                item.Pointer.Capture(this);
                if (revision != _touchRevision) return true;
            }
        }
        args.PreventGestureRecognition();
        ClearHover();
        args.Handled = true;
        return true;
    }

    private bool HandleTouchReleased(PointerReleasedEventArgs args)
    {
        if (args.Pointer.Type != PointerType.Touch) return false;
        if (_delegatedTouchPointers.Remove(args.Pointer)) return true;
        var contact = _touchContacts.FirstOrDefault(item => ReferenceEquals(item.Pointer, args.Pointer));
        if (contact is null) return true;
        var position = args.GetPosition(this);
        var claimed = _touchClaimed;
        if (claimed) args.PreventGestureRecognition();
        var tap = _touchContacts.Count == 1 && !_touchHadMultiple && !_touchMoved &&
            FiniteTouchPosition(position) && TouchDifference(position, contact.Origin).Length <= TouchPanThreshold;
        var scene = _touchScene;
        _touchContacts.Remove(contact);
        ReleaseTouchPointer(contact.Pointer);
        if (_touchContacts.Count != 0)
        {
            // A two-finger pinch can continue as a one-finger pan without a viewport jump.
            if (GetScene() is { } current) RebaseTouchGesture(current);
            else InvalidateInputTransform();
            if (claimed) args.Handled = true;
            return true;
        }
        ClearTouchGesture(releaseCapture: false);
        if (claimed) { args.Handled = true; return true; }
        if (!tap || scene is null || !_attached || !IsTouchEnabled || !IsEffectivelyEnabled || !IsEffectivelyVisible ||
            !ReferenceEquals(scene, _scene)) return true;
        if (contact.LegendSeries is { } legend)
        {
            if (IsLegendInteractive && ReferenceEquals(LegendItemAt(scene, position)?.Series, legend)) ToggleSeriesVisibility(legend);
            args.Handled = true;
            return true;
        }
        if (!scene.Plot.Contains(position)) return true;
        var selected = HitTestPoint(position);
        var seriesRevision = _seriesSelectionRevision;
        SelectFromPointer(selected, args.KeyModifiers);
        if (!_attached || !ReferenceEquals(scene, _scene)) { args.Handled = true; return true; }
        if (SeriesSelectionMode != ChartSelectionMode.None && seriesRevision == _seriesSelectionRevision)
            SelectSeriesFromPointer(selected?.Series ?? HitTestSegment(position)?.Series, args.KeyModifiers);
        var command = PointInvokedCommand;
        var inputRevision = _inputTransformRevision;
        if (_attached && ReferenceEquals(scene, _scene) && selected is not null && command?.CanExecute(selected) == true &&
            _attached && IsEffectivelyEnabled && IsEffectivelyVisible && ReferenceEquals(scene, _scene) &&
            inputRevision == _inputTransformRevision && ReferenceEquals(command, PointInvokedCommand))
            command.Execute(selected);
        args.Handled = true;
        return true;
    }

    private void RebaseTouchGesture(ChartScene scene)
    {
        InvalidateInputTransform(preserveTouch: true);
        _touchRevision++;
        _touchScene = scene;
        foreach (var item in _touchContacts) item.Origin = item.Position;
        _touchAnchor = TouchCentroid();
        _touchDistance = TouchDistance();
    }

    private Point TouchCentroid()
    {
        var first = _touchContacts[0].Position;
        return _touchContacts.Count == 1 ? first : new Point(
            (first.X + _touchContacts[1].Position.X) * .5, (first.Y + _touchContacts[1].Position.Y) * .5);
    }

    private double TouchDistance() => _touchContacts.Count < 2 ? 0 :
        TouchDifference(_touchContacts[0].Position, _touchContacts[1].Position).Length;

    private static Vector TouchDifference(Point first, Point second) => new(first.X - second.X, first.Y - second.Y);

    private static bool FiniteTouchPosition(Point point) => double.IsFinite(point.X) && double.IsFinite(point.Y);

    private void HandleTouchCaptureLost(PointerCaptureLostEventArgs args)
    {
        if (_releasingTouchCapture) return;
        if (_touchContacts.Any(contact => ReferenceEquals(contact.Pointer, args.Pointer))) InvalidateInputTransform();
        _delegatedTouchPointers.Remove(args.Pointer);
    }

    private void ReleaseTouchPointer(IPointer pointer)
    {
        if (!ReferenceEquals(pointer.Captured, this)) return;
        _releasingTouchCapture = true;
        try { pointer.Capture(null); }
        finally { _releasingTouchCapture = false; }
    }

    private void ClearTouchGesture(bool releaseCapture)
    {
        var contacts = _touchContacts.ToArray();
        _touchContacts.Clear();
        _touchScene = null;
        _touchClaimed = false;
        _touchHadMultiple = false;
        _touchMoved = false;
        _touchRevision++;
        if (releaseCapture) foreach (var contact in contacts) ReleaseTouchPointer(contact.Pointer);
    }

    private void StopTouchInput()
    {
        ClearTouchGesture(releaseCapture: true);
        _delegatedTouchPointers.Clear();
        _nativePinchScene = null;
    }

    private bool TouchInputPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property != IsTouchEnabledProperty && change.Property != TouchPanThresholdProperty) return false;
        InvalidateInputTransform();
        return true;
    }

    private void RefreshTouchInputAncestors()
    {
        var ancestors = _attached ? this.GetVisualAncestors().ToArray() : [];
        foreach (var ancestor in _touchInputAncestors.Except(ancestors).ToArray())
        { ancestor.PropertyChanged -= TouchInputAncestorChanged; _touchInputAncestors.Remove(ancestor); }
        foreach (var ancestor in ancestors.Except(_touchInputAncestors))
        { ancestor.PropertyChanged += TouchInputAncestorChanged; _touchInputAncestors.Add(ancestor); }
        var window = _attached ? TopLevel.GetTopLevel(this) as Window : null;
        if (ReferenceEquals(window, _touchInputWindow)) return;
        if (_touchInputWindow is { } oldWindow)
        {
            oldWindow.Deactivated -= TouchInputWindowDeactivated;
            oldWindow.RemoveHandler(KeyDownEvent, TouchInputWindowKeyDown);
        }
        _touchInputWindow = window;
        if (window is not null)
        {
            window.Deactivated += TouchInputWindowDeactivated;
            window.AddHandler(KeyDownEvent, TouchInputWindowKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        }
    }

    private void TouchInputWindowDeactivated(object? sender, EventArgs args) => CancelInput();

    private void TouchInputWindowKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key != Key.Escape || !IsTouchInputActive && _panPointer is null && _selectionPointer is null) return;
        CancelInput();
        args.Handled = true;
    }

    private void TouchInputAncestorChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == IsVisibleProperty && sender is Visual { IsVisible: false } ||
            change.Property == IsEnabledProperty && sender is InputElement { IsEffectivelyEnabled: false }) CancelInput();
    }
}
