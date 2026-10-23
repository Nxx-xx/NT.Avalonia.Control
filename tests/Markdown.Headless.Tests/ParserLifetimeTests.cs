using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Markdig.Syntax;
using NT.AvaloniaControl.Markdown;
using Reader = NT.AvaloniaControl.Markdown.Markdown;

namespace Markdown.Headless.Tests;

public sealed class ParserLifetimeTests
{
    [Fact]
    public Task MT21_DetachedReaderCanBeCollectedWhileAHostConverterStillBlocksTheParser() => HeadlessTestScope.Run(() => MT21_DetachedReaderCanBeCollectedWhileAHostConverterStillBlocksTheParserCore());

    private async Task MT21_DetachedReaderCanBeCollectedWhileAHostConverterStillBlocksTheParserCore()
    {
        using var release = new ManualResetEventSlim();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var parser = new MarkdownParserOptions
        {
            Converters = [new MarkdownNodeConverterRegistration<ParagraphBlock>(_ =>
            {
                started.TrySetResult();
                try { if (!release.Wait(TimeSpan.FromSeconds(20))) throw new TimeoutException("The blocked converter was not released."); return null; }
                finally { finished.TrySetResult(); }
            })]
        };
        try
        {
            var (weak, completion) = await MountThenDetach(parser, started.Task);
            Assert.Equal(MarkdownUpdateStatus.Cancelled, (await completion).Status);
            for (var i = 0; i < 8 && weak.IsAlive; i++)
            {
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                await Task.Delay(20, TestContext.Current.CancellationToken);
                Collect();
            }
            Assert.False(finished.Task.IsCompleted);
            Assert.False(weak.IsAlive);
        }
        finally
        {
            release.Set();
            await finished.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<(WeakReference Reader, Task<MarkdownUpdateResult> Completion)> MountThenDetach(MarkdownParserOptions options, Task started)
    {
        var reader = new Reader(); reader.ConfigureParser(options);
        var window = new Window { Width = 600, Height = 280, Content = reader }; window.Show();
        reader.Text = "blocked converter paragraph";
        var completion = reader.WhenUpdatedAsync();
        await started.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var weak = new WeakReference(reader);
        window.Content = null; window.Close();
        return (weak, completion);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Collect() { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
}

