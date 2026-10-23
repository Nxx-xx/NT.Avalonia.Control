using System.Diagnostics;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class StreamingSchedulerTests
{
    [AvaloniaFact]
    public void Invalid_batch_or_pending_limits_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChartStreamingSession<int>(_ => { }, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChartStreamingSession<int>(_ => { }, 4, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChartStreamingSession<int>(_ => { }, 4, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChartStreamingSession<int>(_ => { }, Array.MaxLength / 2 + 1, 2));
        Assert.Throws<ArgumentNullException>(() => new ChartStreamingSession<int>(null!));
    }

    [AvaloniaFact]
    public void Oversized_input_is_rejected_before_acceptance_and_empty_input_uses_no_slot()
    {
        using var session = new ChartStreamingSession<int>(_ => { }, 2);
        Assert.Throws<ArgumentOutOfRangeException>(() => session.TryAppend(new[] { 1, 2, 3 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.AppendAsync(new[] { 1, 2, 3 }).GetAwaiter().GetResult());
        Assert.True(session.TryAppend(ReadOnlyMemory<int>.Empty));
        Assert.True(session.AppendAsync(ReadOnlyMemory<int>.Empty).IsCompletedSuccessfully);
        Assert.Equal(0, session.Statistics.AcceptedSamples);
        Assert.Equal(0, session.Statistics.PendingBatches);
        Drain(session.CompleteAsync());
        Ledger(session.Statistics);
    }

    [AvaloniaFact]
    public void Accepted_input_memory_is_copied_and_paused_callbacks_do_not_apply()
    {
        var applied = new List<int>();
        using var session = new ChartStreamingSession<int>(values => applied.AddRange(values), 4);
        session.Pause();
        var borrowed = new[] { 1, 2, 3 };
        Assert.True(session.TryAppend(borrowed));
        Array.Fill(borrowed, -1);
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(applied);
        Assert.True(session.IsPaused);
        Assert.Equal(3, session.Statistics.AcceptedSamples);
        Assert.Equal(3, session.Statistics.PendingSamples);
        session.Resume();
        Drain(session.CompleteAsync());
        Assert.Equal([1, 2, 3], applied);
        Assert.True(session.IsCompleted);
        Ledger(session.Statistics);
    }

    [AvaloniaFact]
    public void Two_paused_batches_fill_the_bound_and_try_append_explicitly_counts_rejection()
    {
        var applied = new List<int>();
        using var session = new ChartStreamingSession<int>(values => applied.AddRange(values), 4, 2);
        session.Pause();
        Assert.True(session.TryAppend(new[] { 1, 2 }));
        Assert.True(session.TryAppend(new[] { 3 }));
        Assert.False(session.TryAppend(new[] { 4, 5, 6 }));
        Assert.Equal(2, session.Statistics.PendingBatches);
        Assert.Equal(2, session.Statistics.MaximumObservedPendingBatches);
        Assert.Equal(3, session.Statistics.AcceptedSamples);
        Assert.Equal(3, session.Statistics.RejectedSamples);
        session.Resume();
        Drain(session.CompleteAsync());
        Assert.Equal([1, 2, 3], applied);
        Assert.Equal(3, session.Statistics.CommittedSamples);
        Assert.Equal(0, session.Statistics.PendingBatches);
        Ledger(session.Statistics);
    }

    [AvaloniaFact]
    public void A_single_waiting_writer_observes_backpressure_and_preserves_sample_order()
    {
        var applied = new List<int>();
        using var session = new ChartStreamingSession<int>(values => applied.AddRange(values), 4, 2);
        session.Pause();
        Assert.True(session.TryAppend(new[] { 1, 2 }));
        Assert.True(session.TryAppend(new[] { 3, 4 }));
        var waiting = session.AppendAsync(new[] { 5, 6 });
        Assert.False(waiting.IsCompleted);
        Assert.Equal(1, session.Statistics.WaitingWriters);
        Assert.Equal(4, session.Statistics.AcceptedSamples);
        session.Resume();
        Drain(waiting);
        Drain(session.CompleteAsync());
        Assert.Equal([1, 2, 3, 4, 5, 6], applied);
        Assert.Equal(0, session.Statistics.WaitingWriters);
        Assert.InRange(session.Statistics.MaximumObservedPendingBatches, 1, 2);
        Ledger(session.Statistics);
    }

    [AvaloniaFact]
    public void Additional_waiting_writers_fail_explicitly_without_becoming_hidden_queue_entries()
    {
        using var session = new ChartStreamingSession<int>(_ => { }, 4, 1);
        session.Pause();
        Assert.True(session.TryAppend(new[] { 1 }));
        var waiting = session.AppendAsync(new[] { 2 });
        Assert.Equal(1, session.Statistics.WaitingWriters);
        Assert.Throws<InvalidOperationException>(() => session.AppendAsync(new[] { 3 }).GetAwaiter().GetResult());
        Assert.Equal(1, session.Statistics.WaitingWriters);
        Assert.Equal(1, session.Statistics.AcceptedSamples);
        session.Resume();
        Drain(waiting);
        Drain(session.CompleteAsync());
        Ledger(session.Statistics);
    }

    [AvaloniaFact]
    public void Canceling_a_waiting_writer_does_not_cancel_or_accept_its_borrowed_samples()
    {
        var applied = new List<int>();
        using var session = new ChartStreamingSession<int>(values => applied.AddRange(values), 4, 1);
        using var cancellation = new CancellationTokenSource();
        session.Pause();
        Assert.True(session.TryAppend(new[] { 1, 2 }));
        var waiting = session.AppendAsync(new[] { 3, 4 }, cancellation.Token);
        cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => waiting.GetAwaiter().GetResult());
        Assert.Equal(0, session.Statistics.WaitingWriters);
        Assert.Equal(2, session.Statistics.AcceptedSamples);
        Assert.Equal(0, session.Statistics.CanceledSamples);
        session.Resume();
        Drain(session.CompleteAsync());
        Assert.Equal([1, 2], applied);
        Ledger(session.Statistics);
    }

    [AvaloniaFact]
    public void Complete_freezes_input_and_waits_for_the_last_paused_ui_commit()
    {
        var applied = new List<int>();
        using var session = new ChartStreamingSession<int>(values => applied.AddRange(values), 4);
        session.Pause();
        Assert.True(session.TryAppend(new[] { 1, 2 }));
        var completion = session.CompleteAsync();
        Assert.False(completion.IsCompleted);
        Assert.False(session.TryAppend(new[] { 3 }));
        Assert.Throws<InvalidOperationException>(() => session.AppendAsync(new[] { 4 }).GetAwaiter().GetResult());
        Assert.Empty(applied);
        session.Resume();
        Drain(completion);
        Drain(session.Completion);
        Drain(session.CompleteAsync());
        Assert.Equal([1, 2], applied);
        Assert.Equal(2, session.Statistics.CommittedSamples);
        Ledger(session.Statistics);
    }

    [AvaloniaFact]
    public void Cancel_clears_accepted_pending_samples_and_late_posted_callbacks_cannot_apply_them()
    {
        var applied = new List<int>();
        using var session = new ChartStreamingSession<int>(values => applied.AddRange(values), 4);
        Assert.True(session.TryAppend(new[] { 1, 2 }));
        Assert.True(session.TryAppend(new[] { 3, 4 }));
        session.Cancel();
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(applied);
        Assert.ThrowsAny<OperationCanceledException>(() => session.Completion.GetAwaiter().GetResult());
        Assert.Equal(4, session.Statistics.CanceledSamples);
        Assert.Equal(0, session.Statistics.PendingSamples);
        Assert.Equal(0, session.Statistics.PendingBatches);
        Assert.False(session.TryAppend(new[] { 5 }));
        Ledger(session.Statistics);
    }

    [AvaloniaFact]
    public void Cancel_wakes_the_waiting_writer_and_does_not_count_it_as_accepted()
    {
        using var session = new ChartStreamingSession<int>(_ => { }, 4, 1);
        session.Pause();
        Assert.True(session.TryAppend(new[] { 1, 2 }));
        var waiting = session.AppendAsync(new[] { 3, 4 });
        session.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => waiting.GetAwaiter().GetResult());
        Assert.Equal(2, session.Statistics.AcceptedSamples);
        Assert.Equal(2, session.Statistics.CanceledSamples);
        Assert.Equal(0, session.Statistics.WaitingWriters);
        Ledger(session.Statistics);
    }

    [AvaloniaFact]
    public void Dispose_requests_cancel_and_prevents_old_session_callbacks_from_touching_a_new_session()
    {
        var applied = new List<int>();
        var old = new ChartStreamingSession<int>(values => applied.AddRange(values), 4);
        Assert.True(old.TryAppend(new[] { 1, 2 }));
        old.Dispose();
        using var current = new ChartStreamingSession<int>(values => applied.AddRange(values), 4);
        Assert.True(current.TryAppend(new[] { 3, 4 }));
        Drain(current.CompleteAsync());
        Assert.True(old.IsDisposed);
        Assert.Equal([3, 4], applied);
        Assert.Equal(2, old.Statistics.CanceledSamples);
        Ledger(old.Statistics); Ledger(current.Statistics);
    }

    [AvaloniaFact]
    public void Apply_failure_is_observable_and_no_failed_batch_is_silently_retried()
    {
        var calls = 0;
        var failure = new InvalidOperationException("controlled apply failure");
        using var session = new ChartStreamingSession<int>(_ => { calls++; throw failure; }, 4);
        session.Pause();
        Assert.True(session.TryAppend(new[] { 1, 2 }));
        Assert.True(session.TryAppend(new[] { 3, 4 }));
        var completion = session.CompleteAsync();
        session.Resume();
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => Drain(completion)));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, calls);
        Assert.Same(failure, session.Error);
        Assert.Equal(0, session.Statistics.CommittedSamples);
        Assert.Equal(4, session.Statistics.FaultedSamples + session.Statistics.CanceledSamples);
        Assert.Equal(0, session.Statistics.PendingSamples);
        Ledger(session.Statistics);
    }

    [AvaloniaFact]
    public void Inflight_batch_occupies_a_slot_during_reentrant_append()
    {
        var acceptedWhileApplying = true;
        ChartStreamingSession<int>? session = null;
        var applied = new List<int>();
        using (session = new ChartStreamingSession<int>(values =>
        {
            applied.AddRange(values);
            acceptedWhileApplying = session!.TryAppend(new[] { 3 });
        }, 4, 1))
        {
            Assert.True(session.TryAppend(new[] { 1, 2 }));
            Drain(session.FlushAsync());
            Assert.False(acceptedWhileApplying);
            Assert.Equal([1, 2], applied);
            Assert.Equal(1, session.Statistics.RejectedSamples);
            Assert.Equal(1, session.Statistics.MaximumObservedPendingBatches);
            Drain(session.CompleteAsync());
            Ledger(session.Statistics);
        }
    }

    [AvaloniaFact]
    public void Reentrant_dispatcher_pumping_cannot_nest_a_later_ui_commit_inside_the_current_one()
    {
        var events = new List<string>(); var calls = 0; var depth = 0; var maximumDepth = 0;
        ChartStreamingSession<int>? session = null;
        using (session = new ChartStreamingSession<int>(values =>
        {
            var call = ++calls;
            depth++; maximumDepth = Math.Max(maximumDepth, depth);
            events.Add($"begin-{call}:{string.Join(',', values)}");
            if (call == 1)
            {
                Assert.True(session!.TryAppend(new[] { 3, 4 }));
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(1, calls);
                Assert.Equal(2, session.Statistics.PendingBatches);
            }
            events.Add($"end-{call}"); depth--;
        }, 4, 2))
        {
            Assert.True(session.TryAppend(new[] { 1, 2 }));
            Drain(session.FlushAsync());
            Drain(session.CompleteAsync());
            Assert.Equal(["begin-1:1,2", "end-1", "begin-2:3,4", "end-2"], events);
            Assert.Equal(1, maximumDepth);
            Assert.Equal(4, session.Statistics.CommittedSamples);
            Ledger(session.Statistics);
        }
    }

    [AvaloniaFact]
    public void Cancel_inside_a_successful_callback_cancels_only_queued_samples_and_commits_inflight_samples()
    {
        var applied = new List<int>();
        ChartStreamingSession<int>? session = null;
        using (session = new ChartStreamingSession<int>(values =>
        {
            applied.AddRange(values);
            Assert.True(session!.TryAppend(new[] { 3, 4 }));
            session.Cancel();
            Dispatcher.UIThread.RunJobs();
        }, 4, 2))
        {
            Assert.True(session.TryAppend(new[] { 1, 2 }));
            Assert.ThrowsAny<OperationCanceledException>(() => Drain(session.Completion));
            Assert.Equal([1, 2], applied);
            Assert.Equal(4, session.Statistics.AcceptedSamples);
            Assert.Equal(2, session.Statistics.CommittedSamples);
            Assert.Equal(2, session.Statistics.CanceledSamples);
            Assert.Equal(0, session.Statistics.PendingSamples);
            Ledger(session.Statistics);
        }
    }

    [AvaloniaFact]
    public void Complete_inside_a_callback_drains_its_accepted_successor_after_the_current_callback_returns()
    {
        var applied = new List<int>(); var calls = 0;
        ChartStreamingSession<int>? session = null;
        using (session = new ChartStreamingSession<int>(values =>
        {
            applied.AddRange(values);
            if (++calls == 1)
            {
                Assert.True(session!.TryAppend(new[] { 3, 4 }));
                Assert.False(session.CompleteAsync().IsCompleted);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(1, calls);
            }
        }, 4, 2))
        {
            Assert.True(session.TryAppend(new[] { 1, 2 }));
            Drain(session.Completion);
            Assert.Equal([1, 2, 3, 4], applied);
            Assert.Equal(4, session.Statistics.CommittedSamples);
            Assert.Equal(0, session.Statistics.CanceledSamples);
            Ledger(session.Statistics);
        }
    }

    [AvaloniaFact]
    public void A_partially_effectful_throwing_callback_is_not_retried_and_its_queued_successor_is_canceled()
    {
        var sideEffects = new List<int>(); var calls = 0;
        var failure = new InvalidOperationException("failure after a caller-owned effect");
        ChartStreamingSession<int>? session = null;
        using (session = new ChartStreamingSession<int>(values =>
        {
            calls++; sideEffects.AddRange(values);
            Assert.True(session!.TryAppend(new[] { 3, 4 }));
            Dispatcher.UIThread.RunJobs();
            throw failure;
        }, 4, 2))
        {
            Assert.True(session.TryAppend(new[] { 1, 2 }));
            Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => Drain(session.Completion)));
            Assert.Equal(1, calls);
            Assert.Equal([1, 2], sideEffects);
            Assert.Equal(0, session.Statistics.CommittedSamples);
            Assert.Equal(2, session.Statistics.FaultedSamples);
            Assert.Equal(2, session.Statistics.CanceledSamples);
            Assert.Equal(0, session.Statistics.PendingSamples);
            Ledger(session.Statistics);
        }
    }

    [AvaloniaFact]
    public void Flush_is_a_barrier_for_previously_accepted_input_and_does_not_wait_for_future_input()
    {
        var applied = new List<int>();
        ChartStreamingSession<int>? session = null;
        using (session = new ChartStreamingSession<int>(values =>
        {
            applied.AddRange(values);
            if (applied.Count == 2)
            {
                session!.Pause();
                Assert.True(session.TryAppend(new[] { 3, 4 }));
            }
        }, 4, 2))
        {
            Assert.True(session.TryAppend(new[] { 1, 2 }));
            Drain(session.FlushAsync());
            Assert.Equal([1, 2], applied);
            Assert.True(session.IsPaused);
            Assert.Equal(2, session.Statistics.CommittedSamples);
            Assert.Equal(2, session.Statistics.PendingSamples);
            session.Resume();
            Drain(session.CompleteAsync());
            Assert.Equal([1, 2, 3, 4], applied);
            Ledger(session.Statistics);
        }
    }

    [AvaloniaFact]
    public void Canceling_a_flush_wait_does_not_discard_accepted_input()
    {
        var applied = new List<int>();
        using var session = new ChartStreamingSession<int>(values => applied.AddRange(values), 4);
        using var cancellation = new CancellationTokenSource();
        session.Pause();
        Assert.True(session.TryAppend(new[] { 1, 2 }));
        var flush = session.FlushAsync(cancellation.Token);
        cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => flush.GetAwaiter().GetResult());
        Assert.Equal(2, session.Statistics.PendingSamples);
        Assert.Equal(0, session.Statistics.CanceledSamples);
        session.Resume();
        Drain(session.CompleteAsync());
        Assert.Equal([1, 2], applied);
        Ledger(session.Statistics);
    }

    [AvaloniaFact]
    public void Canceling_a_complete_wait_still_closes_input_and_drains_accepted_samples()
    {
        var applied = new List<int>();
        using var session = new ChartStreamingSession<int>(values => applied.AddRange(values), 4);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        session.Pause();
        Assert.True(session.TryAppend(new[] { 1, 2 }));
        var wait = session.CompleteAsync(cancellation.Token);
        Assert.ThrowsAny<OperationCanceledException>(() => wait.GetAwaiter().GetResult());
        Assert.False(session.TryAppend(new[] { 3 }));
        Drain(session.Completion);
        Assert.Equal([1, 2], applied);
        Assert.Equal(0, session.Statistics.CanceledSamples);
        Ledger(session.Statistics);
    }

    [AvaloniaFact]
    public void Background_producer_backpressure_preserves_all_batches_and_sample_order()
    {
        var applied = new List<int>();
        using var session = new ChartStreamingSession<int>(values => applied.AddRange(values), 4);
        var producer = Task.Run(async () =>
        {
            for (var index = 0; index < 100; index++) await session.AppendAsync(new[] { index * 2, index * 2 + 1 });
        });
        Drain(producer);
        Drain(session.CompleteAsync());
        Assert.Equal(Enumerable.Range(0, 200), applied);
        Assert.Equal(200, session.Statistics.AcceptedSamples);
        Assert.Equal(200, session.Statistics.CommittedSamples);
        Assert.Equal(0, session.Statistics.RejectedSamples);
        Assert.InRange(session.Statistics.MaximumObservedPendingBatches, 1, 2);
        Ledger(session.Statistics);
    }

    private static void Ledger(ChartStreamingStatistics statistics) => Assert.Equal(statistics.AcceptedSamples,
        statistics.CommittedSamples + statistics.CanceledSamples + statistics.FaultedSamples + statistics.PendingSamples);

    private static void Drain(Task task)
    {
        var timeout = Stopwatch.StartNew();
        while (!task.IsCompleted)
        {
            Dispatcher.UIThread.RunJobs();
            if (timeout.Elapsed > TimeSpan.FromSeconds(10)) throw new TimeoutException("Controlled streaming operation did not reach its expected boundary.");
            Thread.Yield();
        }
        task.GetAwaiter().GetResult();
    }
}
