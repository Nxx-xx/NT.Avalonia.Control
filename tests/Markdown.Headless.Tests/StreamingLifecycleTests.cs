using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using NT.AvaloniaControl.Markdown;
using Reader = NT.AvaloniaControl.Markdown.Markdown;

namespace Markdown.Headless.Tests;

public sealed class StreamingLifecycleTests
{
    [Fact]
    public Task MT11_PipelineChangeRetainsAcceptedUnappliedTailAndCancelsOldSession() => HeadlessTestScope.Run(() => MT11_PipelineChangeRetainsAcceptedUnappliedTailAndCancelsOldSessionCore());

    private async Task MT11_PipelineChangeRetainsAcceptedUnappliedTailAndCancelsOldSessionCore()
    {
        var reader = new Reader { Text = "seed" };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync();
            reader.SetSelection(0, 4);
            var session = reader.BeginStreaming();
            session.Append(" accepted tail");
            // This call precedes the merge deadline and must use received source, not old Text.
            reader.ConfigurePipeline(null);
            Assert.Equal(MarkdownUpdateStatus.Cancelled, (await session.Completion).Status);
            await reader.WhenUpdatedAsync();
            Assert.Equal("seed accepted tail", reader.Text);
            Assert.Equal("seed accepted tail", reader.Document.Source);
            Assert.Equal("seed", reader.SelectedText);
            Assert.Throws<InvalidOperationException>(() => session.Append("stale"));
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task MT11_CancelRetainsAcceptedSourceAndNextSessionDoesNotReviveOldProducer() => HeadlessTestScope.Run(() => MT11_CancelRetainsAcceptedSourceAndNextSessionDoesNotReviveOldProducerCore());

    private async Task MT11_CancelRetainsAcceptedSourceAndNextSessionDoesNotReviveOldProducerCore()
    {
        var reader = new Reader { Text = "seed" };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync();
            var published = new List<string>();
            reader.Updated += (_, _) => published.Add(reader.Document.Source);
            var cancelled = reader.BeginStreaming();
            cancelled.Append(" accepted");
            reader.CancelStreaming();
            Assert.Equal(MarkdownUpdateStatus.Cancelled, (await cancelled.CompleteAsync()).Status);
            Assert.Equal("seed accepted", reader.Text);
            await cancelled.DisposeAsync();
            Assert.Throws<InvalidOperationException>(() => cancelled.Append("old producer"));
            var fresh = reader.BeginStreaming();
            fresh.Append(" fresh");
            var result = await fresh.CompleteAsync();
            Assert.Equal(MarkdownUpdateStatus.Completed, result.Status);
            Assert.Equal("seed accepted fresh", reader.Document.Source);
            Assert.DoesNotContain("seed accepted", published);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task MT11_CompletedSessionDisposeOrCancelCannotCancelTheNewSession() => HeadlessTestScope.Run(() => MT11_CompletedSessionDisposeOrCancelCannotCancelTheNewSessionCore());

    private async Task MT11_CompletedSessionDisposeOrCancelCannotCancelTheNewSessionCore()
    {
        var reader = new Reader { Text = "seed" };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync();
            var completed = reader.BeginStreaming(); completed.Append(" first");
            var firstResult = await completed.CompleteAsync();
            Assert.Equal(MarkdownUpdateStatus.Completed, firstResult.Status);
            var fresh = reader.BeginStreaming(); fresh.Append(" second");
            completed.Cancel(); completed.Dispose(); await completed.DisposeAsync();
            Assert.Same(firstResult, await completed.CompleteAsync());
            var result = await fresh.CompleteAsync();
            Assert.Equal(MarkdownUpdateStatus.Completed, result.Status);
            Assert.Equal("seed first second", reader.Document.Source);
            reader.CancelStreaming();
            Assert.Same(result, await reader.CompleteStreamingAsync());
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task MT24_RepeatedControlCompletionPreservesHiddenLayoutBoundary() => HeadlessTestScope.Run(() => MT24_RepeatedControlCompletionPreservesHiddenLayoutBoundaryCore());

    private async Task MT24_RepeatedControlCompletionPreservesHiddenLayoutBoundaryCore()
    {
        var reader = new Reader { Text = "seed" };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync();
            reader.IsVisible = false;
            reader.AppendText(" hidden");
            var result = await reader.CompleteStreamingAsync();
            Assert.Equal(MarkdownUpdateStatus.Completed, result.Status);
            Assert.True(result.LayoutPending);
            Assert.Same(result, await reader.CompleteStreamingAsync());
            reader.CancelStreaming();
            Assert.Same(result, await reader.CompleteStreamingAsync());
            reader.IsVisible = true;
            window.UpdateLayout();
            Assert.Equal("seed hidden", reader.Document.Source);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task MT17_ConfigurationCallbackFailureLeavesExistingSessionAndPipelineUsable() => HeadlessTestScope.Run(() => MT17_ConfigurationCallbackFailureLeavesExistingSessionAndPipelineUsableCore());

    private async Task MT17_ConfigurationCallbackFailureLeavesExistingSessionAndPipelineUsableCore()
    {
        var reader = new Reader { Text = "seed" };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync();
            var session = reader.BeginStreaming(); session.Append(" **tail**");
            Assert.Throws<FormatException>(() => reader.ConfigurePipeline(_ => throw new FormatException("invalid host pipeline")));
            Assert.Equal(MarkdownUpdateStatus.Completed, (await session.CompleteAsync()).Status);
            Assert.Equal("seed tail", reader.Document.Text);
        }
        finally { window.Close(); }
    }

    private static Window Show(Reader reader) { var window = new Window { Width = 600, Height = 300, Content = reader }; window.Show(); return window; }
}

