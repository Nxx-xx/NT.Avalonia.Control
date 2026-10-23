using System.Security.Cryptography;
using System.Text;
using PdfViewer.Tests;

namespace PdfViewer.P0;

internal static class SaveProbe
{
    internal static object Run(string directory)
    {
        Directory.CreateDirectory(directory);
        var checks = new List<object>();
        void Check(string id, bool passed, string detail) => checks.Add(new { Id = id, Passed = passed, Detail = detail });
        var original = PdfFixture.ReadingDocument();
        File.WriteAllBytes(Path.Combine(directory, "input.pdf"), original);
        using (var document = new SaveDocument(original))
        {
            AddPage(document.Handle, "P0 saved edit");
            foreach (var (name, flags) in new[] { ("full", 2u), ("incremental", 1u) })
            {
                using var stream = new MemoryStream();
                var saved = SaveNative.Save(document.Handle, stream, flags);
                Check($"{name}-callback", saved.Success && saved.Error == null, $"bytes={stream.Length}");
                var bytes = stream.ToArray();
                File.WriteAllBytes(Path.Combine(directory, $"{name}.pdf"), bytes);
                using var reopened = new SaveDocument(bytes);
                var text = SaveNative.Text(reopened.Handle);
                Check($"{name}-roundtrip", SaveNative.FPDF_GetPageCount(reopened.Handle) == 3 && text.Contains("P0 saved edit") && text.Contains("Hello PDF reader") && text.Contains("Second page hello"), "Edited page and original text survive save/reopen.");
                Check($"{name}-original-prefix", bytes.AsSpan().StartsWith(original) == (flags == 1), "Incremental must preserve every original byte; full rewrite must replace the file.");
            }
            var target = Path.Combine(directory, "atomic-target.pdf");
            File.WriteAllBytes(target, original);
            var initialHash = SHA256.HashData(original);
            using (var faultedStream = new MemoryStream())
            {
                var callbackFailure = SaveNative.Save(document.Handle, faultedStream, failAfter: 0);
                Check("callback-failure-classification", !callbackFailure.Success && callbackFailure.Error is IOException,
                    $"NativeSuccess={callbackFailure.NativeSuccess}; callback exception must override native BOOL.");
            }
            var failed = AtomicWrite(document.Handle, target, failAfter: 0);
            Check("callback-io-error-original-unchanged", !failed.Success && failed.Error is IOException && SHA256.HashData(File.ReadAllBytes(target)).SequenceEqual(initialHash), "Injected WriteBlock I/O failure is captured outside native code; original hash unchanged.");
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var cancelled = AtomicWrite(document.Handle, target, token: cancellation.Token);
            Check("cancel-original-unchanged", !cancelled.Success && cancelled.Error is OperationCanceledException && SHA256.HashData(File.ReadAllBytes(target)).SequenceEqual(initialHash), "Cancellation in native callback returns false; source remains intact.");
            using (var locked = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var blocked = AtomicWrite(document.Handle, target);
                if (OperatingSystem.IsWindows())
                    Check("locked-target-original-unchanged", !blocked.Success && SHA256.HashData(File.ReadAllBytes(target)).SequenceEqual(initialHash), "Windows denies replacement of this open target; clean temp and preserve source.");
                else
                {
                    var oldBytes = new byte[original.Length];
                    locked.ReadExactly(oldBytes);
                    Check("open-target-posix-replacement", blocked.Success && SHA256.HashData(oldBytes).SequenceEqual(initialHash), "POSIX replacement may succeed while the old descriptor keeps the old inode; verify both views on the actual target filesystem.");
                }
            }
            var committed = AtomicWrite(document.Handle, target);
            using var final = new SaveDocument(File.ReadAllBytes(target));
            Check("atomic-commit", committed.Success && SaveNative.FPDF_GetPageCount(final.Handle) == 3, "Same-directory temporary file, Flush(true), re-open validation, File.Replace.");
            Check("temp-cleanup", !Directory.EnumerateFiles(directory, "*.writing").Any(), "No temporary files left by success, failure or cancellation.");
        }

