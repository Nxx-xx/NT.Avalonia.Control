using Avalonia.Headless;
using Avalonia.Threading;
using System.Runtime.ExceptionServices;

namespace Markdown.Themes.Tests;

internal static class HeadlessTestScope
{
    private static void RunJobs()
    {
        if (ExecutionContext.Capture() is { } caller)
            ExecutionContext.Run(caller, static _ => Dispatcher.UIThread.RunJobs(), null);
        else Dispatcher.UIThread.RunJobs();
    }

    internal static Task Run(Action test) => Run(() => { test(); return Task.CompletedTask; });

    internal static Task Run(Func<Task> test)
    {
        // Avalonia 12.0.3's xUnit adapter enters Dispatch with captureEC:false.
        // Keep xUnit on its real caller scope and use the public UI session for
        // the unchanged body. An explicit async TResult avoids Task<Task>.
        var caller = ExecutionContext.Capture() ?? throw new InvalidOperationException("The real xUnit caller scope must flow into the UI session.");
        return HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestApplication).Assembly).Dispatch<int>(() =>
        {
            Task<int>? result = null;
            ExecutionContext.Run(caller, _ => result = Execute(), null);
            return result!;
        }, TestContext.Current.CancellationToken);

        async Task<int> Execute()
        {
            Exception? bodyFailure = null;
            try { await test(); }
            catch (Exception error) { bodyFailure = error; }
            // Drain on UI while this caller scope is valid, before the public
            // session disposes its per-test application. Preserve both errors.
            try { RunJobs(); }
            catch (Exception cleanupFailure)
            {
                if (bodyFailure is not null) throw new AggregateException(bodyFailure, cleanupFailure);
                throw;
            }
            if (bodyFailure is not null) ExceptionDispatchInfo.Capture(bodyFailure).Throw();
            return 0;
        }
    }
}

