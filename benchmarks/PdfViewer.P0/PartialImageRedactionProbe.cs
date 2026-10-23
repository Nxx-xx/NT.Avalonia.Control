using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Content;
using PdfSharp.Pdf.Content.Objects;
using PdfSharp.Pdf.IO;
using NT.AvaloniaControl.PdfViewer.Editing;
using SharpDocument = PdfSharp.Pdf.PdfDocument;

namespace PdfViewer.P0;

/// <summary>
/// Fixture-bounded research: lossless pixel erasure, not a production partial-image API.
/// A selected image invocation gets a private replacement stream. Pixel cells whose transformed
/// polygons overlap the selected page rectangle with positive area are replaced by opaque white.
/// The public strict sanitizer supplies fresh-catalog saving and unused-resource cleanup.
/// </summary>
internal static class PartialImageRedactionProbe
{
    private const int Width = 32, Height = 24;
    private const string HiddenTail = "P0_IMAGE_TRAILING_SECRET_8C4E";
    private const string HiddenInfo = "P0_IMAGE_HIDDEN_INFO_2BD9";
    // Real, decodable 32x24 RGB JPEG, generated from an opaque constant-color Pillow image.
    // It is deliberately rejected by encoding policy before attempting lossless sample editing.
    private const string Jpeg32x24 = "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkSEw8UHRofHh0aHBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/2wBDAQkJCQwLDBgNDRgyIRwhMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjL/wAARCAAYACADASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwCnRRRXYcgUUUUAFFFFABRRRQB//9k=";
    private static readonly Matrix Standard = new(192, 0, 0, 144, 40, 60);
    private static readonly Rectangle StandardRegion = new(100, 102, 48, 36);

    internal static async Task<object> RunAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var cases = new List<object>();
        foreach (var definition in new[]
        {
            new Definition("axis-rgb", Standard, StandardRegion),
            new Definition("axis-gray", Standard, StandardRegion, Components: 1),
            new Definition("rotate90", new(0, 192, -144, 0, 260, 40), new(152, 100, 36, 48)),
            new Definition("mirror", new(-192, 0, 0, 144, 260, 60), new(152, 102, 48, 36)),
            new Definition("affine", new(192, 48, 36, 144, 40, 40), new(140, 110, 48, 36)),
            new Definition("shared-same-page", new(192, 0, 0, 144, 20, 40), new(80, 82, 48, 36), SharedSamePage: true),
            new Definition("shared-cross-page", new(192, 0, 0, 144, 20, 40), new(80, 82, 48, 36), SharedCrossPage: true)
        }) cases.Add(await Positive(directory, definition));

