using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace NT.AvaloniaControl.PdfViewer.Saving;

internal static class PdfStoragePrimitives
{
    internal static void Publish(string candidate, string target, string displaced, bool existed)
    {
        if (OperatingSystem.IsWindows())
        {
            if (existed) File.Replace(candidate, target, displaced);
            else
            {
                try { File.Move(candidate, target); }
                catch (IOException error) when ((error.HResult & 0xffff) is 80 or 183) { throw new PdfPublicationCollisionException(error); }
            }
            return;
        }
        if (!OperatingSystem.IsLinux()) throw new NotSupportedException("Recoverable atomic publication currently supports Windows and Linux.");
        if (existed)
        {
            if (renameat2(-100, candidate, -100, target, 2) != 0) ThrowNative("Atomic exchange", Marshal.GetLastPInvokeError());
            // After EXCHANGE the old directory entry is in candidate. Leaving it there is
            // valid recovery state, so lack of space for this rename never deletes it.
            try { File.Move(candidate, displaced); }
            catch (IOException) { }
        }
        else
        {
            // link is a no-replace publication even on filesystems lacking renameat2.
            if (link(candidate, target) != 0)
            {
                var error = Marshal.GetLastPInvokeError();
                if (error == 17) throw new PdfPublicationCollisionException(new Win32Exception(error));
                ThrowNative("No-replace publication", error);
            }
            File.Delete(candidate);
        }
    }

    internal static void FlushFile(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.Read | FileShare.Delete);
            file.Flush(true);
        }
        else
        {
            var fd = open(path, 0x80000); // O_RDONLY | O_CLOEXEC; fsync also accepts readonly descriptors.
            if (fd < 0) ThrowNative("Open for synchronization", Marshal.GetLastPInvokeError());
            try { if (fsync(fd) != 0) ThrowNative("File synchronization", Marshal.GetLastPInvokeError()); }
            finally { close(fd); }
        }
    }

    internal static void FlushDirectory(string path)
    {
        if (OperatingSystem.IsWindows()) return; // Windows records a post-rename file/commit flush, not a POSIX directory-fsync claim.
        if (!OperatingSystem.IsLinux()) throw new NotSupportedException("Directory synchronization is only implemented for Linux.");
        var fd = open(path, 0x90000); // O_DIRECTORY | O_CLOEXEC.
        if (fd < 0) ThrowNative("Open directory for synchronization", Marshal.GetLastPInvokeError());
        try { if (fsync(fd) != 0) ThrowNative("Directory synchronization", Marshal.GetLastPInvokeError()); }
        finally { close(fd); }
    }

    private static void ThrowNative(string operation, int error)
    {
        if (error is 22 or 38 or 95) throw new NotSupportedException(operation + " is not supported by this filesystem.", new Win32Exception(error));
        throw new IOException(operation + " failed.", new Win32Exception(error));
    }
    [DllImport("libc", SetLastError = true)] private static extern int renameat2(int olddirfd, string oldpath, int newdirfd, string newpath, uint flags);
    [DllImport("libc", SetLastError = true)] private static extern int link(string oldpath, string newpath);
    [DllImport("libc", SetLastError = true)] private static extern int open(string path, int flags);
    [DllImport("libc", SetLastError = true)] private static extern int fsync(int fd);
    [DllImport("libc", SetLastError = true)] private static extern int close(int fd);
}

internal sealed class PdfPublicationCollisionException(Exception inner) : IOException("The new target appeared during publication.", inner);
