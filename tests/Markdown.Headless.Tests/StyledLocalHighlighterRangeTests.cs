using System.Collections.Concurrent;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Markdown;
using Reader = NT.AvaloniaControl.Markdown.Markdown;

namespace Markdown.Headless.Tests;

// Separate from the unchanged 13-case fixture. Source preparation only.
public sealed class StyledLocalHighlighterRangeTests
{
    [Fact]
    public Task AStyledNodeHighlighterWorksWithANullRootDuringFollowingAndCannotFormatAReplacedDocument() =>
        HeadlessTestScope.Run(async () =>
        {
            var local = new PendingHighlighter();
            var reader = new Reader
            {
                Text = Paragraphs(30) + "\n\n```text\nseed 😀e\u0301\t  \n```",
                AutoScrollToEnd = true,
            };
            AddLocalCodeStyle(reader, local);
            var window = Show(reader);
            try
            {
                await Updated(reader);
                await Wait(window, () => local.Requests.Any(request => request.Code.StartsWith("seed ", StringComparison.Ordinal) &&
                    !request.Token.IsCancellationRequested));
                var seedRequest = local.Requests.Last(request => request.Code.StartsWith("seed ", StringComparison.Ordinal) &&
                    !request.Token.IsCancellationRequested);
                var seed = Codes(reader).Single(code => code.Code.StartsWith("seed ", StringComparison.Ordinal));
                var seedText = CodeText(seed);
                Assert.Null(reader.CodeHighlighter);
                Assert.Same(local, seed.CodeHighlighter);
                Assert.Equal(seed.Code, seedRequest.Code);
                Assert.Equal("text", seedRequest.Language);
                Assert.EndsWith("\t  \n", seedRequest.Code);
                var emoji = seedText.TextStart + seed.Code.IndexOf("😀e\u0301", StringComparison.Ordinal);
                Assert.True(emoji >= 0);
                reader.SetSelection(emoji, emoji + "😀e\u0301".Length);
                Assert.Equal("😀e\u0301", reader.SelectedText);
                var selection = reader.Selection;
                var nextSource = reader.Text + "\n\n" + Paragraphs(70) + "\n\n```text\nnew-tail 中😀e\u0301\t  \n```";
                var tailConfirmedAtUpdated = false;
                reader.Updated += (_, _) =>
                {
                    if (reader.Document.Source != nextSource) return;
                    AssertTailVisible(reader);
                    tailConfirmedAtUpdated = true;
                };

                using (var session = reader.BeginStreaming())
                {
                    session.Append(nextSource.AsMemory(reader.Text!.Length));
                    var result = await session.CompleteAsync();
                    Assert.Equal(MarkdownUpdateStatus.Completed, result.Status);
                    Assert.False(result.LayoutPending);
                }
                Assert.True(tailConfirmedAtUpdated);
                Assert.Equal(selection, reader.Selection);
                Assert.Equal("😀e\u0301", reader.SelectedText);
                Assert.Equal(reader.Selection, reader.Selection.Normalize(reader.Document.Text));
                await Wait(window, () => seedRequest.Token.IsCancellationRequested &&
                    local.Requests.Any(request => request.Code.StartsWith("new-tail ", StringComparison.Ordinal) &&
                        !request.Token.IsCancellationRequested));
                var tailRequest = local.Requests.Last(request => request.Code.StartsWith("new-tail ", StringComparison.Ordinal) &&
                    !request.Token.IsCancellationRequested);
                var tail = Codes(reader).Single(code => code.Code.StartsWith("new-tail ", StringComparison.Ordinal));
                var tailText = CodeText(tail);
                Assert.Null(reader.CodeHighlighter);
                Assert.Same(local, tail.CodeHighlighter);
                Assert.Equal(tail.Code, tailRequest.Code);
                Assert.Equal(reader.Document.Text.Substring(tailText.TextStart, tailText.TextLength), Projection(tailText));
                Assert.True(local.Subscribers > 0);

                reader.Text = "replacement 中文😀e\u0301";
                await Updated(reader);
                await Wait(window, () => tailRequest.Token.IsCancellationRequested && local.Subscribers == 0);
                Assert.Empty(Codes(reader));
                var replacement = reader.Document;
                using var late = new LateEffects(reader, [seedText, tailText]);
                var requestCount = local.Requests.Length;
                local.Invalidate();
                local.FinishAll("#ff00ff");
                await Wait(window, () => reader.ActiveResourceRequestCount == 0);
                await Drain(window);
                Assert.Equal(requestCount, local.Requests.Length);
                Assert.Equal(0, late.DocumentEvents);
                Assert.Equal(0, late.FormattingMutations);
                Assert.Same(replacement, reader.Document);
                reader.SelectAll();
                Assert.Equal("replacement 中文😀e\u0301", reader.SelectedText);
                Assert.Empty(reader.RenderingDiagnostics);
                Assert.Equal(0, local.DisposeCount);
            }
            finally
            {
                window.Content = null;
                window.Close();
                local.FinishAll("#0000ff");
                await Wait(null, () => local.Subscribers == 0 && reader.ActiveResourceRequestCount == 0);
            }
            Assert.Equal(0, local.Subscribers);
            Assert.Equal(0, local.DisposeCount);
        });

