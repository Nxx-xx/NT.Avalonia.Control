using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using NT.AvaloniaControl.PdfViewer;
using NT.AvaloniaControl.PdfViewer.Editing;

namespace PdfViewer.P0;

internal static class ProductionFontSubsetProbe
{
    internal static async Task<object> RunAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var corpusPath = Path.Combine(directory, "corpus.json");
        using var corpus = JsonDocument.Parse(File.ReadAllBytes(corpusPath));
        var checks = new List<object>();
        void Check(string id, bool passed, object detail) => checks.Add(new { Id = id, Passed = passed, Detail = detail });
        var input = PdfViewer.Tests.PdfFixture.SinglePage("KEEP FONT SUBSET ORIGINAL", 600, 300);
        File.WriteAllBytes(Path.Combine(directory, "input.pdf"), input);
        var cases = new List<object>();
        foreach (var item in corpus.RootElement.GetProperty("Cases").EnumerateArray())
        {
            var id = item.GetProperty("Id").GetString()!;
            var fontPath = Path.Combine(directory, item.GetProperty("File").GetString()!);
            var bytes = File.ReadAllBytes(fontPath);
            var text = item.GetProperty("Text").GetString()!;
            var updated = item.GetProperty("UpdatedText").GetString()!;
            Check(id + ":frozen-source", Convert.ToHexString(SHA256.HashData(bytes)) == item.GetProperty("SHA256").GetString(), new { SourceBytes = bytes.Length });
            var stages = new List<object>();
            foreach (var mode in new[] { PdfFontEmbeddingMode.AutomaticSubset, PdfFontEmbeddingMode.Full })
            {
                var label = mode == PdfFontEmbeddingMode.Full ? "full" : "auto";
                var font = new PdfEmbeddedTrueTypeFont(bytes, mode);
                await using var session = await PdfEditSession.OpenAsync(input);
                var elapsed = Stopwatch.StartNew();
                var allocated = GC.GetTotalAllocatedBytes();
                var annotation = await session.AddFreeTextAsync(0, new PdfEditRectangle(25, 130, 550, 70), text, font, 18);
                elapsed.Stop();
                var created = await session.ExportAsync();
                var createdFile = $"{id}-{label}-created.pdf";
                File.WriteAllBytes(Path.Combine(directory, createdFile), created);
                stages.Add(new { File = createdFile, Stage = "created", Mode = label, Text = text, Milliseconds = elapsed.Elapsed.TotalMilliseconds,
                    ManagedAllocatedBytes = GC.GetTotalAllocatedBytes() - allocated, WorkingSetBytes = Environment.WorkingSet,
                    Revision = session.Revision, AnnotationId = annotation.Id });
                await using var reopened = await PdfEditSession.OpenAsync(created);
                var restored = (await reopened.GetFreeTextAnnotationsAsync(0)).Single();
                Check(id + ":" + label + ":persisted-id-text", restored.Id == annotation.Id && restored.Text == text, new { restored.Id, restored.Text });
                elapsed.Restart();
                allocated = GC.GetTotalAllocatedBytes();
                await reopened.UpdateFreeTextAsync(restored.Id, 0, new PdfEditRectangle(25, 130, 550, 70), updated, font, 18);
                elapsed.Stop();
                var edited = await reopened.ExportAsync();
                var editedFile = $"{id}-{label}-updated.pdf";
                File.WriteAllBytes(Path.Combine(directory, editedFile), edited);
                stages.Add(new { File = editedFile, Stage = "updated", Mode = label, Text = updated, Milliseconds = elapsed.Elapsed.TotalMilliseconds,
                    ManagedAllocatedBytes = GC.GetTotalAllocatedBytes() - allocated, WorkingSetBytes = Environment.WorkingSet,
                    Revision = reopened.Revision, AnnotationId = restored.Id });
                using var reader = await NT.AvaloniaControl.PdfViewer.PdfDocument.LoadAsync(edited);
                var rendered = await reader.RenderAsync(0, 600, 300);
                Check(id + ":" + label + ":native-render", rendered.Pixels.Any(value => value < 128), new { rendered.Width, rendered.Height });
            }
            cases.Add(new { Id = id, FontFile = item.GetProperty("File").GetString(), SourceSHA256 = Convert.ToHexString(SHA256.HashData(bytes)),
                RealFullFont = item.GetProperty("RealFullFont").GetBoolean(), Purpose = item.GetProperty("Purpose").GetString(),
                NoSubsetting = (item.GetProperty("FsType").GetInt32() & 0x100) != 0, Stages = stages });
        }
        return new { Status = "production-managed-font-subsetting", CorpusSHA256 = Hash(corpusPath),
            FunctionalAssemblySHA256 = Hash(typeof(PdfEditSession).Assembly.Location), Checks = checks, Cases = cases,
            Outputs = Directory.EnumerateFiles(directory, "*.pdf").OrderBy(path => path).Select(path => new { File = Path.GetFileName(path), SHA256 = Hash(path), Bytes = new FileInfo(path).Length }).ToArray(),
            MeasurementScope = "Single Windows process observations after each operation; no repetition, no process peak sampling, no official SLA/performance parity claim.",
            Limitations = new[] { "Three licensed static TrueType families and explicit synthetic edge fixtures; no universal CJK/font compatibility claim.",
                "Single line with precomposed characters; shaping, vertical writing, variation sequences, variable fonts, TTC and CFF remain outside the supported contract.",
                "Linux/Kylin physical, UI/IME, native external-editor editability and official viewer comparison remain unverified." } };
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
