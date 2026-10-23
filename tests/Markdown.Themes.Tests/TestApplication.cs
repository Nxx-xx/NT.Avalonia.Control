using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Themes.Fluent;
using NT.AvaloniaControl.Markdown.Themes;

[assembly: AvaloniaTestApplication(typeof(Markdown.Themes.Tests.TestApplication))]

namespace Markdown.Themes.Tests;

public sealed class TestApplication : Application
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApplication>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
    public override void Initialize() { Styles.Add(new FluentTheme()); Styles.Add(new MarkdownTheme()); }
}
