using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.Versioning;
using NT.AvaloniaControl.PdfViewer.Editing;

namespace NT.AvaloniaControl.PdfViewer.Saving;

internal static partial class PdfAtomicFileSave
{
    private const string Prefix = ".nt-pdf-txn-";
    private const string JournalName = "transaction.json";
    private const string OwnerName = "owner.lock";
    private const string CandidateName = "candidate.pdf";
    private const string OutputName = "output.pdf";
    private const string DisplacedName = "displaced.pdf";
    private const string CommitName = "commit.json";
    private static readonly JsonSerializerOptions Json = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    private sealed record Journal(int Version, string TargetFileName, PdfFileSnapshot ExpectedTarget, PdfFileSnapshot Output, int Pages);

    internal static async Task<PdfFileSnapshot> CaptureAsync(string path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        EnsureNoLinks(path);
        try
        {
            if ((File.GetAttributes(path) & FileAttributes.Directory) != 0) throw new IOException("The save target is a directory.");
            await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var length = file.Length;
            var hash = await SHA256.HashDataAsync(file, token).ConfigureAwait(false);
            if (file.Position != length || file.Length != length) throw new IOException("The save target changed while its fingerprint was read.");
            return new(true, length, Convert.ToHexString(hash));
        }
        catch (FileNotFoundException) { return PdfFileSnapshot.Missing; }
        catch (DirectoryNotFoundException) { return PdfFileSnapshot.Missing; }
    }

