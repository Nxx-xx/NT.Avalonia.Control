using System.Collections.Concurrent;
using System.Threading.Channels;
using NT.AvaloniaControl.Markdown;

namespace Markdown.Streaming.Tests;

public sealed class MarkdownUpdateSchedulerTests
{
    [Fact]
    public async Task MT11_SlowParserCommitsPrefixesDuringSustainedInputAndKeepsOneLatestRequest()
    {
        var parser = new ControlledParser();
        var commits = Channel.CreateUnbounded<MarkdownUpdateRequest>();
        using var scheduler = Create(parser, update =>
        {
            Assert.True(update.TryCommit(() => commits.Writer.TryWrite(update.Request)));
            return Task.FromResult(MarkdownApplyResult.Applied);
        });
        var session = scheduler.BeginStreaming();
        session.Append("first");
        var first = await parser.NextAsync();
        for (var i = 0; i < 200; i++) session.Append(".");
        Assert.Equal(1, scheduler.ActiveParseCount);
        Assert.Equal(1, scheduler.PendingRequestCount);
        first.Finish();
        var prefix = await commits.Reader.ReadAsync(TestContext.Current.CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal("first", prefix.Source);
        Assert.False(session.Completion.IsCompleted);
        var second = await parser.NextAsync();
        Assert.Equal(205, second.Request.Source.Length);
        session.Append("tail");
        var final = session.CompleteAsync();
        second.Finish();
        var secondCommit = await commits.Reader.ReadAsync(TestContext.Current.CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.True(secondCommit.SourceVersion > prefix.SourceVersion);
        var third = await parser.NextAsync();
        Assert.True(third.Request.IsFinal);
        third.Finish();
        var result = await final.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(MarkdownUpdateStatus.Completed, result.Status);
        Assert.EndsWith("tail", result.Source);
        Assert.Equal(result.Source, scheduler.SourceText);
        Assert.Equal(1, parser.MaximumActive);
        Assert.False(scheduler.HasActiveSession);
        Assert.Throws<InvalidOperationException>(() => session.Append("late"));
    }

    [Fact]
    public async Task MT10_ExternalReplacementRejectsAnUncancellableOldParse()
    {
        var parser = new ControlledParser();
        var commits = new ConcurrentQueue<string>();
        using var scheduler = Create(parser, update =>
        {
            update.TryCommit(() => commits.Enqueue(update.Snapshot));
            return Task.FromResult(MarkdownApplyResult.Applied);
        });
        var session = scheduler.BeginStreaming();
        session.Append("old");
        var old = await parser.NextAsync();
        session.Append(" discarded tail");
        var replacement = scheduler.ReplaceText("new external text", 12);
        Assert.Equal(MarkdownUpdateStatus.Cancelled, (await session.Completion).Status);
        Assert.Throws<InvalidOperationException>(() => session.Append("stale"));
        old.Finish();
        var current = await parser.NextAsync();
        Assert.False(current.Request.IsStreaming);
        Assert.Equal(12, current.Request.ConfigurationVersion);
        current.Finish();
        Assert.Equal(MarkdownUpdateStatus.Completed, (await replacement.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Status);
        Assert.Equal(["new external text"], commits.ToArray());
        Assert.Equal(1, parser.MaximumActive);
    }

    [Fact]
    public async Task MT10_QueuedUiCommitValidatesAgainAfterReplacement()
    {
        var parser = new ControlledParser();
        var applyEntered = NewSignal();
        var releaseUi = NewSignal();
        var commits = new ConcurrentQueue<string>();
        var oldCommitAccepted = true;
        using var scheduler = Create(parser, async update =>
        {
            if (update.Request.Source == "old")
            {
                applyEntered.TrySetResult();
                await releaseUi.Task;
                oldCommitAccepted = update.TryCommit(() => commits.Enqueue("old"));
            }
            else update.TryCommit(() => commits.Enqueue(update.Snapshot));
            return MarkdownApplyResult.Applied;
        });
        var oldResult = scheduler.ReplaceText("old");
        (await parser.NextAsync()).Finish();
        await applyEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var freshResult = scheduler.ReplaceText("fresh");
        releaseUi.TrySetResult();
        (await parser.NextAsync()).Finish();
        Assert.Equal(MarkdownUpdateStatus.Cancelled, (await oldResult).Status);
        Assert.Equal(MarkdownUpdateStatus.Completed, (await freshResult.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Status);
        Assert.False(oldCommitAccepted);
        Assert.Equal(["fresh"], commits.ToArray());
    }

    [Fact]
    public async Task MT11_CompleteFreezesInputAndWaitsForApplyBoundary()
    {
        var parser = new ControlledParser();
        var applied = NewSignal();
        var releaseLayout = NewSignal();
        using var scheduler = Create(parser, async update =>
        {
            update.TryCommit(() => applied.TrySetResult());
            await releaseLayout.Task;
            return MarkdownApplyResult.Applied;
        });
        var session = scheduler.BeginStreaming();
        session.Append("last batch");
        var call = await parser.NextAsync();
        var complete = session.CompleteAsync();
        Assert.Throws<InvalidOperationException>(() => session.Append("late"));
        Assert.Same(complete, session.CompleteAsync());
        call.Finish();
        await applied.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.False(complete.IsCompleted);
        releaseLayout.TrySetResult();
        Assert.Equal("last batch", (await complete.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Source);
    }

    [Fact]
    public async Task MT11_ParallelAppendsPreserveEachChunkWithoutLoss()
    {
        using var scheduler = new MarkdownUpdateScheduler<string>((request, _) => Task.FromResult(request.Source),
            (update, _) => { update.TryCommit(() => { }); return Task.FromResult(MarkdownApplyResult.Applied); }, TimeSpan.Zero);
        var session = scheduler.BeginStreaming();
        await Task.WhenAll(Enumerable.Range(0, 50).Select(i => Task.Run(() => session.Append($"[{i:D2}]"), TestContext.Current.CancellationToken)));
        var result = await session.CompleteAsync().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(200, result.Source.Length);
        for (var i = 0; i < 50; i++) Assert.Equal(1, result.Source.Split($"[{i:D2}]").Length - 1);
    }

    [Fact]
    public async Task MT09_BorrowedMemoryIsCopiedAndEmptyChunksDoNotAdvanceVersion()
    {
        var parser = new ControlledParser();
        using var scheduler = Create(parser);
        var session = scheduler.BeginStreaming();
        var chars = "original".ToCharArray();
        session.Append(chars.AsMemory());
        Array.Fill(chars, 'x');
        var version = scheduler.SourceVersion;
        session.Append(string.Empty);
        Assert.Equal(version, scheduler.SourceVersion);
        var call = await parser.NextAsync();
        var final = session.CompleteAsync();
        call.Finish();
        Assert.Equal("original", (await final.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Source);
    }

    [Fact]
    public async Task MT11_ExplicitAndImplicitInputsHaveObservableSeparateLifetimes()
    {
        using var scheduler = Immediate();
        var explicitSession = scheduler.BeginStreaming();
        Assert.Throws<InvalidOperationException>(() => scheduler.BeginStreaming());
        Assert.Throws<InvalidOperationException>(() => scheduler.AppendText("mixed"));
        explicitSession.Append("a");
        explicitSession.Dispose();
        Assert.Equal(MarkdownUpdateStatus.Completed, (await explicitSession.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Status);
        scheduler.AppendText("b");
        Assert.Throws<InvalidOperationException>(() => scheduler.BeginStreaming());
        Assert.Equal("ab", (await scheduler.CompleteStreamingAsync().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Source);
        await using var finalSession = scheduler.BeginStreaming();
        finalSession.Append("c");
        await finalSession.DisposeAsync();
        Assert.Equal("abc", (await finalSession.Completion).Source);
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("detach")]
    [InlineData("dispose")]
    [InlineData("configure")]
    public async Task MT10_MT24_InvalidationCompletesPromptlyAndOldFailuresStaySilent(string action)
    {
        var parser = new ControlledParser();
        var commits = new ConcurrentQueue<string>();
        using var scheduler = Create(parser, update =>
        {
            update.TryCommit(() => commits.Enqueue(update.Snapshot));
            return Task.FromResult(MarkdownApplyResult.Applied);
        });
        var session = scheduler.BeginStreaming();
        session.Append("retained");
        var old = await parser.NextAsync();
        Task<MarkdownUpdateResult>? fresh = null;
        switch (action)
        {
            case "cancel": session.Cancel(); break;
            case "detach": scheduler.Invalidate(); break;
            case "dispose": scheduler.Dispose(); break;
            case "configure": fresh = scheduler.Reconfigure(3); break;
        }
        Assert.Equal(MarkdownUpdateStatus.Cancelled, (await session.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Status);
        Assert.Equal("retained", scheduler.SourceText);
        old.Fail(new InvalidOperationException("old parse failure"));
        if (fresh is not null)
        {
            (await parser.NextAsync()).Finish();
            Assert.Equal(MarkdownUpdateStatus.Completed, (await fresh.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Status);
            Assert.Equal(["retained"], commits.ToArray());
        }
        else Assert.Empty(commits);
    }

    [Theory]
    [InlineData(MarkdownApplyResult.Applied, MarkdownUpdateStatus.Completed, false)]
    [InlineData(MarkdownApplyResult.LayoutPending, MarkdownUpdateStatus.Completed, true)]
    [InlineData(MarkdownApplyResult.NoPresentationEnvironment, MarkdownUpdateStatus.Error, false)]
    public async Task MT24_HostCompletionBoundaryIsExplicit(MarkdownApplyResult applyResult, MarkdownUpdateStatus expected, bool layoutPending)
    {
        using var scheduler = new MarkdownUpdateScheduler<string>((request, _) => Task.FromResult(request.Source),
            (update, _) => { update.TryCommit(() => { }); return Task.FromResult(applyResult); }, TimeSpan.Zero);
        var session = scheduler.BeginStreaming();
        session.Append("document");
        var result = await session.CompleteAsync().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(expected, result.Status);
        Assert.Equal(layoutPending, result.LayoutPending);
        if (expected == MarkdownUpdateStatus.Error) Assert.IsType<InvalidOperationException>(result.Error);
    }

    [Fact]
    public async Task MT24_ParseAndPresentationFailureEndWaitAndAllowLaterReplacement()
    {
        var parser = new ControlledParser();
        using var scheduler = Create(parser, update =>
        {
            if (update.Snapshot == "presentation failure") throw new FormatException("presenter");
            update.TryCommit(() => { });
            return Task.FromResult(MarkdownApplyResult.Applied);
        });
        var failedSession = scheduler.BeginStreaming();
        failedSession.Append("parser failure");
        var failedParse = await parser.NextAsync();
        var completion = failedSession.CompleteAsync();
        failedParse.Fail(new FormatException("parser"));
        Assert.IsType<FormatException>((await completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Error);
        var presentation = scheduler.ReplaceText("presentation failure");
        (await parser.NextAsync()).Finish();
        Assert.Equal(MarkdownUpdateStatus.Error, (await presentation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Status);
        var recovery = scheduler.ReplaceText("recovered");
        (await parser.NextAsync()).Finish();
        Assert.Equal(MarkdownUpdateStatus.Completed, (await recovery.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Status);
    }

    [Fact]
    public async Task MT23_InternalSourceSynchronizationDoesNotReplaceTheActiveSession()
    {
        // Pure scheduler evidence only. AXAML/TwoWay binding is a separate host integration test.
        var parser = new ControlledParser();
        string hostText = string.Empty;
        using var scheduler = Create(parser, update =>
        {
            update.TryCommit(() => hostText = update.Request.Source);
            return Task.FromResult(MarkdownApplyResult.Applied);
        });
        var session = scheduler.BeginStreaming();
        var generation = scheduler.DocumentGeneration;
        session.Append("a");
        var first = await parser.NextAsync();
        session.Append("b");
        first.Finish();
        var second = await parser.NextAsync();
        Assert.Equal("a", hostText);
        Assert.Equal(generation, scheduler.DocumentGeneration);
        var final = session.CompleteAsync();
        second.Finish();
        Assert.Equal("ab", (await final.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Source);
        Assert.Equal("ab", hostText);
    }

    [Fact]
    public async Task MT11_CompletionAfterAppliedPrefixReusesItAndRetainsHiddenBoundary()
    {
        var parser = new ControlledParser();
        var applied = NewSignal();
        using var scheduler = Create(parser, update =>
        {
            update.TryCommit(() => applied.TrySetResult());
            return Task.FromResult(MarkdownApplyResult.LayoutPending);
        });
        var session = scheduler.BeginStreaming();
        session.Append("hidden");
        (await parser.NextAsync()).Finish();
        await applied.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var result = await session.CompleteAsync().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.True(result.LayoutPending);
        Assert.Equal(MarkdownUpdateStatus.Completed, result.Status);
    }

    [Fact]
    public void MT11_BufferAndMergeLimitsFailClearlyWithoutTruncatingAcceptedInput()
    {
        using var scheduler = new MarkdownUpdateScheduler<string>((request, _) => Task.FromResult(request.Source),
            (update, _) => { update.TryCommit(() => { }); return Task.FromResult(MarkdownApplyResult.Applied); },
            TimeSpan.Zero, maximumTextLength: 4);
        var session = scheduler.BeginStreaming();
        session.Append("1234");
        Assert.Throws<InvalidOperationException>(() => session.Append("5"));
        Assert.Equal("1234", scheduler.SourceText);
        Assert.Throws<InvalidOperationException>(() => { _ = scheduler.ReplaceText("12345"); });
        Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.AppendText("", TimeSpan.FromSeconds(2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MarkdownUpdateScheduler<string>((request, _) => Task.FromResult(request.Source),
            (_, _) => Task.FromResult(MarkdownApplyResult.Applied), TimeSpan.FromMilliseconds(-1)));
    }

    private static MarkdownUpdateScheduler<string> Create(ControlledParser parser,
        Func<MarkdownUpdate<string>, Task<MarkdownApplyResult>>? apply = null) => new(parser.ParseAsync,
        (update, _) => apply?.Invoke(update) ?? Commit(update), TimeSpan.Zero);

    private static Task<MarkdownApplyResult> Commit(MarkdownUpdate<string> update)
    {
        update.TryCommit(() => { });
        return Task.FromResult(MarkdownApplyResult.Applied);
    }

    private static MarkdownUpdateScheduler<string> Immediate() => new((request, _) => Task.FromResult(request.Source),
        (update, _) => Commit(update), TimeSpan.Zero);

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
