using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;

namespace Markdown.Headless.Tests;

// Avalonia 12.0.3 DispatcherOperation restores a queued ExecutionContext without
// restoring the previous one afterward. Headless input drains that queue inline.
// Keep its actual input/layout behavior inside the current caller's balanced scope.
internal static class ScopedHeadlessInput
{
    internal static void RunJobs() => Run(() => Dispatcher.UIThread.RunJobs());
    public static void KeyPress(this Window window, Key key, RawInputModifiers modifiers, PhysicalKey physicalKey, string? symbol) =>
        Run(() => HeadlessWindowExtensions.KeyPress(window, key, modifiers, physicalKey, symbol));
    public static void KeyRelease(this Window window, Key key, RawInputModifiers modifiers, PhysicalKey physicalKey, string? symbol) =>
        Run(() => HeadlessWindowExtensions.KeyRelease(window, key, modifiers, physicalKey, symbol));
    public static void MouseDown(this Window window, Point point, MouseButton button, RawInputModifiers modifiers = RawInputModifiers.None) =>
        Run(() => HeadlessWindowExtensions.MouseDown(window, point, button, modifiers));
    public static void MouseMove(this Window window, Point point, RawInputModifiers modifiers = RawInputModifiers.None) =>
        Run(() => HeadlessWindowExtensions.MouseMove(window, point, modifiers));
    public static void MouseUp(this Window window, Point point, MouseButton button, RawInputModifiers modifiers = RawInputModifiers.None) =>
        Run(() => HeadlessWindowExtensions.MouseUp(window, point, button, modifiers));
    public static void MouseWheel(this Window window, Point point, Vector delta, RawInputModifiers modifiers = RawInputModifiers.None) =>
        Run(() => HeadlessWindowExtensions.MouseWheel(window, point, delta, modifiers));

    private static void Run(Action action)
    {
        if (ExecutionContext.Capture() is { } caller)
            ExecutionContext.Run(caller, static callback => ((Action)callback!).Invoke(), action);
        else action();
    }
}

