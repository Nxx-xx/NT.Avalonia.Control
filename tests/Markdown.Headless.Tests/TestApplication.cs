using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using NT.AvaloniaControl.Markdown.Themes;

[assembly: AvaloniaTestApplication(typeof(Markdown.Headless.Tests.TestApplication))]
[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerTest)]
[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace Markdown.Headless.Tests;
public sealed class TestApplication : Application
{
    public override void Initialize() { Styles.Add(new FluentTheme()); Styles.Add(new MarkdownTheme()); }
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApplication>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
