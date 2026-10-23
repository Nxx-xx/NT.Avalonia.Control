using System.Security.Cryptography;
using NT.AvaloniaControl.PdfViewer.Interop;

namespace NT.AvaloniaControl.PdfViewer.Editing;

public sealed partial class PdfEditSession
{
    /// <summary>
    /// Removes hidden/non-page data using an explicit policy and commits only after structural and
    /// native reopen checks. Removes annotation/form appearances as well as their stored values.
    /// The operation is a complete rewrite; signed and encrypted input is refused at session open.
    /// </summary>
    public Task<PdfSanitizationResult> SanitizeAsync(PdfHiddenInformationPolicy policy,
        CancellationToken cancellationToken = default) => SanitizeCoreAsync([], policy, cancellationToken);

    /// <summary>
    /// Deletes fully covered page objects, prunes unused resources and all non-page data, performs a
    /// full rewrite, and audits the candidate before committing it. Regions use bottom-left PDF
    /// coordinates. No opaque cover is painted. Partial-object intersections and unsupported retained
    /// painting are refused; any failure or cancellation leaves bytes, revision and dirty state intact.
    /// </summary>
    public Task<PdfSanitizationResult> ApplyRedactionsAsync(IReadOnlyList<PdfRedactionRegion> regions,
        PdfHiddenInformationPolicy policy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(regions);
        if (regions.Count is 0 or > 1024)
            throw new ArgumentException("Supply between 1 and 1024 whole-object redaction regions.", nameof(regions));
        // Callers can mutate their list while the transaction awaits the session gate.
        var owned = regions.ToArray();
        foreach (var region in owned)
        {
            ArgumentNullException.ThrowIfNull(region);
            ValidatePage(region.PageIndex);
            region.Bounds.Validate();
        }
        return SanitizeCoreAsync(owned, policy, cancellationToken);
    }

    private async Task<PdfSanitizationResult> SanitizeCoreAsync(IReadOnlyList<PdfRedactionRegion> regions,
        PdfHiddenInformationPolicy policy, CancellationToken token)
    {
        if (policy != PdfHiddenInformationPolicy.RemoveAllNonPageData)
            throw new ArgumentOutOfRangeException(nameof(policy), "An explicit supported hidden-information removal policy is required.");
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            EnsureUsable();
            var source = _bytes;
            // First reject unsupported retained painting and discard interactive/hidden surfaces.
            // Native object editing then sees exactly the page content this policy will retain.
            var candidate = await Task.Run(() => PdfHiddenInformationSanitizer.Rewrite(source, token), token).ConfigureAwait(false);
            if (regions.Count != 0)
            {
                var cleanInput = candidate;
                candidate = await PdfiumRuntime.RunAsync(() => PdfContentRedactor.Apply(cleanInput, regions, token), token).ConfigureAwait(false);
                // PDFium can retain now-unused resources. Rebuild their live closure after deletion.
                var nativeOutput = candidate;
                candidate = await Task.Run(() => PdfHiddenInformationSanitizer.Rewrite(nativeOutput, token), token).ConfigureAwait(false);
            }
            var auditedOutput = candidate;
            await Task.Run(() => PdfHiddenInformationSanitizer.Audit(auditedOutput, token), token).ConfigureAwait(false);
            if (await PdfWriteValidation.ValidateAsync(candidate, token).ConfigureAwait(false) != PageCount)
                throw new InvalidDataException("Sanitization changed the page count.");
            if (regions.Count != 0)
                await PdfiumRuntime.RunAsync(() =>
                {
                    PdfContentRedactor.Audit(auditedOutput, regions, token);
                    return true;
                }, token).ConfigureAwait(false);
            var inputHash = Convert.ToHexString(SHA256.HashData(source));
            var outputHash = Convert.ToHexString(SHA256.HashData(candidate));
            token.ThrowIfCancellationRequested();
            EnsureUsable();
            _bytes = candidate;
            var revision = Interlocked.Increment(ref _revision);
            return new PdfSanitizationResult(revision, PageCount, regions.Count, policy, inputHash, outputHash);
        }
        finally { _gate.Release(); }
    }
}