    [Fact]
    public Task StyledLocalInvalidationAndUnloadCancelPendingWorkWithoutLateCallbacksOrUtf16Loss() =>
        HeadlessTestScope.Run(async () =>
        {
            var local = new PendingHighlighter();
            var reader = new Reader { Text = "```text\nlocal 中😀e\u0301\t  \n```", AutoScrollToEnd = true };
            AddLocalCodeStyle(reader, local);
            var window = Show(reader);
            try
            {
                await Updated(reader);
                await Wait(window, () => local.Requests.Length > 0);
                var code = Assert.Single(Codes(reader));
                var text = CodeText(code);
                Assert.Null(reader.CodeHighlighter);
                Assert.Same(local, code.CodeHighlighter);
                Assert.Equal(1, local.Subscribers);
                local.FinishAll("#0000ff");
                await Wait(window, () => reader.ActiveResourceRequestCount == 0 && !code.IsHighlighting);
                Assert.Equal(code.Code, Projection(text));
                Assert.Equal(Colors.Blue, Assert.IsAssignableFrom<ISolidColorBrush>(text.Inlines![0].Foreground).Color);
                var position = reader.Document.Text.IndexOf("😀e\u0301", StringComparison.Ordinal);
                reader.SetSelection(position, position + "😀e\u0301".Length);
                var selected = reader.SelectedText;
                var selection = reader.Selection;
                var completedRequests = local.Requests.Length;
                local.Invalidate();
                await Wait(window, () => local.Requests.Length > completedRequests && code.IsHighlighting);
                var pending = local.Requests.Where(request => !request.IsCompleted).ToArray();
                Assert.NotEmpty(pending);
                Assert.All(pending, request => Assert.Equal(code.Code, request.Code));
                Assert.Equal(selected, reader.SelectedText);
                Assert.Equal(selection, reader.Selection);

                window.Content = null;
                window.Close();
                await Wait(null, () => local.Subscribers == 0 && pending.All(request => request.Token.IsCancellationRequested));
                var document = reader.Document;
                using var late = new LateEffects(reader, [text]);
                var requestsAtUnload = local.Requests.Length;
                local.Invalidate();
                local.FinishAll("#ff0000");
                await Wait(null, () => reader.ActiveResourceRequestCount == 0);
                await Drain(null);
                Assert.Equal(requestsAtUnload, local.Requests.Length);
                Assert.Equal(0, late.DocumentEvents);
                Assert.Equal(0, late.FormattingMutations);
                Assert.Equal(0, local.Subscribers);
                Assert.Equal(0, local.DisposeCount);
                Assert.Same(document, reader.Document);
                Assert.Equal(selection, reader.Selection);
                Assert.Equal("😀e\u0301", reader.SelectedText);
                Assert.Equal(reader.Selection, reader.Selection.Normalize(reader.Document.Text));
                Assert.Equal(code.Code, Projection(text));
                Assert.Empty(reader.RenderingDiagnostics);
            }
            finally
            {
                window.Content = null;
                window.Close();
                local.FinishAll("#0000ff");
                await Wait(null, () => local.Subscribers == 0 && reader.ActiveResourceRequestCount == 0);
            }
        });

    private static void AddLocalCodeStyle(Reader reader, IMarkdownCodeHighlighter local)
    {
        var style = new Style(selector => selector.OfType<MarkdownCodeBlock>());
        style.Setters.Add(new Setter(MarkdownCodeBlock.CodeHighlighterProperty, local));
        reader.Styles.Add(style);
    }

    private static Window Show(Reader reader)
    {
        var window = new Window { Width = 640, Height = 320, Content = reader };
        window.Show();
        return window;
    }

