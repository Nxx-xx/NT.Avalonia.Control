using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using NT.AvaloniaControl.Demo;

[assembly: AvaloniaTestApplication(typeof(Demo.Headless.Tests.TestApplication))]
[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerTest)]
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Demo.Headless.Tests;

public static class TestApplication
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
