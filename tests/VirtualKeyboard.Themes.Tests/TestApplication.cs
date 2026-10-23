using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Themes.Fluent;
using NT.AvaloniaControl.VirtualKeyboard.Themes;

[assembly: AvaloniaTestApplication(typeof(VirtualKeyboard.Themes.Tests.TestApplication))]

namespace VirtualKeyboard.Themes.Tests;

public sealed class TestApplication : Application
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApplication>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions());

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Styles.Add(new VirtualKeyboardTheme());
    }
}
