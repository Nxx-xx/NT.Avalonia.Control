using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Themes.Fluent;
using NT.AvaloniaControl.Charts.Themes;

[assembly: AvaloniaTestApplication(typeof(Charts.Headless.Tests.TestApplication))]
[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerTest)]
// The headless session owns one UI dispatcher. Concurrent xUnit collections can
// block each other's dispatcher work; test ordering is not a chart concurrency test.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Charts.Headless.Tests;

public sealed class TestApplication : Application
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApplication>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions());

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Styles.Add(new ChartsTheme());
    }
}
