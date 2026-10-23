using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using Markdig.Syntax;
using NT.AvaloniaControl.Markdown;
using Reader = NT.AvaloniaControl.Markdown.Markdown;

namespace Markdown.Headless.Tests;

public sealed class CustomWrappedPlainTextTests
{
    [Fact]
    public Task PublicCodeTextHelperRetainsWrapAndCompletesLeadingBlankLf() => HeadlessTestScope.Run(async () =>
    {
        var reader = new Reader { Text = "```text\n\n\nfirst 中文😀e\u0301\n\nlast אבג\n\n\n```" };
        reader.ConfigureParser(new()
        {
            Converters = [new MarkdownNodeConverterRegistration<FencedCodeBlock>(_ => new("blank-code"))]
        });
        reader.NodeRenderers = new(blocks: [new("blank-code", context =>
        {
            var text = context.CreateText();
            Trace(new { Stage = "helper-created", Wrap = text.TextWrapping.ToString(), TextStart = text.TextStart,
                TextLength = text.TextLength, PlainTextLength = text.Text?.Length,
                NativeInlineCount = text.Inlines?.Count ?? 0 });
            Assert.Equal(TextWrapping.Wrap, text.TextWrapping);
            Assert.NotNull(text.Text);
            Assert.True(text.TextLength > 0);
            return new ConstraintWitness { Child = text };
        })]);
        var window = new Window { Width = 520, Height = 320, Content = reader };
        try
        {
            Trace(new { Stage = "before-show" });
            window.Show();
            Trace(new { Stage = "after-show" });
            await reader.WhenUpdatedAsync();
            window.UpdateLayout();
            Trace(new { Stage = "updated" });
            var text = Assert.Single(reader.GetVisualDescendants().OfType<MarkdownTextPresenter>());
            Assert.Equal(TextWrapping.Wrap, text.TextWrapping);
            Assert.Equal(0, text.Inlines?.Count ?? 0);
            Assert.Equal(reader.Document.Text, text.Text);
            Assert.StartsWith("\n\nfirst", text.Text);
            Assert.True(text.TextLayout.TextLines.Count >= 7);
            Assert.True(double.IsFinite(text.TextLayout.Height));
            reader.SelectAll();
            Assert.Equal(reader.Document.Text, reader.SelectedText);
            var drawings = new DrawingGroup();
            using (var drawing = drawings.Open()) text.Render(drawing);
            Assert.Empty(reader.RenderingDiagnostics);
            Trace(new { Stage = "completed", Lines = text.TextLayout.TextLines.Count,
                LayoutWidth = text.TextLayout.Width, LayoutHeight = text.TextLayout.Height });
        }
        finally { window.Close(); }
    });

    private sealed class ConstraintWitness : Decorator
    {
        private int _measureCount;
        protected override Size MeasureOverride(Size availableSize)
        {
            if (++_measureCount <= 8)
                Trace(new { Stage = "measure-child", Sequence = _measureCount,
                    Width = availableSize.Width.ToString("R", CultureInfo.InvariantCulture),
                    Height = availableSize.Height.ToString("R", CultureInfo.InvariantCulture),
                    WidthFinite = double.IsFinite(availableSize.Width),
                    HeightPositiveInfinity = double.IsPositiveInfinity(availableSize.Height) });
            var desired = base.MeasureOverride(availableSize);
            Trace(new { Stage = "measure-returned", Width = desired.Width, Height = desired.Height });
            return desired;
        }
    }

    private static void Trace<T>(T value)
    {
        var line = JsonSerializer.Serialize(value);
        Console.WriteLine(line);
        if (Environment.GetEnvironmentVariable("MARKDOWN_WRAPPED_LF_TRACE") is { Length: > 0 } path)
            File.AppendAllText(path, line + Environment.NewLine);
    }
}
