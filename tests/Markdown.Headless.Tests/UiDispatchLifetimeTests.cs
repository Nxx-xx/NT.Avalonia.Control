using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Markdig.Syntax;
using NT.AvaloniaControl.Markdown;
using Reader = NT.AvaloniaControl.Markdown.Markdown;

namespace Markdown.Headless.Tests;

public sealed class UiDispatchLifetimeTests
{
    [Fact]
    public Task ResourceCommitIsNeutralEvenWhenAnEarlierNativeJobHasPoisonedTheReceiverScope() => HeadlessTestScope.Run(() => ResourceCommitIsNeutralEvenWhenAnEarlierNativeJobHasPoisonedTheReceiverScopeCore());

    private async Task ResourceCommitIsNeutralEvenWhenAnEarlierNativeJobHasPoisonedTheReceiverScopeCore()
    {
        var ambient = new AsyncLocal<string?>(); var service = new AmbientService(ambient);
        var reader = new Reader { Text = "![image](https://example.test/receiver)", ImageLoader = service };
        var window = Show(reader); string? received = "not-called"; var nativeJobObserved = false;
        try
        {
            await reader.WhenUpdatedAsync(); await WaitFor(() => reader.ActiveResourceRequestCount == 0, window);
            var image = reader.GetVisualDescendants().OfType<MarkdownImage>().Single();
            service.DelayNext = true;
            image.PropertyChanged += (_, change) =>
            { if (change.Property == MarkdownImage.IsLoadingProperty && !image.IsLoading) received = ambient.Value; };
            image.Reload(); await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            // The raw control is contained inside one balanced outer scope, so it
            // cannot contaminate xUnit or subsequent cases. The original native job
            // really restores the marker before the public node's UI completion.
            ExecutionContext.Run(ExecutionContext.Capture()!, _ =>
            {
                service.Finish(false);
                var deadline = System.Diagnostics.Stopwatch.StartNew();
                while (image.IsLoading && deadline.Elapsed < TimeSpan.FromSeconds(5))
                {
                    ambient.Value = "poisoned-receiver";
                    Dispatcher.UIThread.Post(() => nativeJobObserved |= ambient.Value == "poisoned-receiver", DispatcherPriority.Send);
                    ambient.Value = null;
                    Dispatcher.UIThread.RunJobs();
                    if (image.IsLoading) Thread.Sleep(1);
                }
                Assert.False(image.IsLoading);
            }, null);
            Assert.True(nativeJobObserved);
            Assert.Null(received);
            Assert.Null(ambient.Value);
        }
        finally { ambient.Value = null; service.Finish(false); window.Close(); }
    }

    [Fact]
    public Task DragAutoscrollTicksDoNotReplayThePointerPressScopeAndReleaseStillStopsThem() => HeadlessTestScope.Run(() => DragAutoscrollTicksDoNotReplayThePointerPressScopeAndReleaseStillStopsThemCore());

