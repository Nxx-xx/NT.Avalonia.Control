using System.Buffers.Binary;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;
using PdfViewer.Tests;
using NT.AvaloniaControl.PdfViewer.Editing;

namespace PdfViewer.Core.Tests;

public sealed class FontSubsettingTests
{
    private static CancellationToken TestToken => TestContext.Current.CancellationToken;
    private static readonly PdfEditRectangle Bounds = new(10, 80, 280, 50);
    private static byte[] SourceFont => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "P0Font.ttf"));

    [Fact]
    public async Task Default_embedding_subsets_the_actual_font_program_and_preserves_appearance_pixels()
    {
        var source = SourceFont;
        var automatic = new PdfEmbeddedTrueTypeFont(source);
        Assert.Equal(PdfFontEmbeddingMode.AutomaticSubset, automatic.EmbeddingMode);
        var subsetPdf = await Create("中文 ABC", automatic);
        var fullPdf = await Create("中文 ABC", new PdfEmbeddedTrueTypeFont(source, PdfFontEmbeddingMode.Full));
        var subsetFont = Assert.Single(ReadFontPrograms(subsetPdf));
        var fullFont = Assert.Single(ReadFontPrograms(fullPdf));
        Assert.Equal(source, fullFont);
        Assert.True(subsetFont.Length < fullFont.Length, "Automatic embedding must reduce actual FontFile2 bytes.");
        Assert.True(GlyphCount(subsetFont) < GlyphCount(fullFont));
        AssertValidChecksums(subsetFont);

        using var subsetReader = await NT.AvaloniaControl.PdfViewer.PdfDocument.LoadAsync(subsetPdf, cancellationToken: TestToken);
        using var fullReader = await NT.AvaloniaControl.PdfViewer.PdfDocument.LoadAsync(fullPdf, cancellationToken: TestToken);
        var subsetImage = await subsetReader.RenderAsync(0, 300, 200, cancellationToken: TestToken);
        var fullImage = await fullReader.RenderAsync(0, 300, 200, cancellationToken: TestToken);
        Assert.Equal(fullImage.Pixels, subsetImage.Pixels);
    }

    [Fact]
    public void Embedding_mode_is_explicit_and_unknown_values_are_rejected()
    {
        Assert.Equal(PdfFontEmbeddingMode.Full, new PdfEmbeddedTrueTypeFont(SourceFont, PdfFontEmbeddingMode.Full).EmbeddingMode);
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfEmbeddedTrueTypeFont(SourceFont, (PdfFontEmbeddingMode)99));
    }

    [Fact]
    public async Task NoSubsetting_permission_is_honored_with_complete_font_embedding()
    {
        var source = SourceFont;
        var os2 = Table(source, "OS/2");
        Write16(source, os2.Offset + 8, 0x0100);
        RepairChecksums(source);
        var output = await Create("ABC", new PdfEmbeddedTrueTypeFont(source));
        Assert.Equal(source, Assert.Single(ReadFontPrograms(output)));
    }

    [Fact]
    public async Task Subset_font_program_is_deterministic_for_the_same_text_and_owned_font()
    {
        var data = SourceFont;
        var font = new PdfEmbeddedTrueTypeFont(data);
        Array.Fill(data, (byte)0);
        var first = Assert.Single(ReadFontPrograms(await Create("中文 ABC", font)));
        var second = Assert.Single(ReadFontPrograms(await Create("中文 ABC", font)));
        Assert.Equal(first, second);
        AssertValidChecksums(first);
    }

    [Fact]
    public async Task Updating_replaces_the_owned_subset_and_removes_the_old_font_from_all_saved_objects()
    {
        await using var edit = await Open();
        var font = new PdfEmbeddedTrueTypeFont(SourceFont);
        var annotation = await edit.AddFreeTextAsync(0, Bounds, "A", font, 20, TestToken);
        var firstProgram = Assert.Single(ReadFontPrograms(await edit.ExportAsync(TestToken)));
        await edit.UpdateFreeTextAsync(annotation.Id, 0, Bounds, "中", font, 20, TestToken);
        var output = await edit.ExportAsync(TestToken);
        var updatedProgram = Assert.Single(ReadFontPrograms(output));
        Assert.False(firstProgram.AsSpan().SequenceEqual(updatedProgram));
        Assert.Equal("中", Assert.Single(await edit.GetFreeTextAnnotationsAsync(0, TestToken)).Text);
        AssertValidChecksums(updatedProgram);
    }

    [Fact]
    public async Task Reusing_a_font_for_multiple_annotations_builds_independent_usable_subsets()
    {
        await using var edit = await Open();
        var font = new PdfEmbeddedTrueTypeFont(SourceFont);
        await edit.AddFreeTextAsync(0, Bounds, "A", font, 20, TestToken);
        await edit.AddFreeTextAsync(0, new PdfEditRectangle(10, 20, 280, 50), "中文", font, 20, TestToken);
        var programs = ReadFontPrograms(await edit.ExportAsync(TestToken));
        Assert.Equal(2, programs.Count);
        Assert.All(programs, program => Assert.True(program.Length < SourceFont.Length));
        Assert.All(programs, AssertValidChecksums);
    }

    [Fact]
    public async Task Missing_glyph_in_automatic_mode_leaves_the_session_unchanged()
    {
        await using var edit = await Open();
        var snapshot = await edit.ExportAsync(TestToken);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => edit.AddFreeTextAsync(0, Bounds, "Z", new PdfEmbeddedTrueTypeFont(SourceFont), 20, TestToken));
        Assert.Contains("U+005A", error.Message);
        Assert.Equal(snapshot, await edit.ExportAsync(TestToken));
        Assert.Equal(0, edit.Revision);
    }

    [Fact]
    public async Task Composite_glyph_dependencies_are_retained_and_render_identically_to_the_full_font()
    {
        var source = Fixture("P0CompositeGlyph.ttf");
        const string text = "한글 ÉéÀ ABC";
        var automaticPdf = await Create(text, new PdfEmbeddedTrueTypeFont(source));
        var fullPdf = await Create(text, new PdfEmbeddedTrueTypeFont(source, PdfFontEmbeddingMode.Full));
        var subset = Assert.Single(ReadFontPrograms(automaticPdf));
        Assert.True(GlyphCount(subset) < GlyphCount(source));
        AssertCompositeClosure(subset);
        AssertValidChecksums(subset);
        using var subsetReader = await NT.AvaloniaControl.PdfViewer.PdfDocument.LoadAsync(automaticPdf, cancellationToken: TestToken);
        using var fullReader = await NT.AvaloniaControl.PdfViewer.PdfDocument.LoadAsync(fullPdf, cancellationToken: TestToken);
        var subsetImage = await subsetReader.RenderAsync(0, 300, 200, cancellationToken: TestToken);
        var fullImage = await fullReader.RenderAsync(0, 300, 200, cancellationToken: TestToken);
        Assert.Equal(fullImage.Pixels, subsetImage.Pixels);
    }

    [Fact]
    public async Task Licensed_NoSubsetting_fixture_keeps_the_original_outline_program()
    {
        var source = Fixture("P0NoSubsetting.ttf");
        var output = await Create("한글 ÉéÀ", new PdfEmbeddedTrueTypeFont(source));
        Assert.Equal(source, Assert.Single(ReadFontPrograms(output)));
    }

    [Theory]
    [InlineData(PdfFontEmbeddingMode.AutomaticSubset)]
    [InlineData(PdfFontEmbeddingMode.Full)]
    public async Task Format12_only_font_preserves_supplementary_CJK_scalars_in_the_actual_appearance(PdfFontEmbeddingMode mode)
    {
        const string text = "字 𠀃𠀋 ABC";
        var output = await Create(text, new PdfEmbeddedTrueTypeFont(Fixture("P0Cmap12Only.ttf"), mode));
        Assert.Equal(text, ReadAppearanceText(output));
        await using var reopened = await PdfEditSession.OpenAsync(output, TestToken);
        Assert.Equal(text, Assert.Single(await reopened.GetFreeTextAnnotationsAsync(0, TestToken)).Text);
        using var reader = await NT.AvaloniaControl.PdfViewer.PdfDocument.LoadAsync(output, cancellationToken: TestToken);
        var image = await reader.RenderAsync(0, 300, 200, cancellationToken: TestToken);
        var darkPixels = 0;
        for (var y = 72; y < 100; y++)
        for (var x = 12; x < 100; x++)
        {
            var position = y * image.Stride + x * 4;
            if (image.Pixels[position] < 128 && image.Pixels[position + 1] < 128 && image.Pixels[position + 2] < 128) darkPixels++;
        }
        Assert.True(darkPixels > 50, "The CJK appearance must paint visible strokes inside its rectangle.");
    }

    [Theory]
    [InlineData(PdfFontEmbeddingMode.AutomaticSubset)]
    [InlineData(PdfFontEmbeddingMode.Full)]
    public async Task Unicode_aliases_sharing_one_glyph_keep_distinct_appearance_character_mappings(PdfFontEmbeddingMode mode)
    {
        var source = Fixture("P0AliasGlyph.ttf");
        var output = await Create("AB", new PdfEmbeddedTrueTypeFont(source, mode));
        Assert.Equal("AB", ReadAppearanceText(output));
        // The fixture deliberately maps A and B to the same outline. Their visual
        // identity must not collapse their different canonical Unicode mappings.
        var repeated = await Create("AA", new PdfEmbeddedTrueTypeFont(source, mode));
        using var aliasReader = await NT.AvaloniaControl.PdfViewer.PdfDocument.LoadAsync(output, cancellationToken: TestToken);
        using var repeatedReader = await NT.AvaloniaControl.PdfViewer.PdfDocument.LoadAsync(repeated, cancellationToken: TestToken);
        var aliasImage = await aliasReader.RenderAsync(0, 300, 200, cancellationToken: TestToken);
        var repeatedImage = await repeatedReader.RenderAsync(0, 300, 200, cancellationToken: TestToken);
        Assert.Equal(repeatedImage.Pixels, aliasImage.Pixels);
    }

    [Fact]
    public async Task Cyclic_composite_requested_glyph_is_rejected_without_committing_an_edit()
    {
        var font = SourceFont;
        var head = Table(font, "head");
        var glyf = Table(font, "glyf");
        var loca = Table(font, "loca");
        var longLocations = Read16(font, head.Offset + 50) == 1;
        var glyph = BmpGlyph(font, 'A');
        var start = longLocations ? checked((int)Read32(font, loca.Offset + glyph * 4)) : Read16(font, loca.Offset + glyph * 2) * 2;
        var end = longLocations ? checked((int)Read32(font, loca.Offset + (glyph + 1) * 4)) : Read16(font, loca.Offset + (glyph + 1) * 2) * 2;
        Assert.True(end - start >= 18, "The fixture needs room for a composite glyph.");
        var position = glyf.Offset + start;
        Write16(font, position, ushort.MaxValue); // numberOfContours = -1.
        Write16(font, position + 10, 3); // Word XY arguments, one component.
        Write16(font, position + 12, checked((ushort)glyph)); // The requested glyph refers to itself.
        Write16(font, position + 14, 0);
        Write16(font, position + 16, 0);
        RepairChecksums(font);
        await using var edit = await Open();
        var snapshot = await edit.ExportAsync(TestToken);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => edit.AddFreeTextAsync(0, Bounds, "A", new PdfEmbeddedTrueTypeFont(font), 20, TestToken));
        Assert.Contains("cycle", error.Message);
        Assert.Equal(snapshot, await edit.ExportAsync(TestToken));
        Assert.Equal(0, edit.Revision);
    }

    private static Task<PdfEditSession> Open() => PdfEditSession.OpenAsync(PdfFixture.SinglePage("Original page", 300, 200), TestToken);

    private static async Task<byte[]> Create(string text, PdfEmbeddedTrueTypeFont font)
    {
        await using var edit = await Open();
        await edit.AddFreeTextAsync(0, Bounds, text, font, 20, TestToken);
        return await edit.ExportAsync(TestToken);
    }

    // Inspect every saved indirect object, including fonts reachable only through stale resources.
    private static List<byte[]> ReadFontPrograms(byte[] pdf)
    {
        using var stream = new MemoryStream(pdf, writable: false);
        using var document = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
        var programs = new List<byte[]>();
        var seen = new HashSet<PdfDictionary>(ReferenceEqualityComparer.Instance);
        foreach (var value in document.Internals.GetAllObjects().OfType<PdfDictionary>())
        {
            var fontFile = value.Elements.GetDictionary("/FontFile2");
            if (fontFile?.Stream is null || !seen.Add(fontFile)) continue;
            programs.Add(fontFile.Stream.UnfilteredValue);
        }
        return programs;
    }

    private static string ReadAppearanceText(byte[] pdf)
    {
        using var stream = new MemoryStream(pdf, writable: false);
        using var document = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
        var annotations = document.Pages[0].Elements.GetArray("/Annots")!;
        var annotation = (PdfDictionary)((PdfReference)annotations.Elements[0]).Value;
        var appearance = annotation.Elements.GetDictionary("/AP")!.Elements.GetDictionary("/N")!;
        var fontResources = appearance.Elements.GetDictionary("/Resources")!.Elements.GetDictionary("/Font")!;
        var font = fontResources.Elements.GetDictionary(fontResources.Elements.KeyNames.Single().Value)!;
        var cmap = System.Text.Encoding.ASCII.GetString(font.Elements.GetDictionary("/ToUnicode")!.Stream.UnfilteredValue);
        var mappings = new Dictionary<int, string>();
        foreach (System.Text.RegularExpressions.Match pair in System.Text.RegularExpressions.Regex.Matches(cmap, @"<([0-9A-Fa-f]{4})>\s*<([0-9A-Fa-f]+)>"))
        {
            var cid = Convert.ToInt32(pair.Groups[1].Value, 16);
            if (cid == 0) continue; // codespacerange is not a character mapping.
            mappings.Add(cid, System.Text.Encoding.BigEndianUnicode.GetString(Convert.FromHexString(pair.Groups[2].Value)));
        }
        var result = new System.Text.StringBuilder();
        var operators = PdfSharp.Pdf.Content.ContentReader.ReadContent(appearance.Stream.UnfilteredValue);
        foreach (var operation in operators.OfType<PdfSharp.Pdf.Content.Objects.COperator>())
        {
            IEnumerable<PdfSharp.Pdf.Content.Objects.CString> strings = operation.Name switch
            {
                "TJ" => ((PdfSharp.Pdf.Content.Objects.CArray)operation.Operands[0]).OfType<PdfSharp.Pdf.Content.Objects.CString>(),
                "Tj" => [(PdfSharp.Pdf.Content.Objects.CString)operation.Operands[0]],
                _ => []
            };
            foreach (var value in strings)
            {
                var bytes = value.Value.Select(character => checked((byte)character)).ToArray();
                Assert.Equal(0, bytes.Length % 2);
                for (var offset = 0; offset < bytes.Length; offset += 2)
                    result.Append(mappings[Read16(bytes, offset)]);
            }
        }
        return result.ToString();
    }

    private static int GlyphCount(byte[] font) => Read16(font, Table(font, "maxp").Offset + 4);
    private static byte[] Fixture(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "FontSubset", name));

    private static int BmpGlyph(byte[] font, int codepoint)
    {
        var cmap = Table(font, "cmap");
        for (var record = 0; record < Read16(font, cmap.Offset + 2); record++)
        {
            var entry = cmap.Offset + 4 + record * 8;
            var map = cmap.Offset + checked((int)Read32(font, entry + 4));
            if (Read16(font, map) != 4) continue;
            var count = Read16(font, map + 6) / 2;
            for (var index = 0; index < count; index++)
            {
                var end = Read16(font, map + 14 + index * 2);
                var start = Read16(font, map + 16 + count * 2 + index * 2);
                if (codepoint < start || codepoint > end) continue;
                var delta = Read16(font, map + 16 + count * 4 + index * 2);
                var position = map + 16 + count * 6 + index * 2;
                var range = Read16(font, position);
                if (range == 0) return (codepoint + delta) & 0xFFFF;
                var glyph = Read16(font, position + range + (codepoint - start) * 2);
                return glyph == 0 ? 0 : (glyph + delta) & 0xFFFF;
            }
        }
        throw new InvalidDataException("Missing test fixture glyph.");
    }

    private static void AssertCompositeClosure(byte[] font)
    {
        var count = GlyphCount(font);
        var glyf = Table(font, "glyf");
        var loca = Table(font, "loca");
        var isLong = Read16(font, Table(font, "head").Offset + 50) == 1;
        int Location(int glyph) => isLong ? checked((int)Read32(font, loca.Offset + glyph * 4)) : Read16(font, loca.Offset + glyph * 2) * 2;
        var composites = 0;
        for (var glyph = 0; glyph < count; glyph++)
        {
            var offset = Location(glyph);
            if (offset == Location(glyph + 1) || unchecked((short)Read16(font, glyf.Offset + offset)) >= 0) continue;
            composites++;
            var position = glyf.Offset + offset + 10;
            ushort flags;
            do
            {
                flags = Read16(font, position);
                var component = Read16(font, position + 2);
                Assert.InRange(component, (ushort)0, (ushort)(count - 1));
                Assert.True(Location(component + 1) > Location(component), "Every retained component must have its outline.");
                position += 4 + ((flags & 1) != 0 ? 4 : 2) + ((flags & 8) != 0 ? 2 : (flags & 0x40) != 0 ? 4 : (flags & 0x80) != 0 ? 8 : 0);
            } while ((flags & 0x20) != 0);
        }
        Assert.True(composites > 0, "The test must exercise actual composite glyphs.");
    }
    private static ushort Read16(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset, 2));
    private static uint Read32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4));
    private static void Write16(byte[] bytes, int offset, ushort value) => BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(offset, 2), value);
    private static void Write32(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset, 4), value);

    private static (int Entry, int Offset, int Length) Table(byte[] bytes, string tag)
    {
        for (var index = 0; index < Read16(bytes, 4); index++)
        {
            var entry = 12 + index * 16;
            if (System.Text.Encoding.ASCII.GetString(bytes, entry, 4) == tag)
                return (entry, checked((int)Read32(bytes, entry + 8)), checked((int)Read32(bytes, entry + 12)));
        }
        throw new InvalidDataException($"Missing TrueType table: {tag}.");
    }

    private static uint Checksum(ReadOnlySpan<byte> bytes)
    {
        uint sum = 0;
        for (var index = 0; index < bytes.Length; index += 4)
        {
            uint word = 0;
            for (var part = 0; part < 4; part++) word = (word << 8) | (index + part < bytes.Length ? bytes[index + part] : 0u);
            sum = unchecked(sum + word);
        }
        return sum;
    }

    private static void AssertValidChecksums(byte[] font)
    {
        Assert.Equal(0xB1B0AFBAu, Checksum(font));
        for (var index = 0; index < Read16(font, 4); index++)
        {
            var entry = 12 + index * 16;
            var offset = checked((int)Read32(font, entry + 8));
            var length = checked((int)Read32(font, entry + 12));
            var data = font.AsSpan(offset, length).ToArray();
            if (font.AsSpan(entry, 4).SequenceEqual("head"u8)) Write32(data, 8, 0);
            Assert.Equal(Read32(font, entry + 4), Checksum(data));
        }
    }

    private static void RepairChecksums(byte[] font)
    {
        var head = Table(font, "head");
        Write32(font, head.Offset + 8, 0);
        for (var index = 0; index < Read16(font, 4); index++)
        {
            var entry = 12 + index * 16;
            var offset = checked((int)Read32(font, entry + 8));
            var length = checked((int)Read32(font, entry + 12));
            Write32(font, entry + 4, Checksum(font.AsSpan(offset, length)));
        }
        Write32(font, head.Offset + 8, unchecked(0xB1B0AFBAu - Checksum(font)));
    }
}
