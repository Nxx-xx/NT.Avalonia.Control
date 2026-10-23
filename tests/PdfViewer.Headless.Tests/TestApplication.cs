using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using NT.AvaloniaControl.PdfViewer.Themes;

[assembly: AvaloniaTestApplication(typeof(PdfViewer.Headless.Tests.TestApplication))]
// PdfViewerStrings is process-wide; serialize so string-override tests cannot race default checks.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace PdfViewer.Headless.Tests;

public sealed class TestApplication : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Styles.Add(new PdfViewerTheme());
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApplication>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