    internal static async Task<PdfFileSaveResult> SaveAsync(string path, byte[] output, int pages, PdfFileSaveOptions options, CancellationToken token, Action validateOwner)
    {
        var target = Path.GetFullPath(path);
        ValidateTargetName(Path.GetFileName(target));
        EnsureNoLinks(target);
        options.ExpectedTarget?.Validate();
        await using var targetLock = AcquireTargetLock(target);
        var expected = options.ExpectedTarget ?? await CaptureAsync(target, token).ConfigureAwait(false);
        await CheckExpectedAsync(target, expected, token).ConfigureAwait(false);
        var transaction = Path.Combine(Path.GetDirectoryName(target)!, Prefix + Guid.NewGuid().ToString("N"));
        FileStream? owner = null;
        var committed = false;
        var retain = false;
        try
        {
            CreatePrivateDirectory(transaction);
            owner = new FileStream(Path.Combine(transaction, OwnerName), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            var stamp = PdfFileSnapshot.FromBytes(output);
            var journal = new Journal(2, Path.GetFileName(target), expected, stamp, pages);
            await WriteJournalAsync(transaction, journal, token).ConfigureAwait(false);
            PdfStoragePrimitives.FlushDirectory(transaction);
            PdfStoragePrimitives.FlushDirectory(Path.GetDirectoryName(transaction)!);
            await Hit(PdfFileSaveStage.JournalFlushed).ConfigureAwait(false);
            await using (var file = CreatePrivateFile(Path.Combine(transaction, OutputName), 65536))
            {
                for (var offset = 0; offset < output.Length; offset += 65536)
                {
                    await file.WriteAsync(output.AsMemory(offset, Math.Min(65536, output.Length - offset)), token).ConfigureAwait(false);
                    await Hit(PdfFileSaveStage.WriteChunk).ConfigureAwait(false);
                }
                await Hit(PdfFileSaveStage.BeforeFlush).ConfigureAwait(false);
                await file.FlushAsync(token).ConfigureAwait(false);
                file.Flush(true);
            }
            await CopyOutputCandidateAsync(transaction, token).ConfigureAwait(false);
            PdfStoragePrimitives.FlushDirectory(transaction);
            await Hit(PdfFileSaveStage.FileFlushed).ConfigureAwait(false);
            await ValidateCandidateAsync(transaction, journal, token).ConfigureAwait(false);
            await Hit(PdfFileSaveStage.Validated).ConfigureAwait(false);
            await Hit(PdfFileSaveStage.BeforeReplace).ConfigureAwait(false);
            var durability = await PublishPreparedAsync(transaction, target, journal, token, validateOwner, () => retain = true).ConfigureAwait(false);
            committed = true; // Cancellation / cleanup failure after this point cannot undo a committed file.
            try { await Hit(PdfFileSaveStage.CleanupStarting).ConfigureAwait(false); }
            catch (Exception error)
            {
                retain = true;
                return new(target, stamp, transaction, error.Message) { Durability = durability };
            }
            owner.Dispose(); owner = null;
            var warning = TryCleanup(transaction);
            retain = warning is not null;
            return new(target, stamp, retain ? transaction : null, warning) { Durability = durability };
        }
        catch (PdfSaveConflictException error) when (error.WasPublished) { retain = true; throw; }
        catch (PdfSaveDurabilityException) { retain = true; throw; }
        finally
        {
            owner?.Dispose();
            if (!committed && !retain) TryCleanup(transaction);
        }

        Task Hit(PdfFileSaveStage stage) => PdfSaveFaultInjection.HitAsync(stage, target, transaction, token);
    }

    internal static async Task<IReadOnlyList<PdfSaveRecoveryEntry>> ScanAsync(string directory, CancellationToken token)
    {
        var parent = Path.GetFullPath(directory);
        EnsureNoLinks(parent);
        var entries = new List<PdfSaveRecoveryEntry>();
        foreach (var transaction in Directory.EnumerateDirectories(parent, Prefix + "*", SearchOption.TopDirectoryOnly))
        {
            token.ThrowIfCancellationRequested();
            if (entries.Count >= 4096) throw new IOException("Too many recovery directories; inspect this directory before continuing.");
            try
            {
                ValidateLayout(transaction);
                using var owner = OpenOwner(transaction);
                var journal = await ReadJournalAsync(transaction, token).ConfigureAwait(false);
                var target = Target(transaction, journal);
                entries.Add(await InspectTransactionAsync(transaction, journal, token).ConfigureAwait(false));
            }
            catch (OperationCanceledException) { throw; }
            catch (IOException error) when (IsOwnerLocked(transaction)) { entries.Add(new(transaction, null, PdfSaveRecoveryStatus.Active, error.Message)); }
            catch (Exception error) when (error is IOException or InvalidDataException or PdfDocumentException or UnauthorizedAccessException or ArgumentException or JsonException or NotSupportedException)
            { entries.Add(new(transaction, null, PdfSaveRecoveryStatus.Invalid, error.Message)); }
        }
        return entries;
    }

    internal static async Task<PdfFileSaveResult> RecoverAsync(string transactionDirectory, CancellationToken token)
    {
        var transaction = Path.GetFullPath(transactionDirectory);
        ValidateLayout(transaction);
        var owner = OpenOwner(transaction);
        try
        {
            var journal = await ReadJournalAsync(transaction, token).ConfigureAwait(false);
            var target = Target(transaction, journal);
            await using var targetLock = AcquireTargetLock(target);
            var inspection = await InspectTransactionAsync(transaction, journal, token).ConfigureAwait(false);
            if (inspection.Status == PdfSaveRecoveryStatus.PublishedConflict)
                throw await PublishedConflictAsync(transaction, target, journal, token).ConfigureAwait(false);
            PdfSaveDurability durability;
            if (inspection.Status is PdfSaveRecoveryStatus.AlreadyCommitted or PdfSaveRecoveryStatus.DurabilityPending)
            {
                durability = await ConfirmDurabilityAsync(transaction, target, journal).ConfigureAwait(false);
            }
            else
            {
                await CheckExpectedAsync(target, journal.ExpectedTarget, token).ConfigureAwait(false);
                await ValidateCandidateAsync(transaction, journal, token).ConfigureAwait(false);
                if (!File.Exists(Path.Combine(transaction, OutputName)))
                {
                    await CopyPrivateFileAsync(Path.Combine(transaction, CandidateName), Path.Combine(transaction, OutputName), token).ConfigureAwait(false);
                    PdfStoragePrimitives.FlushDirectory(transaction);
                }
                durability = await PublishPreparedAsync(transaction, target, journal, token, () => { }, () => { }).ConfigureAwait(false);
            }
            owner.Dispose();
            var warning = TryCleanup(transaction);
            return new(target, journal.Output, warning is null ? null : transaction, warning) { Durability = durability };
        }
        finally { owner.Dispose(); }
    }

    internal static async Task DiscardAsync(string transactionDirectory, CancellationToken token)
    {
        var transaction = Path.GetFullPath(transactionDirectory);
        ValidateLayout(transaction);
        using (var owner = OpenOwner(transaction))
        {
            var journal = await ReadJournalAsync(transaction, token).ConfigureAwait(false);
            if ((await InspectTransactionAsync(transaction, journal, token).ConfigureAwait(false)).Status == PdfSaveRecoveryStatus.PublishedConflict)
                throw new InvalidOperationException("Export the preserved conflict versions before explicitly resolving this transaction; automatic discard is refused.");
            token.ThrowIfCancellationRequested();
            foreach (var name in new[] { CandidateName, DisplacedName, OutputName, CommitName, JournalName }) File.Delete(Path.Combine(transaction, name));
            PdfStoragePrimitives.FlushDirectory(transaction);
        }
        File.Delete(Path.Combine(transaction, OwnerName));
        Directory.Delete(transaction, false);
        PdfStoragePrimitives.FlushDirectory(Path.GetDirectoryName(transaction)!);
    }

    private static async Task CheckExpectedAsync(string target, PdfFileSnapshot expected, CancellationToken token)
    {
        var actual = await CaptureAsync(target, token).ConfigureAwait(false);
        if (!expected.Matches(actual)) throw new PdfSaveConflictException(target, expected, actual);
    }
    private static FileStream AcquireTargetLock(string target)
    {
        var identity = OperatingSystem.IsWindows() ? target.ToUpperInvariant() : target;
        var name = ".nt-pdf-save-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))) + ".lock";
        var path = Path.Combine(Path.GetDirectoryName(target)!, name);
        EnsureNoLinks(path);
        // Retained intentionally: unlinking this inode on Unix could split concurrent writers across two locks.
        return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }
    private static FileStream OpenOwner(string transaction) => new(Path.Combine(transaction, OwnerName), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    private static bool IsOwnerLocked(string transaction)
    {
        try { ValidateLayout(transaction); }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { return false; }
        try { using var owner = OpenOwner(transaction); return false; }
        catch (IOException) { return File.Exists(Path.Combine(transaction, OwnerName)); }
        catch (Exception error) when (error is UnauthorizedAccessException or ArgumentException or NotSupportedException) { return false; }
    }
    private static async Task WriteJournalAsync(string transaction, Journal journal, CancellationToken token)
    {
        await using var file = CreatePrivateFile(Path.Combine(transaction, JournalName), 4096);
        await JsonSerializer.SerializeAsync(file, journal, Json, token).ConfigureAwait(false);
        await file.FlushAsync(token).ConfigureAwait(false);
        file.Flush(true);
    }
    private static async Task<Journal> ReadJournalAsync(string transaction, CancellationToken token)
    {
        var path = Path.Combine(transaction, JournalName);
        if (new FileInfo(path).Length > 16384) throw new InvalidDataException("The recovery journal is too large.");
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        var journal = await JsonSerializer.DeserializeAsync<Journal>(file, Json, token).ConfigureAwait(false) ?? throw new InvalidDataException("The recovery journal is empty.");
        if (journal.Version is not (1 or 2) || journal.Pages < 1 || journal.Output is null || journal.ExpectedTarget is null ||
            !journal.Output.Exists || journal.Output.Length is <= 0 or > 256L * 1024 * 1024)
            throw new InvalidDataException("Unsupported recovery journal.");
        journal.Output.Validate(); journal.ExpectedTarget.Validate();
        ValidateTargetName(journal.TargetFileName);
        EnsureNoLinks(Target(transaction, journal));
        return journal;
    }
    private static void ValidateTargetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || name.IndexOfAny(['/', '\\', ':', '\0']) >= 0 ||
            name != Path.GetFileName(name) || name.StartsWith(".nt-pdf-", StringComparison.Ordinal) ||
            (OperatingSystem.IsWindows() && (name.EndsWith('.') || name.EndsWith(' ') || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)))
            throw new InvalidDataException("Invalid recovery target name.");
    }
    private static string Target(string transaction, Journal journal) => Path.Combine(Path.GetDirectoryName(transaction)!, journal.TargetFileName);
    private static async Task ValidateCandidateAsync(string transaction, Journal journal, CancellationToken token)
    {
        await ValidateOutputFileAsync(Path.Combine(transaction, CandidateName), journal, token).ConfigureAwait(false);
        if (journal.Version >= 2) await ValidateOutputFileAsync(Path.Combine(transaction, OutputName), journal, token).ConfigureAwait(false);
    }
    private static async Task ValidateOutputFileAsync(string candidate, Journal journal, CancellationToken token)
    {
        var stamp = await CaptureAsync(candidate, token).ConfigureAwait(false);
        if (!journal.Output.Matches(stamp)) throw new InvalidDataException("The recovery candidate does not match its recorded content.");
        var bytes = await File.ReadAllBytesAsync(candidate, token).ConfigureAwait(false);
        if (!journal.Output.Matches(PdfFileSnapshot.FromBytes(bytes)) || await PdfWriteValidation.ValidateAsync(bytes, token).ConfigureAwait(false) != journal.Pages)
            throw new InvalidDataException("The recovery candidate failed PDF validation.");
    }
    private static void ValidateLayout(string transaction)
    {
        var name = Path.GetFileName(transaction);
        if (!name.StartsWith(Prefix, StringComparison.Ordinal) || !Guid.TryParseExact(name[Prefix.Length..], "N", out _))
            throw new InvalidDataException("Not an owned PDF save transaction directory.");
        EnsureNoLinks(transaction);
        foreach (var entry in Directory.EnumerateFileSystemEntries(transaction))
        {
            if (Path.GetFileName(entry) is not (JournalName or OwnerName or CandidateName or OutputName or DisplacedName or CommitName)) throw new InvalidDataException("Unknown recovery directory contents.");
            EnsureNoLinks(entry);
            if ((File.GetAttributes(entry) & FileAttributes.Directory) != 0) throw new InvalidDataException("Nested recovery directories are not allowed.");
        }
    }
    private static string? TryCleanup(string transaction)
    {
        try
        {
            ValidateLayout(transaction);
            if (!File.Exists(Path.Combine(transaction, OwnerName))) { Directory.Delete(transaction, false); return null; }
            using (var owner = OpenOwner(transaction))
            {
                foreach (var name in new[] { CandidateName, DisplacedName, OutputName, CommitName, JournalName }) File.Delete(Path.Combine(transaction, name));
                PdfStoragePrimitives.FlushDirectory(transaction);
            }
            File.Delete(Path.Combine(transaction, OwnerName));
            Directory.Delete(transaction, false);
            PdfStoragePrimitives.FlushDirectory(Path.GetDirectoryName(transaction)!);
            return null;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or NotSupportedException) { return error.Message; }
    }
    private static void EnsureNoLinks(string path)
    {
        for (var part = Path.GetFullPath(path); part is not null; part = Path.GetDirectoryName(part))
        {
            try { if ((File.GetAttributes(part) & FileAttributes.ReparsePoint) != 0) throw new NotSupportedException("File saves and recovery refuse symbolic links / reparse points."); }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }

    private static FileStream CreatePrivateFile(string path, int bufferSize)
    {
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None, BufferSize = bufferSize, Options = FileOptions.Asynchronous };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        return new FileStream(path, options);
    }
    private static void CreatePrivateDirectory(string path)
    {
        if (OperatingSystem.IsWindows()) CreatePrivateWindowsDirectory(path);
        else
        {
            var mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
            Directory.CreateDirectory(path, mode);
            if (File.GetUnixFileMode(path) != mode)
                throw new NotSupportedException("The save filesystem did not enforce private Unix staging permissions.");
        }
    }
    [SupportedOSPlatform("windows")]
    private static void CreatePrivateWindowsDirectory(string path)
    {
        using var identity = WindowsIdentity.GetCurrent();
        var user = identity.User ?? throw new IOException("The current user has no security identity for a private recovery directory.");
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(true, false);
        security.SetOwner(user);
        security.AddAccessRule(new FileSystemAccessRule(user, FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(path).Create(security);
    }
}
