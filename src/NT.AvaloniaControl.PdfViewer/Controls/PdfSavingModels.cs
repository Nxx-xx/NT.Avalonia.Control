namespace NT.AvaloniaControl.PdfViewer;

public enum PdfUnsavedChangesDecision { Cancel, Save, Discard }
public enum PdfDocumentChangeReason { Close, Replace }

public sealed record PdfUnsavedChangesContext(string DocumentName, string? SavePath, PdfDocumentChangeReason Reason);

/// <summary>Optional asynchronous host confirmation, called on the UI thread outside the edit queue.</summary>
public interface IPdfUnsavedChangesService
{
    Task<PdfUnsavedChangesDecision> ConfirmAsync(PdfUnsavedChangesContext context, CancellationToken cancellationToken = default);
}

public sealed class PdfUnsavedChangesEventArgs(PdfUnsavedChangesContext context) : EventArgs
{
    public PdfUnsavedChangesContext Context { get; } = context;
    /// <summary>Supply a decision task to replace the service / built-in prompt.</summary>
    public Task<PdfUnsavedChangesDecision>? Decision { get; set; }
}

public sealed class PdfSaveRequestedEventArgs(string? path, bool saveAs, bool autoSave) : EventArgs
{
    /// <summary>Override the local target, or leave null to use the file picker.</summary>
    public string? Path { get; set; } = path;
    public bool IsSaveAs { get; } = saveAs;
    public bool IsAutoSave { get; } = autoSave;
    public bool Cancel { get; set; }
}

public sealed class PdfDocumentSavedEventArgs(string? path, long revision, bool autoSave) : EventArgs
{
    public PdfFileSaveResult? FileResult { get; init; }
    /// <summary>Null for stream saves.</summary>
    public string? Path { get; } = path;
    public long Revision { get; } = revision;
    public bool IsAutoSave { get; } = autoSave;
}

public sealed class PdfSaveFailedEventArgs(PdfError error, bool autoSave) : EventArgs
{
    public PdfError Error { get; } = error;
    public bool IsAutoSave { get; } = autoSave;
}
