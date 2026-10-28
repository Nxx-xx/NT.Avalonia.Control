namespace NT.AvaloniaControl.PdfViewer.Saving;

// Deliberately internal. No environment variable or PDF content can enable production fault injection.
internal enum PdfFileSaveStage { JournalFlushed, WriteChunk, BeforeFlush, FileFlushed, Validated, BeforeReplace, Replaced, CommitPrepared, CommitFlushed, CleanupStarting }
internal sealed record PdfFileSaveProbe(PdfFileSaveStage Stage, string TargetPath, string TransactionDirectory, string CandidatePath);
internal static class PdfSaveFaultInjection
{
    internal static readonly AsyncLocal<Func<PdfFileSaveProbe, CancellationToken, Task>?> Current = new();
    internal static Task HitAsync(PdfFileSaveStage stage, string target, string transaction, CancellationToken token) =>
        Current.Value?.Invoke(new(stage, target, transaction, Path.Combine(transaction, "candidate.pdf")), token) ?? Task.CompletedTask;
}
