using System.Diagnostics;
using System.Text.Json;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace TreeDataGridNativeBenchmarks;

internal sealed class NativeAutomation : IAsyncDisposable
{
    private readonly Process _client;
    private readonly Task<string> _stderr;
    private readonly NativeReport _report;
    private int _sequence;
    private readonly string _workload;
    internal NativeAutomation(string path, NativeReport report, string workload, string backend)
    {
        _report = report;
        _workload = workload;
        var info = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        // A collector suspends the host, not its JSON-protocol child process.
        info.Environment.Remove("DOTNET_DefaultDiagnosticPortSuspend");
        info.ArgumentList.Add(path);
        info.ArgumentList.Add("--workload"); info.ArgumentList.Add(workload);
        info.ArgumentList.Add("--client-backend"); info.ArgumentList.Add(backend);
        _client = Process.Start(info) ?? throw new InvalidOperationException("Cannot start Windows UIA client.");
        _stderr = _client.StandardError.ReadToEndAsync();
        report.Event("automation-client-started", new { hostPid = Environment.ProcessId, clientPid = _client.Id, sha256 = NativeReport.Hash(path) });
    }

    internal async Task ProbeAsync(Window window, string gridId, NativeFixture? fixture, int frameId, string phase)
    {
        var readTree = _workload != "events" || phase != "frame";
        var expected = fixture is null || !readTree ? [] : Expected(fixture);
        var request = new AutomationRequest(++_sequence, window.TryGetPlatformHandle()!.Handle, Environment.ProcessId, gridId, fixture is null, readTree);
        var start = Stopwatch.GetTimestamp();
        _report.Event("automation-query-request", new { query = request, frameId, phase, expectedHash = AutomationProtocol.Hash(expected), expected });
        await _client.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request));
        await _client.StandardInput.FlushAsync();
        var line = await _client.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30));
        var reply = JsonSerializer.Deserialize<AutomationReply>(line ?? throw new InvalidOperationException("UIA client exited before its reply."))!;
        var end = Stopwatch.GetTimestamp();
        _report.Event("automation-query-completed", new { frameId, phase, reply, start, end });
        NativeFixture.Require(reply.Error is null && reply.Id == request.Id && reply.ClientPid == _client.Id && reply.ClientPid != Environment.ProcessId
            && reply.HostPid == request.HostPid && reply.GridId == gridId && reply.Retired == request.Retired,
            "Invalid external UIA reply: " + reply.Error);
        NativeFixture.Require(reply.Workload == _workload && reply.SubscriptionGridId == (fixture is null || _workload == "query" ? null : gridId), "External UIA subscription/workload belongs to the wrong fixture lifetime.");
        if (_report.Options.AutomationClientBackend == "explicit-com")
            NativeFixture.Require(reply.Ownership is { Version: 1, LiveQueryReferences: 0, ClientLifetimeReferences: 3, FinalizerReleases: 0, HandlerObjects: 2 } ownership
                && ownership.Acquired == ownership.Released && ownership.Acquired > 0, "External UIA query did not release its native references.");
        NativeFixture.Require(reply.GridFound == (fixture is not null) && reply.Hash == AutomationProtocol.Hash(expected)
            && reply.Hash == AutomationProtocol.Hash(reply.Items), "External UIA tree differs from current row/column/value/selection identities.");
        NativeFixture.Require(start <= reply.StartTicks && reply.StartTicks <= reply.EndTicks && reply.EndTicks <= end, "UIA query clocks are inconsistent.");
    }

    private static AutomationItem[] Expected(NativeFixture fixture)
    {
        var grid = fixture.Grid; var result = new List<AutomationItem>();
        foreach (var row in grid.RowsPresenter!.RealizedRows)
        {
            var node = row.Row!.Id; var selected = grid.State!.SelectedNodes.Contains(node);
            result.Add(new($"TreeDataGrid.Row.{node.Value}", $"Row {row.RowIndex + 1}", null,
                grid.SelectionUnit == NT.AvaloniaControl.TreeDataGrid.TreeDataGridSelectionUnit.Row ? selected : null));
            foreach (var cell in row.RealizedCells)
            {
                var column = cell.Column!;
                var address = new NT.AvaloniaControl.TreeDataGrid.CellAddress(node, column.Id);
                var value = grid.EditSession is { } edit && edit.Cell == address ? edit.Value?.ToString() ?? "" : cell.Value?.ToString() ?? "";
                result.Add(new($"TreeDataGrid.Cell.{node.Value}.{column.Id.Value}", $"{column.Header}: {value}", value,
                    grid.SelectionUnit == NT.AvaloniaControl.TreeDataGrid.TreeDataGridSelectionUnit.Row ? selected : grid.State.IsCellSelected(address)));
            }
        }
        if (grid.ShowColumnHeaders)
            foreach (var header in grid.GetVisualDescendants().OfType<NT.AvaloniaControl.TreeDataGrid.TreeDataGridColumnHeader>())
                if (header.Column is { } column) result.Add(new($"TreeDataGrid.Column.{column.Id.Value}", column.Header?.ToString() ?? "", null, null));
        return result.OrderBy(i => i.Id, StringComparer.Ordinal).ToArray();
    }

    public async ValueTask DisposeAsync()
    {
        _client.StandardInput.Close();
        try
        {
            await _client.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
            var shutdown = await _client.StandardOutput.ReadLineAsync();
            _report.Event("automation-client-exited", new { clientPid = _client.Id, exitCode = _client.ExitCode, stderr = await _stderr, queries = _sequence, shutdown });
            NativeFixture.Require(_client.ExitCode == 0 && shutdown is not null, "External UIA client did not drain/exit cleanly.");
        }
        finally
        {
            if (!_client.HasExited) { _client.Kill(true); await _client.WaitForExitAsync(); }
            _client.Dispose();
        }
    }
}
