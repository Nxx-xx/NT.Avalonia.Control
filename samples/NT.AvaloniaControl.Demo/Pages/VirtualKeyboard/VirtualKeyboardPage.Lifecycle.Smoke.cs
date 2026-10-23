using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NT.AvaloniaControl.VirtualKeyboard;
using KeyboardControl = NT.AvaloniaControl.VirtualKeyboard.VirtualKeyboard;

namespace NT.AvaloniaControl.Demo.Pages.VirtualKeyboard;

internal sealed partial class VirtualKeyboardPage
{
    private async Task RunLifecycleSmokeAsync(Dictionary<string, bool> checks, Dictionary<string, double> measurements)
    {
        _keyboard.RepeatDelay = TimeSpan.FromMilliseconds(100);
        _keyboard.RepeatInterval = TimeSpan.FromMilliseconds(40);
        var previousWidth = _hostWindow.Width;
        var previousHeight = _hostWindow.Height;
        var previousVariant = _hostWindow.RequestedThemeVariant;
        var previousTemplate = _keyboard.Template;
        var previousTheme = _keyboard.Theme;
        try
        {
            SelectTarget(_first);
            if (!await WaitForAsync(() => _hostWindow.IsActive && _first.IsFocused && _keyboard.IsInputAvailable))
                throw new InvalidOperationException("Lifecycle probe requires an active native editing session.");

            // This is a second native window, rather than a raised Deactivated event.
            _first.Text = new string('x', 60);
            _first.CaretIndex = 60;
            var inactivePress = await Press("Backspace");
            var foreign = new Window { Width = 240, Height = 120, Title = "Keyboard lifecycle activation probe" };
            try
            {
                foreign.Show();
                foreign.Activate();
                checks["secondNativeWindowDeactivatesHost"] = await WaitForAsync(() => foreign.IsActive && !_hostWindow.IsActive);
                var stoppedLength = _first.Text!.Length;
                checks["windowDeactivationReleasesCapture"] = inactivePress.Pointer.Captured is null;
                await Task.Delay(230);
                checks["inactiveNativeWindowStopsRepeat"] = _first.Text.Length == stoppedLength && !_keyboard.IsInputAvailable;
                foreign.Close();
                _hostWindow.Activate();
                checks["hostWindowReactivates"] = await WaitForAsync(() => _hostWindow.IsActive);
                SelectTarget(_first);
                checks["editingSessionRestoredAfterActivation"] = await WaitForAsync(() => _first.IsFocused && _keyboard.IsInputAvailable);
                await Task.Delay(230);
                Release(inactivePress);
                checks["reactivationDoesNotResumeOldPress"] = _first.Text.Length == stoppedLength;
            }
            finally { foreign.Close(); inactivePress.Pointer.Dispose(); }

            var editorAncestor = _first.GetVisualParent() as Control
                ?? throw new InvalidOperationException("The native editor has no visible form ancestor.");
            foreach (var (name, control) in new[]
            {
                ("keyboard", (Control)_keyboard),
                ("keyboardAncestor", (Control)_keyboardHost),
                ("targetAncestor", editorAncestor),
            })
            {
                await CheckStateCancellationAsync(name + "Hidden", () => control.IsVisible = false,
                    () => control.IsVisible = true, checks);
                await CheckStateCancellationAsync(name + "Disabled", () => control.IsEnabled = false,
                    () => control.IsEnabled = true, checks);
            }

            await CheckStateCancellationAsync("layoutChanged", () => _keyboard.Layout = VirtualKeyboardLayout.Numeric,
                () => _keyboard.Layout = VirtualKeyboardLayout.English, checks);

            await Tap("CapsLock");
            await Tap("Shift");
            _hostWindow.RequestedThemeVariant = ThemeVariant.Light;
            await CheckStateCancellationAsync("lightDarkChanged", () => _hostWindow.RequestedThemeVariant = ThemeVariant.Dark,
                () => _hostWindow.RequestedThemeVariant = ThemeVariant.Light, checks);
            checks["nativeVariantChangePreservesModifiers"] = _keyboard.IsCapsLockActive && _keyboard.IsShiftActive
                && ReferenceEquals(_keyboard.Target, _first) && _keyboard.Layout == VirtualKeyboardLayout.English;

            await CheckStateCancellationAsync("templateReplaced", () => _keyboard.Template = PlaceholderTemplate(),
                () => _keyboard.Template = previousTemplate, checks);
            checks["nativeTemplateChangePreservesModifiers"] = _keyboard.IsCapsLockActive && _keyboard.IsShiftActive;

            // Both ControlThemes share the same template object. Avalonia can briefly
            // clear Template while switching setters without reapplying that object.
            var themeA = new ControlTheme(typeof(KeyboardControl));
            var themeB = new ControlTheme(typeof(KeyboardControl));
            themeA.Setters.Add(new Setter(TemplatedControl.TemplateProperty, previousTemplate));
            themeB.Setters.Add(new Setter(TemplatedControl.TemplateProperty, previousTemplate));
            _keyboard.Theme = themeA;
            _hostWindow.UpdateLayout();
            await CheckStateCancellationAsync("sharedTemplateThemeChanged", () => _keyboard.Theme = themeB,
                () => _keyboard.Theme = themeA, checks);
            checks["nativeSharedTemplateThemeKeepsInputAvailable"] = _keyboard.IsInputAvailable
                && _keyboard.IsCapsLockActive && _keyboard.IsShiftActive;
            _keyboard.Theme = previousTheme;
            _keyboard.Template = previousTemplate;

            var requestsBefore = _returnRequests;
            var navigationClock = Stopwatch.StartNew();
            for (var round = 0; round < 5; round++)
            {
                ((MainWindow)_hostWindow).SelectPage("overview");
                ((MainWindow)_hostWindow).SelectPage("virtualkeyboard");
                if (!await WaitForAsync(() => _first.IsFocused && ReferenceEquals(_keyboard.Target, _first) && _keyboard.IsInputAvailable))
                    throw new InvalidOperationException("Repeated navigation did not restore the native editing target.");
                await Tap("Return");
            }
            measurements["fiveNavigationRoundTripsMs"] = navigationClock.Elapsed.TotalMilliseconds;
            checks["repeatedNativeNavigationHasOneHostRequestEach"] = _returnRequests == requestsBefore + 5;

            _hostWindow.Width = _hostWindow.MinWidth;
            _hostWindow.Height = _hostWindow.MinHeight;
            await Task.Delay(120);
            _hostWindow.UpdateLayout();
            checks["minimumDemoSizeKeepsKeysSeparated"] = KeysDoNotOverlap(measurements);
            _first.Text = "AB";
            _first.CaretIndex = 2;
            await Tap("Backspace");
            checks["minimumDemoSizeScrollsToClickableKey"] = _first.Text == "A" && _first.IsFocused;
        }
        finally
        {
            _keyboard.CancelInput();
            _keyboard.IsVisible = true;
            _keyboard.IsEnabled = true;
            _keyboardHost.IsVisible = true;
            _keyboardHost.IsEnabled = true;
            _keyboard.Theme = previousTheme;
            _keyboard.Template = previousTemplate;
            _keyboard.Layout = VirtualKeyboardLayout.English;
            _keyboard.RepeatDelay = TimeSpan.FromMilliseconds(500);
            _keyboard.RepeatInterval = TimeSpan.FromMilliseconds(70);
            _hostWindow.Width = previousWidth;
            _hostWindow.Height = previousHeight;
            _hostWindow.RequestedThemeVariant = previousVariant;
            _hostWindow.UpdateLayout();
        }
    }

