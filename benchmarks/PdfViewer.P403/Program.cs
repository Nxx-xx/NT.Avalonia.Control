using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using NT.AvaloniaControl.PdfViewer;
using NT.AvaloniaControl.PdfViewer.Editing;
using NT.AvaloniaControl.PdfViewer.Saving;
using PdfViewer.Tests;

namespace PdfViewer.P403;

internal static partial class Program
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    internal static async Task<int> Main(string[] args)
    {
        if (args[0] == "storage") return await RunStorageProbeAsync(args);
        var mode = args[0]; var directory = Path.GetFullPath(args[1]); var scenario = args.Length > 2 ? args[2] : "";
        Directory.CreateDirectory(directory);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var token = timeout.Token;
        var report = new Dictionary<string, object?> { ["Mode"] = mode, ["Scenario"] = scenario,
            ["ProcessId"] = Environment.ProcessId, ["OS"] = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            ["FunctionalAssemblySHA256"] = Hash(typeof(PdfEditSession).Assembly.Location), ["HostAssemblySHA256"] = Hash(typeof(Program).Assembly.Location) };
        try
        {
            var target = Path.Combine(directory, "original.pdf");
            if (mode == "fixture")
            {
                var text = "AAAA";
                if (scenario is "crash-WriteChunk" or "storage-large")
                {
                    var random = new byte[131072]; new Random(42).NextBytes(random);
                    text += Convert.ToHexString(random);
                }
                await File.WriteAllBytesAsync(target, PdfFixture.SinglePage(text, 400, 300), token);
                await File.WriteAllBytesAsync(Path.Combine(directory, "external.pdf"), PdfFixture.SinglePage("BBBB", 400, 300), token);
                report["Passed"] = true;
            }
            else if (mode == "save")
            {
                var source = await File.ReadAllBytesAsync(target, token);
                var expected = await PdfFileSnapshot.CaptureAsync(target, token);
                await using var session = await PdfEditSession.OpenAsync(source, token);
                var font = new PdfEmbeddedTrueTypeFont(await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "P0Font.ttf"), token));
                await session.AddFreeTextAsync(0, new(40, 150, 280, 60), "B", font, 32, token);
                await session.SetUserBookmarksAsync([new(Guid.NewGuid(), "Recovered bookmark", 0, PdfUserBookmarkFlags.Bold)], token);
                var edited = await session.ExportAsync(token);
                await File.WriteAllBytesAsync(Path.Combine(directory, "expected-output.pdf"), edited, token);
                var stamp = File.GetLastWriteTimeUtc(target);
                PdfSaveFaultInjection.Current.Value = async (probe, ct) =>
                {
                    if (scenario.StartsWith("crash-", StringComparison.Ordinal) && probe.Stage.ToString() == scenario[6..])
                    {
                        await using (var signal = new FileStream(Path.Combine(directory, "stage.json"), FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                        {
                            await JsonSerializer.SerializeAsync(signal, new { probe.Stage, probe.TransactionDirectory, probe.CandidatePath, ProcessId = Environment.ProcessId }, Json, ct);
                            await signal.FlushAsync(ct); signal.Flush(true);
                        }
                        await Task.Delay(Timeout.Infinite, ct); // The supervisor kills only this owned process.
                    }
                    if (scenario == "disk-full" && probe.Stage == PdfFileSaveStage.WriteChunk) throw new IOException("Injected disk full", unchecked((int)0x80070070));
                    if (scenario == "flush-failure" && probe.Stage == PdfFileSaveStage.BeforeFlush) throw new IOException("Injected flush failure");
                    if (scenario == "mount-unavailable" && probe.Stage == PdfFileSaveStage.BeforeReplace) throw new IOException("Injected unavailable mount", unchecked((int)0x80070035));
                    if (scenario == "cancel" && probe.Stage == PdfFileSaveStage.BeforeReplace) timeout.Cancel();
                    if (scenario == "corrupt-candidate" && probe.Stage == PdfFileSaveStage.FileFlushed) await File.WriteAllBytesAsync(probe.CandidatePath, "bad"u8.ToArray(), ct);
                    if (scenario == "late-edit" && probe.Stage == PdfFileSaveStage.BeforeReplace)
                    { await File.WriteAllBytesAsync(target, await File.ReadAllBytesAsync(Path.Combine(directory, "external.pdf"), ct), ct); File.SetLastWriteTimeUtc(target, stamp); }
                    if (scenario == "external-replace" && probe.Stage == PdfFileSaveStage.BeforeReplace) File.Move(Path.Combine(directory, "external.pdf"), target, true);
                    if (scenario == "external-delete" && probe.Stage == PdfFileSaveStage.BeforeReplace) File.Delete(target);
                    if (scenario == "postcommit-failure" && probe.Stage == PdfFileSaveStage.CleanupStarting) throw new IOException("Injected postcommit cleanup failure");
                };
                IDisposable? guard = null;
                try
                {
                    if (scenario == "target-lock") guard = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.None);
                    if (scenario == "readonly-target") File.SetAttributes(target, File.GetAttributes(target) | FileAttributes.ReadOnly);
                    if (scenario == "readonly-parent" && !OperatingSystem.IsWindows()) File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserExecute);
                    var outcome = await session.SaveAsync(target, new() { ExpectedTarget = expected }, token);
                    report["Outcome"] = outcome;
                    report["CommitSucceeded"] = true;
                }
                catch (Exception error)
                {
                    report["CommitSucceeded"] = false; report["ErrorType"] = error.GetType().FullName;
                    report["HResult"] = error.HResult; report["Error"] = error.Message;
                }
                finally
                {
                    guard?.Dispose();
                    if (scenario == "readonly-parent" && !OperatingSystem.IsWindows()) File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                    if (scenario == "readonly-target") File.SetAttributes(target, FileAttributes.Normal);
                }
                report["Dirty"] = session.IsDirty;
                report["OriginalSHA256"] = Convert.ToHexString(SHA256.HashData(source));
                report["ExpectedOutputSHA256"] = Convert.ToHexString(SHA256.HashData(edited));
                report["TargetSHA256"] = File.Exists(target) ? Hash(target) : null;
                report["Recoveries"] = await PdfSaveRecovery.ScanAsync(directory, CancellationToken.None);
                report["Passed"] = true;
            }
            else if (mode == "scan")
            {
                report["Entries"] = await PdfSaveRecovery.ScanAsync(directory, token); report["Passed"] = true;
            }
            else if (mode == "recover")
            {
                var entries = await PdfSaveRecovery.ScanAsync(directory, token);
                report["Before"] = entries;
                foreach (var entry in entries)
                    if (entry.Status is PdfSaveRecoveryStatus.Prepared or PdfSaveRecoveryStatus.AlreadyCommitted or PdfSaveRecoveryStatus.DurabilityPending)
                        report["Result"] = await PdfSaveRecovery.RecoverAsync(entry.TransactionDirectory, token);
                report["After"] = await PdfSaveRecovery.ScanAsync(directory, token);
                report["TargetSHA256"] = File.Exists(target) ? Hash(target) : null;
                report["Passed"] = true;
            }
            else if (mode == "discard")
            {
                foreach (var entry in await PdfSaveRecovery.ScanAsync(directory, token)) await PdfSaveRecovery.DiscardAsync(entry.TransactionDirectory, token);
                report["After"] = await PdfSaveRecovery.ScanAsync(directory, token); report["Passed"] = true;
            }
            else if (mode == "attempt-recover")
            {
                var entry = (await PdfSaveRecovery.ScanAsync(directory, token)).Single();
                try { report["Result"] = await PdfSaveRecovery.RecoverAsync(entry.TransactionDirectory, token); report["Recovered"] = true; }
                catch (Exception error) { report["Recovered"] = false; report["ErrorType"] = error.GetType().FullName; }
                report["After"] = await PdfSaveRecovery.ScanAsync(directory, token); report["Passed"] = true;
            }
            else throw new ArgumentException("Unknown probe mode.");
        }
        catch (Exception error) { report["Passed"] = false; report["Failure"] = error.ToString(); }
        finally
        {
            PdfSaveFaultInjection.Current.Value = null;
            await File.WriteAllTextAsync(Path.Combine(directory, mode + "-result.json"), JsonSerializer.Serialize(report, Json));
        }
        return report["Passed"] is true ? 0 : 1;
    }
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
