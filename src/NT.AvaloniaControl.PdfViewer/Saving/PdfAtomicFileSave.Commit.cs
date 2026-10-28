using System.Text.Json;

namespace NT.AvaloniaControl.PdfViewer.Saving;

internal static partial class PdfAtomicFileSave
{
    private sealed record CommitRecord(int Version, PdfFileSnapshot Output);

    private static Task CopyOutputCandidateAsync(string transaction, CancellationToken token) =>
        CopyPrivateFileAsync(Path.Combine(transaction, OutputName), Path.Combine(transaction, CandidateName), token);

    private static async Task CopyPrivateFileAsync(string source, string destination, CancellationToken token)
    {
        await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
        await using var output = CreatePrivateFile(destination, 65536);
        await input.CopyToAsync(output, token).ConfigureAwait(false);
        await output.FlushAsync(token).ConfigureAwait(false);
        output.Flush(true);
    }

    private static async Task<PdfSaveDurability> PublishPreparedAsync(string transaction, string target, Journal journal,
        CancellationToken token, Action validateOwner, Action publicationAttempted)
    {
        EnsureNoLinks(target);
        var candidate = Path.Combine(transaction, CandidateName);
        if (!OperatingSystem.IsWindows() && journal.ExpectedTarget.Exists)
        {
            try { File.SetUnixFileMode(candidate, File.GetUnixFileMode(target)); }
            catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
            { throw new PdfSaveConflictException(target, journal.ExpectedTarget, await CaptureAsync(target, token).ConfigureAwait(false)); }
            PdfStoragePrimitives.FlushFile(candidate);
        }
        await CheckExpectedAsync(target, journal.ExpectedTarget, token).ConfigureAwait(false);
        await PdfSaveFaultInjection.HitAsync(PdfFileSaveStage.CommitPrepared, target, transaction, token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        validateOwner();
        publicationAttempted();
        try { PdfStoragePrimitives.Publish(candidate, target, Path.Combine(transaction, DisplacedName), journal.ExpectedTarget.Exists); }
        catch (PdfPublicationCollisionException)
        { throw new PdfSaveConflictException(target, journal.ExpectedTarget, await CaptureAsync(target, token).ConfigureAwait(false)); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { throw new PdfSaveDurabilityException(target, transaction, error); }

        // From this point on cancellation cannot justify deleting any preserved version.
        try
        {
            await PdfSaveFaultInjection.HitAsync(PdfFileSaveStage.Replaced, target, transaction, CancellationToken.None).ConfigureAwait(false);
            var displaced = FindDisplaced(transaction, journal);
            var old = displaced is null ? PdfFileSnapshot.Missing : await CaptureAsync(displaced, CancellationToken.None).ConfigureAwait(false);
            var actual = await CaptureAsync(target, CancellationToken.None).ConfigureAwait(false);
            if ((journal.ExpectedTarget.Exists && !journal.ExpectedTarget.Matches(old)) || !journal.Output.Matches(actual))
                throw new PdfSaveConflictException(target, journal.ExpectedTarget, displaced is null ? actual : old,
                    transaction, displaced, Path.Combine(transaction, OutputName));
            return await ConfirmDurabilityAsync(transaction, target, journal).ConfigureAwait(false);
        }
        catch (PdfSaveConflictException) { throw; }
        catch (PdfSaveDurabilityException) { throw; }
        catch (Exception error) { throw new PdfSaveDurabilityException(target, transaction, error); }
    }

    private static async Task<PdfSaveDurability> ConfirmDurabilityAsync(string transaction, string target, Journal journal)
    {
        try
        {
            PdfStoragePrimitives.FlushFile(target);
            PdfStoragePrimitives.FlushDirectory(transaction);
            PdfStoragePrimitives.FlushDirectory(Path.GetDirectoryName(target)!);
            var commit = Path.Combine(transaction, CommitName);
            if (!await HasCommitRecordAsync(transaction, journal, CancellationToken.None).ConfigureAwait(false))
            {
                // ENOSPC or a power cut can leave a partial acknowledgement. All output
                // and displaced hashes were checked before reaching this repair step.
                File.Delete(commit);
                await using var file = CreatePrivateFile(commit, 4096);
                await JsonSerializer.SerializeAsync(file, new CommitRecord(2, journal.Output), Json).ConfigureAwait(false);
                await file.FlushAsync().ConfigureAwait(false); file.Flush(true);
            }
            PdfStoragePrimitives.FlushDirectory(transaction);
            PdfStoragePrimitives.FlushDirectory(Path.GetDirectoryName(target)!);
            await PdfSaveFaultInjection.HitAsync(PdfFileSaveStage.CommitFlushed, target, transaction, CancellationToken.None).ConfigureAwait(false);
            return OperatingSystem.IsWindows() ? PdfSaveDurability.PostRenameFileAndCommitRecordFlushed : PdfSaveDurability.FileAndDirectoryEntriesFlushed;
        }
        catch (PdfSaveDurabilityException) { throw; }
        catch (Exception error) { throw new PdfSaveDurabilityException(target, transaction, error); }
    }

    private static string? FindDisplaced(string transaction, Journal journal)
    {
        var displaced = Path.Combine(transaction, DisplacedName);
        if (File.Exists(displaced)) return displaced;
        // An abrupt stop can occur between Linux EXCHANGE and naming the displaced entry.
        var candidate = Path.Combine(transaction, CandidateName);
        if (journal.ExpectedTarget.Exists && File.Exists(candidate) && File.Exists(Path.Combine(transaction, OutputName))) return candidate;
        return null;
    }

    private static async Task<bool> HasCommitRecordAsync(string transaction, Journal journal, CancellationToken token)
    {
        var path = Path.Combine(transaction, CommitName);
        if (!File.Exists(path)) return false;
        if (new FileInfo(path).Length > 2048) return false;
        await using var file = File.OpenRead(path);
        try
        {
            var record = await JsonSerializer.DeserializeAsync<CommitRecord>(file, Json, token).ConfigureAwait(false);
            return record is { Version: 2 } && record.Output is not null && journal.Output.Matches(record.Output);
        }
        catch (JsonException) { return false; }
    }

    private static async Task<PdfSaveRecoveryEntry> InspectTransactionAsync(string transaction, Journal journal, CancellationToken token)
    {
        var target = Target(transaction, journal);
        var actual = await CaptureAsync(target, token).ConfigureAwait(false);
        var output = Path.Combine(transaction, OutputName);
        var displaced = FindDisplaced(transaction, journal);
        if (journal.Output.Matches(actual))
        {
            await ValidateOutputFileAsync(target, journal, token).ConfigureAwait(false);
            if (displaced is not null)
            {
                var old = await CaptureAsync(displaced, token).ConfigureAwait(false);
                if (!journal.ExpectedTarget.Matches(old)) return Entry(PdfSaveRecoveryStatus.PublishedConflict, "The version displaced during publication differs from the expected target.");
            }
            if (File.Exists(output)) await ValidateOutputFileAsync(output, journal, token).ConfigureAwait(false);
            var confirmed = await HasCommitRecordAsync(transaction, journal, token).ConfigureAwait(false);
            return Entry(confirmed || journal.Version == 1 ? PdfSaveRecoveryStatus.AlreadyCommitted : PdfSaveRecoveryStatus.DurabilityPending, null);
        }
        // An existing displaced slot is evidence that publication was attempted, including
        // uncertain network responses. Do not replay over a newer external target.
        if (File.Exists(Path.Combine(transaction, DisplacedName))) return Entry(PdfSaveRecoveryStatus.PublishedConflict, "A displaced version is retained and the current target differs from the intended output.");
        if (!journal.ExpectedTarget.Matches(actual)) return Entry(PdfSaveRecoveryStatus.Conflict, "The target no longer matches the pre-save content.");
        var candidate = await CaptureAsync(Path.Combine(transaction, CandidateName), token).ConfigureAwait(false);
        var snapshot = journal.Version == 1 ? journal.Output : await CaptureAsync(output, token).ConfigureAwait(false);
        if (!journal.Output.Matches(candidate) || !journal.Output.Matches(snapshot)) return Entry(PdfSaveRecoveryStatus.Incomplete, "The candidate or independent output snapshot is missing or incomplete.");
        await ValidateCandidateAsync(transaction, journal, token).ConfigureAwait(false);
        return Entry(PdfSaveRecoveryStatus.Prepared, null);

        PdfSaveRecoveryEntry Entry(PdfSaveRecoveryStatus status, string? detail) => new(transaction, target, status, detail)
        { OutputSnapshotPath = File.Exists(output) ? output : null, PreservedExternalPath = status == PdfSaveRecoveryStatus.PublishedConflict ? displaced : null };
    }

    private static async Task<PdfSaveConflictException> PublishedConflictAsync(string transaction, string target, Journal journal, CancellationToken token)
    {
        var displaced = FindDisplaced(transaction, journal);
        var actual = await CaptureAsync(displaced ?? target, token).ConfigureAwait(false);
        return new(target, journal.ExpectedTarget, actual, transaction, displaced, Path.Combine(transaction, OutputName));
    }

    internal static async Task<byte[]> ReadVersionAsync(string directory, PdfSaveRecoveryVersion version, CancellationToken token)
    {
        if (!Enum.IsDefined(version)) throw new ArgumentOutOfRangeException(nameof(version));
        var transaction = Path.GetFullPath(directory);
        ValidateLayout(transaction);
        using var owner = OpenOwner(transaction);
        var journal = await ReadJournalAsync(transaction, token).ConfigureAwait(false);
        if (version == PdfSaveRecoveryVersion.DisplacedExternal)
        {
            var state = await InspectTransactionAsync(transaction, journal, token).ConfigureAwait(false);
            if (state.Status != PdfSaveRecoveryStatus.PublishedConflict) throw new InvalidOperationException("No published external conflict version is available.");
        }
        var path = version == PdfSaveRecoveryVersion.SavedOutput ? Path.Combine(transaction, OutputName) : FindDisplaced(transaction, journal);
        if (path is null || !File.Exists(path)) throw new InvalidOperationException("The requested preserved version is not present.");
        if (new FileInfo(path).Length > 256L * 1024 * 1024) throw new NotSupportedException("ReadVersionAsync is limited to 256 MiB; inspect the preserved file directly for larger versions.");
        if (version == PdfSaveRecoveryVersion.SavedOutput) await ValidateOutputFileAsync(path, journal, token).ConfigureAwait(false);
        return await File.ReadAllBytesAsync(path, token).ConfigureAwait(false);
    }
}
