using System.ComponentModel;
using System.Security.Cryptography;
using System.Text.Json;
using NT.AvaloniaControl.PdfViewer;
using NT.AvaloniaControl.PdfViewer.Editing;
using NT.AvaloniaControl.PdfViewer.Saving;

namespace PdfViewer.P403;

internal static partial class Program
{
    private static async Task<int> RunStorageProbeAsync(string[] args)
    {
        var directory = Path.GetFullPath(args[1]); var control = Path.GetFullPath(args[2]);
        var stage = args.Length > 3 ? args[3] : "none";
        var target = Path.Combine(directory, args.Length > 4 ? args[4] : "original.pdf");
        Directory.CreateDirectory(control);
        var report = new Dictionary<string, object?> { ["Target"] = target, ["Control"] = control, ["PauseStage"] = stage,
            ["ProcessId"] = Environment.ProcessId, ["FunctionalAssemblySHA256"] = Hash(typeof(PdfEditSession).Assembly.Location), ["HostAssemblySHA256"] = Hash(typeof(Program).Assembly.Location) };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(180));
        var token = timeout.Token;
        try
        {
            var source = await File.ReadAllBytesAsync(Path.Combine(directory, "original.pdf"), token);
            var expected = await PdfFileSnapshot.CaptureAsync(target, token);
            await using var session = await PdfEditSession.OpenAsync(source, token);
            var font = new PdfEmbeddedTrueTypeFont(await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "P0Font.ttf"), token));
            await session.AddFreeTextAsync(0, new(40, 150, 280, 60), "B", font, 32, token);
            await session.SetUserBookmarksAsync([new(Guid.NewGuid(), "Durable bookmark", 0, PdfUserBookmarkFlags.Bold)], token);
            var output = await session.ExportAsync(token);
            await File.WriteAllBytesAsync(Path.Combine(control, "expected-output.pdf"), output, token);
            report["InputSHA256"] = Convert.ToHexString(SHA256.HashData(source));
            report["ExpectedOutputSHA256"] = Convert.ToHexString(SHA256.HashData(output));
            report["ExpectedTarget"] = expected;
            PdfSaveFaultInjection.Current.Value = async (probe, ct) =>
            {
                if (probe.Stage.ToString() != stage) return;
                await WriteControlAsync("stage.json", new { Stage = probe.Stage.ToString(), probe.TargetPath, probe.TransactionDirectory, ProcessId = Environment.ProcessId }, ct);
                while (!File.Exists(Path.Combine(control, "release"))) await Task.Delay(15, ct);
            };
            try
            {
                report["Outcome"] = await session.SaveAsync(target, new() { ExpectedTarget = expected }, token);
                report["CommitSucceeded"] = true;
            }
            catch (Exception error)
            {
                report["CommitSucceeded"] = false; report["ErrorType"] = error.GetType().FullName;
                report["HResult"] = error.HResult; report["Error"] = error.Message;
                report["NativeError"] = (error.InnerException as Win32Exception ?? error.InnerException?.InnerException as Win32Exception)?.NativeErrorCode;
                if (error is PdfSaveConflictException conflict)
                {
                    report["WasPublished"] = conflict.WasPublished; report["RecoveryDirectory"] = conflict.RecoveryDirectory;
                    report["PreservedExternalPath"] = conflict.PreservedExternalPath; report["OutputSnapshotPath"] = conflict.OutputSnapshotPath;
                }
                if (error is PdfSaveDurabilityException uncertain) report["RecoveryDirectory"] = uncertain.RecoveryDirectory;
            }
            report["Dirty"] = session.IsDirty;
            // Control data is outside the tested volume / share. Reconnection happens before
            // any audit of the unavailable path, so failures are observable without another I/O hang.
            await WriteControlAsync("operation.json", report, CancellationToken.None);
            if (File.Exists(Path.Combine(control, "wait-audit")))
                while (!File.Exists(Path.Combine(control, "audit-release"))) await Task.Delay(15, token);
            report["TargetSnapshot"] = await PdfFileSnapshot.CaptureAsync(target, token);
            report["Recoveries"] = await PdfSaveRecovery.ScanAsync(directory, token);
            report["Passed"] = true;
        }
        catch (Exception error) { report["Passed"] = false; report["Failure"] = error.ToString(); }
        finally { PdfSaveFaultInjection.Current.Value = null; await WriteControlAsync("storage-result.json", report, CancellationToken.None); }
        return report["Passed"] is true ? 0 : 1;

        async Task WriteControlAsync(string name, object value, CancellationToken ct)
        {
            await using var file = new FileStream(Path.Combine(control, name), FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough);
            await JsonSerializer.SerializeAsync(file, value, Json, ct); await file.FlushAsync(ct); file.Flush(true);
        }
    }
}
