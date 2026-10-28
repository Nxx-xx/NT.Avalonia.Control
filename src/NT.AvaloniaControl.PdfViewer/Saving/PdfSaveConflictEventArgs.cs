namespace NT.AvaloniaControl.PdfViewer;

public sealed class PdfSaveConflictEventArgs(PdfSaveConflictException conflict, bool autoSave) : EventArgs
{
    public PdfSaveConflictException Conflict { get; } = conflict;
    public bool IsAutoSave { get; } = autoSave;
}
