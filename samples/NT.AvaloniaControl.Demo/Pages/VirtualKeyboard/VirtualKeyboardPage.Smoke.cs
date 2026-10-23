using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NT.AvaloniaControl.VirtualKeyboard;

namespace NT.AvaloniaControl.Demo.Pages.VirtualKeyboard;

internal sealed partial class VirtualKeyboardPage
{
    private readonly Stopwatch _presentationClock = Stopwatch.StartNew();
    private Dictionary<string, bool>? _pointerHitChecks;

    internal async Task RunKeyboardSmokeAsync(string[] args)
    {
        var outputPath = Argument(args, "--smoke-output") ?? Path.Combine("artifacts", "platform", "windows-keyboard-smoke.json");
        var imagePath = Argument(args, "--smoke-image");
        var checks = new Dictionary<string, bool>();
        var measurements = new Dictionary<string, double>();
        var pointerHitChecks = new Dictionary<string, bool>();
        _pointerHitChecks = pointerHitChecks;
        string? error = null;
        var nativeFocusValidated = false;
        try
        {
            _hostWindow.Activate();
            checks["nativeWindowCreated"] = _hostWindow.TryGetPlatformHandle() is { Handle: not 0 };
            checks["nativeWindowActive"] = await WaitForAsync(() => _hostWindow.IsActive);
            if (!checks["nativeWindowActive"])
                throw new InvalidOperationException("The native window did not activate. Native editing focus was not validated.");
            SelectTarget(_first);
            checks["actualEditingFocus"] = await WaitForAsync(() => _first.IsFocused && _keyboard.IsInputAvailable);
            if (!checks["actualEditingFocus"])
                throw new InvalidOperationException("The editor did not obtain actual focus. Native editing focus was not validated.");
            nativeFocusValidated = true;
            checks["virtualKeyboardPageSelected"] = ((MainWindow)_hostWindow).SelectedPageId == "virtualkeyboard";
            checks["templateAndEnglishRows"] = _keyboard.TemplateDiagnostic is null && Buttons().Count() == _keyboard.Rows.Sum(row => row.Keys.Count);
            measurements["firstKeyboardLayoutFromPageConstructionMs"] = _presentationClock.Elapsed.TotalMilliseconds;

            _first.Text = "ABC";
            _first.SelectionStart = 1;
            _first.SelectionEnd = 2;
            var latency = Stopwatch.StartNew();
            await Tap("a");
            measurements["routedKeyToTextChangeMs"] = latency.Elapsed.TotalMilliseconds;
            checks["selectionReplacementOnce"] = _first.Text == "AaC" && _first.CaretIndex == 2;
            checks["pointerPreservesEditorFocus"] = _first.IsFocused && _hostWindow.IsActive;
            checks["twoWayViewModelUpdate"] = _viewModel.FirstText == "AaC";
            RouteKey(_first, Key.Z, KeyModifiers.Control);
            checks["nativeUndoAndBinding"] = _first.Text == "ABC" && _viewModel.FirstText == "ABC";
            RouteKey(_first, Key.Y, KeyModifiers.Control);
            checks["nativeRedoAndBinding"] = _first.Text == "AaC" && _viewModel.FirstText == "AaC";

            await Tap("Shift");
            await Tap("b");
            checks["oneShotShift"] = _first.Text == "AaBC" && !_keyboard.IsShiftActive;
            await Tap("CapsLock");
            checks["capsLockVisualState"] = _keyboard.IsCapsLockActive && FindButton("CapsLock").IsLocked;
            var target = _keyboard.Target;
            _hostWindow.RequestedThemeVariant = ThemeVariant.Light;
            _hostWindow.UpdateLayout();
            var light = _keyboard.Background;
            _hostWindow.RequestedThemeVariant = ThemeVariant.Dark;
            _hostWindow.UpdateLayout();
            checks["lightDarkPreservesState"] = !Equals(light, _keyboard.Background) && _keyboard.IsCapsLockActive && ReferenceEquals(target, _keyboard.Target);
            ToggleResources();
            _hostWindow.UpdateLayout();
            checks["themeResourcesCanBeOverridden"] = FindButton("a").FontSize == 20 && FindButton("a").CornerRadius == new CornerRadius(12);
            ToggleResources();

            SelectTarget(_limited);
            _limited.Text = "1234567";
            _limited.CaretIndex = 7;
            await Tap("a");
            await Tap("b");
            checks["maxLengthRespected"] = _limited.Text == "1234567a";
            SelectTarget(_readOnly);
            var readOnlyText = _readOnly.Text;
            await Tap("a");
            checks["readOnlyUnavailable"] = !_keyboard.IsInputAvailable && _readOnly.Text == readOnlyText;
            SelectTarget(_disabled);
            checks["disabledCannotBecomeTarget"] = _keyboard.Target is null && !_disabled.IsFocused;

            SelectTarget(_password);
            _password.Text = string.Empty;
            await Tap("a");
            checks["passwordInputUsesMaskedTarget"] = _password.Text?.Length == 1 && _password.PasswordChar != '\0' && _password.IsFocused;
            _password.Text = string.Empty;
            SelectTarget(_numeric);
            _numeric.Text = string.Empty;
            await Tap("1");
            await Tap(".");
            await Tap("2");
            checks["numericLayoutInput"] = _numeric.Text == "1.2";
            _keyboard.ShowDecimalPoint = false;
            _keyboard.ShowNegativeSign = false;
            _hostWindow.UpdateLayout();
            checks["numericOptionsUpdateKeys"] = Buttons().All(button => button.Key!.Id is not "." and not "-");
            _keyboard.ShowDecimalPoint = true;
            _keyboard.ShowNegativeSign = true;

            SelectTarget(_multiline);
            _multiline.Text = "A";
            _multiline.CaretIndex = 1;
            var returnsBefore = _returnRequests;
            var nextBefore = _nextRequests;
            await Tap("Return");
            await Tap("Tab");
            checks["multilineEnterTabHandledOnce"] = _multiline.Text == "A" + _multiline.NewLine + "\t" && _returnRequests == returnsBefore && _nextRequests == nextBefore;
            SelectTarget(_first);
            await Tap("Return");
            checks["singleLineReturnHostRequest"] = _returnRequests == returnsBefore + 1 && ReferenceEquals(_keyboard.Target, _first);
            var firstText = _first.Text;
            await Tap("Tab");
            checks["singleLineTabHostChangesTarget"] = _nextRequests == nextBefore + 1 && _second.IsFocused && ReferenceEquals(_keyboard.Target, _second);
            _second.Text = string.Empty;
            await Tap("z");
            checks["targetSwitchWritesOnlyNewEditor"] = _second.Text == "z" && _first.Text == firstText && _viewModel.SecondText == "z";

            SelectTarget(_first);
            _first.Text = new string('x', 40);
            _first.CaretIndex = 40;
            _keyboard.RepeatDelay = TimeSpan.FromMilliseconds(100);
            _keyboard.RepeatInterval = TimeSpan.FromMilliseconds(40);
            var repeatClock = Stopwatch.StartNew();
            var repeatPress = await Press("Backspace");
            await Task.Delay(300);
            var repeatedLength = _first.Text!.Length;
            measurements["repeatObservationWindowMs"] = repeatClock.Elapsed.TotalMilliseconds;
            measurements["repeatActionsObservedIncludingInitial"] = 40 - repeatedLength;
            Release(repeatPress);
            await Task.Delay(150);
            checks["repeatAndReleaseStop"] = repeatedLength < 39 && _first.Text.Length == repeatedLength;
            var cancelled = await Press("Backspace");
            var beforeNavigation = _first.Text!.Length;
            var cancelClock = Stopwatch.StartNew();
            ((MainWindow)_hostWindow).SelectPage("overview");
            measurements["pageDeactivationCancelCallMs"] = cancelClock.Elapsed.TotalMilliseconds;
            await Task.Delay(200);
            Release(cancelled);
            checks["pageDeactivationStopsInput"] = _keyboard.Target is null && !_keyboard.IsInputAvailable && _first.Text.Length == beforeNavigation;
            ((MainWindow)_hostWindow).SelectPage("virtualkeyboard");
            checks["pageReactivationStartsNewSession"] = await WaitForAsync(() => _first.IsFocused && ReferenceEquals(_keyboard.Target, _first));
            await Task.Delay(150);
            checks["pageReactivationDoesNotResumeRepeat"] = _first.Text.Length == beforeNavigation;
            _keyboard.RepeatDelay = TimeSpan.FromMilliseconds(500);
            _keyboard.RepeatInterval = TimeSpan.FromMilliseconds(70);
            if (args.Contains("--keyboard-lifecycle-smoke", StringComparer.Ordinal))
                await RunLifecycleSmokeAsync(checks, measurements);
            if (imagePath is not null) CaptureImage(imagePath);
            checks["routedPointerCoordinatesHitKeys"] = pointerHitChecks.Count > 0 && pointerHitChecks.Values.All(value => value);
        }
        catch (Exception exception) { error = exception.ToString(); }
        _pointerHitChecks = null;
        checks["routedPointerCoordinatesHitKeys"] = pointerHitChecks.Count > 0 && pointerHitChecks.Values.All(value => value);
        var passed = error is null && checks.Count > 0 && checks.Values.All(value => value);
        try
        {
            var fullOutput = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullOutput)!);
            File.WriteAllText(fullOutput, JsonSerializer.Serialize(new
            {
                Passed = passed, TimestampUtc = DateTimeOffset.UtcNow,
                OS = RuntimeInformation.OSDescription, Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                Runtime = Environment.Version.ToString(), AvaloniaVersion = typeof(Application).Assembly.GetName().Version?.ToString(),
                WindowWidthDip = _hostWindow.ClientSize.Width, WindowHeightDip = _hostWindow.ClientSize.Height,
                RenderScaling = _hostWindow.RenderScaling, Checks = checks, PointerCoordinateHitTests = pointerHitChecks, Measurements = measurements, Error = error,
                NativeEditingFocusValidated = nativeFocusValidated, PhysicalPointerValidated = false, NativeTouchValidated = false,
                NativeImeValidated = false, DpiMatrixValidated = false, KylinValidated = false,
                ImageOutput = imagePath is null ? null : Path.GetFullPath(imagePath),
                ImageCaptureKind = "Avalonia RenderTargetBitmap rendering of the visible native Window; not a desktop screenshot.",
                MeasurementNotes = "First layout observation starts at page construction and includes window activation/dispatcher setup. Routed-key timing includes scrolling the key into view, waiting for the native hit-test scene to update, application event routing and layout; it is not physical pointer delivery or rendered-frame latency. Repeat observation windows include key preparation, and action counts include the initial action.",
                Notes = "Visible native Window with actual activation and editor focus. Pointer events and Ctrl+Z/Ctrl+Y are application-routed simulations; physical input, touch, IME and DPI matrix require separate device validation. Timings are observations, not latency guarantees. No password text is recorded.",
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.Shutdown(passed ? 0 : 1);
            else _hostWindow.Close();
        }
    }

    private IEnumerable<VirtualKeyboardKeyButton> Buttons() => _keyboard.GetVisualDescendants().OfType<VirtualKeyboardKeyButton>();
    private VirtualKeyboardKeyButton FindButton(string id)
    {
        _hostWindow.UpdateLayout();
        return Buttons().Single(button => button.Key?.Id == id);
    }
    private async Task Tap(string id) => Release(await Press(id));
    private async Task<(VirtualKeyboardKeyButton Button, Pointer Pointer, Point Point)> Press(string id)
    {
        var button = FindButton(id);
        button.BringIntoView();
        var hitReady = await WaitForAsync(() =>
        {
            var position = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), _hostWindow);
            var hit = position is { } candidate ? _hostWindow.InputHitTest(candidate) : null;
            return ReferenceEquals(hit, button) || hit is Visual hitVisual && hitVisual.GetVisualAncestors().Contains(button);
        });
        if (_pointerHitChecks is { } hitChecks)
            hitChecks[$"{id}:{hitChecks.Count + 1}"] = hitReady;
        if (!hitReady) throw new InvalidOperationException($"Native pointer coordinates did not hit key '{id}' after scrolling/render preparation.");
        var point = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), _hostWindow)!.Value;
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        button.RaiseEvent(new PointerPressedEventArgs(button, pointer, _hostWindow, point, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None, 1)
            { RoutedEvent = InputElement.PointerPressedEvent });
        return (button, pointer, point);
    }
    private void Release((VirtualKeyboardKeyButton Button, Pointer Pointer, Point Point) press)
    {
        press.Button.RaiseEvent(new PointerReleasedEventArgs(press.Button, press.Pointer, _hostWindow, press.Point, 1,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left)
            { RoutedEvent = InputElement.PointerReleasedEvent });
        press.Pointer.Dispose();
        _hostWindow.UpdateLayout();
    }
    private static void RouteKey(TextBox editor, Key key, KeyModifiers modifiers) => editor.RaiseEvent(new KeyEventArgs
    { RoutedEvent = InputElement.KeyDownEvent, Source = editor, Key = key, KeyModifiers = modifiers });
    private async Task<bool> WaitForAsync(Func<bool> condition)
    {
        for (var i = 0; i < 30; i++)
        {
            _hostWindow.UpdateLayout();
            if (condition()) return true;
            await Task.Delay(100);
        }
        return false;
    }
    private void CaptureImage(string path)
    {
        var size = _hostWindow.ClientSize;
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height)), new Vector(96, 96));
        bitmap.Render(_hostWindow);
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        bitmap.Save(fullPath);
    }
    private static string? Argument(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
