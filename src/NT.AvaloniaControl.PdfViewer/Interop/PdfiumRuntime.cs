namespace NT.AvaloniaControl.PdfViewer.Interop;

internal static class PdfiumRuntime
{
    // PDFium explicitly requires serialization across all documents, not just each document.
    private static readonly PdfiumWorkScheduler Scheduler = new();
    private static bool _initialized;

    internal static Task<T> RunAsync<T>(Func<T> operation, CancellationToken cancellationToken,
        PdfiumWorkPriority priority = PdfiumWorkPriority.Foreground) =>
        Scheduler.Enqueue(() =>
        {
            EnsureInitialized();
            return operation();
        }, cancellationToken, priority);

    private static void EnsureInitialized()
    {
        if (_initialized)
            return;

        try
        {
            PdfiumNative.FPDF_InitLibrary();
            _initialized = true;
        }
        catch (DllNotFoundException exception)
        {
            throw new PlatformNotSupportedException(
                PdfViewerStrings.ErrorPdfiumLibraryLoad, exception);
        }
        catch (BadImageFormatException exception)
        {
            throw new PlatformNotSupportedException(PdfViewerStrings.ErrorPdfiumArchitecture, exception);
        }

        // Keep the library initialized for the process lifetime. Destroying its global state
        // while another consumer/finalizer can still use it would invalidate live handles.
    }
}
