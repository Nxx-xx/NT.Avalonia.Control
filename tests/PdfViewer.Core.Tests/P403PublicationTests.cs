using NT.AvaloniaControl.PdfViewer;
using NT.AvaloniaControl.PdfViewer.Editing;
using NT.AvaloniaControl.PdfViewer.Saving;
using PdfViewer.Tests;

namespace PdfViewer.Core.Tests;

public sealed class P403PublicationTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("rewrite")] [InlineData("replace")] [InlineData("after-publication")]
    public async Task Last_window_changes_preserve_both_versions_and_require_explicit_resolution(string mutation)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "publication-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "original.pdf");
            var source = PdfFixture.SinglePage("AAAA"); var external = PdfFixture.SinglePage("BBBB");
            await File.WriteAllBytesAsync(path, source, Token);
            await using var session = await PdfEditSession.OpenAsync(source, Token);
            await session.SetUserBookmarksAsync([new(Guid.NewGuid(), "Saved output", 0, PdfUserBookmarkFlags.Bold)], Token);
            var output = await session.ExportAsync(Token);
            PdfSaveFaultInjection.Current.Value = async (probe, ct) =>
            {
                if (probe.Stage != (mutation == "after-publication" ? PdfFileSaveStage.Replaced : PdfFileSaveStage.CommitPrepared)) return;
                if (mutation == "replace")
                {
                    var temporary = Path.Combine(directory, "external.tmp");
                    await File.WriteAllBytesAsync(temporary, external, ct); File.Move(temporary, path, true);
                }
                else await File.WriteAllBytesAsync(path, external, ct);
            };
            var error = await Assert.ThrowsAsync<PdfSaveConflictException>(() => session.SaveAsync(path, Token));
            Assert.True(error.WasPublished); Assert.True(session.IsDirty);
            Assert.Equal(output, await PdfSaveRecovery.ReadVersionAsync(error.RecoveryDirectory!, PdfSaveRecoveryVersion.SavedOutput, Token));
            Assert.Equal(mutation == "after-publication" ? source : external,
                await PdfSaveRecovery.ReadVersionAsync(error.RecoveryDirectory!, PdfSaveRecoveryVersion.DisplacedExternal, Token));
            Assert.Equal(mutation == "after-publication" ? external : output, await File.ReadAllBytesAsync(path, Token));
            Assert.Equal(PdfSaveRecoveryStatus.PublishedConflict, Assert.Single(await PdfSaveRecovery.ScanAsync(directory, Token)).Status);
            PdfSaveFaultInjection.Current.Value = null;
            await Assert.ThrowsAsync<PdfSaveConflictException>(() => PdfSaveRecovery.RecoverAsync(error.RecoveryDirectory!, Token));
            await Assert.ThrowsAsync<InvalidOperationException>(() => PdfSaveRecovery.DiscardAsync(error.RecoveryDirectory!, Token));
            await session.SaveAsync(Path.Combine(directory, "resolved.pdf"), Token); Assert.False(session.IsDirty);
        }
        finally { PdfSaveFaultInjection.Current.Value = null; Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task Last_window_new_target_creation_is_not_overwritten()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "publication-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "new.pdf"); var external = PdfFixture.SinglePage("BBBB");
            await using var session = await PdfEditSession.OpenAsync(PdfFixture.SinglePage("AAAA"), Token);
            await session.SetUserBookmarksAsync([new(Guid.NewGuid(), "Output", 0, PdfUserBookmarkFlags.None)], Token);
            PdfSaveFaultInjection.Current.Value = async (probe, ct) => { if (probe.Stage == PdfFileSaveStage.CommitPrepared) await File.WriteAllBytesAsync(path, external, ct); };
            var conflict = await Assert.ThrowsAsync<PdfSaveConflictException>(() => session.SaveAsync(path, Token));
            Assert.False(conflict.WasPublished); Assert.True(session.IsDirty); Assert.Equal(external, await File.ReadAllBytesAsync(path, Token));
        }
        finally { PdfSaveFaultInjection.Current.Value = null; Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Publication_without_durable_ack_is_dirty_and_reconciles_after_restart(bool partialRecord)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "publication-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "original.pdf"); var source = PdfFixture.SinglePage(); await File.WriteAllBytesAsync(path, source, Token);
            await using var session = await PdfEditSession.OpenAsync(source, Token);
            await session.SetUserBookmarksAsync([new(Guid.NewGuid(), "Output", 0, PdfUserBookmarkFlags.None)], Token);
            PdfSaveFaultInjection.Current.Value = (probe, _) => probe.Stage == PdfFileSaveStage.Replaced ? Task.FromException(new IOException("No durable acknowledgement")) : Task.CompletedTask;
            var error = await Assert.ThrowsAsync<PdfSaveDurabilityException>(() => session.SaveAsync(path, Token));
            Assert.Equal(PdfErrorKind.SaveDurabilityUncertain, PdfError.FromException(error).Kind); Assert.True(session.IsDirty);
            if (partialRecord) await File.WriteAllTextAsync(Path.Combine(error.RecoveryDirectory, "commit.json"), "{", Token);
            Assert.Equal(PdfSaveRecoveryStatus.DurabilityPending, Assert.Single(await PdfSaveRecovery.ScanAsync(directory, Token)).Status);
            PdfSaveFaultInjection.Current.Value = null;
            var recovered = await PdfSaveRecovery.RecoverAsync(error.RecoveryDirectory, Token);
            Assert.NotEqual(PdfSaveDurability.Unknown, recovered.Durability); Assert.Empty(await PdfSaveRecovery.ScanAsync(directory, Token));
        }
        finally { PdfSaveFaultInjection.Current.Value = null; Directory.Delete(directory, true); }
    }
}
