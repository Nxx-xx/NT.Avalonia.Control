using Avalonia.Controls;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private object? _fontStartupEvidence;

    private Control CreateFontStartupExamples() => new Expander
    {
        Header = "平台字体与启动诊断", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
        Content = new FontStartupScene().View,
    };

    private async Task RunFontStartupSmokeAsync(Dictionary<string, bool> checks, string image)
    {
        _fontStartupEvidence = await FontStartupScene.RunSmokeAsync(_hostWindow, image, checks, Program.NativeTextBeforeUi);
        var output = SmokeArgument(Program.Arguments, "--smoke-output") ?? Path.ChangeExtension(image, ".json");
        var evidence = Path.ChangeExtension(Path.GetFullPath(output), ".font-startup.json");
        Directory.CreateDirectory(Path.GetDirectoryName(evidence)!);
        using var stream = File.Open(evidence, FileMode.CreateNew);
        System.Text.Json.JsonSerializer.Serialize(stream, _fontStartupEvidence, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }
}
