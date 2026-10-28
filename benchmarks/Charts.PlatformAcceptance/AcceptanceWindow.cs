using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace Charts.PlatformAcceptance;

internal sealed class AcceptanceSuiteResult
{
    public Dictionary<string, bool> Checks { get; } = new(StringComparer.Ordinal);
    public List<object> Observations { get; } = [];
    public List<object> Artifacts { get; } = [];
    public List<string> Faults { get; } = [];
}

// This window consumes public chart contracts. Programmatic probes and actual
// window-routed input are recorded separately; neither is physical-device proof.
internal sealed partial class AcceptanceWindow : Window
{
    private readonly Options _options;
    private readonly AcceptanceSuiteResult _suite = new();
    private readonly Grid _body = new();
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    private readonly StackPanel _commands = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
    private readonly Button _reset = new() { Content = "重置交互 / Reset", Name = "ResetInteraction" };
    private readonly Button _update = new() { Content = "更新交互证据 / Save checkpoint", Name = "SaveCheckpoint" };
    private readonly Button _auxiliary = new() { Content = "打开辅助窗口 / Focus window", Name = "OpenFocusWindow" };
    private readonly Button _finish = new() { Content = "保存并结束验证 / Finish", Name = "FinishAcceptance" };
    private readonly TaskCompletionSource _interactiveFinished = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly StreamWriter _events;
    private CartesianChart _cartesian = null!;
    private CartesianChart _dates = null!;
    private FunnelChart _funnel = null!;
    private MosaicChart _mosaic = null!;
    private ChartRangeSelector _range = null!;
    private TextBlock _cartesianCaption = null!, _funnelCaption = null!, _mosaicCaption = null!;
    private ChartPlotAreaContext? _cartesianContext;
    private ChartPlotAreaContext? _dateContext;
    private ChartDataPlotAreaContext? _funnelContext, _mosaicContext;
    private readonly List<object> _equalPairs = [], _differentPairs = [], _glyphProofs = [];
    private readonly List<object> _glyphOrientationProofs = [];
    private readonly HashSet<string> _observedSelectedIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _observedBrowsingIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _keyboardActivatedIds = new(StringComparer.Ordinal);
    private bool _externalPhase, _leftDragActive, _panObserved, _wheelZoomObserved;
    private int _eventSequence, _checkpoint, _pointerPressed, _wheelEvents, _keyEvents, _userRangeChanges, _resetClicks, _resizeEvents, _deactivated, _auxiliaryOpened, _auxiliaryClosed;
    private Window? _focusWindow;
    private Size _interactiveInitialSize;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly JsonSerializerOptions CompactJson = new(JsonSerializerDefaults.Web);

