using System.Diagnostics;
using System.Security.Cryptography;
using PdfViewer.Tests;
using NT.AvaloniaControl.PdfViewer;

namespace PdfViewer.P0;

internal static class BaselineProbe
{
    internal static async Task<object> RunAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var measurements = new List<object>();
        foreach (var (name, bytes) in new[] { ("reading-2", PdfFixture.ReadingDocument()), ("text-1000", PdfFixture.ManyPages(1000)) })
        {
            File.WriteAllBytes(Path.Combine(directory, name + ".pdf"), bytes);
            // First process/library work is retained separately; six warm samples
            // cannot establish a UI p95, a release SLA or an official comparison.
            for (var iteration = 0; iteration < 7; iteration++)
            {
                var allocatedBefore = GC.GetTotalAllocatedBytes();
                var watch = Stopwatch.StartNew();
                var document = await PdfDocument.LoadAsync(bytes);
                var loadMs = watch.Elapsed.TotalMilliseconds;
                watch.Restart();
                var raster = await document.RenderAsync(0, 1200, 1600);
                var renderMs = watch.Elapsed.TotalMilliseconds;
                watch.Restart();
                var results = await document.SearchAsync("Page", maxResults: 10000);
                var searchMs = watch.Elapsed.TotalMilliseconds;
                watch.Restart();
                document.Dispose();
                var disposeMs = watch.Elapsed.TotalMilliseconds;
                var process = Process.GetCurrentProcess();
                process.Refresh();
                measurements.Add(new { Corpus = name, SHA256 = Convert.ToHexString(SHA256.HashData(bytes)),
                    Bytes = bytes.Length, Iteration = iteration, ColdLibrary = name == "reading-2" && iteration == 0,
                    LoadMs = loadMs, RenderMs = renderMs, SearchMs = searchMs, DisposeMs = disposeMs,
                    Hits = results.Count, RasterBytes = raster.Pixels.Length,
                    AllocatedBytes = GC.GetTotalAllocatedBytes() - allocatedBefore,
                    PrivateBytes = process.PrivateMemorySize64, WorkingSetBytes = process.WorkingSet64 });
            }
        }
        return new { Status = "backend-baseline-only", RenderWidth = 1200, RenderHeight = 1600,
            Measurements = measurements, Limitations = new[] { "Synthetic text corpus only; no scanned/vector/mixed/large-image corpus yet.",
                "Load/raster/search/dispose are backend timings, not UI first-present/frame latency; no official runtime, Linux hardware, p95 or pass/fail SLA claim." } };
    }
}