    private async Task CheckStateCancellationAsync(string name, Action invalidate, Action restore,
        Dictionary<string, bool> checks)
    {
        SelectTarget(_first);
        _first.Text = new string('x', 60);
        _first.CaretIndex = 60;
        var press = await Press("Backspace");
        var stoppedLength = _first.Text!.Length;
        try
        {
            invalidate();
            checks[name + "ReleasesCapture"] = press.Pointer.Captured is null;
            await Task.Delay(180);
            checks[name + "StopsRepeat"] = _first.Text.Length == stoppedLength;
        }
        finally { restore(); }
        _hostWindow.UpdateLayout();
        // A hidden or disabled native editor may complete focus loss on a later
        // dispatcher turn. The host establishes a new focus session after restore.
        var recovered = await WaitForAsync(() =>
        {
            if (!_first.IsFocused || !ReferenceEquals(_keyboard.Target, _first)) SelectTarget(_first);
            return _hostWindow.IsActive && _first.IsFocused && _keyboard.IsInputAvailable;
        });
        checks[name + "RestoresNewFocusedSession"] = recovered;
        if (!recovered)
            throw new InvalidOperationException($"Native input did not recover after {name}: active={_hostWindow.IsActive}, focused={_first.IsFocused}, enabled={_first.IsEffectivelyEnabled}, visible={_first.IsEffectivelyVisible}, target={ReferenceEquals(_keyboard.Target, _first)}, template={_keyboard.TemplateDiagnostic}.");
        await Task.Delay(180);
        Release(press);
        checks[name + "DoesNotRestoreOldPress"] = _first.Text!.Length == stoppedLength;
    }

    private bool KeysDoNotOverlap(Dictionary<string, double> measurements)
    {
        var minimumWidth = double.PositiveInfinity;
        var minimumHeight = double.PositiveInfinity;
        var minimumGap = double.PositiveInfinity;
        var keyCount = 0;
        var valid = true;
        // Adjacent independently rounded native child rectangles can lose one
        // device pixel of the requested logical gap, without overlapping.
        var roundingAllowance = _keyboard.UseLayoutRounding ? 1 / _hostWindow.RenderScaling : 0;
        foreach (var row in _keyboard.GetVisualDescendants().OfType<VirtualKeyboardRowPanel>())
        {
            var keys = row.GetVisualDescendants().OfType<VirtualKeyboardKeyButton>().ToArray();
            for (var index = 0; index < keys.Length; index++)
            {
                keyCount++;
                minimumWidth = Math.Min(minimumWidth, keys[index].Bounds.Width);
                minimumHeight = Math.Min(minimumHeight, keys[index].Bounds.Height);
                if (keys[index].Bounds.Width < 32 || keys[index].Bounds.Height < 44) valid = false;
                if (index == 0) continue;
                var previousRight = keys[index - 1].TranslatePoint(default, row)!.Value.X + keys[index - 1].Bounds.Width;
                var nextLeft = keys[index].TranslatePoint(default, row)!.Value.X;
                minimumGap = Math.Min(minimumGap, nextLeft - previousRight);
                if (nextLeft < previousRight - 1e-6 || nextLeft - previousRight + roundingAllowance + 1e-6 < row.Spacing)
                    valid = false;
            }
        }
        measurements["minimumWindowKeyWidthDip"] = minimumWidth;
        measurements["minimumWindowKeyHeightDip"] = minimumHeight;
        measurements["minimumWindowKeyGapDip"] = minimumGap;
        measurements["minimumWindowObservedKeyCount"] = keyCount;
        measurements["nativeLayoutRoundingAllowanceDip"] = roundingAllowance;
        return valid && keyCount > 0;
    }

    private static FuncControlTemplate<KeyboardControl> PlaceholderTemplate() => new((_, scope) =>
    {
        var rows = new ItemsControl();
        scope.Register("PART_RowsPresenter", rows);
        return rows;
    });
}