        foreach (var refusal in new[] { "jpeg", "soft-mask", "clip", "singular", "unsupported-bpc", "form-xobject" })
            cases.Add(await Refusal(directory, refusal));
        cases.Add(UnsafeControl(directory, "unsafe-overlay", crop: false));
        cases.Add(UnsafeControl(directory, "unsafe-crop", crop: true));
        var caseRows = JsonSerializer.SerializeToElement(cases);
        var checks = caseRows.EnumerateArray().SelectMany(row => row.GetProperty("Checks").EnumerateArray().Select(check => new
        {
            Id = row.GetProperty("Id").GetString() + ":" + check.GetProperty("Id").GetString(),
            Passed = check.GetProperty("Passed").GetBoolean(), Detail = check.GetProperty("Detail").GetString()
        })).ToArray();
        return new
        {
            Status = "bounded-partial-image-feasibility-not-production-api",
            Algorithm = "Affine pixel-cell polygon intersection; positive overlap replaces every component by 255; invocation-private image; public RemoveAllNonPageData full rewrite.",
            FunctionalAssemblySHA256 = Hash(File.ReadAllBytes(typeof(PdfEditSession).Assembly.Location)),
            Cases = cases,
            Checks = checks,
            Outputs = Directory.EnumerateFiles(directory, "*.pdf").OrderBy(Path.GetFileName, StringComparer.Ordinal)
                .Select(path => new { File = Path.GetFileName(path), SHA256 = Hash(File.ReadAllBytes(path)), Bytes = new FileInfo(path).Length }).ToArray(),
            Limitations = new[]
            {
                "Authored 32x24 lossless DeviceRGB or DeviceGray 8-bit images and page-level q/Q/cm/Do only; arbitrary content and nested Forms are refused.",
                "JPEG/JPX/JBIG2, image masks, soft masks, Decode arrays, clipping, transparency, singular transforms and non-default page geometry are refused before mutation.",
                "Intersecting pixel cells are erased conservatively, including their portions outside the region. Exact subpixel vector boundaries require resampling and a separate quality policy.",
                "Shared unselected invocations intentionally retain original visible pixels. The erasure claim is scoped to the selected invocation, not global absence of a repeated image.",
                "The experiment proves pixel-sample removal and immutable original input, not filesystem history erasure, production selection/UI, Linux qualification, throughput or arbitrary-PDF compatibility."
            }
        };
    }

    private static async Task<object> Positive(string directory, Definition definition)
    {
        var input = Fixture(definition);
        var inputName = definition.Id + ".input.pdf";
        var outputName = definition.Id + ".redacted.pdf";
        File.WriteAllBytes(Path.Combine(directory, inputName), input);
        var inputHash = Hash(input);
        var rewritten = RewritePixels(input, 0, 0, definition.Region);
        await using var session = await PdfEditSession.OpenAsync(rewritten.Bytes);
        var sanitation = await session.SanitizeAsync(PdfHiddenInformationPolicy.RemoveAllNonPageData);
        var output = await session.ExportAsync();
        File.WriteAllBytes(Path.Combine(directory, outputName), output);
        var selected = ReadInvocations(output, 0)[0];
        var original = Pattern(definition.Components);
        var expected = original.ToArray();
        var changed = 0;
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
        {
            if (!Overlaps(definition.Transform, x, y, definition.Region)) continue;
            for (var component = 0; component < definition.Components; component++) expected[(y * Width + x) * definition.Components + component] = 255;
            changed++;
        }
        var otherPreserved = true;
        if (definition.SharedSamePage) otherPreserved = ReadInvocations(output, 0)[1].Pixels.SequenceEqual(original);
        if (definition.SharedCrossPage) otherPreserved = ReadInvocations(output, 1)[0].Pixels.SequenceEqual(original);
        var allSamplesTrimmed = true;
        using (var stream = new MemoryStream(output, writable: false))
        using (var document = PdfReader.Open(stream, PdfDocumentOpenMode.Import))
            foreach (var page in document.Pages)
                foreach (var image in ReadInvocations(document, page))
                    allSamplesTrimmed &= image.DecodedLength == Width * Height * definition.Components;
        return new
        {
            definition.Id, Kind = "positive", InputFile = inputName, OutputFile = outputName,
            InputSHA256 = inputHash, OutputSHA256 = Hash(output),
            PageCount = definition.SharedCrossPage ? 2 : 1, PageIndex = 0, InstanceIndex = 0,
            Matrix = definition.Transform.Values, Region = definition.Region, Width, Height, definition.Components,
            definition.SharedSamePage, definition.SharedCrossPage,
            ExpectedRemovedPixelCount = changed, HiddenTail, HiddenInfo,
            SelectedResourceName = selected.Name,
            Checks = new[]
            {
                Check("pixel-samples-rewritten", selected.Pixels.SequenceEqual(expected) && changed > 0 && changed < Width * Height, "Native-reopened selected resource has every overlapping cell white and every other original sample byte unchanged."),
                Check("transform-preserved", selected.Transform.Near(definition.Transform), "The original invocation matrix is preserved, including affine, rotation and reflection cases."),
                Check("instance-isolated", otherPreserved, "Unselected uses of a shared image retain exact original visible sample bytes."),
                Check("public-cleanup-committed", session.Revision == 1 && session.IsDirty && sanitation.PageCount == (definition.SharedCrossPage ? 2 : 1), "The public strict sanitizer reopened and committed the fresh page-only output."),
                Check("hidden-tail-trimmed", allSamplesTrimmed && !Encoding.Latin1.GetString(output).Contains(HiddenTail, StringComparison.Ordinal), "Decoded image bytes end at their declared raster length; source trailing payload is not retained."),
                Check("input-immutable", Hash(input) == inputHash && File.ReadAllBytes(Path.Combine(directory, inputName)).SequenceEqual(input), "The original input array and file remain byte-for-byte unchanged.")
            }
        };
    }

    private static async Task<object> Refusal(string directory, string reason)
    {
        var definition = new Definition("refuse-" + reason, reason == "singular" ? new(192, 0, 96, 0, 40, 60) : Standard, StandardRegion);
        var input = Fixture(definition, reason);
        var inputName = definition.Id + ".input.pdf";
        var outputName = definition.Id + ".redacted.pdf";
        var outputPath = Path.Combine(directory, outputName);
        File.Delete(outputPath); // Never leave a stale successful artifact after a refused request.
        File.WriteAllBytes(Path.Combine(directory, inputName), input);
        var before = Hash(input);
        var refused = false;
        var detail = "";
        try
        {
            var rewritten = RewritePixels(input, 0, 0, definition.Region);
            await using var session = await PdfEditSession.OpenAsync(rewritten.Bytes);
            await session.SanitizeAsync(PdfHiddenInformationPolicy.RemoveAllNonPageData);
            throw new InvalidOperationException("Unsupported image case was accepted.");
        }
        catch (NotSupportedException exception) { refused = true; detail = exception.Message; }
        return new
        {
            definition.Id, Kind = "refusal", InputFile = inputName, OutputFile = (string?)null,
            InputSHA256 = before, OutputSHA256 = (string?)null, PageCount = 1, PageIndex = 0, InstanceIndex = 0,
            Matrix = definition.Transform.Values, Region = definition.Region, Width, Height, Components = 3,
            Reason = reason, RefusalMessage = detail,
            Checks = new[] { Check("unsupported-refused-without-output", refused && !File.Exists(outputPath) && Hash(input) == before && File.ReadAllBytes(Path.Combine(directory, inputName)).SequenceEqual(input), "All geometry/resource validation precedes resource mutation or output publication: " + detail) }
        };
    }

    private static object UnsafeControl(string directory, string id, bool crop)
    {
        var definition = new Definition(id, Standard, StandardRegion);
        var input = Fixture(definition);
        var output = Fixture(definition, crop ? "unsafe-crop" : "unsafe-overlay");
        var inputName = id + ".input.pdf";
        var outputName = id + ".unsafe.pdf";
        File.WriteAllBytes(Path.Combine(directory, inputName), input);
        File.WriteAllBytes(Path.Combine(directory, outputName), output);
        using var bytes = new MemoryStream(output, writable: false);
        using var document = PdfReader.Open(bytes, PdfDocumentOpenMode.Import);
        var image = document.Pages[0].Elements.GetDictionary("/Resources")!.Elements.GetDictionary("/XObject")!.Elements.GetDictionary("/Im0")!;
        var data = image.Stream!.UnfilteredValue;
        var recovered = data.AsSpan(0, Width * Height * 3).SequenceEqual(Pattern(3));
        return new
        {
            Id = id, Kind = "unsafe-control", InputFile = inputName, OutputFile = outputName,
            InputSHA256 = Hash(input), OutputSHA256 = Hash(output), PageCount = 1, PageIndex = 0, InstanceIndex = 0,
            Matrix = Standard.Values, Region = StandardRegion, Width, Height, Components = 3, HiddenTail,
            Checks = new[] { Check("unsafe-control-original-pixels-recoverable", recovered && Encoding.Latin1.GetString(data).Contains(HiddenTail, StringComparison.Ordinal), "Clipping/cropping or an opaque white rectangle changes visibility but leaves all original pixel samples and the trailing payload recoverable.") }
        };
    }

    /// <summary>Private bounded candidate builder; no public production API is added.</summary>
    private static Rewritten RewritePixels(byte[] source, int pageIndex, int instanceIndex, Rectangle region)
    {
        if (!region.Valid) throw new ArgumentException("A finite, positive page rectangle is required.");
        using var bytes = new MemoryStream(source, writable: false);
        using var document = PdfReader.Open(bytes, PdfDocumentOpenMode.Modify);
        if (document.SecuritySettings.IsEncrypted)
            throw new NotSupportedException("Encrypted PDFs are outside the experiment.");
        if (pageIndex < 0 || pageIndex >= document.PageCount) throw new ArgumentOutOfRangeException(nameof(pageIndex));

        // Validate every page and image before changing any object. This is deliberately stricter
        // than a generic PDF editing engine: unknown painting cannot silently bypass the proof.
        var pagePlans = document.Pages.Cast<PdfPage>().Select(page => Plan(document, page)).ToArray();
        var plan = pagePlans[pageIndex];
        if (instanceIndex < 0 || instanceIndex >= plan.Invocations.Count) throw new ArgumentOutOfRangeException(nameof(instanceIndex));
        var target = plan.Invocations[instanceIndex];
        var pixels = target.Pixels.ToArray();
        var changed = 0;
        for (var y = 0; y < target.Height; y++)
        for (var x = 0; x < target.Width; x++)
        {
            if (!Overlaps(target.Transform, x, y, region, target.Width, target.Height)) continue;
            for (var component = 0; component < target.Components; component++) pixels[(y * target.Width + x) * target.Components + component] = 255;
            changed++;
        }
        if (changed == 0 || changed == target.Width * target.Height)
            throw new NotSupportedException("This experiment requires a genuine nonempty partial raster intersection.");

        var replacement = new PdfDictionary(document);
        replacement.Elements.SetName("/Type", "/XObject");
        replacement.Elements.SetName("/Subtype", "/Image");
        replacement.Elements.SetInteger("/Width", target.Width);
        replacement.Elements.SetInteger("/Height", target.Height);
        replacement.Elements.SetInteger("/BitsPerComponent", 8);
        replacement.Elements.SetName("/ColorSpace", target.Components == 3 ? "/DeviceRGB" : "/DeviceGray");
        replacement.CreateStream(pixels);
        document.Internals.AddObject(replacement);

        // Resource containers can be shared: copy both layers and change only this invocation's Do.
        var page = document.Pages[pageIndex];
        var oldResources = page.Elements.GetDictionary("/Resources")!;
        var resources = CopyDictionary(document, oldResources);
        var xobjects = CopyDictionary(document, oldResources.Elements.GetDictionary("/XObject")!);
        var alias = "/NTPI0";
        while (xobjects.Elements[alias] is not null) alias += "_";
        xobjects.Elements[alias] = replacement.Reference!;
        resources.Elements["/XObject"] = xobjects;
        page.Elements["/Resources"] = resources;
        target.Operator.Operands[0] = new CName(alias);
        var content = new PdfDictionary(document);
        content.CreateStream(plan.Sequence.ToContent());
        document.Internals.AddObject(content);
        page.Elements["/Contents"] = content.Reference!;
        using var output = new MemoryStream();
        document.Save(output, closeStream: false);
        return new(output.ToArray(), changed);
    }

    private static PlanResult Plan(SharpDocument document, PdfPage page, bool outputInspection = false)
    {
        if (page.Rotate != 0 || page.Elements["/UserUnit"] is not null && page.Elements.GetReal("/UserUnit") != 1 || !outputInspection && page.Elements["/Group"] is not null)
            throw new NotSupportedException("Page rotation, custom UserUnit or transparency group requires another coordinate/painter policy.");
        var resource = page.Elements.GetDictionary("/Resources") ?? throw new NotSupportedException("Explicit page resources are required.");
        if (resource.Elements.KeyNames.Any(key => key.Value != "/XObject")) throw new NotSupportedException("Only XObject resources are supported by this fixture engine.");
        var xobjects = resource.Elements.GetDictionary("/XObject") ?? throw new NotSupportedException("Missing XObjects.");
        var stream = page.Elements.GetDictionary("/Contents") ?? throw new NotSupportedException("A single explicit content stream is required.");
        var sequence = ContentReader.ReadContent(stream.Stream!.UnfilteredValue);
        var stack = new Stack<Matrix>();
        var transform = Matrix.Identity;
        var invocations = new List<Invocation>();
        foreach (var token in sequence)
        {
            if (token is CComment) continue;
            if (token is not COperator operation) throw new NotSupportedException("Unexpected content token.");
            switch (operation.Name)
            {
                case "q":
                    if (operation.Operands.Count != 0 || stack.Count >= 32) throw new NotSupportedException("Malformed/excessive graphics state stack.");
                    stack.Push(transform); break;
                case "Q":
                    if (operation.Operands.Count != 0 || !stack.TryPop(out transform)) throw new NotSupportedException("Unbalanced graphics state.");
                    break;
                case "cm":
                    if (operation.Operands.Count != 6) throw new NotSupportedException("Malformed matrix.");
                    var values = operation.Operands.Select(Number).ToArray();
                    var matrix = new Matrix(values[0], values[1], values[2], values[3], values[4], values[5]);
                    transform = transform.Compose(matrix);
                    if (!transform.Valid) throw new NotSupportedException("A singular, non-finite or ill-conditioned image transform is not supported.");
                    break;
                case "Do":
                    if (operation.Operands.Count != 1 || operation.Operands[0] is not CName name) throw new NotSupportedException("Malformed XObject invocation.");
                    var image = xobjects.Elements.GetDictionary(name.Name) ?? throw new NotSupportedException("Missing image resource.");
                    if (image.Elements.GetName("/Subtype") != "/Image") throw new NotSupportedException("Nested Forms and non-image XObjects are outside this proof.");
                    if (image.Elements.GetInteger("/BitsPerComponent") != 8) throw new NotSupportedException("Only 8-bit image samples are supported.");
                    var components = image.Elements.GetName("/ColorSpace") switch { "/DeviceRGB" => 3, "/DeviceGray" => 1, _ => throw new NotSupportedException("Only DeviceRGB and DeviceGray are supported.") };
                    foreach (var key in new[] { "/Mask", "/SMask", "/ImageMask", "/Decode", "/Matte", "/SMaskInData", "/DecodeParms" })
                        if (image.Elements[key] is not null) throw new NotSupportedException("Image masking, alternate sample decode or predictors are outside this proof: " + key);
                    if (image.Elements["/Filter"] is not null && image.Elements.GetName("/Filter") != "/FlateDecode") throw new NotSupportedException("Only raw or Flate lossless image samples are supported.");
                    var width = image.Elements.GetInteger("/Width");
                    var height = image.Elements.GetInteger("/Height");
                    if (width is < 1 or > 4096 || height is < 1 or > 4096 || (long)width * height * components > 32 * 1024 * 1024)
                        throw new NotSupportedException("Image dimensions exceed this experiment's bounds.");
                    var decoded = image.Stream?.UnfilteredValue ?? throw new NotSupportedException("Image stream is required.");
                    var length = checked(width * height * components);
                    if (decoded.Length < length || decoded.Length > length + 1024) throw new NotSupportedException("Unexpected raster sample length.");
                    invocations.Add(new(name.Name, transform, decoded.AsSpan(0, length).ToArray(), width, height, components, decoded.Length, operation));
                    break;
                default: throw new NotSupportedException("Unsupported painting or clipping operator: " + operation.Name);
            }
        }
        if (stack.Count != 0 || invocations.Count == 0) throw new NotSupportedException("Unbalanced/empty image painting.");
        return new(sequence, invocations);
    }

    private static List<Invocation> ReadInvocations(byte[] bytes, int index)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var document = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
        return ReadInvocations(document, document.Pages[index]);
    }
    private static List<Invocation> ReadInvocations(SharpDocument document, PdfPage page) => Plan(document, page, outputInspection: true).Invocations;
    private static PdfDictionary CopyDictionary(SharpDocument document, PdfDictionary source)
    {
        var result = new PdfDictionary(document);
        foreach (var key in source.Elements.KeyNames) result.Elements[key.Value] = source.Elements[key.Value];
        return result;
    }
    private static double Number(CObject value) => value switch { CInteger integer => integer.Value, CReal real => real.Value, _ => throw new NotSupportedException("Matrix operand must be numeric.") };

    private static bool Overlaps(Matrix matrix, int x, int y, Rectangle region, int width = Width, int height = Height)
    {
        List<Point> polygon =
        [
            matrix.Map((double)x / width, 1d - (double)y / height),
            matrix.Map((double)(x + 1) / width, 1d - (double)y / height),
            matrix.Map((double)(x + 1) / width, 1d - (double)(y + 1) / height),
            matrix.Map((double)x / width, 1d - (double)(y + 1) / height)
        ];
        polygon = Clip(polygon, p => p.X - region.Left, (a, b, t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t));
        polygon = Clip(polygon, p => region.Left + region.Width - p.X, (a, b, t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t));
        polygon = Clip(polygon, p => p.Y - region.Bottom, (a, b, t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t));
        polygon = Clip(polygon, p => region.Bottom + region.Height - p.Y, (a, b, t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t));
        double area = 0;
        for (var i = 0; i < polygon.Count; i++) { var a = polygon[i]; var b = polygon[(i + 1) % polygon.Count]; area += a.X * b.Y - b.X * a.Y; }
        return Math.Abs(area) > 2e-9;
    }
    private static List<Point> Clip(List<Point> input, Func<Point, double> side, Func<Point, Point, double, Point> crossing)
    {
        var output = new List<Point>();
        if (input.Count == 0) return output;
        var previous = input[^1];
        var previousSide = side(previous);
        foreach (var current in input)
        {
            var currentSide = side(current);
            if ((currentSide >= 0) != (previousSide >= 0)) output.Add(crossing(previous, current, previousSide / (previousSide - currentSide)));
            if (currentSide >= 0) output.Add(current);
            previous = current; previousSide = currentSide;
        }
        return output;
    }

    private static byte[] Pattern(int components)
    {
        var result = new byte[Width * Height * components];
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
        for (var c = 0; c < components; c++) result[(y * Width + x) * components + c] = (byte)(17 + (x * 19 + y * 37 + c * 61) % 211);
        return result;
    }

    private static byte[] Fixture(Definition definition, string special = "")
    {
        static byte[] Text(string text) => Encoding.ASCII.GetBytes(text);
        static byte[] Stream(byte[] bytes, string extra = "")
        {
            using var result = new MemoryStream();
            result.Write(Text($"<< /Length {bytes.Length} {extra} >>\nstream\n")); result.Write(bytes); result.Write(Text("\nendstream")); return result.ToArray();
        }
        var objects = new List<byte[]>();
        int Add(byte[] bytes) { objects.Add(bytes); return objects.Count; }
        int Reserve() => Add([]);
        var catalog = Reserve(); var pages = Reserve(); var first = Reserve();
        var second = definition.SharedCrossPage ? Reserve() : 0;
        var imageId = Reserve();
        var pixels = Pattern(definition.Components).Concat(Text(HiddenTail)).ToArray();
        var imageExtras = $"/Type /XObject /Subtype /Image /Width {Width} /Height {Height} /ColorSpace {(definition.Components == 3 ? "/DeviceRGB" : "/DeviceGray")} /BitsPerComponent {(special == "unsupported-bpc" ? 4 : 8)}";
        if (special == "jpeg") imageExtras += " /Filter /DCTDecode";
        if (special == "soft-mask")
        {
            var mask = Add(Stream(Enumerable.Repeat((byte)255, Width * Height).ToArray(), $"/Type /XObject /Subtype /Image /Width {Width} /Height {Height} /ColorSpace /DeviceGray /BitsPerComponent 8"));
            imageExtras += $" /SMask {mask} 0 R";
        }
        objects[imageId - 1] = special == "form-xobject"
            ? Stream(Text("0 0 1 rg 0 0 1 1 re f"), "/Type /XObject /Subtype /Form /BBox [0 0 1 1] /Resources << >>")
            : Stream(special == "jpeg" ? Convert.FromBase64String(Jpeg32x24) : pixels, imageExtras);
        var content = InvocationText(definition.Transform);
        if (definition.SharedSamePage) content += InvocationText(new(96, 0, 0, 72, 270, 220));
        if (special == "clip") content = "q 60 80 100 100 re W n\n" + content + "Q\n";
        if (special == "unsafe-overlay") content += "1 g 100 102 48 36 re f\n";
        var contentId = Add(Stream(Text(content)));
        var info = Add(Text($"<< /Title ({HiddenInfo}) >>"));
        _ = Add(Stream(Text("P0_IMAGE_UNREACHABLE_STREAM_C8A1")));
        var crop = special == "unsafe-crop" ? " /CropBox [40 60 100 204]" : "";
        string Page(int contentObject) => $"<< /Type /Page /Parent {pages} 0 R /MediaBox [0 0 400 400]{crop} /Resources << /XObject << /Im0 {imageId} 0 R >> >> /Contents {contentObject} 0 R /PrivateSecret ({HiddenInfo}) >>";
        objects[first - 1] = Text(Page(contentId));
        if (second != 0)
        {
            var secondContent = Add(Stream(Text(InvocationText(Standard))));
            objects[second - 1] = Text(Page(secondContent));
        }
        objects[catalog - 1] = Text($"<< /Type /Catalog /Pages {pages} 0 R /PrivateSecret ({HiddenInfo}) >>");
        objects[pages - 1] = Text($"<< /Type /Pages /Count {(second != 0 ? 2 : 1)} /Kids [{first} 0 R {(second != 0 ? second + " 0 R" : "")}] >>");
        using var output = new MemoryStream();
        void Append(string text) => output.Write(Text(text));
        Append("%PDF-1.7\n");
        var offsets = new List<long> { 0 };
        for (var i = 0; i < objects.Count; i++) { offsets.Add(output.Position); Append($"{i + 1} 0 obj\n"); output.Write(objects[i]); Append("\nendobj\n"); }
        var xref = output.Position;
        Append($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) Append(offset.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
        Append($"trailer\n<< /Size {objects.Count + 1} /Root {catalog} 0 R /Info {info} 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return output.ToArray();
    }
    private static string InvocationText(Matrix matrix) => "q " + string.Join(" ", matrix.Values.Select(value => value.ToString("R", CultureInfo.InvariantCulture))) + " cm /Im0 Do Q\n";
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static object Check(string id, bool passed, string detail) => new { Id = id, Passed = passed, Detail = detail };
    private sealed record Definition(string Id, Matrix Transform, Rectangle Region, int Components = 3, bool SharedSamePage = false, bool SharedCrossPage = false);
    private sealed record Rewritten(byte[] Bytes, int ChangedPixels);
    private sealed record PlanResult(CSequence Sequence, List<Invocation> Invocations);
    private sealed record Invocation(string Name, Matrix Transform, byte[] Pixels, int Width, int Height, int Components, int DecodedLength, COperator Operator);
    private readonly record struct Point(double X, double Y);
    private readonly record struct Rectangle(double Left, double Bottom, double Width, double Height)
    {
        internal bool Valid => double.IsFinite(Left) && double.IsFinite(Bottom) && double.IsFinite(Width) && double.IsFinite(Height) && Width > 0 && Height > 0;
    }
    private readonly record struct Matrix(double A, double B, double C, double D, double E, double F)
    {
        internal static Matrix Identity => new(1, 0, 0, 1, 0, 0);
        internal double[] Values => [A, B, C, D, E, F];
        internal bool Valid => Values.All(double.IsFinite) && Values.All(value => Math.Abs(value) <= 1e6) && Math.Abs(A * D - B * C) > 1e-6;
        internal Point Map(double u, double v) => new(A * u + C * v + E, B * u + D * v + F);
        internal Matrix Compose(Matrix m) => new(A * m.A + C * m.B, B * m.A + D * m.B, A * m.C + C * m.D, B * m.C + D * m.D, A * m.E + C * m.F + E, B * m.E + D * m.F + F);
        internal bool Near(Matrix other) => Values.Zip(other.Values).All(pair => Math.Abs(pair.First - pair.Second) < 0.0001);
    }
}
