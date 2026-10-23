using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.VisualTree;
using NT.AvaloniaControl.VirtualKeyboard;
using KeyboardControl = NT.AvaloniaControl.VirtualKeyboard.VirtualKeyboard;

namespace VirtualKeyboard.Usage;

public partial class MainWindow
{
    private const int BenchmarkWarmupCount = 20;
    private const int BenchmarkSampleCount = 100;
    private const int BenchmarkHitPollMilliseconds = 16;

    private async Task RunKeyboardBenchmarkAsync(string[] args)
    {
        var outputPath = Argument(args, "--benchmark-output") ?? Argument(args, "--smoke-output")
            ?? Path.Combine("artifacts", "platform", "virtualkeyboard-usage-benchmark.json");
        var imagePath = Argument(args, "--benchmark-image") ?? Argument(args, "--smoke-image");
        var editor = this.FindControl<TextBox>("Editor")!;
        var keyboard = this.FindControl<KeyboardControl>("Keyboard")!;
        var model = (InputViewModel)DataContext!;
        var checks = new Dictionary<string, bool>();
        var firstObservations = new Dictionary<string, double>();
        var groups = new List<BenchmarkSampleGroup>();
        var repeatProfiles = new List<BenchmarkRepeatProfile>();
        var focusObservations = new List<object>();
        object? visualFixture = null;
        var nativeFocusValidated = false;
        string? error = null;
        try
        {
            Activate();
            checks["nativeWindowCreated"] = TryGetPlatformHandle() is { Handle: not 0 };
            checks["nativeWindowActive"] = await WaitForAsync(() => IsActive);
            if (!checks["nativeWindowActive"]) throw new InvalidOperationException("Benchmark window did not activate; native editing focus is unverified.");
            checks["editorFocusEstablished"] = editor.Focus();
            if (editor.IsFocused) keyboard.Target = editor;
            checks["actualEditingFocus"] = await WaitForAsync(() => IsActive && editor.IsFocused && keyboard.IsInputAvailable);
            focusObservations.Add(new { Stage = "initial", WindowIsActive = IsActive, EditorIsFocused = editor.IsFocused, keyboard.IsInputAvailable });
            if (!checks["actualEditingFocus"]) throw new InvalidOperationException("Benchmark editor did not establish native editing focus.");
            nativeFocusValidated = true;
            keyboard.Layout = VirtualKeyboardLayout.English;
            checks["defaultTemplateLoaded"] = keyboard.TemplateDiagnostic is null;
            firstObservations["focusAvailableFromWindowConstructionMs"] = _smokePresentationClock.Elapsed.TotalMilliseconds;
            firstObservations["keyboardLayoutObservedFromWindowConstructionMs"] = _smokePresentationClock.Elapsed.TotalMilliseconds;
            var firstPrepared = await PrepareBenchmarkKeyAsync(keyboard, "a");
            visualFixture = new
            {
                ActualThemeVariant = ActualThemeVariant.ToString(),
                KeyFontFamily = firstPrepared.Button.FontFamily.ToString(),
                KeyFontSize = firstPrepared.Button.FontSize,
                KeyMinimumWidthDip = firstPrepared.Button.MinWidth,
                KeyMinimumHeightDip = firstPrepared.Button.MinHeight,
            };
            firstObservations["firstKeyCoordinateHitFromWindowConstructionMs"] = _smokePresentationClock.Elapsed.TotalMilliseconds;
            firstObservations["firstKeyPreparationMs"] = firstPrepared.PreparationTotalMs;
            firstObservations["firstNativeHitSceneWaitMs"] = firstPrepared.SceneWaitMs;
            checks["firstKeyCoordinateHit"] = true;

            foreach (var scenario in new[] { "character-insertion", "selection-replacement" })
            {
                for (var iteration = 0; iteration < BenchmarkWarmupCount; iteration++)
                    await MeasureBenchmarkSampleAsync(keyboard, editor, model, scenario, iteration);
                var samples = new List<BenchmarkSample>(BenchmarkSampleCount);
                for (var iteration = 0; iteration < BenchmarkSampleCount; iteration++)
                    samples.Add(await MeasureBenchmarkSampleAsync(keyboard, editor, model, scenario, iteration));
                groups.Add(new BenchmarkSampleGroup(scenario, BenchmarkWarmupCount, samples.Count,
                    Summarize(samples.Select(sample => sample.PreparationTotalMs)),
                    Summarize(samples.Select(sample => sample.PreparationBeforeSceneWaitMs)),
                    Summarize(samples.Select(sample => sample.SceneWaitMs)),
                    Summarize(samples.Select(sample => sample.PreparedPointerRouteToTextPropertyMs)),
                    Summarize(samples.Select(sample => sample.ReleaseRouteToTextPropertyMs)), samples));
                checks[$"{scenario}:100ValidSamples"] = samples.Count == BenchmarkSampleCount && samples.All(sample => sample.TextChanges == 1 && sample.ViewModelMatched);
            }
            foreach (var profile in new[] { (Name: "default-500-70", Delay: 500, Interval: 70), (Name: "short-100-40", Delay: 100, Interval: 40) })
            {
                var observation = await MeasureBenchmarkRepeatAsync(keyboard, editor, model, profile.Name, profile.Delay, profile.Interval);
                repeatProfiles.Add(observation);
                checks[$"{profile.Name}:initialAndRepeatedActionsObserved"] = observation.TextChangeTimesFromPreparedPressMs.Count >= 2;
                checks[$"{profile.Name}:cancelReleasesCapture"] = observation.CaptureReleasedByCancel;
                checks[$"{profile.Name}:noInputAfterCancelOrStaleRelease"] = observation.TextChangesAfterCancel == 0 && observation.TextAndViewModelStableAfterCancel;
            }
            keyboard.RepeatDelay = TimeSpan.FromMilliseconds(500);
            keyboard.RepeatInterval = TimeSpan.FromMilliseconds(70);
            focusObservations.Add(new { Stage = "completed", WindowIsActive = IsActive, EditorIsFocused = editor.IsFocused, keyboard.IsInputAvailable });
            checks["focusPreservedThroughBenchmark"] = IsActive && editor.IsFocused && keyboard.IsInputAvailable;
            editor.Text = "Benchmark completed";
            if (imagePath is not null) CaptureImage(imagePath);
        }
        catch (Exception exception) { error = exception.ToString(); }
        finally { keyboard.CancelInput(); }

        var passed = error is null && checks.Count > 0 && checks.Values.All(value => value);
        try
        {
            var fullOutput = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullOutput)!);
            File.WriteAllText(fullOutput, JsonSerializer.Serialize(new
            {
                Passed = passed, Mode = "keyboard-benchmark", TimestampUtc = DateTimeOffset.UtcNow,
                OS = RuntimeInformation.OSDescription, Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                Runtime = Environment.Version.ToString(), AvaloniaVersion = typeof(Application).Assembly.GetName().Version?.ToString(),
                BuildConfiguration = BenchmarkBuildConfiguration, WindowWidthDip = ClientSize.Width, WindowHeightDip = ClientSize.Height,
                RenderScaling, VisualFixture = visualFixture,
                StopwatchFrequencyHz = Stopwatch.Frequency, NativeHitScenePollingIntervalMs = BenchmarkHitPollMilliseconds,
                Checks = checks, FirstObservations = firstObservations, FocusObservations = focusObservations,
                SampleGroups = groups, RepeatProfiles = repeatProfiles, Error = error,
                NativeEditingFocusValidated = nativeFocusValidated, PhysicalPointerValidated = false, PhysicalKeyboardValidated = false,
                NativeTouchValidated = false, NativeImeValidated = false, RenderedFrameLatencyValidated = false,
                DpiMatrixValidated = false, KylinValidated = false, PerformanceReleaseThresholdsDefined = false,
                ImageOutput = imagePath is null ? null : Path.GetFullPath(imagePath),
                ImageCaptureKind = "Avalonia RenderTargetBitmap of the visible native Window; not a desktop screenshot.",
                MeasurementDefinitions = new
                {
                    Preparation = "Key lookup, BringIntoView and native InputHitTest readiness are timed separately from input routing. Scene wait includes layout, coordinate checks and 16 ms polling; it is an observation, not an actual presented-frame timestamp.",
                    PointerRoute = "After the key coordinate hits its native visual, time from raising PointerPressed to the first TextBox.TextProperty change. PointerReleased is raised synchronously for each ordinary key. Release-route timing starts immediately before PointerReleased. Neither includes key preparation or physical input delivery.",
                    Samples = "Each scenario has 20 discarded warmup iterations and 100 validated recorded iterations. Text/caret/selection setup occurs before preparation; one TextProperty change and a matching TwoWay ViewModel value are required. Percentiles use linear interpolation between sorted observations.",
                    Repeat = "TextProperty change timestamps begin at the prepared Backspace PointerPressed route. First repeat lateness is the gap from initial TextProperty change to the second change minus configured RepeatDelay. Subsequent intervals omit the initial-to-first-repeat gap. No scheduler catch-up or latency guarantee is inferred.",
                    Cancellation = "CancelInput is called while the pointer is held. Capture state, all later TextProperty changes, text and TwoWay model stability are observed through a wait of RepeatDelay + 3 * RepeatInterval, a stale pointer release, and a further wait of 3 * RepeatInterval.",
                    Thresholds = "This mode gathers a repeatable baseline and validates input/count/cancellation semantics. It sets no arbitrary performance release thresholds; compare repeated runs on the same platform, window size, font, theme and build configuration before selecting thresholds.",
                },
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.Shutdown(passed ? 0 : 1);
            else Close();
        }
    }

    private static string BenchmarkBuildConfiguration =>
#if DEBUG
        "Debug";
#else
        "Release";
#endif

    private sealed record BenchmarkPreparedKey(VirtualKeyboardKeyButton Button, Point Point,
        double PreparationTotalMs, double PreparationBeforeSceneWaitMs, double SceneWaitMs);
    private sealed record BenchmarkStatistics(int Count, double P50, double P95, double Max);
    private sealed record BenchmarkSample(int Iteration, double PreparationTotalMs, double PreparationBeforeSceneWaitMs,
        double SceneWaitMs, double PreparedPointerRouteToTextPropertyMs, double ReleaseRouteToTextPropertyMs,
        int TextChanges, bool ViewModelMatched);
    private sealed record BenchmarkSampleGroup(string Scenario, int DiscardedWarmupCount, int SampleCount,
        BenchmarkStatistics PreparationTotalMs, BenchmarkStatistics PreparationBeforeSceneWaitMs,
        BenchmarkStatistics SceneWaitMs, BenchmarkStatistics PreparedPointerRouteToTextPropertyMs,
        BenchmarkStatistics ReleaseRouteToTextPropertyMs, IReadOnlyList<BenchmarkSample> Samples);
    private sealed record BenchmarkRepeatProfile(string Name, int ConfiguredDelayMs, int ConfiguredIntervalMs,
        double PreparationTotalMs, double SceneWaitMs, double HoldObservationWindowMs,
        double? InitialActionFromPreparedPressMs, double? FirstRepeatFromPreparedPressMs, double? FirstRepeatLatenessMs,
        IReadOnlyList<double> TextChangeTimesFromPreparedPressMs, IReadOnlyList<double> SubsequentIntervalsMs,
        BenchmarkStatistics? SubsequentIntervalStatisticsMs, double CancelCallMs, bool CaptureReleasedByCancel,
        double PostCancelObservationWindowMs, int TextChangesAfterCancel, bool TextAndViewModelStableAfterCancel);

    private async Task<BenchmarkPreparedKey> PrepareBenchmarkKeyAsync(KeyboardControl keyboard, string id)
    {
        var begin = Stopwatch.GetTimestamp();
        var button = FindButton(keyboard, id);
        button.BringIntoView();
        var sceneWaitBegin = Stopwatch.GetTimestamp();
        Point point = default;
        var ready = false;
        for (var attempt = 0; attempt < 188; attempt++)
        {
            UpdateLayout();
            if (button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), this) is { } candidate)
            {
                var hit = this.InputHitTest(candidate);
                if (ReferenceEquals(hit, button) || hit is Visual visual && visual.GetVisualAncestors().Contains(button))
                { point = candidate; ready = true; break; }
            }
            await Task.Delay(BenchmarkHitPollMilliseconds);
        }
        var end = Stopwatch.GetTimestamp();
        if (!ready) throw new InvalidOperationException($"Benchmark key '{id}' did not become a native coordinate hit within the observation window.");
        if (!IsActive || !keyboard.IsInputAvailable) throw new InvalidOperationException("Native editing focus was lost while preparing a benchmark key.");
        return new BenchmarkPreparedKey(button, point, ElapsedMilliseconds(begin, end),
            ElapsedMilliseconds(begin, sceneWaitBegin), ElapsedMilliseconds(sceneWaitBegin, end));
    }

    private async Task<BenchmarkSample> MeasureBenchmarkSampleAsync(KeyboardControl keyboard, TextBox editor,
        InputViewModel model, string scenario, int iteration)
    {
        var selection = scenario == "selection-replacement";
        editor.Text = selection ? "ABC" : "AB";
        editor.CaretIndex = 1;
        editor.SelectionStart = 1;
        editor.SelectionEnd = selection ? 2 : 1;
        var prepared = await PrepareBenchmarkKeyAsync(keyboard, "a");
        long? changedAt = null;
        var changes = 0;
        void Observe(object? sender, AvaloniaPropertyChangedEventArgs change)
        {
            if (change.Property != TextBox.TextProperty) return;
            changedAt ??= Stopwatch.GetTimestamp();
            changes++;
        }
        editor.PropertyChanged += Observe;
        KeyPress? press = null;
        long routeStart;
        long releaseStart;
        try
        {
            routeStart = Stopwatch.GetTimestamp();
            press = RouteBenchmarkPress(prepared);
            if (changes != 0)
                throw new InvalidOperationException("An ordinary benchmark key committed text before pointer release.");
            releaseStart = Stopwatch.GetTimestamp();
            Release(press.Value);
            press = null;
        }
        finally
        {
            editor.PropertyChanged -= Observe;
            if (press is { } remaining) { keyboard.CancelInput(); remaining.Pointer.Dispose(); }
        }
        var expected = selection ? "AaC" : "AaB";
        var modelMatched = model.Text == expected;
        if (changedAt is not { } textChangedAt || textChangedAt < releaseStart || changes != 1 || editor.Text != expected || !modelMatched || !editor.IsFocused || !IsActive)
            throw new InvalidOperationException($"Benchmark {scenario} iteration {iteration} did not produce exactly one focused TextProperty change with a matching TwoWay value (observed changes: {changes}).");
        return new BenchmarkSample(iteration, prepared.PreparationTotalMs, prepared.PreparationBeforeSceneWaitMs,
            prepared.SceneWaitMs, ElapsedMilliseconds(routeStart, textChangedAt), ElapsedMilliseconds(releaseStart, textChangedAt), changes, modelMatched);
    }

    private async Task<BenchmarkRepeatProfile> MeasureBenchmarkRepeatAsync(KeyboardControl keyboard, TextBox editor,
        InputViewModel model, string name, int delayMs, int intervalMs)
    {
        keyboard.RepeatDelay = TimeSpan.FromMilliseconds(delayMs);
        keyboard.RepeatInterval = TimeSpan.FromMilliseconds(intervalMs);
        editor.Text = new string('x', 512);
        editor.CaretIndex = 512;
        editor.SelectionStart = 512;
        editor.SelectionEnd = 512;
        var prepared = await PrepareBenchmarkKeyAsync(keyboard, "Backspace");
        var eventTimes = new List<double>();
        var pressStart = 0L;
        void Observe(object? sender, AvaloniaPropertyChangedEventArgs change)
        {
            if (change.Property == TextBox.TextProperty) eventTimes.Add(ElapsedMilliseconds(pressStart, Stopwatch.GetTimestamp()));
        }
        editor.PropertyChanged += Observe;
        KeyPress? press = null;
        try
        {
            pressStart = Stopwatch.GetTimestamp();
            press = RouteBenchmarkPress(prepared);
            await Task.Delay(delayMs + 30 * intervalMs);
            var holdEnd = Stopwatch.GetTimestamp();
            var timesBeforeCancel = eventTimes.ToArray();
            var textBeforeCancel = editor.Text;
            var changesAtCancel = eventTimes.Count;
            var cancelBegin = Stopwatch.GetTimestamp();
            keyboard.CancelInput();
            var cancelEnd = Stopwatch.GetTimestamp();
            var captureReleased = press.Value.Pointer.Captured is null;
            await Task.Delay(delayMs + 3 * intervalMs);
            Release(press.Value);
            press = null;
            await Task.Delay(3 * intervalMs);
            var observationEnd = Stopwatch.GetTimestamp();
            var subsequentIntervals = timesBeforeCancel.Skip(2).Select((time, index) => time - timesBeforeCancel[index + 1]).ToArray();
            return new BenchmarkRepeatProfile(name, delayMs, intervalMs, prepared.PreparationTotalMs, prepared.SceneWaitMs,
                ElapsedMilliseconds(pressStart, holdEnd), timesBeforeCancel.Length > 0 ? timesBeforeCancel[0] : null,
                timesBeforeCancel.Length > 1 ? timesBeforeCancel[1] : null,
                timesBeforeCancel.Length > 1 ? timesBeforeCancel[1] - timesBeforeCancel[0] - delayMs : null,
                timesBeforeCancel, subsequentIntervals, subsequentIntervals.Length > 0 ? Summarize(subsequentIntervals) : null,
                ElapsedMilliseconds(cancelBegin, cancelEnd), captureReleased, ElapsedMilliseconds(cancelEnd, observationEnd),
                eventTimes.Count - changesAtCancel, editor.Text == textBeforeCancel && model.Text == textBeforeCancel);
        }
        finally
        {
            keyboard.CancelInput();
            editor.PropertyChanged -= Observe;
            if (press is { } remaining) remaining.Pointer.Dispose();
        }
    }

    private KeyPress RouteBenchmarkPress(BenchmarkPreparedKey prepared)
    {
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        try
        {
            prepared.Button.RaiseEvent(new PointerPressedEventArgs(prepared.Button, pointer, this, prepared.Point, 0,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None, 1)
                { RoutedEvent = InputElement.PointerPressedEvent });
            return new KeyPress(prepared.Button, pointer, prepared.Point);
        }
        catch { pointer.Capture(null); pointer.Dispose(); throw; }
    }
    private static double ElapsedMilliseconds(long start, long end) => (end - start) * 1000d / Stopwatch.Frequency;
    private static BenchmarkStatistics Summarize(IEnumerable<double> observations)
    {
        var sorted = observations.Order().ToArray();
        if (sorted.Length == 0) throw new InvalidOperationException("Cannot summarize an empty benchmark observation set.");
        double Percentile(double percentile)
        {
            var rank = (sorted.Length - 1) * percentile;
            var lower = (int)Math.Floor(rank);
            var upper = (int)Math.Ceiling(rank);
            return sorted[lower] + (sorted[upper] - sorted[lower]) * (rank - lower);
        }
        return new BenchmarkStatistics(sorted.Length, Percentile(.5), Percentile(.95), sorted[^1]);
    }
}
