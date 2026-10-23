using Avalonia.Headless;
using Avalonia.Threading;
using System.Runtime.ExceptionServices;

namespace PdfViewer.Headless.Tests;

internal static class HeadlessTestScope
{
    internal static Task Run(Func<Task> test)
    {
        // Avalonia 12's xUnit adapter dispatches without the caller's execution
        // context. Use the public session while retaining xUnit v3's test scope.
        var caller = ExecutionContext.Capture() ?? throw new InvalidOperationException("A test execution context is required.");
        return HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestApplication).Assembly).Dispatch<int>(() =>
        {
            Task<int>? result = null;
            ExecutionContext.Run(caller, _ => result = Execute(), null);
            return result!;
        }, TestContext.Current.CancellationToken);

        async Task<int> Execute()
        {
            Exception? failure = null;
            try { await test(); }
            catch (Exception error) { failure = error; }
            try { Drain(); }
            catch (Exception cleanup)
            {
                if (failure is not null) throw new AggregateException(failure, cleanup);
                throw;
            }
            if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
            return 0;
        }
    }

    internal static void Drain()
    {
        var caller = ExecutionContext.Capture();
        if (caller is null) Dispatcher.UIThread.RunJobs();
        else ExecutionContext.Run(caller, _ => Dispatcher.UIThread.RunJobs(), null);
    }
}