    private async Task DragAutoscrollTicksDoNotReplayThePointerPressScopeAndReleaseStillStopsThemCore()
    {
        var ambient = new AsyncLocal<string?>();
        var reader = new Reader { Text = string.Join("\n\n", Enumerable.Range(0, 200).Select(i => $"paragraph {i:D3}")) };
        var window = Show(reader); var contexts = new List<string?>();
        try
        {
            await reader.WhenUpdatedAsync(); await Flush(window);
            var scroll = reader.GetVisualDescendants().OfType<ScrollViewer>().Single(scroll => scroll.Name == "PART_ScrollViewer");
            scroll.PropertyChanged += (_, change) => { if (change.Property == ScrollViewer.OffsetProperty && scroll.Offset.Y > 0) contexts.Add(ambient.Value); };
            var text = reader.GetVisualDescendants().OfType<MarkdownTextPresenter>().OrderBy(text => text.TextStart).First();
            var rect = text.TextLayout.HitTestTextPosition(0);
            var start = text.TranslatePoint(new Point(rect.X + text.Padding.Left + .1, rect.Y + text.Padding.Top + rect.Height / 2), window)!.Value;
            var outside = new Point(start.X, window.ClientSize.Height + 60);
            ambient.Value = "retired-pointer"; window.MouseDown(start, MouseButton.Left); ambient.Value = null;
            window.MouseMove(outside, RawInputModifiers.LeftMouseButton);
            await WaitFor(() => scroll.Offset.Y > 20, window);
            Assert.NotEmpty(contexts); Assert.All(contexts, context => Assert.Null(context));
            Assert.True(reader.Selection.Length > 0);
            window.MouseUp(outside, MouseButton.Left); await Flush(window);
            var stopped = scroll.Offset;
            await Task.Delay(120, TestContext.Current.CancellationToken); window.UpdateLayout();
            Assert.Equal(stopped, scroll.Offset); Assert.Null(ambient.Value);
        }
        finally { ambient.Value = null; reader.CancelInteraction(); window.Close(); }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public Task UpdateErrorReportsOnlyTheCurrentFailureAndNeverReplaysTheFailedProducerScope(int nextAction) => HeadlessTestScope.Run(() => UpdateErrorReportsOnlyTheCurrentFailureAndNeverReplaysTheFailedProducerScopeCore(nextAction));

    private async Task UpdateErrorReportsOnlyTheCurrentFailureAndNeverReplaysTheFailedProducerScopeCore(int nextAction)
    {
        var ambient = new AsyncLocal<string?>(); var fail = false;
        var reader = new Reader();
        reader.ConfigureParser(new() { ConfigurePipeline = builder => builder.DocumentProcessed += _ =>
        { if (Volatile.Read(ref fail)) throw new InvalidOperationException("controlled parser failure"); } });
        var window = Show(reader); var errors = new List<string?>();
        try
        {
            await reader.WhenUpdatedAsync(); await Flush(window);
            reader.Error += (_, args) => { if (args.Category == "Update") errors.Add(ambient.Value); };
            fail = true;
            ambient.Value = "failed-producer"; reader.Text = "fail this parse"; ambient.Value = null;
            var failed = reader.WhenUpdatedAsync();
            // Parsing errors complete off the dispatcher. Keep its UI notification pending
            // so replacement/detach can happen before notification delivery.
            Assert.True(SpinWait.SpinUntil(() => failed.IsCompleted, TimeSpan.FromSeconds(5)));
            Assert.Equal(MarkdownUpdateStatus.Error, (await failed).Status);
            if (nextAction == 1) { fail = false; reader.Text = "replacement"; await reader.WhenUpdatedAsync(); }
            else if (nextAction == 2) window.Content = null;
            await Flush(window);
            if (nextAction == 0) Assert.Null(Assert.Single(errors));
            else Assert.Empty(errors);
            Assert.Null(ambient.Value);
        }
        finally { ambient.Value = null; window.Close(); }
    }

    [Fact]
    public Task UpdateCommitDoesNotReplayAProducerAmbientScopeWhileTheParserStillReceivesIt() => HeadlessTestScope.Run(() => UpdateCommitDoesNotReplayAProducerAmbientScopeWhileTheParserStillReceivesItCore());

    private async Task UpdateCommitDoesNotReplayAProducerAmbientScopeWhileTheParserStillReceivesItCore()
    {
        var ambient = new AsyncLocal<string?>(); string? parsed = null; string? committed = "not-called";
        var reader = new Reader();
        reader.ConfigureParser(new()
        {
            Converters = [new MarkdownNodeConverterRegistration<ParagraphBlock>(_ => { parsed = ambient.Value; return null; })]
        });
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync(); await Flush(window);
            reader.Updated += (_, _) => committed = ambient.Value;
            ambient.Value = "producer"; reader.Text = "producer paragraph"; ambient.Value = null;
            await reader.WhenUpdatedAsync(); await Flush(window);
            Assert.Equal("producer", parsed);
            Assert.Null(committed);
            Assert.Null(ambient.Value);
            Assert.Equal("producer paragraph", reader.Document.Text);
        }
        finally { ambient.Value = null; window.Close(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ResourceUiCompletionDropsTheRetiredScopeWithoutChangingHostBackgroundServiceContext(bool code) => HeadlessTestScope.Run(() => ResourceUiCompletionDropsTheRetiredScopeWithoutChangingHostBackgroundServiceContextCore(code));

    private async Task ResourceUiCompletionDropsTheRetiredScopeWithoutChangingHostBackgroundServiceContextCore(bool code)
    {
        var ambient = new AsyncLocal<string?>();
        var service = new AmbientService(ambient);
        var reader = new Reader { Text = code ? "```text\ncode\n```" : "![image](https://example.test/ambient)", ImageLoader = service, CodeHighlighter = service };
        var window = Show(reader); string? received = "not-called";
        try
        {
            await reader.WhenUpdatedAsync(); await WaitFor(() => reader.ActiveResourceRequestCount == 0, window);
            service.DelayNext = true;
            if (code)
            {
                var node = reader.GetVisualDescendants().OfType<MarkdownCodeBlock>().Single();
                node.PropertyChanged += (_, change) => { if (change.Property == MarkdownCodeBlock.IsHighlightingProperty && !node.IsHighlighting) received = ambient.Value; };
                ambient.Value = "service-caller"; node.Reload(); ambient.Value = null;
            }
            else
            {
                var node = reader.GetVisualDescendants().OfType<MarkdownImage>().Single();
                node.PropertyChanged += (_, change) => { if (change.Property == MarkdownImage.IsLoadingProperty && !node.IsLoading) received = ambient.Value; };
                ambient.Value = "service-caller"; node.Reload(); ambient.Value = null;
            }
            await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.Equal("service-caller", service.HostContext);
            service.Finish(code);
            await WaitFor(() => reader.ActiveResourceRequestCount == 0, window);
            Assert.Null(received);
            Assert.Null(ambient.Value);
            Assert.Null(reader.GetVisualDescendants().OfType<MarkdownImage>().FirstOrDefault()?.LoadError);
        }
        finally { ambient.Value = null; service.Finish(code); window.Close(); }
    }

    [Fact]
    public Task HighlighterInvalidationReloadsOnceWithoutReplayingItsSenderScope() => HeadlessTestScope.Run(() => HighlighterInvalidationReloadsOnceWithoutReplayingItsSenderScopeCore());

    private async Task HighlighterInvalidationReloadsOnceWithoutReplayingItsSenderScopeCore()
    {
        var ambient = new AsyncLocal<string?>(); var service = new AmbientService(ambient);
        var reader = new Reader { Text = "```text\ncode\n```", CodeHighlighter = service };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync(); await WaitFor(() => reader.ActiveResourceRequestCount == 0, window);
            var previous = service.Calls;
            ambient.Value = "retired-invalidation"; service.Invalidate(); service.Invalidate(); ambient.Value = null;
            await WaitFor(() => service.Calls > previous && reader.ActiveResourceRequestCount == 0, window);
            Assert.Equal(previous + 1, service.Calls);
            Assert.Null(service.HostContext);
            Assert.Null(ambient.Value);
        }
        finally { ambient.Value = null; window.Close(); }
    }

    [Fact]
    public Task CoarseReadingRestorationCoalescesItsLatestTargetAndDropsTheSenderScope() => HeadlessTestScope.Run(() => CoarseReadingRestorationCoalescesItsLatestTargetAndDropsTheSenderScopeCore());

    private async Task CoarseReadingRestorationCoalescesItsLatestTargetAndDropsTheSenderScopeCore()
    {
        var ambient = new AsyncLocal<string?>(); var reader = new Reader { Text = string.Join("\n\n", Enumerable.Range(0, 200).Select(i => $"paragraph {i:D3}")) };
        var window = Show(reader); var contexts = new List<string?>(); var offsets = new List<double>();
        try
        {
            await reader.WhenUpdatedAsync(); await Flush(window);
            var scroll = reader.GetVisualDescendants().OfType<ScrollViewer>().Single(scroll => scroll.Name == "PART_ScrollViewer");
            var first = reader.Document.Blocks[1]; var last = reader.Document.Blocks[3];
            var anchorType = typeof(Reader).Assembly.GetType("NT.AvaloniaControl.Markdown.MarkdownReadingAnchor", true)!;
            var restore = typeof(Reader).GetMethod("RestoreReadingAnchor", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var offsetMethod = typeof(MarkdownDocumentPresenter).GetMethod("AnchorOffset", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var expected = (double)offsetMethod.Invoke(reader.DocumentPresenter, [last.Id, 0d])!;
            scroll.PropertyChanged += (_, change) =>
            { if (change.Property == ScrollViewer.OffsetProperty) { contexts.Add(ambient.Value); offsets.Add(scroll.Offset.Y); } };
            ambient.Value = "retired-coarse";
            restore.Invoke(reader, [Activator.CreateInstance(anchorType, [first.Id, 0d, null])]);
            restore.Invoke(reader, [Activator.CreateInstance(anchorType, [last.Id, 0d, null])]);
            ambient.Value = null;
            await Flush(window);
            Assert.Equal(expected, scroll.Offset.Y, .1);
            Assert.Single(offsets);
            Assert.All(contexts, context => Assert.Null(context));
            Assert.Null(ambient.Value);
        }
        finally { ambient.Value = null; window.Close(); }
    }

    private static Window Show(Reader reader) { var window = new Window { Width = 600, Height = 280, Content = reader }; window.Show(); return window; }
    private static async Task Flush(Window window)
    { await Task.Delay(20, TestContext.Current.CancellationToken); await Dispatcher.UIThread.InvokeAsync(window.UpdateLayout, DispatcherPriority.Loaded); }
    private static async Task WaitFor(Func<bool> predicate, Window window)
    {
        for (var i = 0; i < 150; i++) { await Flush(window); if (predicate()) return; }
        Assert.Fail("The bounded UI completion wait did not converge.");
    }

    private sealed class AmbientService(AsyncLocal<string?> ambient) : IMarkdownImageLoader, IMarkdownCodeHighlighter
    {
        private readonly TaskCompletionSource<MarkdownImageLease> _image = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<IReadOnlyList<MarkdownHighlightSpan>> _code = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _calls;
        public int Calls => Volatile.Read(ref _calls);
        public bool DelayNext { get; set; }
        public string? HostContext { get; private set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public event EventHandler? Invalidated;
        public void Invalidate() => Invalidated?.Invoke(this, EventArgs.Empty);
        public ValueTask<MarkdownImageLease> LoadAsync(Uri uri, CancellationToken cancellationToken)
        {
            HostContext = ambient.Value; Interlocked.Increment(ref _calls);
            if (!DelayNext) return ValueTask.FromResult(Image());
            Started.TrySetResult(); return new(_image.Task);
        }
        public ValueTask<IReadOnlyList<MarkdownHighlightSpan>> HighlightAsync(string code, string? language, CancellationToken cancellationToken)
        {
            HostContext = ambient.Value; Interlocked.Increment(ref _calls);
            if (!DelayNext) return ValueTask.FromResult<IReadOnlyList<MarkdownHighlightSpan>>([]);
            Started.TrySetResult(); return new(_code.Task);
        }
        public void Finish(bool code)
        {
            if (code) _code.TrySetResult([]);
            else if (!_image.Task.IsCompleted) _image.TrySetResult(Image());
        }
        private static MarkdownImageLease Image() => new(new WriteableBitmap(new PixelSize(64, 4), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul));
    }
}