        var encrypted = PdfFixture.PasswordProtected();
        File.WriteAllBytes(Path.Combine(directory, "encrypted-input.pdf"), encrypted);
        using (var protectedDocument = new SaveDocument(encrypted, "reader"))
        {
            AddPage(protectedDocument.Handle, "Encrypted saved edit");
            using var stream = new MemoryStream();
            var saved = SaveNative.Save(protectedDocument.Handle, stream);
            var bytes = stream.ToArray();
            File.WriteAllBytes(Path.Combine(directory, "encrypted-full.pdf"), bytes);
            using var reopened = new SaveDocument(bytes, "reader");
            var noPasswordRejected = false;
            try { using var unexpected = new SaveDocument(bytes); }
            catch (InvalidDataException) { noPasswordRejected = true; }
            Check("encryption-retained", saved.Success && noPasswordRejected && SaveNative.FPDF_GetSecurityHandlerRevision(reopened.Handle) == 2 && SaveNative.Text(reopened.Handle).Contains("Encrypted saved edit"), "R2/RC4 fixture retains encryption and user password. This is a compatibility fixture, not a production encryption recommendation.");
        }

        var signedPath = Path.Combine(Directory.GetParent(directory)!.FullName, "fixtures", "signed-input.pdf");
        if (File.Exists(signedPath))
        {
            var signed = File.ReadAllBytes(signedPath);
            using var document = new SaveDocument(signed);
            Check("signature-detected", SaveNative.FPDF_GetSignatureCount(document.Handle) == 1, "Genuine detached CMS fixture; independent checker verifies cryptography.");
            AddPage(document.Handle, "Edit after signature");
            foreach (var (name, flags) in new[] { ("signed-full", 2u), ("signed-incremental", 1u) })
            {
                using var stream = new MemoryStream();
                var saved = SaveNative.Save(document.Handle, stream, flags);
                var bytes = stream.ToArray();
                File.WriteAllBytes(Path.Combine(directory, $"{name}.pdf"), bytes);
                using var reread = new SaveDocument(bytes);
                Check($"{name}-save", saved.Success && SaveNative.FPDF_GetPageCount(reread.Handle) == 2 && SaveNative.Text(reread.Handle).Contains("Edit after signature"), $"original-prefix={bytes.AsSpan().StartsWith(signed)}; independent CMS validation required; modified-document policy separate.");
            }
        }
        else throw new FileNotFoundException("Run prepare_fixtures.py before the save probe.", signedPath);
        return new { Status = "prototype", Checks = checks,
            Limitations = new[] { "Not exposed as a product API; revision/IsDirty/Undo integration belongs to P4.",
                "Fault injection covers callback I/O/cancellation and Windows locked replacement, not actual disk exhaustion, process crash or network filesystems.",
                "Only R2 security tested; AES/owner-role/permission matrix pending.",
                "Signed incremental update preserves signed bytes only; DocMDP/FieldMDP and trust policy require separate validation. Redaction must never use incremental save." } };
    }

    private static void AddPage(IntPtr document, string text)
    {
        var page = SaveNative.FPDFPage_New(document, SaveNative.FPDF_GetPageCount(document), 300, 400);
        try
        {
            var obj = SaveNative.FPDFPageObj_NewTextObj(document, "Helvetica", 18);
            if (obj == IntPtr.Zero || SaveNative.FPDFText_SetText(obj, text) == 0) throw new InvalidOperationException("Create text failed");
            SaveNative.FPDFPageObj_Transform(obj, 1, 0, 0, 1, 30, 330);
            SaveNative.FPDFPage_InsertObject(page, obj);
            if (SaveNative.FPDFPage_GenerateContent(page) == 0) throw new InvalidOperationException("Generate content failed");
        }
        finally { SaveNative.FPDF_ClosePage(page); }
    }

    private static (bool Success, Exception? Error) AtomicWrite(IntPtr document, string target, long? failAfter = null, CancellationToken token = default)
    {
        var temp = target + "." + Guid.NewGuid().ToString("N") + ".writing";
        var ownsTemp = false;
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                ownsTemp = true;
                var saved = SaveNative.Save(document, stream, failAfter: failAfter, token: token);
                if (!saved.Success) return (false, saved.Error);
                stream.Flush(true);
            }
            using (var validate = new SaveDocument(File.ReadAllBytes(temp)))
                if (SaveNative.FPDF_GetPageCount(validate.Handle) != 3) throw new InvalidDataException("Candidate verification failed");
            token.ThrowIfCancellationRequested();
            File.Replace(temp, target, null);
            return (true, null);
        }
        catch (Exception exception) { return (false, exception); }
        finally { if (ownsTemp && File.Exists(temp)) File.Delete(temp); }
    }
}
