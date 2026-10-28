using NT.AvaloniaControl.PdfViewer;
using NT.AvaloniaControl.PdfViewer.Editing;
using NT.AvaloniaControl.PdfViewer.Saving;
using PdfViewer.Tests;
using System.Security.AccessControl;
using System.Security.Principal;

namespace PdfViewer.Core.Tests;

public sealed class P403FileSaveTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static string CreateDirectory()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "p403-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory); return directory;
    }
    private static async Task<PdfEditSession> Edit(byte[] source)
    {
        var session = await PdfEditSession.OpenAsync(source, Token);
        await session.SetUserBookmarksAsync([new(Guid.NewGuid(), "Unsaved bookmark", 0, PdfUserBookmarkFlags.Bold)], Token);
        return session;
    }

    [Theory]
    [InlineData("same-length")] [InlineData("replace")] [InlineData("delete")]
    public async Task Expected_content_rejects_external_changes_even_with_identical_length_and_mtime(string mutation)
    {
        var directory = CreateDirectory(); var path = Path.Combine(directory, "original.pdf");
        try
        {
            var source = PdfFixture.SinglePage("AAAA"); var external = PdfFixture.SinglePage("BBBB");
            await File.WriteAllBytesAsync(path, source, Token);
            var expected = await PdfFileSnapshot.CaptureAsync(path, Token);
            var time = File.GetLastWriteTimeUtc(path);
            await using var session = await Edit(source);
            if (mutation == "delete") File.Delete(path);
            else if (mutation == "replace") { var other = Path.Combine(directory, "external.pdf"); await File.WriteAllBytesAsync(other, external, Token); File.Move(other, path, true); }
            else { Assert.Equal(source.Length, external.Length); await File.WriteAllBytesAsync(path, external, Token); File.SetLastWriteTimeUtc(path, time); }
            var error = await Assert.ThrowsAsync<PdfSaveConflictException>(() => session.SaveAsync(path, new() { ExpectedTarget = expected }, Token));
            Assert.Equal(PdfErrorKind.SaveConflict, PdfError.FromException(error).Kind);
            Assert.Equal(path, error.TargetPath); Assert.Equal(expected, error.Expected); Assert.True(session.IsDirty);
            if (mutation == "delete") Assert.False(File.Exists(path)); else Assert.Equal(external, await File.ReadAllBytesAsync(path, Token));
            Assert.Empty(Directory.GetDirectories(directory, ".nt-pdf-txn-*"));
            var copy = Path.Combine(directory, "copy.pdf");
            var saved = await session.SaveAsync(copy, new(), Token);
            Assert.False(session.IsDirty); Assert.Equal(await PdfFileSnapshot.CaptureAsync(copy, Token), saved.Output);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData("write")] [InlineData("flush")] [InlineData("mount")]
    [InlineData("corrupt")] [InlineData("cancel")] [InlineData("late-change")]
    public async Task Precommit_faults_preserve_original_and_dirty_and_clean_owned_transaction(string fault)
    {
        var directory = CreateDirectory(); var path = Path.Combine(directory, "original.pdf");
        try
        {
            var source = PdfFixture.SinglePage("AAAA"); var external = PdfFixture.SinglePage("BBBB");
            await File.WriteAllBytesAsync(path, source, Token);
            await using var session = await Edit(source);
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
            PdfSaveFaultInjection.Current.Value = async (probe, _) =>
            {
                if (fault == "write" && probe.Stage == PdfFileSaveStage.WriteChunk) throw new IOException("Injected ENOSPC", unchecked((int)0x80070070));
                if (fault == "flush" && probe.Stage == PdfFileSaveStage.BeforeFlush) throw new IOException("Injected flush failure");
                if (fault == "mount" && probe.Stage == PdfFileSaveStage.BeforeReplace) throw new IOException("Injected unavailable mount", unchecked((int)0x80070035));
                if (fault == "corrupt" && probe.Stage == PdfFileSaveStage.FileFlushed) await File.WriteAllBytesAsync(probe.CandidatePath, "broken"u8.ToArray(), Token);
                if (fault == "cancel" && probe.Stage == PdfFileSaveStage.BeforeReplace) cancellation.Cancel();
                if (fault == "late-change" && probe.Stage == PdfFileSaveStage.BeforeReplace) await File.WriteAllBytesAsync(path, external, Token);
            };
            if (fault == "cancel") await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.SaveAsync(path, cancellation.Token));
            else if (fault == "corrupt") await Assert.ThrowsAsync<InvalidDataException>(() => session.SaveAsync(path, Token));
            else await Assert.ThrowsAnyAsync<IOException>(() => session.SaveAsync(path, Token));
            Assert.True(session.IsDirty);
            Assert.Equal(fault == "late-change" ? external : source, await File.ReadAllBytesAsync(path, Token));
            Assert.Empty(Directory.GetDirectories(directory, ".nt-pdf-txn-*"));
            PdfSaveFaultInjection.Current.Value = null;
            await session.SaveAsync(path, Token); Assert.False(session.IsDirty);
        }
        finally { PdfSaveFaultInjection.Current.Value = null; Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task Postcommit_cleanup_failure_is_saved_and_recovery_is_idempotent()
    {
        var directory = CreateDirectory(); var path = Path.Combine(directory, "original.pdf");
        try
        {
            var source = PdfFixture.SinglePage(); await File.WriteAllBytesAsync(path, source, Token);
            await using var session = await Edit(source);
            PdfSaveFaultInjection.Current.Value = (probe, _) => probe.Stage == PdfFileSaveStage.CleanupStarting ? Task.FromException(new IOException("Injected cleanup failure")) : Task.CompletedTask;
            var result = await session.SaveAsync(path, new(), Token);
            Assert.False(session.IsDirty); Assert.NotNull(result.CleanupWarning); Assert.NotNull(result.RecoveryDirectory);
            var committed = await File.ReadAllBytesAsync(path, Token);
            var entry = Assert.Single(await PdfSaveRecovery.ScanAsync(directory, Token));
            Assert.Equal(PdfSaveRecoveryStatus.AlreadyCommitted, entry.Status);
            var recovered = await PdfSaveRecovery.RecoverAsync(entry.TransactionDirectory, Token);
            Assert.Null(recovered.RecoveryDirectory); Assert.Equal(committed, await File.ReadAllBytesAsync(path, Token));
            Assert.Empty(await PdfSaveRecovery.ScanAsync(directory, Token));
        }
        finally { PdfSaveFaultInjection.Current.Value = null; Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task Live_writer_is_active_and_second_writer_cannot_commit_through_same_lock()
    {
        var directory = CreateDirectory(); var path = Path.Combine(directory, "original.pdf");
        try
        {
            var source = PdfFixture.SinglePage(); await File.WriteAllBytesAsync(path, source, Token);
            await using var first = await Edit(source); await using var second = await Edit(source);
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            PdfSaveFaultInjection.Current.Value = async (probe, token) => { if (probe.Stage == PdfFileSaveStage.JournalFlushed) { entered.SetResult(); await release.Task.WaitAsync(token); } };
            var save = first.SaveAsync(path, Token);
            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), Token);
                PdfSaveFaultInjection.Current.Value = null;
                Assert.Equal(PdfSaveRecoveryStatus.Active, Assert.Single(await PdfSaveRecovery.ScanAsync(directory, Token)).Status);
                await Assert.ThrowsAnyAsync<IOException>(() => second.SaveAsync(path, Token)); Assert.True(second.IsDirty);
            }
            finally { release.TrySetResult(); await save; }
        }
        finally { PdfSaveFaultInjection.Current.Value = null; Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task Staging_is_private_and_unix_replacement_preserves_existing_mode()
    {
        var directory = CreateDirectory(); var path = Path.Combine(directory, "private.pdf");
        try
        {
            var source = PdfFixture.SinglePage(); await File.WriteAllBytesAsync(path, source, Token);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            await using var session = await Edit(source);
            var inspected = false;
            PdfSaveFaultInjection.Current.Value = (probe, _) =>
            {
                if (probe.Stage == PdfFileSaveStage.FileFlushed)
                {
                    if (OperatingSystem.IsWindows())
                    {
                        using var identity = WindowsIdentity.GetCurrent();
                        var security = new DirectoryInfo(probe.TransactionDirectory).GetAccessControl();
                        Assert.True(security.AreAccessRulesProtected);
                        foreach (FileSystemAccessRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
                            Assert.Equal(identity.User, rule.IdentityReference);
                    }
                    else
                    {
                        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(probe.TransactionDirectory));
                        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(probe.CandidatePath));
                    }
                    inspected = true;
                }
                return Task.CompletedTask;
            };
            await session.SaveAsync(path, Token); Assert.True(inspected);
            if (!OperatingSystem.IsWindows()) Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
        }
        finally { PdfSaveFaultInjection.Current.Value = null; Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task Forged_or_unknown_recovery_paths_cannot_modify_unrelated_files()
    {
        var directory = CreateDirectory(); var path = Path.Combine(directory, "protected.pdf");
        try
        {
            var source = PdfFixture.SinglePage(); await File.WriteAllBytesAsync(path, source, Token);
            var transaction = Path.Combine(directory, ".nt-pdf-txn-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(transaction);
            await File.WriteAllTextAsync(Path.Combine(transaction, "owner.lock"), "", Token);
            await File.WriteAllTextAsync(Path.Combine(transaction, "transaction.json"), "{\"Version\":1,\"TargetFileName\":\"../protected.pdf\"}", Token);
            Assert.Equal(PdfSaveRecoveryStatus.Invalid, Assert.Single(await PdfSaveRecovery.ScanAsync(directory, Token)).Status);
            await Assert.ThrowsAsync<InvalidDataException>(() => PdfSaveRecovery.RecoverAsync(transaction, Token));
            await File.WriteAllTextAsync(Path.Combine(transaction, "foreign.txt"), "Do not delete", Token);
            await Assert.ThrowsAsync<InvalidDataException>(() => PdfSaveRecovery.DiscardAsync(transaction, Token));
            Assert.True(File.Exists(Path.Combine(transaction, "foreign.txt"))); Assert.Equal(source, await File.ReadAllBytesAsync(path, Token));
        }
        finally { Directory.Delete(directory, true); }
    }
}
