using System.Security.Cryptography;

namespace NT.AvaloniaControl.PdfViewer;

/// <summary>Content identity, not an mtime heuristic. Metadata-only changes are intentionally ignored.</summary>
public sealed record PdfFileSnapshot(bool Exists, long Length, string Sha256)
{
    public static PdfFileSnapshot Missing { get; } = new(false, 0, "");
    internal static PdfFileSnapshot FromBytes(byte[] bytes) => new(true, bytes.LongLength, Convert.ToHexString(SHA256.HashData(bytes)));
    public static Task<PdfFileSnapshot> CaptureAsync(string path, CancellationToken cancellationToken = default) =>
        Saving.PdfAtomicFileSave.CaptureAsync(Path.GetFullPath(path), cancellationToken);

    internal void Validate()
    {
        if (!Exists ? Length != 0 || Sha256 != "" : Length < 0 || Sha256 is null || Sha256.Length != 64 || !Sha256.All(Uri.IsHexDigit))
            throw new ArgumentException("Invalid file content fingerprint.");
    }
    internal bool Matches(PdfFileSnapshot other) => Exists == other.Exists && Length == other.Length &&
        StringComparer.OrdinalIgnoreCase.Equals(Sha256, other.Sha256);
}

public sealed record PdfFileSaveOptions
{
    /// <summary>The opened / last-saved identity for an original target. Null captures the target at the start of this Save As operation.</summary>
    public PdfFileSnapshot? ExpectedTarget { get; init; }
}

public enum PdfSaveDurability { Unknown, FileAndDirectoryEntriesFlushed, PostRenameFileAndCommitRecordFlushed }
public sealed record PdfFileSaveResult(string TargetPath, PdfFileSnapshot Output, string? RecoveryDirectory, string? CleanupWarning)
{
    public PdfSaveDurability Durability { get; init; }
}

/// <summary>The target changed or disappeared. Publication races retain both versions for explicit resolution.</summary>
public sealed class PdfSaveConflictException : IOException
{
    public PdfSaveConflictException(string targetPath, PdfFileSnapshot expected, PdfFileSnapshot actual)
        : this(targetPath, expected, actual, null, null, null) { }
    internal PdfSaveConflictException(string targetPath, PdfFileSnapshot expected, PdfFileSnapshot actual,
        string? recoveryDirectory, string? preservedExternalPath, string? outputSnapshotPath)
        : base(recoveryDirectory is null ? PdfViewerStrings.ErrorSaveConflict : PdfViewerStrings.ErrorPublishedSaveConflict)
    {
        TargetPath = targetPath; Expected = expected; Actual = actual;
        RecoveryDirectory = recoveryDirectory; PreservedExternalPath = preservedExternalPath; OutputSnapshotPath = outputSnapshotPath;
    }
    public string TargetPath { get; }
    public PdfFileSnapshot Expected { get; }
    public PdfFileSnapshot Actual { get; }
    /// <summary>True when a race was found after atomic publication. Both versions are retained; no automatic rollback is attempted.</summary>
    public bool WasPublished => RecoveryDirectory is not null;
    public string? RecoveryDirectory { get; }
    public string? PreservedExternalPath { get; }
    public string? OutputSnapshotPath { get; }
}

public sealed class PdfSaveDurabilityException(string targetPath, string recoveryDirectory, Exception innerException)
    : IOException(PdfViewerStrings.ErrorSaveDurabilityUncertain, innerException)
{
    public string TargetPath { get; } = targetPath;
    public string RecoveryDirectory { get; } = recoveryDirectory;
}

public enum PdfSaveRecoveryStatus { Active, Incomplete, Prepared, AlreadyCommitted, Conflict, Invalid, PublishedConflict, DurabilityPending }
public sealed record PdfSaveRecoveryEntry(string TransactionDirectory, string? TargetPath, PdfSaveRecoveryStatus Status, string? Detail)
{
    public string? OutputSnapshotPath { get; init; }
    public string? PreservedExternalPath { get; init; }
}
public enum PdfSaveRecoveryVersion { SavedOutput, DisplacedExternal }

/// <summary>Explicit recovery of interrupted save transactions; never scans or overwrites documents automatically.</summary>
public static class PdfSaveRecovery
{
    /// <summary>Reads an owned copy of a selected retained version; it never overwrites the original target.</summary>
    public static Task<byte[]> ReadVersionAsync(string transactionDirectory, PdfSaveRecoveryVersion version, CancellationToken cancellationToken = default) =>
        Saving.PdfAtomicFileSave.ReadVersionAsync(transactionDirectory, version, cancellationToken);
    public static Task<IReadOnlyList<PdfSaveRecoveryEntry>> ScanAsync(string directory, CancellationToken cancellationToken = default) =>
        Saving.PdfAtomicFileSave.ScanAsync(directory, cancellationToken);
    /// <summary>Revalidates the journal, candidate and target under the same writer locks before committing.</summary>
    public static Task<PdfFileSaveResult> RecoverAsync(string transactionDirectory, CancellationToken cancellationToken = default) =>
        Saving.PdfAtomicFileSave.RecoverAsync(transactionDirectory, cancellationToken);
    /// <summary>Deletes only the known files of an inactive, valid transaction. Unknown files / links are refused.</summary>
    public static Task DiscardAsync(string transactionDirectory, CancellationToken cancellationToken = default) =>
        Saving.PdfAtomicFileSave.DiscardAsync(transactionDirectory, cancellationToken);
}