    private static async Task Updated(Reader reader) => Assert.Equal(MarkdownUpdateStatus.Completed,
        (await reader.WhenUpdatedAsync().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Status);

    private static MarkdownCodeBlock[] Codes(Reader reader) => reader.GetVisualDescendants().OfType<MarkdownCodeBlock>().ToArray();
    private static MarkdownTextPresenter CodeText(MarkdownCodeBlock code) =>
        Assert.Single(code.GetVisualDescendants().OfType<MarkdownTextPresenter>());
    private static string Paragraphs(int count) => string.Join("\n\n", Enumerable.Range(0, count)
        .Select(index => $"paragraph {index:D3} 中文😀e\u0301 ordinary reading text"));

    private static void AssertTailVisible(Reader reader)
    {
        var scroll = reader.GetVisualDescendants().OfType<ScrollViewer>().Single(view => view.Name == "PART_ScrollViewer");
        var lastId = reader.Document.Blocks[^1].Id;
        var tail = reader.DocumentPresenter!.Children.OfType<MarkdownBlockPresenter>().Single(view => view.NodeId == lastId);
        var origin = tail.TranslatePoint(default, scroll)!.Value;
        Assert.InRange(Math.Abs(scroll.Extent.Height - scroll.Viewport.Height - scroll.Offset.Y), 0, .5);
        Assert.True(origin.Y + tail.Bounds.Height > scroll.Padding.Top + .01);
        Assert.True(origin.Y < scroll.Padding.Top + scroll.Viewport.Height - .01);
        Assert.True(reader.DocumentPresenter!.IsMeasureValid && reader.DocumentPresenter!.IsArrangeValid);
    }

    private static string Projection(MarkdownTextPresenter text) => text.Inlines is { Count: > 0 } inlines
        ? string.Concat(inlines.Select(InlineProjection)) : text.Text ?? string.Empty;
    private static string InlineProjection(Inline inline) => inline switch
    {
        Run run => run.Text ?? string.Empty,
        Span span => string.Concat(span.Inlines.Select(InlineProjection)),
        LineBreak => "\n",
        _ => throw new InvalidOperationException("Only native text runs are expected in code."),
    };

    private static async Task Wait(Window? window, Func<bool> condition)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            window?.UpdateLayout();
            if (condition()) return;
            await Task.Delay(10, TestContext.Current.CancellationToken);
            await Dispatcher.UIThread.InvokeAsync(() => window?.UpdateLayout(), DispatcherPriority.Loaded);
        }
        Assert.Fail("Styled node service state did not settle within the fixture boundary.");
    }

    private static async Task Drain(Window? window)
    {
        for (var turn = 0; turn < 4; turn++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
            await Dispatcher.UIThread.InvokeAsync(() => window?.UpdateLayout(), DispatcherPriority.Loaded);
        }
    }

    private sealed class LateEffects : IDisposable
    {
        private readonly Reader _reader;
        private readonly MarkdownTextPresenter[] _texts;
        private readonly INotifyCollectionChanged[] _inlines;
        public int DocumentEvents { get; private set; }
        public int FormattingMutations { get; private set; }
        public LateEffects(Reader reader, MarkdownTextPresenter[] texts)
        {
            _reader = reader;
            _texts = texts;
            _inlines = texts.Select(text => (INotifyCollectionChanged)text.Inlines!).ToArray();
            reader.Updated += Updated;
            reader.UpdateMeasured += Measured;
            foreach (var text in _texts) text.PropertyChanged += TextChanged;
            foreach (var inlines in _inlines) inlines.CollectionChanged += InlinesChanged;
        }
        private void Updated(object? sender, EventArgs result) => DocumentEvents++;
        private void Measured(object? sender, MarkdownUpdateMetrics metrics) => DocumentEvents++;
        private void TextChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
        {
            if (change.Property == TextBlock.TextProperty) FormattingMutations++;
        }
        private void InlinesChanged(object? sender, NotifyCollectionChangedEventArgs change) => FormattingMutations++;
        public void Dispose()
        {
            _reader.Updated -= Updated;
            _reader.UpdateMeasured -= Measured;
            foreach (var text in _texts) text.PropertyChanged -= TextChanged;
            foreach (var inlines in _inlines) inlines.CollectionChanged -= InlinesChanged;
        }
    }

    private sealed class PendingHighlighter : IMarkdownCodeHighlighter, IDisposable
    {
        private readonly ConcurrentQueue<HighlightRequest> _requests = new();
        private EventHandler? _invalidated;
        private int _subscribers;
        private int _disposeCount;
        public HighlightRequest[] Requests => _requests.ToArray();
        public int Subscribers => Volatile.Read(ref _subscribers);
        public int DisposeCount => Volatile.Read(ref _disposeCount);
        public event EventHandler? Invalidated
        {
            add { _invalidated += value; Interlocked.Increment(ref _subscribers); }
            remove { _invalidated -= value; Interlocked.Decrement(ref _subscribers); }
        }
        public ValueTask<IReadOnlyList<MarkdownHighlightSpan>> HighlightAsync(string code, string? language, CancellationToken token)
        {
            var request = new HighlightRequest(code, language, token);
            _requests.Enqueue(request);
            return new(request.Result.Task);
        }
        public void Invalidate() => _invalidated?.Invoke(this, EventArgs.Empty);
        public void FinishAll(string color)
        {
            foreach (var request in Requests) request.Finish(color);
        }
        public void Dispose() => Interlocked.Increment(ref _disposeCount);
    }

    private sealed class HighlightRequest(string code, string? language, CancellationToken token)
    {
        public string Code { get; } = code;
        public string? Language { get; } = language;
        public CancellationToken Token { get; } = token;
        public bool IsCompleted => Result.Task.IsCompleted;
        public TaskCompletionSource<IReadOnlyList<MarkdownHighlightSpan>> Result { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        // Deliberately ignore cancellation until the test supplies a late result.
        public void Finish(string color) => Result.TrySetResult([new(0, Code.Length, color)]);
    }
}
