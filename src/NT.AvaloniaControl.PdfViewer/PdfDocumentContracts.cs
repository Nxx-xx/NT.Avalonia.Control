namespace NT.AvaloniaControl.PdfViewer;

/// <summary>
/// Access inferred from native permission flags. UserOrOwner means the PDF grants all user permissions,
/// so PDFium's public permission APIs cannot distinguish the password identity. This is not signature trust.
/// </summary>
public enum PdfPasswordAccess { Unencrypted, User, Owner, UserOrOwner }

/// <summary>Immutable native permission snapshot. Flags are the PDF permission bits, not host policy.</summary>
public sealed record PdfDocumentPermissions(uint EffectiveFlags, uint UserFlags, int SecurityHandlerRevision,
    PdfPasswordAccess PasswordAccess)
{
    public bool IsEncrypted => SecurityHandlerRevision >= 0;
    public bool CanCopy => (EffectiveFlags & (1u << 4)) != 0;
    public bool CanPrint => (EffectiveFlags & (1u << 2)) != 0;
    public bool CanPrintHighQuality => CanPrint && (SecurityHandlerRevision < 3 || (EffectiveFlags & (1u << 11)) != 0);
    public bool CanModify => (EffectiveFlags & (1u << 3)) != 0;
    public bool CanAnnotate => (EffectiveFlags & (1u << 5)) != 0;
    public bool CanFillForms => SecurityHandlerRevision < 3 ? CanAnnotate : (EffectiveFlags & (1u << 8)) != 0;
    public bool CanAssemble => SecurityHandlerRevision < 3 ? CanModify : (EffectiveFlags & (1u << 10)) != 0;
}

/// <summary>Document information strings, kept raw for dates because PDF permits incomplete date values.</summary>
public sealed record PdfDocumentMetadata(string Title, string Author, string Subject, string Keywords,
    string Creator, string Producer, string CreationDate, string ModificationDate, int? FileVersion,
    int SignatureCount);

public enum PdfCapabilityUnavailableReason { None, NoDocument, Busy, Disposed, HostDisabled, DocumentPermission, ReadOnly, NotImplemented, UnsupportedDocument }

/// <summary>Implementation support and current permission are separate; CanExecute requires both.</summary>
public sealed record PdfCapability(bool IsImplemented, bool IsAllowed, PdfCapabilityUnavailableReason Reason)
{
    public bool CanExecute => IsImplemented && IsAllowed;
    public static PdfCapability Available { get; } = new(true, true, PdfCapabilityUnavailableReason.None);
    internal static PdfCapability Unavailable(PdfCapabilityUnavailableReason reason, bool implemented = true) => new(implemented, false, reason);
}

/// <summary>Reader capability snapshot. Editing, form input, saving and printing await their viewer stages.</summary>
public sealed record PdfDocumentCapabilities(PdfCapability Read, PdfCapability Search, PdfCapability Copy,
    PdfCapability Annotate, PdfCapability FillForms, PdfCapability Save, PdfCapability Print, PdfCapability Share)
{
    public bool CanRead => Read.CanExecute;
    public bool CanSearch => Search.CanExecute;
    public bool CanCopy => Copy.CanExecute;
    public bool CanAnnotate => Annotate.CanExecute;
    public bool CanFillForms => FillForms.CanExecute;
    public bool CanSave => Save.CanExecute;
    public bool CanPrint => Print.CanExecute;
    public bool CanShare => Share.CanExecute;

    internal static PdfDocumentCapabilities Create(PdfDocumentPermissions? permissions,
        PdfCapabilityUnavailableReason unavailable = PdfCapabilityUnavailableReason.None,
        bool allowCopy = true, bool allowSearch = true, bool isReadOnly = true,
        bool allowAnnotations = true, bool allowForms = true, bool allowPrinting = true)
    {
        PdfCapability Reader(bool host, bool permission = true) => unavailable != PdfCapabilityUnavailableReason.None
            ? PdfCapability.Unavailable(unavailable)
            : !host ? PdfCapability.Unavailable(PdfCapabilityUnavailableReason.HostDisabled)
            : !permission ? PdfCapability.Unavailable(PdfCapabilityUnavailableReason.DocumentPermission)
            : PdfCapability.Available;
        PdfCapability Future(bool host, bool permission, bool write) => unavailable != PdfCapabilityUnavailableReason.None
            ? PdfCapability.Unavailable(unavailable, false)
            : !host ? PdfCapability.Unavailable(PdfCapabilityUnavailableReason.HostDisabled, false)
            : !permission ? PdfCapability.Unavailable(PdfCapabilityUnavailableReason.DocumentPermission, false)
            : write && isReadOnly ? PdfCapability.Unavailable(PdfCapabilityUnavailableReason.ReadOnly, false)
            : PdfCapability.Unavailable(PdfCapabilityUnavailableReason.NotImplemented, false);
        return new(Reader(true), Reader(allowSearch), Reader(allowCopy, permissions?.CanCopy ?? false),
            Future(allowAnnotations, permissions?.CanAnnotate ?? false, true),
            Future(allowForms, permissions?.CanFillForms ?? false, true), Future(true, true, false),
            Future(allowPrinting, permissions?.CanPrint ?? false, false), Future(true, true, false));
    }
}

public enum PdfErrorKind { Unknown, FileAccess, InvalidDocument, PasswordRequiredOrIncorrect, UnsupportedSecurity, PageContent, NativeRuntime, PermissionDenied, UnsupportedOperation, Canceled, Disposed, SaveConflict, SaveDurabilityUncertain }

/// <summary>Stable error classification; the original exception remains available for diagnostics.</summary>
public sealed record PdfError(PdfErrorKind Kind, string Message, uint? NativeErrorCode, Exception Exception)
{
    public static PdfError FromException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var native = exception as PdfDocumentException;
        var kind = exception switch
        {
            PdfSaveConflictException => PdfErrorKind.SaveConflict,
            PdfSaveDurabilityException => PdfErrorKind.SaveDurabilityUncertain,
            PdfDocumentException pdf => pdf.Kind,
            OperationCanceledException => PdfErrorKind.Canceled,
            ObjectDisposedException => PdfErrorKind.Disposed,
            DllNotFoundException or BadImageFormatException or EntryPointNotFoundException => PdfErrorKind.NativeRuntime,
            PlatformNotSupportedException { InnerException: DllNotFoundException or BadImageFormatException or EntryPointNotFoundException } => PdfErrorKind.NativeRuntime,
            UnauthorizedAccessException => PdfErrorKind.PermissionDenied,
            NotSupportedException => PdfErrorKind.UnsupportedOperation,
            IOException => PdfErrorKind.FileAccess,
            _ => PdfErrorKind.Unknown
        };
        return new(kind, exception.Message, native?.ErrorCode, exception);
    }
}

public enum PdfViewerDocumentState { Empty, Loading, Ready, Error, Disposed }

public sealed class PdfDocumentStateChangedEventArgs(PdfViewerDocumentState state, Guid? sessionId) : EventArgs
{
    public PdfViewerDocumentState State { get; } = state;
    public Guid? SessionId { get; } = sessionId;
}

public sealed class PdfDocumentLoadFailedEventArgs(PdfError error, PdfDocumentSource? source) : EventArgs
{
    public PdfError Error { get; } = error;
    public PdfDocumentSource? Source { get; } = source;
}
