using Avalonia.Controls;

namespace Charts.Usage;

public sealed partial class MainWindow
{
    internal object? FontStartupEvidence { get; private set; }

    private void ConfigureFontStartupScene()
    {
        var grid = (Grid)((ScrollViewer)Content!).Content!;
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        var diagnostic = new Expander { Name = "UsageFontStartupExamples", Header = "平台字体与启动诊断",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch, Content = new FontStartupScene().View };
        Grid.SetRow(diagnostic, grid.RowDefinitions.Count - 1);
        grid.Children.Add(diagnostic);
    }

    private async Task VerifyFontStartupAsync(Dictionary<string, bool> checks, string output)
    {
        var index = Array.IndexOf(Program.Arguments, "--smoke-image");
        var image = index >= 0 && index + 1 < Program.Arguments.Length ? Program.Arguments[index + 1] : Path.ChangeExtension(Path.GetFullPath(output), ".png");
        FontStartupEvidence = await FontStartupScene.RunSmokeAsync(this, image, checks, Program.NativeTextBeforeUi);
        // Keep detailed evidence separate from the legacy binding-check schema.
        var evidence = Path.ChangeExtension(Path.GetFullPath(output), ".font-startup.json");
        using var stream = File.Open(evidence, FileMode.CreateNew);
        System.Text.Json.JsonSerializer.Serialize(stream, FontStartupEvidence, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }
}
