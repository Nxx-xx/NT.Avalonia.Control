using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NT.AvaloniaControl.PdfViewer;
using NT.AvaloniaControl.PdfViewer.Editing;

namespace PdfViewer.P0;

/// <summary>Public-API observations against pinned, published documents. This is not a writer API.</summary>
internal static class RealCorpusProbe
{
    internal static async Task<object> RunAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var manifestPath = Path.Combine(directory, "sources", "manifest.json");
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
        var checks = new List<object>();
        var cases = new List<object>();
        void Check(string id, bool passed, string detail) => checks.Add(new { Id = id, Passed = passed, Detail = detail });
        var fontPath = Path.Combine(Directory.GetParent(directory)!.FullName, "fonts", "P0AnnotationCjk.ttf");
        if (!File.Exists(fontPath)) fontPath = Path.GetFullPath(Path.Combine("benchmarks", "PdfViewer.P0", "Fixtures", "P0AnnotationCjk.ttf"));
        var font = new PdfEmbeddedTrueTypeFont(await File.ReadAllBytesAsync(fontPath));
        foreach (var source in Property(manifest.RootElement, "Sources").EnumerateArray())
        {
            var id = String(source, "Id");
            if (id.Length == 0 || id.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')) throw new InvalidDataException("Unsafe corpus ID.");
            var file = String(source, "File");
            if (Path.GetFileName(file) != file) throw new InvalidDataException("Corpus files must be flat source basenames.");
            var inputPath = Path.Combine(directory, "sources", file);
            var bytes = await File.ReadAllBytesAsync(inputPath);
            var inputHash = Hash(bytes);
            Check(id + ":source-hash", inputHash.Equals(String(source, "SHA256"), StringComparison.OrdinalIgnoreCase), "Pinned published source bytes; no fixture rewriting.");
            var caseDirectory = Path.Combine(directory, id);
            Directory.CreateDirectory(caseDirectory);
            var operations = new List<object>();
            object? read = null;
            PdfPageInfo[] pages = [];
            PdfTextPage? firstText = null;
            try
            {
                using var document = await PdfDocument.LoadAsync(bytes);
                pages = document.Pages.ToArray();
                firstText = await document.GetTextAsync(0);
                var representative = pages.Length == 1 ? new[] { 0 } : new[] { 0, pages.Length / 2 };
                var evidence = await ReadPages(document, representative, directory, caseDirectory, "input");
                read = new { Outcome = "accepted", PageCount = pages.Length, BookmarkCount = document.Bookmarks.Count, Pages = evidence };
                var expectedPageCount = Property(source, "Pages");
                Check(id + ":read", pages.Length > 0 && (expectedPageCount.ValueKind != JsonValueKind.Number || pages.Length == expectedPageCount.GetInt32()), "Native public reader extracts text and renders representative pages.");
            }
            catch (Exception error)
            {
                read = new { Outcome = "unexpected-failure", ExceptionType = error.GetType().Name, Reason = error.Message };
                Check(id + ":read", false, error.ToString());
            }

            var openAccepted = false;
            try
            {
                await using var edit = await PdfEditSession.OpenAsync(bytes);
                openAccepted = true;
                var snapshot = await edit.ExportAsync();
                var state = new State(Hash(snapshot), edit.Revision, edit.IsDirty);
                AddOperation("open-edit", "accepted", null, null, state, state, null,
                    new { PageCount = edit.PageCount, Meaning = "Unsigned/unencrypted session validation only; not a structural-write compatibility claim." });
                AddOperation("snapshot-export", "accepted", null, null, state, state, null,
                    new { Bytes = snapshot.Length, SHA256 = Hash(snapshot), ByteIdentical = snapshot.SequenceEqual(bytes), Meaning = "Owned byte snapshot; no structural rewrite." });
                Check(id + ":snapshot-copy", snapshot.SequenceEqual(bytes) && !edit.IsDirty && edit.Revision == 0, "No-op export retains exact source bytes without making a structural-save claim.");
                using var stream = new MemoryStream();
                await edit.SaveAsync(stream);
                var saved = stream.ToArray();
                using var reopened = await PdfDocument.LoadAsync(saved);
                AddOperation("save-unmodified", "accepted", null, null, state, new State(Hash(await edit.ExportAsync()), edit.Revision, edit.IsDirty), null,
                    new { Bytes = saved.Length, SHA256 = Hash(saved), ByteIdentical = saved.SequenceEqual(bytes), PageCount = reopened.Pages.Count, Meaning = "Public stream save of unmodified bytes and native reopen; no structural rewrite." });
                Check(id + ":save-unmodified", saved.SequenceEqual(bytes) && reopened.Pages.Count == pages.Length && !edit.IsDirty && edit.Revision == 0, "Actual public stream output reopened; source bytes remain identical.");
            }
            catch (Exception error)
            {
                var outcome = error is NotSupportedException ? "refused" : "unexpected-failure";
                AddOperation("open-edit", outcome, error.GetType().Name, error.Message, new State(inputHash, 0, false), new State(inputHash, 0, false), null, null);
                Check(id + ":open-edit-expected-refusal-type", error is NotSupportedException, error.ToString());
            }

            if (openAccepted && pages.Length > 0)
            {
                var bounds = new PdfEditRectangle(20, 20, Math.Min(300, pages[0].Width - 40), 45);
                await Edit("freetext", async edit =>
                {
                    var annotation = await edit.AddFreeTextAsync(0, bounds, "中文批注 ABC 123", font, 12);
                    return new { AnnotationId = annotation.Id, AnnotationText = annotation.Text, Bounds = bounds, FontSHA256 = Hash(await File.ReadAllBytesAsync(fontPath)), Meaning = "Real structural rewrite: adds an annotation and embedded AP font, preserving existing page content and form structures for independent comparison." };
                });
                await Edit("sanitize", async edit =>
                {
                    var result = await edit.SanitizeAsync(PdfHiddenInformationPolicy.RemoveAllNonPageData);
                    return new { Result = result, Meaning = "Destructive strict painting rewrite removes all forms/annotations and non-page data. Visual annotation/form appearances are deliberately removed." };
                });
                if (id == "gnu-make")
                {
                    var fullPage = new PdfEditRectangle(0, 0, pages[0].Width, pages[0].Height);
                    await Edit("redact-whole-first-page", async edit =>
                    {
                        var result = await edit.ApplyRedactionsAsync([new PdfRedactionRegion(0, fullPage)], PdfHiddenInformationPolicy.RemoveAllNonPageData);
                        return new { Result = result, Region = fullPage, Target = "all-first-page-painting", Meaning = "One pinned published, unrotated page. Full coverage of every first-page object exercises supported whole-object deletion; independent checks must prove the first page blank and preserve other page painting." };
                    }, new { Region = fullPage, Target = "all-first-page-painting" }, [0, pages.Length / 2, pages.Length - 1]);
                }
                var tiny = PickTinyRegion(pages[0], firstText);
                await Edit("redact-tiny-region", async edit =>
                {
                    var result = await edit.ApplyRedactionsAsync([new PdfRedactionRegion(0, tiny.Bounds)], PdfHiddenInformationPolicy.RemoveAllNonPageData);
                    return new { Result = result, Region = tiny.Bounds, Target = tiny.Target, Meaning = "Tests this concrete 1pt region. Native displayed text bounds are converted assuming unrotated/unshifted page; independent structure must determine whether an object was actually intersected. Not a general partial-text proof." };
                }, new { Region = tiny.Bounds, Target = tiny.Target, Meaning = "Tests a concrete 1pt candidate region; a prior sanitizer rejection may occur before object intersection." });
            }
            cases.Add(new { Id = id, Input = new { File = "sources/" + file, SHA256 = inputHash, Bytes = bytes.Length }, Read = read, Operations = operations });

            void AddOperation(string name, string outcome, string? exceptionType, string? reason, State before, State after, object? output, object? details)
            {
                var expected = Property(Property(source, "Expected"), name);
                var matched = expected.ValueKind == JsonValueKind.Object && String(expected, "Outcome") == outcome &&
                    (outcome == "accepted" || (String(expected, "ExceptionType") == exceptionType &&
                        String(expected, "ReasonContains").Length != 0 && (reason ?? "").Contains(String(expected, "ReasonContains"), StringComparison.Ordinal)));
                operations.Add(new { Name = name, Outcome = outcome, ExceptionType = exceptionType, Reason = reason, ExpectationMatched = matched, Before = before, After = after, Output = output, Details = details });
                Check(id + ":" + name + ":locked-expectation", matched, "Exact accepted/refused operation expectation; refusals require exception type and actual reason fragment in the pinned manifest.");
                Check(id + ":" + name + ":no-unexpected-failure", outcome != "unexpected-failure", "Only explicit NotSupportedException is a bounded refusal; other errors remain failures.");
                if (outcome == "refused") Check(id + ":" + name + ":atomic-refusal", before == after, "Refusal leaves bytes, revision and dirty state unchanged.");
            }

            async Task Edit(string name, Func<PdfEditSession, Task<object>> operation, object? refusedDetails = null, int[]? representativeOverride = null)
            {
                await using var edit = await PdfEditSession.OpenAsync(bytes);
                var before = new State(Hash(await edit.ExportAsync()), edit.Revision, edit.IsDirty);
                try
                {
                    var details = await operation(edit);
                    var output = await edit.ExportAsync();
                    var after = new State(Hash(output), edit.Revision, edit.IsDirty);
                    var outputPath = Path.Combine(caseDirectory, name + ".pdf");
                    await File.WriteAllBytesAsync(outputPath, output);
                    using var reopened = await PdfDocument.LoadAsync(output);
                    var representative = representativeOverride ?? (name == "freetext" && pages.Length > 1 ? new[] { 0, pages.Length / 2 } : new[] { 0 });
                    var evidence = await ReadPages(reopened, representative, directory, caseDirectory, name);
                    Check(id + ":" + name + ":commit-and-reopen", after.Revision == before.Revision + 1 && after.IsDirty && reopened.Pages.Count == pages.Length, "Successful rewrite commits exactly one revision and reopens with the original page count.");
                    AddOperation(name, "accepted", null, null, before, after,
                        new { File = Relative(directory, outputPath), SHA256 = Hash(output), Bytes = output.Length, PageCount = reopened.Pages.Count, Pages = evidence }, details);
                }
                catch (Exception error)
                {
                    var after = new State(Hash(await edit.ExportAsync()), edit.Revision, edit.IsDirty);
                    AddOperation(name, error is NotSupportedException ? "refused" : "unexpected-failure", error.GetType().Name, error.Message, before, after, null, refusedDetails);
                }
            }
        }
        return new { Status = "real-published-corpus-operation-boundaries", ManifestFile = "sources/manifest.json", ManifestSHA256 = Hash(await File.ReadAllBytesAsync(manifestPath)),
            FunctionalAssemblySHA256 = Hash(await File.ReadAllBytesAsync(typeof(PdfEditSession).Assembly.Location)), Cases = cases, Checks = checks,
            Limitations = new[] { "Exact source revisions and sampled pages only; no arbitrary PDF compatibility claim.", "Read/render, owned snapshot export, no-op stream save, annotation structural rewrite and destructive sanitization are distinct capabilities.", "Whole-object public redaction is bounded; CJK cluster and partial-image algorithms remain separate benchmark prototypes.", "Single Windows native runtime; no Linux, IME, external editor, UI latency or memory-parity claim." } };
    }