    internal AcceptanceWindow(Options options)
    {
        _options = options;
        Directory.CreateDirectory(options.Output);
        _events = new StreamWriter(File.Open(Path.Combine(options.Output, "native-events.jsonl"), FileMode.CreateNew, FileAccess.Write, FileShare.Read)) { AutoFlush = true };
        Width = 1280; Height = 800; MinWidth = 560; MinHeight = 340; CanResize = true;
        Title = $"C7-02 Charts platform acceptance · {options.Renderer} · {options.Culture} · {options.Theme}";
        Background = options.Theme is "dark" or "hc-dark" ? Brushes.Black : Brushes.White;
        var heading = new TextBlock { Text = "C7-02 · 桌面 / DPI / 字体与原始数据交互", FontSize = 18, FontWeight = FontWeight.SemiBold };
        var instructions = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap,
            Text = "系统输入：点击左图 B；Home / End / Space；滚轮缩放；拖动图内平移；拖动范围条；重置；拖窗口边框；打开辅助窗口并按 Escape。每步可保存证据，最后点击 Finish。" };
        _commands.Children.Add(_reset); _commands.Children.Add(_update); _commands.Children.Add(_auxiliary); _commands.Children.Add(_finish);
        var top = new StackPanel { Spacing = 6, Children = { heading, instructions, _commands, _status } };
        var root = new Grid { RowDefinitions = new("Auto,*"), Margin = new Thickness(12), RowSpacing = 8 };
        root.Children.Add(top); Grid.SetRow(_body, 1); root.Children.Add(_body);
        root.Children.Add(new PlatformProbeControl { Width = 4, Height = 4, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false });
        Content = root;
        BuildScenes();
        _commands.IsVisible = false;
        _reset.Click += (_, _) => { _resetClicks++; _cartesian.ResetViewport(); _cartesian.SelectedPoint = null; _funnel.ClearDataSelection(); _mosaic.ClearDataSelection(); SaveInteractiveCheckpoint("reset-button"); };
        _update.Click += (_, _) => SaveInteractiveCheckpoint("checkpoint-button");
        _auxiliary.Click += (_, _) => OpenFocusWindow();
        _finish.Click += (_, _) => { SaveInteractiveCheckpoint("finish-button"); _interactiveFinished.TrySetResult(); };
        AddHandler(PointerPressedEvent, OnObservedPointerPressed, RoutingStrategies.Tunnel, true);
        AddHandler(PointerReleasedEvent, (_, args) => { if (_externalPhase) { RecordEvent("pointer-released", new { position = P(args.GetPosition(this)) }); _leftDragActive = false; } }, RoutingStrategies.Bubble, true);
        AddHandler(PointerMovedEvent, (_, args) => { if (_externalPhase && _leftDragActive) RecordEvent("pointer-moved-with-left-drag", new { position = P(args.GetPosition(this)) }); }, RoutingStrategies.Bubble, true);
        AddHandler(PointerWheelChangedEvent, OnObservedWheel, RoutingStrategies.Tunnel, true);
        AddHandler(KeyDownEvent, (_, args) =>
        {
            if (!_externalPhase) return;
            _keyEvents++; var key = args.Key; var modifiers = args.KeyModifiers.ToString();
            var source = args.Source as Visual;
            var sourceBelongsToChart = ReferenceEquals(source, _cartesian) || source?.GetVisualAncestors().Any(parent => ReferenceEquals(parent, _cartesian)) == true;
            var chartHadKeyboardFocus = _cartesian.IsKeyboardFocusWithin;
            var chartKey = sourceBelongsToChart && chartHadKeyboardFocus;
            var sourceType = args.Source?.GetType().FullName;
            var sourceName = (args.Source as StyledElement)?.Name;
            var currentBefore = _cartesian.CurrentPoint?.StableId?.ToString();
            var selectedBefore = _cartesian.SelectedPoint?.StableId?.ToString();
            RecordEvent("key-down", new { key = key.ToString(), modifiers, chartHadKeyboardFocus, sourceBelongsToChart,
                sourceType, sourceName, currentBefore, selectedBefore });
            Dispatcher.UIThread.Post(() =>
            {
                if (!_externalPhase) return;
                if (chartKey && (key is Key.Space or Key.Enter) && _cartesian.CurrentPoint?.StableId?.ToString() is { } id && Equals(_cartesian.SelectedPoint?.StableId?.ToString(), id))
                    _keyboardActivatedIds.Add(id);
                RecordEvent("key-state-after-routing", new { key = key.ToString(), modifiers, chartHadKeyboardFocus, sourceBelongsToChart, sourceType, sourceName,
                    currentBefore, selectedBefore, currentAfter = _cartesian.CurrentPoint?.StableId?.ToString(), selectedAfter = _cartesian.SelectedPoint?.StableId?.ToString() });
                SaveInteractiveCheckpoint("key-state");
            }, DispatcherPriority.Background);
        }, RoutingStrategies.Tunnel, true);
        Deactivated += (_, _) => { if (_externalPhase) { _deactivated++; RecordEvent("window-deactivated", null); } };
        Activated += (_, _) => { if (_externalPhase) RecordEvent("window-activated", null); };
        SizeChanged += (_, _) => { if (_externalPhase && Math.Abs(ClientSize.Width - _interactiveInitialSize.Width) + Math.Abs(ClientSize.Height - _interactiveInitialSize.Height) > 1) { _resizeEvents++; RecordEvent("window-client-size-changed", new { clientSize = S(ClientSize), renderScaling = RenderScaling }); } };
        Closed += (_, _) =>
        {
            if (_options.Interactive && !_interactiveFinished.Task.IsCompleted)
                _interactiveFinished.TrySetException(new InvalidOperationException("Acceptance window closed before Finish; external input protocol incomplete."));
            _externalPhase = false; _focusWindow?.Close(); _events.Dispose();
        };
    }

    internal async Task<AcceptanceSuiteResult> RunAsync()
    {
        try
        {
            await RunProgrammaticScenesAsync();
            _suite.Observations.Add(new { kind = "oracle", oracle = new { pngEqualPairs = _equalPairs, pngDifferentPairs = _differentPairs, glyphProofs = _glyphProofs, glyphOrientationProofs = _glyphOrientationProofs } });
            await ShowInteractiveBoardAsync();
            await PlatformDiagnostics.ObserveNativeFrameAsync(this);
            if (_options.Interactive)
            {
                _commands.IsVisible = true;
                await SettleAsync();
                _observedSelectedIds.Clear(); _observedBrowsingIds.Clear(); _keyboardActivatedIds.Clear();
                _pointerPressed = _wheelEvents = _keyEvents = _userRangeChanges = _resetClicks = _resizeEvents = _deactivated = _auxiliaryOpened = _auxiliaryClosed = 0;
                _leftDragActive = _panObserved = _wheelZoomObserved = false;
                _externalPhase = true; _interactiveInitialSize = ClientSize;
                WriteReady(); UpdateStatus();
                await _interactiveFinished.Task;
                _externalPhase = false;
                Check("interactive-original-point-B-selected", _observedSelectedIds.Contains("B"));
                Check("interactive-keyboard-original-point-C-selected", _observedSelectedIds.Contains("C") && _observedBrowsingIds.Contains("C") && _keyboardActivatedIds.Contains("C") && _keyEvents > 0);
                Check("interactive-wheel-changes-viewport", _wheelEvents > 0 && _wheelZoomObserved);
                Check("interactive-left-drag-changes-viewport", _panObserved);
                Check("interactive-range-user-request-observed", _userRangeChanges > 0);
                Check("interactive-reset-command-observed", _resetClicks > 0);
                Check("interactive-native-window-resize-observed", _resizeEvents > 0);
                Check("interactive-owned-focus-window-cycle-observed", _auxiliaryOpened > 0 && _auxiliaryClosed > 0 && _deactivated > 0);
                _suite.Observations.Add(new { kind = "interactive-final", state = InteractiveState(), qualification = "Native-window event and state observations require an independent external system-input action receipt; physical hardware remains unverified." });
            }
        }
        catch (Exception error)
        {
            _suite.Checks["suite-unhandled-failure"] = false;
            _suite.Faults.Add(error.ToString());
        }
        return _suite;
    }

    private async Task SettleAsync()
    {
        UpdateLayout();
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
        await Task.Delay(65);
        UpdateLayout();
    }

    private void Check(string name, bool condition)
    {
        if (!_suite.Checks.TryAdd(name, condition)) throw new InvalidOperationException("Duplicate acceptance check: " + name);
        if (!condition) _suite.Faults.Add(name);
    }

    private void OnObservedPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (!_externalPhase) return;
        _pointerPressed++;
        var chartPosition = args.GetPosition(_cartesian);
        _leftDragActive = args.GetCurrentPoint(_cartesian).Properties.IsLeftButtonPressed && _cartesian.PlotArea.Contains(chartPosition);
        RecordEvent("pointer-pressed", new { position = P(args.GetPosition(this)), chartPosition = P(chartPosition), left = args.GetCurrentPoint(this).Properties.IsLeftButtonPressed });
        Dispatcher.UIThread.Post(() => SaveInteractiveCheckpoint("pointer-state"), DispatcherPriority.Background);
    }

    private void OnObservedWheel(object? sender, PointerWheelEventArgs args)
    {
        if (!_externalPhase) return;
        _wheelEvents++; var before = _cartesian.VisibleRange;
        RecordEvent("pointer-wheel", new { position = P(args.GetPosition(this)), delta = new[] { args.Delta.X, args.Delta.Y }, before });
        Dispatcher.UIThread.Post(() => { _wheelZoomObserved |= before != _cartesian.VisibleRange; SaveInteractiveCheckpoint("wheel-state"); }, DispatcherPriority.Background);
    }

    private void RecordEvent(string kind, object? detail)
    {
        if (_eventSequence >= 2048) return;
        _events.WriteLine(JsonSerializer.Serialize(new { sequence = ++_eventSequence, utc = DateTimeOffset.UtcNow,
            kind, detail, state = InteractiveState(), inputClassification = "observed-native-window-routed-event", physicalInputValidated = false }, CompactJson));
    }

    private object InteractiveState() => new
    {
        clientSize = S(ClientSize), renderScaling = RenderScaling, active = IsActive,
        cartesianSelectedId = _cartesian.SelectedPoint?.StableId?.ToString(), currentPointId = _cartesian.CurrentPoint?.StableId?.ToString(),
        viewport = _cartesian.VisibleRange, range = new[] { _range.Range.Lower, _range.Range.Upper },
        funnelSelectedIds = _funnel.SelectedData.Select(p => p.StableId?.ToString()).ToArray(),
        mosaicSelectedIds = _mosaic.SelectedData.Select(p => p.StableId?.ToString()).ToArray(),
        counters = new { pointerPressed = _pointerPressed, wheel = _wheelEvents, key = _keyEvents, rangeUserChanges = _userRangeChanges,
            resetClicks = _resetClicks, resizeEvents = _resizeEvents, deactivated = _deactivated, auxiliaryOpened = _auxiliaryOpened, auxiliaryClosed = _auxiliaryClosed },
        observedSelectedIds = _observedSelectedIds.Order().ToArray(), observedBrowsingIds = _observedBrowsingIds.Order().ToArray(), keyboardActivatedIds = _keyboardActivatedIds.Order().ToArray(),
        panObserved = _panObserved, wheelZoomObserved = _wheelZoomObserved,
    };

    private void SaveInteractiveCheckpoint(string trigger)
    {
        if (!_externalPhase) return;
        UpdateLayout();
        UpdateStatus();
        var index = ++_checkpoint;
        WriteJson($"interactive-checkpoint-{index:D3}.json", new { schema = "charts-platform-interactive-checkpoint/v1", sequence = index,
            utc = DateTimeOffset.UtcNow, trigger, eventSequence = _eventSequence, state = InteractiveState(), targets = InteractiveTargets(), physicalInputValidated = false });
    }

    private void UpdateStatus() => _status.Text = $"实际缩放 {RenderScaling:0.##} · 客户区 {ClientSize.Width:0}×{ClientSize.Height:0} DIP · 选择 {_cartesian.SelectedPoint?.StableId ?? "—"} · " +
        $"Pointer {_pointerPressed} / Wheel {_wheelEvents} / Key {_keyEvents} / Range {_userRangeChanges} / Resize {_resizeEvents} · 本窗口事件不等于物理设备认证";

    private void OpenFocusWindow()
    {
        if (_focusWindow is not null) { _focusWindow.Activate(); return; }
        var child = _focusWindow = new Window { Width = 440, Height = 220, Title = "C7-02 owned focus probe · Escape to close",
            Content = new TextBlock { Margin = new Thickness(24), TextWrapping = TextWrapping.Wrap, FontSize = 18,
                Text = "辅助窗口已获得系统焦点。\n按 Escape 关闭，返回 Charts 验证窗口。\nOwned focus window: press Escape." } };
        child.KeyDown += (_, args) => { if (args.Key == Key.Escape) { args.Handled = true; child.Close(); } };
        child.Opened += (_, _) => { _auxiliaryOpened++; RecordEvent("auxiliary-window-opened", null); };
        child.Closed += (_, _) => { _auxiliaryClosed++; _focusWindow = null; RecordEvent("auxiliary-window-closed", null); SaveInteractiveCheckpoint("focus-window-closed"); };
        child.Show(this);
    }

    private void WriteReady() => WriteJson("ready.json", new { schema = "charts-platform-interactive-ready/v1", processId = Environment.ProcessId,
        utc = DateTimeOffset.UtcNow, title = Title, platformHandle = TryGetPlatformHandle()?.Handle.ToInt64(), clientSize = S(ClientSize),
        actualRenderScaling = RenderScaling, targets = InteractiveTargets(), initialState = InteractiveState(),
        requiredActions = new[] { "click original point B", "Home, End, Space while chart focused", "wheel inside plot", "left drag inside zoomed plot", "drag range handle", "click Reset", "resize native window border", "open owned Focus window and press Escape", "click Finish" },
        physicalInputValidated = false, compositorPresentValidated = false });

    private object InteractiveTargets()
    {
        var plot = _cartesian.PlotArea;
        _cartesianContext = _cartesianCaption.DataContext as ChartPlotAreaContext ?? _cartesianContext;
        var b = _cartesianContext?.Project(2, 60) ?? new Point(plot.Width / 2, plot.Height * .4);
        var c = _cartesianContext?.Project(3, 80) ?? new Point(plot.Width * .75, plot.Height * .2);
        var lower = _range.HandleSize / 2 + _range.Range.Lower * Math.Max(0, _range.Bounds.Width - _range.HandleSize);
        var upper = _range.HandleSize / 2 + _range.Range.Upper * Math.Max(0, _range.Bounds.Width - _range.HandleSize);
        return new { chartPlot = Target(_cartesian, plot), originalPointB = TargetPoint(_cartesian, new(plot.Left + b.X, plot.Top + b.Y)),
            originalPointC = TargetPoint(_cartesian, new(plot.Left + c.X, plot.Top + c.Y)), range = Target(_range, new Rect(_range.Bounds.Size)),
            targetProjectionVisibleRange = _cartesianContext?.VisibleRange,
            rangeLowerHandle = TargetPoint(_range, new(lower, _range.Bounds.Height / 2)),
            rangeUpperHandle = TargetPoint(_range, new(upper, _range.Bounds.Height / 2)),
            resetButton = Target(_reset, new Rect(_reset.Bounds.Size)), checkpointButton = Target(_update, new Rect(_update.Bounds.Size)),
            focusButton = Target(_auxiliary, new Rect(_auxiliary.Bounds.Size)), finishButton = Target(_finish, new Rect(_finish.Bounds.Size)) };
    }

    private object Target(Control control, Rect rect) => new { localDips = R(rect), windowDips = R(new Rect(control.TranslatePoint(rect.TopLeft, this) ?? rect.TopLeft, rect.Size)),
        screenPixelsTopLeft = PP(control.PointToScreen(rect.TopLeft)), screenPixelsBottomRight = PP(control.PointToScreen(rect.BottomRight)) };
    private object TargetPoint(Control control, Point point) => new { localDips = P(point), windowDips = P(control.TranslatePoint(point, this) ?? point), screenPixels = PP(control.PointToScreen(point)) };
    private void WriteJson(string filename, object value) { using var file = File.Open(Path.Combine(_options.Output, filename), FileMode.CreateNew, FileAccess.Write, FileShare.Read); JsonSerializer.Serialize(file, value, Json); }
    private static double[] P(Point value) => [value.X, value.Y];
    private static double[] S(Size value) => [value.Width, value.Height];
    private static int[] PP(PixelPoint value) => [value.X, value.Y];
    private static double[] R(Rect value) => [value.X, value.Y, value.Width, value.Height];
}
