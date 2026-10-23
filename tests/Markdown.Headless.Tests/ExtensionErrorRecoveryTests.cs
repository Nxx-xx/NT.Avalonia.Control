using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Markdig.Syntax;
using NT.AvaloniaControl.Markdown;
using Reader = NT.AvaloniaControl.Markdown.Markdown;
using Inline = Avalonia.Controls.Documents.Inline;

namespace Markdown.Headless.Tests;

public sealed class ExtensionErrorRecoveryTests
{
    [Theory]
    [InlineData("null")]
    [InlineData("enumeration")]
    public Task MalformedHighlightResultsKeepPlainCodeAndReportTheServiceError(string failure) => HeadlessTestScope.Run(() => MalformedHighlightResultsKeepPlainCodeAndReportTheServiceErrorCore(failure));

    private async Task MalformedHighlightResultsKeepPlainCodeAndReportTheServiceErrorCore(string failure)
    {
        var errors = new List<MarkdownErrorEventArgs>();
        var reader = new Reader { Text = "```text\noriginal 中文😀\n```", CodeHighlighter = new ImmediateHighlighter(failure) };
        reader.Error += (_, error) => errors.Add(error);
        var window = Show(reader);
        try
        {
            Assert.Equal(MarkdownUpdateStatus.Completed, (await reader.WhenUpdatedAsync()).Status);
            await Wait(window, () => errors.Any(error => error.Category == "Highlight"));
            var code = reader.GetVisualDescendants().OfType<MarkdownCodeBlock>().Single();
            var text = code.GetVisualDescendants().OfType<MarkdownTextPresenter>().Single();
            Assert.False(code.IsHighlighting);
            Assert.Equal("original 中文😀\n", code.Code);
            Assert.Equal(code.Code, text.Text);
            Assert.Empty(text.Inlines!);
            reader.SelectAll(); Assert.Equal(code.Code, reader.SelectedText);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task LateMalformedResultsCannotEraseTheCurrentServicesFormatting() => HeadlessTestScope.Run(() => LateMalformedResultsCannotEraseTheCurrentServicesFormattingCore());

    private async Task LateMalformedResultsCannotEraseTheCurrentServicesFormattingCore()
    {
        var delayed = new DeferredHighlighter(); var errors = new List<string>();
        var reader = new Reader { Text = "```text\ncurrent code\n```", CodeHighlighter = delayed };
        reader.Error += (_, error) => errors.Add(error.Category);
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync(); await delayed.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            reader.CodeHighlighter = new ImmediateHighlighter("valid");
            await Wait(window, () => reader.GetVisualDescendants().OfType<MarkdownTextPresenter>().Any(text =>
                text.Inlines is { Count: > 0 } inlines && inlines[0].Foreground is ISolidColorBrush { Color: var color } && color == Colors.Blue));
            delayed.Result.TrySetResult(new ThrowingRanges());
            await Wait(window, () => reader.ActiveResourceRequestCount == 0);
            var text = reader.GetVisualDescendants().OfType<MarkdownTextPresenter>().Single();
            Assert.Equal(Colors.Blue, Assert.IsAssignableFrom<ISolidColorBrush>(text.Inlines![0].Foreground).Color);
            Assert.Equal("current code\n", string.Concat(text.Inlines.Select(InlineText)));
            Assert.Empty(errors);
        }
        finally { delayed.Result.TrySetResult([]); window.Close(); }
    }

    [Theory]
    [InlineData("throw")]
    [InlineData("wrong-text")]
    public Task FailedRendererDisposesEveryResourceInvalidatesItsContextAndStillFallsBack(string failure) => HeadlessTestScope.Run(() => FailedRendererDisposesEveryResourceInvalidatesItsContextAndStillFallsBackCore(failure));

    private async Task FailedRendererDisposesEveryResourceInvalidatesItsContextAndStillFallsBackCore(string failure)
    {
        var events = new List<string>(); var errors = new List<MarkdownErrorEventArgs>();
        var first = new Resource("first", events, throws: true); var second = new Resource("second", events);
        var late = new Resource("late", events); MarkdownBlockRenderContext? failedContext = null; var lateRejected = false;
        var reader = CustomReader(); reader.Error += (_, error) => errors.Add(error);
        reader.NodeRenderers = new([new("card", context =>
        {
            failedContext = context;
            first.OnDispose = () =>
            {
                try { context.RegisterDisposable(late); }
                catch (ObjectDisposedException) { lateRejected = true; }
            };
            context.RegisterDisposable(first); context.RegisterDisposable(second);
            if (failure == "throw") throw new InvalidOperationException("fixture renderer failure");
            var text = context.CreateText(); text.Text = "wrong"; return text;
        })]);
        var window = Show(reader);
        try
        {
            Assert.Equal(MarkdownUpdateStatus.Completed, (await reader.WhenUpdatedAsync()).Status);
            window.UpdateLayout();
            Assert.Equal(1, first.DisposeCount); Assert.Equal(1, second.DisposeCount); Assert.Equal(1, late.DisposeCount);
            Assert.True(lateRejected);
            Assert.Equal(new[] { "first", "late", "second" }, events);
            Assert.Contains(errors, error => error.Category == "Renderer");
            Assert.Contains(errors, error => error.Category == "RendererResource" && error.Error.Message == "fixture dispose failure");
            Assert.Equal("MDRENDER", Assert.Single(reader.RenderingDiagnostics).Code);
            var fallback = reader.GetVisualDescendants().OfType<MarkdownTextPresenter>().Single();
            Assert.Equal("custom original 中😀", Text(fallback));
            Assert.NotNull(failedContext);
            Assert.Throws<ObjectDisposedException>(() => failedContext.CreateText());
            reader.SelectAll(); Assert.Equal("custom original 中😀", reader.SelectedText);
        }
        finally { window.Close(); }
        Assert.Equal(1, first.DisposeCount); Assert.Equal(1, second.DisposeCount);
    }

    [Fact]
    public Task UnloadingDisposesEveryResourceOnceDespiteErrorsAndReentrantConfiguration() => HeadlessTestScope.Run(() => UnloadingDisposesEveryResourceOnceDespiteErrorsAndReentrantConfigurationCore());

    private async Task UnloadingDisposesEveryResourceOnceDespiteErrorsAndReentrantConfigurationCore()
    {
        var events = new List<string>(); var errors = new List<MarkdownErrorEventArgs>();
        var first = new Resource("first", events, throws: true); var second = new Resource("second", events); var late = new Resource("late", events);
        var reader = CustomReader(); var lateRejected = false; MarkdownBlockRenderContext? context = null;
        reader.Error += (_, error) => errors.Add(error);
        reader.NodeRenderers = new([new("card", value =>
        {
            context = value;
            first.OnDispose = () =>
            {
                reader.NodeRenderers = MarkdownRendererRegistry.Empty;
                try { value.RegisterDisposable(late); }
                catch (ObjectDisposedException) { lateRejected = true; }
            };
            value.RegisterDisposable(first); value.RegisterDisposable(second);
            return value.CreateText();
        })]);
        var window = Show(reader);
        try
        {
            Assert.Equal(MarkdownUpdateStatus.Completed, (await reader.WhenUpdatedAsync()).Status);
            window.UpdateLayout();
            window.Content = null;
            Assert.Equal(1, first.DisposeCount); Assert.Equal(1, second.DisposeCount); Assert.Equal(1, late.DisposeCount);
            Assert.True(lateRejected);
            Assert.Equal(new[] { "first", "late", "second" }, events);
            Assert.Contains(errors, error => error.Category == "RendererResource" && error.Error.Message == "fixture dispose failure");
            Assert.NotNull(context); Assert.Throws<ObjectDisposedException>(() => context.CreateDefault());
        }
        finally { window.Close(); }
        Assert.Equal(1, first.DisposeCount); Assert.Equal(1, second.DisposeCount);
    }

    private static Reader CustomReader()
    {
        var reader = new Reader { Text = "custom original 中😀" };
        reader.ConfigureParser(new() { Converters = [new MarkdownNodeConverterRegistration<ParagraphBlock>(_ => new("card"))] });
        return reader;
    }
    private static Window Show(Control content) { var window = new Window { Width = 650, Height = 340, Content = content }; window.Show(); return window; }
    private static async Task Wait(Window window, Func<bool> condition)
    {
        for (var index = 0; index < 100; index++)
        {
            window.UpdateLayout(); if (condition()) return;
            await Task.Delay(10, TestContext.Current.CancellationToken);
            await Dispatcher.UIThread.InvokeAsync(window.UpdateLayout);
        }
        Assert.Fail("The error recovery fixture did not settle.");
    }
    private static string Text(MarkdownTextPresenter text) => text.Inlines is { Count: > 0 } inlines ? string.Concat(inlines.Select(InlineText)) : text.Text ?? string.Empty;
    private static string InlineText(Inline inline) => inline switch { Run run => run.Text ?? string.Empty, Span span => string.Concat(span.Inlines.Select(InlineText)), LineBreak => "\n", _ => throw new InvalidOperationException("Unexpected fixture inline.") };
    private sealed class Resource(string name, List<string> events, bool throws = false) : IDisposable
    {
        public int DisposeCount { get; private set; }
        public Action? OnDispose { get; set; }
        public void Dispose()
        {
            DisposeCount++; events.Add(name); OnDispose?.Invoke();
            if (throws) throw new IOException("fixture dispose failure");
        }
    }
    private sealed class ThrowingRanges : IReadOnlyList<MarkdownHighlightSpan>
    {
        public int Count => 1;
        public MarkdownHighlightSpan this[int index] => new(0, 1);
        public IEnumerator<MarkdownHighlightSpan> GetEnumerator() => throw new IOException("fixture range enumeration failure");
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    private sealed class ImmediateHighlighter(string result) : IMarkdownCodeHighlighter
    {
        public event EventHandler? Invalidated { add { } remove { } }
        public ValueTask<IReadOnlyList<MarkdownHighlightSpan>> HighlightAsync(string code, string? language, CancellationToken token) =>
            new(result switch { "null" => null!, "enumeration" => new ThrowingRanges(), _ => new[] { new MarkdownHighlightSpan(0, code.Length, "#0000ff") } });
    }
    private sealed class DeferredHighlighter : IMarkdownCodeHighlighter
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<IReadOnlyList<MarkdownHighlightSpan>> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public event EventHandler? Invalidated { add { } remove { } }
        public ValueTask<IReadOnlyList<MarkdownHighlightSpan>> HighlightAsync(string code, string? language, CancellationToken token) { Started.TrySetResult(); return new(Result.Task); }
    }
}

