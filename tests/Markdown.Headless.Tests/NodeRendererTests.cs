using System.Collections.Immutable;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using NT.AvaloniaControl.Markdown;
using Reader = NT.AvaloniaControl.Markdown.Markdown;
using Inline = Avalonia.Controls.Documents.Inline;

namespace Markdown.Headless.Tests;

public sealed class NodeRendererTests
{
    [Fact]
    public Task CustomBlockTextHelperPreservesPointerSelectionClassesAndRealizationLifetime() => HeadlessTestScope.Run(() => CustomBlockTextHelperPreservesPointerSelectionClassesAndRealizationLifetimeCore());

    private async Task CustomBlockTextHelperPreservesPointerSelectionClassesAndRealizationLifetimeCore()
    {
        var owned = new Resource(); MarkdownBlockRenderContext? context = null; var prepared = 0;
        var reader = CreateBlockReader();
        reader.ElementPrepared += (_, args) => { if (args.Element.Classes.Contains("custom-card")) prepared++; };
        reader.NodeRenderers = new([new("card", value =>
        {
            context = value; value.RegisterDisposable(owned);
            return new Border { Padding = new Thickness(10), Child = value.CreateText(), Background = Brushes.AliceBlue };
        })]);
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync(); window.UpdateLayout(); Assert.Equal(1, prepared); Assert.NotNull(context);
            var text = reader.GetVisualDescendants().OfType<MarkdownTextPresenter>().Single();
            Assert.Equal(context.Block.TextStart, text.TextStart); Assert.Equal(context.Block.TextLength, text.TextLength);
            var first = PointAt(text, window, 0); var last = PointAt(text, window, text.TextLength);
            window.MouseDown(first, MouseButton.Left); window.MouseMove(last, RawInputModifiers.LeftMouseButton); window.MouseUp(last, MouseButton.Left);
            Assert.Equal("custom 中😀e\u0301", reader.SelectedText);
            reader.Text = string.Empty; await reader.WhenUpdatedAsync(); Assert.Equal(1, owned.DisposeCount);
            Assert.Throws<ObjectDisposedException>(() => context.CreateText());
            var late = new Resource(); Assert.Throws<ObjectDisposedException>(() => context.RegisterDisposable(late)); Assert.Equal(1, late.DisposeCount);
        }
        finally { window.Close(); }
        Assert.Equal(1, owned.DisposeCount);
    }

    [Theory]
    [InlineData("throw")]
    [InlineData("missing")]
    [InlineData("unmapped")]
    [InlineData("partial")]
    [InlineData("wrong-text")]
    public Task FailedOrUnmappedBlockRendererKeepsNativeProjectionAndReportsDiagnostic(string failure) => HeadlessTestScope.Run(() => FailedOrUnmappedBlockRendererKeepsNativeProjectionAndReportsDiagnosticCore(failure));

    private async Task FailedOrUnmappedBlockRendererKeepsNativeProjectionAndReportsDiagnosticCore(string failure)
    {
        var resource = new Resource(); var reader = CreateBlockReader();
        if (failure != "missing") reader.NodeRenderers = new([new("card", context =>
        {
            context.RegisterDisposable(resource);
            return failure switch
            {
                "throw" => throw new InvalidOperationException("fixture renderer failure"),
                "unmapped" => new TextBlock { Text = context.Block.Text },
                "partial" => context.CreateText(context.Block.TextStart, 3),
                "wrong-text" => Corrupt(context.CreateText()),
                _ => throw new InvalidOperationException(),
            };
        })]);
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync(); window.UpdateLayout(); reader.SelectAll();
            Assert.Equal("custom 中😀e\u0301", reader.SelectedText);
            Assert.Single(reader.RenderingDiagnostics); Assert.Equal("MDRENDER", reader.RenderingDiagnostics[0].Code);
            var fallback = reader.GetVisualDescendants().OfType<MarkdownTextPresenter>().Single();
            Assert.Equal("custom 中😀e\u0301", fallback.Inlines is { Count: > 0 } inlines ? string.Concat(inlines.Select(InlineText)) : fallback.Text);
            Assert.Equal(failure == "missing" ? 0 : 1, resource.DisposeCount);
            reader.Text = "changed"; await reader.WhenUpdatedAsync(); Assert.Single(reader.RenderingDiagnostics);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task CustomInlineRendererKeepsExactTextAndGetsMappedSemanticClasses() => HeadlessTestScope.Run(() => CustomInlineRendererKeepsExactTextAndGetsMappedSemanticClassesCore());

    private async Task CustomInlineRendererKeepsExactTextAndGetsMappedSemanticClassesCore()
    {
        var reader = new Reader { Text = "before *中😀* after" };
        reader.ConfigureParser(new() { Converters = [new MarkdownNodeConverterRegistration<EmphasisInline>(node => new("emphasis") { StyleClasses = ["host-emphasis"] })] });
        reader.NodeRenderers = new(inlines: [new("emphasis", context => new Span { FontWeight = FontWeight.Bold, Inlines = { context.CreateText() } })]);
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync(); window.UpdateLayout(); reader.SelectAll(); Assert.Equal("before 中😀 after", reader.SelectedText);
            var text = reader.GetVisualDescendants().OfType<MarkdownTextPresenter>().Single();
            var custom = text.Inlines!.OfType<MarkdownSpan>().Single(span => span.Classes.Contains("host-emphasis"));
            Assert.Equal("before ".Length, custom.TextStart); Assert.Equal("中😀".Length, custom.TextLength);
            Assert.Empty(reader.RenderingDiagnostics);
        }
        finally { window.Close(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task InlineRendererRejectsChangedTextAndUiContainersWithoutBreakingGlobalOffsets(bool container) => HeadlessTestScope.Run(() => InlineRendererRejectsChangedTextAndUiContainersWithoutBreakingGlobalOffsetsCore(container));

    private async Task InlineRendererRejectsChangedTextAndUiContainersWithoutBreakingGlobalOffsetsCore(bool container)
    {
        var reader = new Reader { Text = "first middle last" };
        reader.ConfigureParser(new() { Converters = [new MarkdownNodeConverterRegistration<LiteralInline>(_ => new("literal"))] });
        reader.NodeRenderers = new(inlines: [new("literal", context => container ? new InlineUIContainer(new TextBlock { Text = context.Inline.Text }) : new Run("wrong"))]);
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync(); reader.SelectAll(); Assert.Equal("first middle last", reader.SelectedText);
            Assert.Single(reader.RenderingDiagnostics); Assert.Equal("MDRENDER", reader.RenderingDiagnostics[0].Code);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task ChangingRendererRegistryReparsesAcceptedTailAndKeepsSelection() => HeadlessTestScope.Run(() => ChangingRendererRegistryReparsesAcceptedTailAndKeepsSelectionCore());

    private async Task ChangingRendererRegistryReparsesAcceptedTailAndKeepsSelectionCore()
    {
        var reader = CreateBlockReader(); var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync(); reader.SetSelection(0, 6);
            var session = reader.BeginStreaming(); session.Append(" accepted tail");
            reader.NodeRenderers = new([new("card", context => context.CreateText())]);
            await reader.WhenUpdatedAsync();
            Assert.EndsWith(" accepted tail", reader.Text); Assert.Equal("custom", reader.SelectedText);
            Assert.Equal(MarkdownUpdateStatus.Cancelled, (await session.CompleteAsync()).Status);
            Assert.Empty(reader.RenderingDiagnostics);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task RendererRegistryCapturesCallbacksAndRejectsMalformedRegistrations() => HeadlessTestScope.Run(() => RendererRegistryCapturesCallbacksAndRejectsMalformedRegistrationsCore());

    private void RendererRegistryCapturesCallbacksAndRejectsMalformedRegistrationsCore()
    {
        var registrations = new List<MarkdownBlockRendererRegistration> { new("card", context => context.CreateText()) };
        var registry = new MarkdownRendererRegistry(registrations); registrations.Clear(); Assert.Single(registry.Blocks);
        Assert.Throws<ArgumentException>(() => new MarkdownRendererRegistry([new(" card ", context => context.CreateText())]));
        Assert.Throws<ArgumentException>(() => new MarkdownRendererRegistry([new("card", context => context.CreateText()), new("card", context => context.CreateText())]));
        Assert.Throws<ArgumentNullException>(() => new MarkdownRendererRegistry([new("card", null!)]));
        Assert.Throws<ArgumentNullException>(() => new MarkdownRendererRegistry(inlines: [new("card", null!)]));
        Assert.Throws<ArgumentNullException>(() => new MarkdownRendererRegistry([null!]));
    }

    private static Reader CreateBlockReader()
    {
        var reader = new Reader { Text = "custom 中😀e\u0301" };
        reader.ConfigureParser(new() { Converters = [new MarkdownNodeConverterRegistration<ParagraphBlock>(_ => new("card") { StyleClasses = ["custom-card"] })] });
        return reader;
    }
    private static MarkdownTextPresenter Corrupt(MarkdownTextPresenter text) { text.Text = "wrong"; return text; }
    private static string InlineText(Inline inline) => inline switch { Run run => run.Text ?? string.Empty, Span span => string.Concat(span.Inlines.Select(InlineText)), LineBreak => "\n", _ => string.Empty };
    private static Window Show(Control reader) { var window = new Window { Width = 700, Height = 360, Content = reader }; window.Show(); return window; }
    private static Point PointAt(MarkdownTextPresenter text, Window window, int position)
    {
        var rectangle = text.TextLayout.HitTestTextPosition(position);
        return text.TranslatePoint(new Point(rectangle.X + text.Padding.Left + .1, rectangle.Y + rectangle.Height / 2 + text.Padding.Top), window)!.Value;
    }
    private sealed class Resource : IDisposable { public int DisposeCount { get; private set; } public void Dispose() => DisposeCount++; }
}