    private sealed record State(string SHA256, long Revision, bool IsDirty);
    private static JsonElement Property(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object ? value.EnumerateObject().FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Value : default;
    private static string String(JsonElement value, string name) => Property(value, name).ValueKind == JsonValueKind.String ? Property(value, name).GetString()! : "";
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static string Relative(string root, string path) => Path.GetRelativePath(root, path).Replace('\\', '/');

    private static (PdfEditRectangle Bounds, string Target) PickTinyRegion(PdfPageInfo page, PdfTextPage? text)
    {
        var target = text?.Characters.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c.Text) && c.Bounds.Width > 2 && c.Bounds.Height > 2 &&
            c.Bounds.Left > 2 && c.Bounds.Top > 2 && c.Bounds.Right < page.Width - 2 && c.Bounds.Bottom < page.Height - 2);
        return target is null ? (new PdfEditRectangle(page.Width / 2, page.Height / 2, 1, 1), "no-usable-native-character-bounds; page-center-candidate") :
            (new PdfEditRectangle(target.Bounds.Center.X - .5, page.Height - target.Bounds.Center.Y - .5, 1, 1), $"native-character-{target.Index}:{target.Text}");
    }

    private static async Task<object[]> ReadPages(PdfDocument document, int[] indices, string root, string directory, string prefix)
    {
        var results = new List<object>();
        foreach (var index in indices)
        {
            var page = document.Pages[index];
            var text = await document.GetTextAsync(index);
            var textPath = Path.Combine(directory, $"{prefix}-page-{index + 1}.txt");
            await File.WriteAllTextAsync(textPath, text.Text, new UTF8Encoding(false));
            var scale = 500 / Math.Max(page.Width, page.Height);
            var rendered = await document.RenderAsync(index, Math.Max(1, (int)Math.Round(page.Width * scale)), Math.Max(1, (int)Math.Round(page.Height * scale)));
            var renderPath = Path.Combine(directory, $"{prefix}-page-{index + 1}.png");
            WritePng(renderPath, rendered);
            results.Add(new { Index = index, Width = page.Width, Height = page.Height, TextFile = Relative(root, textPath), TextSHA256 = Hash(await File.ReadAllBytesAsync(textPath)), TextLength = text.Text.Length, Characters = text.Characters.Count,
                RenderFile = Relative(root, renderPath), RenderSHA256 = Hash(await File.ReadAllBytesAsync(renderPath)), PixelSHA256 = Hash(rendered.Pixels), RenderWidth = rendered.Width, RenderHeight = rendered.Height });
        }
        return results.ToArray();
    }

    private static void WritePng(string path, PdfRenderedPage image)
    {
        using var file = File.Create(path);
        file.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, image.Width); BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), image.Height); header[8] = 8; header[9] = 2;
        Chunk("IHDR", header);
        using var data = new MemoryStream();
        using (var compressed = new ZLibStream(data, CompressionLevel.Optimal, true))
        {
            var row = new byte[checked(image.Width * 3 + 1)];
            for (var y = 0; y < image.Height; y++)
            {
                for (var x = 0; x < image.Width; x++)
                {
                    var offset = y * image.Stride + x * 4;
                    row[x * 3 + 1] = image.Pixels[offset + 2]; row[x * 3 + 2] = image.Pixels[offset + 1]; row[x * 3 + 3] = image.Pixels[offset];
                }
                compressed.Write(row);
            }
        }
        Chunk("IDAT", data.ToArray()); Chunk("IEND", []);
        void Chunk(string type, byte[] bytes)
        {
            Span<byte> size = stackalloc byte[4]; BinaryPrimitives.WriteInt32BigEndian(size, bytes.Length); file.Write(size);
            var kind = Encoding.ASCII.GetBytes(type); file.Write(kind); file.Write(bytes);
            uint crc = uint.MaxValue;
            foreach (var value in kind.Concat(bytes))
            {
                crc ^= value;
                for (var i = 0; i < 8; i++) crc = (crc & 1) == 0 ? crc >> 1 : (crc >> 1) ^ 0xedb88320;
            }
            BinaryPrimitives.WriteUInt32BigEndian(size, ~crc); file.Write(size);
        }
    }
}
