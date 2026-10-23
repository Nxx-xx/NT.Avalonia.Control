using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using NT.AvaloniaControl.VirtualKeyboard;
using KeyboardControl = NT.AvaloniaControl.VirtualKeyboard.VirtualKeyboard;

namespace VirtualKeyboard.Usage;

public partial class MainWindow
{
    private readonly Stopwatch _smokePresentationClock = Stopwatch.StartNew();
    private bool _smokeWindowClosed;

    private async Task RunKeyboardSmokeAsync(string[] args)
    {
        var outputPath = Argument(args, "--smoke-output") ?? Path.Combine("artifacts", "platform", "virtualkeyboard-usage-smoke.json");
        var imagePath = Argument(args, "--smoke-image");
        var editor = this.FindControl<TextBox>("Editor")!;
        var keyboard = this.FindControl<KeyboardControl>("Keyboard")!;
        var model = (InputViewModel)DataContext!;
        var checks = new Dictionary<string, bool>();
        var pointerHits = new Dictionary<string, bool>();
        var measurements = new Dictionary<string, double>();
        var unicodeObservations = new List<object>();
        var focusObservations = new List<object>();
        var nativeEditingFocusValidated = false;
        var actualClientSize = ClientSize;
        var renderScaling = RenderScaling;
        string? error = null;
        KeyPress? activePress = null;
        var returnRequests = 0;
        var nextRequests = 0;
        EventHandler<VirtualKeyboardTargetEventArgs> onReturn = (_, eventArgs) => { if (ReferenceEquals(eventArgs.Target, editor)) returnRequests++; };
        EventHandler<VirtualKeyboardTargetEventArgs> onNext = (_, eventArgs) => { if (ReferenceEquals(eventArgs.Target, editor)) nextRequests++; };
        keyboard.ReturnRequested += onReturn;
        keyboard.NextRequested += onNext;
        try
        {
            Activate();
            checks["nativeWindowCreated"] = TryGetPlatformHandle() is { Handle: not 0 };
            checks["nativeWindowActive"] = await WaitForAsync(() => IsActive);
            if (!checks["nativeWindowActive"])
                throw new InvalidOperationException("The native window did not activate; native editing focus is unverified.");
            checks["editorFocusEstablished"] = editor.Focus();
            if (editor.IsFocused) keyboard.Target = editor;
            checks["actualEditingFocus"] = await WaitForAsync(() => IsActive && editor.IsFocused && keyboard.IsInputAvailable);
            focusObservations.Add(new { Stage = "initial", WindowIsActive = IsActive, EditorIsFocused = editor.IsFocused, keyboard.IsInputAvailable });
            if (!checks["actualEditingFocus"])
                throw new InvalidOperationException("The editor did not establish actual editing focus; native editing focus is unverified.");
            nativeEditingFocusValidated = true;
            checks["defaultTemplateLoaded"] = keyboard.TemplateDiagnostic is null && Buttons(keyboard).Count() == keyboard.Rows.Sum(row => row.Keys.Count);
            measurements["firstKeyboardLayoutObservationFromConstructionMs"] = _smokePresentationClock.Elapsed.TotalMilliseconds;

            editor.Text = "ABC";
            editor.SelectionStart = 1;
            editor.SelectionEnd = 2;
            var keyClock = Stopwatch.StartNew();
            await Tap(keyboard, "a", pointerHits);
            measurements["routedPointerToTextObservationMs"] = keyClock.Elapsed.TotalMilliseconds;
            checks["selectionReplacementOnce"] = editor.Text == "AaC" && editor.CaretIndex == 2;
            checks["twoWayViewModelUpdate"] = model.Text == "AaC";
            checks["pointerPreservesEditorFocus"] = IsActive && editor.IsFocused && ReferenceEquals(keyboard.Target, editor);
            RouteKey(editor, Key.Z, KeyModifiers.Control);
            checks["undoPreservesBinding"] = editor.Text == "ABC" && model.Text == "ABC";
            RouteKey(editor, Key.Y, KeyModifiers.Control);
            checks["redoPreservesBinding"] = editor.Text == "AaC" && model.Text == "AaC";

            await Tap(keyboard, "Shift", pointerHits);
            await Tap(keyboard, "b", pointerHits);
            checks["oneShotShift"] = editor.Text == "AaBC" && !keyboard.IsShiftActive;
            await Tap(keyboard, "CapsLock", pointerHits);
            checks["capsLockVisualState"] = keyboard.IsCapsLockActive && FindButton(keyboard, "CapsLock").IsLocked;
            await Tap(keyboard, "CapsLock", pointerHits);

            editor.Text = "中😀e\u0301X";
            editor.CaretIndex = 3;
            await Tap(keyboard, "Backspace", pointerHits);
            checks["unicodeSurrogateDelete"] = editor.Text == "中e\u0301X" && IsWellFormedUtf16(editor.Text);
            unicodeObservations.Add(new { Scenario = "emoji-backspace", Text = editor.Text, editor.CaretIndex, WellFormedUtf16 = IsWellFormedUtf16(editor.Text!) });
            editor.Text = "中😀X";
            editor.CaretIndex = 3;
            await Tap(keyboard, "Left", pointerHits);
            checks["unicodeCaretSkipsSurrogatePair"] = editor.CaretIndex == 1;
            editor.Text = "e\u0301X";
            editor.CaretIndex = 2;
            await Tap(keyboard, "Backspace", pointerHits);
            var virtualCombiningText = editor.Text;
            var virtualCombiningCaret = editor.CaretIndex;
            editor.Text = "e\u0301X";
            editor.CaretIndex = 2;
            RouteKey(editor, Key.Back);
            checks["combiningDeleteMatchesEditorRoute"] = editor.Text == virtualCombiningText && editor.CaretIndex == virtualCombiningCaret && IsWellFormedUtf16(editor.Text!);
            unicodeObservations.Add(new { Scenario = "combining-backspace", Text = virtualCombiningText, CaretIndex = virtualCombiningCaret, Behavior = "Compared with the same TextBox public KeyDown path; no platform IME composition was active." });

            editor.MaxLength = 8;
            editor.Text = "1234567";
            editor.CaretIndex = 7;
            await Tap(keyboard, "a", pointerHits);
            await Tap(keyboard, "b", pointerHits);
            checks["maxLengthRespected"] = editor.Text == "1234567a" && model.Text == editor.Text;
            editor.MaxLength = 0;
            editor.IsReadOnly = true;
            var constrainedText = editor.Text;
            await Tap(keyboard, "a", pointerHits);
            checks["readOnlyRejectsInput"] = !keyboard.IsInputAvailable && editor.Text == constrainedText;
            editor.IsReadOnly = false;
            editor.IsEnabled = false;
            await Tap(keyboard, "a", pointerHits);
            checks["disabledRejectsInput"] = !keyboard.IsInputAvailable && editor.Text == constrainedText;
            editor.IsEnabled = true;
            checks["focusRestoredAfterConstraint"] = editor.Focus();
            keyboard.Target = editor;

            keyboard.Layout = VirtualKeyboardLayout.Numeric;
            editor.Text = string.Empty;
            await Tap(keyboard, "1", pointerHits);
            await Tap(keyboard, ".", pointerHits);
            await Tap(keyboard, "2", pointerHits);
            checks["numericLayoutInput"] = editor.Text == "1.2" && model.Text == "1.2";
            keyboard.Layout = VirtualKeyboardLayout.English;
            await Tap(keyboard, "Return", pointerHits);
            await Tap(keyboard, "Tab", pointerHits);
            checks["singleLineHostRequestsOnly"] = returnRequests == 1 && nextRequests == 1 && editor.Text == "1.2";
            editor.AcceptsReturn = true;
            editor.AcceptsTab = true;
            editor.Text = "A";
            editor.CaretIndex = 1;
            await Tap(keyboard, "Return", pointerHits);
            await Tap(keyboard, "Tab", pointerHits);
            checks["multilineEditorRouteOnly"] = editor.Text == "A" + editor.NewLine + "\t" && returnRequests == 1 && nextRequests == 1;
            editor.AcceptsReturn = false;
            editor.AcceptsTab = false;

            editor.Text = new string('x', 40);
            editor.CaretIndex = 40;
            keyboard.RepeatDelay = TimeSpan.FromMilliseconds(100);
            keyboard.RepeatInterval = TimeSpan.FromMilliseconds(40);
            var repeatClock = Stopwatch.StartNew();
            activePress = await Press(keyboard, "Backspace", pointerHits);
            await Task.Delay(300);
            var repeatedLength = editor.Text!.Length;
            measurements["repeatObservationWindowMs"] = repeatClock.Elapsed.TotalMilliseconds;
            measurements["repeatActionsObservedIncludingInitial"] = 40 - repeatedLength;
            Release(activePress.Value);
            activePress = null;
            await Task.Delay(160);
            checks["repeatAndReleaseStop"] = repeatedLength < 39 && editor.Text.Length == repeatedLength;
            checks["repeatBindingTracksEditor"] = model.Text == editor.Text;
            focusObservations.Add(new { Stage = "after-editing", WindowIsActive = IsActive, EditorIsFocused = editor.IsFocused, keyboard.IsInputAvailable });
            checks["focusPreservedThroughEditing"] = IsActive && editor.IsFocused && keyboard.IsInputAvailable;
            if (imagePath is not null) CaptureImage(imagePath);
            actualClientSize = ClientSize;
            renderScaling = RenderScaling;

            activePress = await Press(keyboard, "Backspace", pointerHits);
            var textBeforeClose = editor.Text;
            var closeClock = Stopwatch.StartNew();
            Close();
            measurements["closeAndCancelCallMs"] = closeClock.Elapsed.TotalMilliseconds;
            checks["windowClosedAndTargetUnbound"] = _smokeWindowClosed && keyboard.Target is null && !keyboard.IsInputAvailable;
            checks["closeReleasesPointerCapture"] = activePress.Value.Pointer.Captured is null;
            await Task.Delay(220);
            Release(activePress.Value);
            activePress = null;
            checks["closedWindowCannotReceiveOldPress"] = editor.Text == textBeforeClose && model.Text == textBeforeClose;
            measurements["postCloseObservationWindowMs"] = closeClock.Elapsed.TotalMilliseconds;
            checks["pointerCoordinatesHitKeys"] = pointerHits.Count > 0 && pointerHits.Values.All(value => value);
        }
        catch (Exception exception) { error = exception.ToString(); }
        finally
        {
            keyboard.CancelInput();
            if (activePress is { } remaining) { remaining.Pointer.Capture(null); remaining.Pointer.Dispose(); }
            keyboard.ReturnRequested -= onReturn;
            keyboard.NextRequested -= onNext;
        }

        checks["pointerCoordinatesHitKeys"] = pointerHits.Count > 0 && pointerHits.Values.All(value => value);

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
                WindowWidthDip = actualClientSize.Width, WindowHeightDip = actualClientSize.Height, RenderScaling = renderScaling,
                Checks = checks, FocusObservations = focusObservations, PointerCoordinateHitTests = pointerHits,
                UnicodeObservations = unicodeObservations, Measurements = measurements, Error = error,
                NativeEditingFocusValidated = nativeEditingFocusValidated,
                PhysicalPointerValidated = false, PhysicalKeyboardValidated = false, NativeTouchValidated = false,
                NativeImeValidated = false, DpiMatrixValidated = false, KylinValidated = false, GarbageCollectionValidated = false,
                ImageOutput = imagePath is null ? null : Path.GetFullPath(imagePath),
                ImageCaptureKind = "Avalonia RenderTargetBitmap rendering of the visible native Window; not a desktop screenshot.",
                MeasurementNotes = "First layout observation includes construction, activation and dispatcher setup. Input timing includes scrolling the key into view, waiting for the native hit-test scene to update, application event routing and observed text/layout; it is not physical delivery or rendered-frame latency. Repeat observation windows include key preparation, and counts include the initial action. Close observations verify target/capture/timer cancellation; MainWindow and its XAML name scope still own controls during the probe, so this is not a garbage collection test.",
                Notes = "Actual native Window.IsActive and TextBox.IsFocused are checked. Coordinates must hit the key after scrolling it into view, but pointer and Ctrl+Z/Ctrl+Y events are application-routed simulations. Physical keyboard/pointer, touch, active platform IME, DPI matrix and Kylin require separate validation. The sample has no password field.",
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.Shutdown(passed ? 0 : 1);
            else if (!_smokeWindowClosed) Close();
        }
    }

    private readonly record struct KeyPress(VirtualKeyboardKeyButton Button, Pointer Pointer, Point Point);
    private static IEnumerable<VirtualKeyboardKeyButton> Buttons(KeyboardControl keyboard) => keyboard.GetVisualDescendants().OfType<VirtualKeyboardKeyButton>();
    private VirtualKeyboardKeyButton FindButton(KeyboardControl keyboard, string id)
    {
        UpdateLayout();
        return Buttons(keyboard).Single(button => button.Key?.Id == id);
    }
    private async Task Tap(KeyboardControl keyboard, string id, Dictionary<string, bool> hits) => Release(await Press(keyboard, id, hits));
    private async Task<KeyPress> Press(KeyboardControl keyboard, string id, Dictionary<string, bool> hits)
    {
        var button = FindButton(keyboard, id);
        button.BringIntoView();
        var hitReady = await WaitForAsync(() =>
        {
            var position = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), this);
            var hit = position is { } candidate ? this.InputHitTest(candidate) : null;
            return ReferenceEquals(hit, button) || hit is Visual visual && visual.GetVisualAncestors().Contains(button);
        });
        hits[$"{id}:{hits.Count + 1}"] = hitReady;
        if (!hitReady) throw new InvalidOperationException($"Native pointer coordinates did not hit key '{id}' after scrolling/render preparation.");
        var point = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), this)!.Value;
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        button.RaiseEvent(new PointerPressedEventArgs(button, pointer, this, point, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None, 1)
            { RoutedEvent = InputElement.PointerPressedEvent });
        return new KeyPress(button, pointer, point);
    }
    private void Release(KeyPress press)
    {
        press.Button.RaiseEvent(new PointerReleasedEventArgs(press.Button, press.Pointer, this, press.Point, 1,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left)
            { RoutedEvent = InputElement.PointerReleasedEvent });
        press.Pointer.Dispose();
        if (!_smokeWindowClosed) UpdateLayout();
    }
    private static void RouteKey(TextBox editor, Key key, KeyModifiers modifiers = KeyModifiers.None) => editor.RaiseEvent(new KeyEventArgs
    { RoutedEvent = InputElement.KeyDownEvent, Source = editor, Key = key, KeyModifiers = modifiers });
    private async Task<bool> WaitForAsync(Func<bool> condition)
    {
        for (var i = 0; i < 30; i++)
        {
            UpdateLayout();
            if (condition()) return true;
            await Task.Delay(100);
        }
        return false;
    }
    private void CaptureImage(string path)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(ClientSize.Width), (int)Math.Ceiling(ClientSize.Height)), new Vector(96, 96));
        bitmap.Render(this);
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        bitmap.Save(fullPath);
    }
    private static bool IsWellFormedUtf16(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]))
            {
                if (++i >= text.Length || !char.IsLowSurrogate(text[i])) return false;
            }
            else if (char.IsLowSurrogate(text[i])) return false;
        }
        return true;
    }
    private static string? Argument(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
