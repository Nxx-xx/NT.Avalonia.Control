using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using Avalonia.Media.TextFormatting;
using NT.AvaloniaControl.Markdown;

namespace Markdown.Headless.Tests;

public sealed class DynamicFontCollectionLayoutTests
{
    private const string Source = "MMMM WWWW iii office 0123456789";

    [Fact]
    public Task ReplacingACollectionAtTheSameKeyKeepsRemeasureEquivalentToNativeTextBlock() =>
        HeadlessTestScope.Run(() =>
        {
            using var fonts = new ReplacementFonts();
            var actual = Create<MarkdownTextPresenter>(fonts.Family);
            var native = Create<TextBlock>(fonts.Family);
            Layout(actual);
            Layout(native);
            AssertFaceAndGeometry(native, actual, fonts.Regular);

            fonts.ReplaceWithBold();
            actual.InvalidateMeasure();
            native.InvalidateMeasure();
            Layout(actual);
            Layout(native);
            AssertFaceAndGeometry(native, actual, fonts.Bold);
        });

    [Fact]
    public Task ReplacingACollectionBetweenMeasureAndArrangeKeepsNativeFaceAndFiniteLayoutMetadata() =>
        HeadlessTestScope.Run(() =>
        {
            using var fonts = new ReplacementFonts();
            var actual = Create<MarkdownTextPresenter>(fonts.Family);
            var native = Create<TextBlock>(fonts.Family);
            actual.Measure(new Size(800, double.PositiveInfinity));
            native.Measure(new Size(800, double.PositiveInfinity));
            AssertFace(native, fonts.Regular);
            AssertFace(actual, fonts.Regular);

            fonts.ReplaceWithBold();
            // Preserve the ordinary native Arrange operation. No remeasure,
            // property toggle, artificial cache reset or replacement control.
            actual.Arrange(new Rect(0, 0, 800, 200));
            native.Arrange(new Rect(0, 0, 800, 200));
            AssertFaceAndGeometry(native, actual, fonts.Bold);
            Assert.True(double.IsFinite(native.TextLayout.MaxHeight));
            Assert.Equal(native.TextLayout.MaxHeight, actual.TextLayout.MaxHeight);
        });

    [Fact]
    public Task FreshInlineMeasureAfterCollectionReplacementMatchesTheNewNativeFace() =>
        HeadlessTestScope.Run(() =>
        {
            using var fonts = new ReplacementFonts();
            var actual = CreateInline<MarkdownTextPresenter>(fonts.Family);
            var native = CreateInline<TextBlock>(fonts.Family);
            Layout(actual);
            Layout(native);
            AssertFaceAndGeometry(native, actual, fonts.Regular);

            fonts.ReplaceWithBold();
            actual.InvalidateMeasure();
            native.InvalidateMeasure();
            Layout(actual);
            Layout(native);
            AssertFaceAndGeometry(native, actual, fonts.Bold);
        });

    [Fact]
    public Task WrappedPlainLfMeasureThenCollectionReplacementStillResolvesFreshArrangeFonts() =>
        HeadlessTestScope.Run(() =>
        {
            using var fonts = new ReplacementFonts();
            const string source = "\n" + Source + "\n\n";
            var actual = Create<MarkdownTextPresenter>(fonts.Family);
            var native = Create<TextBlock>(fonts.Family);
            actual.Text = source;
            actual.TextWrapping = TextWrapping.Wrap;
            // The newline-free native control is the independent glyph/face
            // oracle. Native raw-LF glyph metrics count NewLineLength, whereas
            // its explicit TextEndOfLine markers do not; that representation
            // metadata is not an oracle for fresh FontManager resolution.
            actual.Measure(new Size(800, double.PositiveInfinity));
            native.Measure(new Size(800, double.PositiveInfinity));
            AssertFace(native, fonts.Regular);
            AssertFace(actual, fonts.Regular);

            fonts.ReplaceWithBold();
            actual.Arrange(new Rect(0, 0, 800, 200));
            native.Arrange(new Rect(0, 0, 800, 200));
            AssertFace(native, fonts.Bold);
            AssertFace(actual, fonts.Bold);
            var content = actual.TextLayout.TextLines[1];
            var reference = Assert.Single(native.TextLayout.TextLines);
            Assert.Equal(new[] { 0, 1, Source.Length + 2, Source.Length + 3 },
                actual.TextLayout.TextLines.Select(line => line.FirstTextSourceIndex));
            Assert.Equal(Source.Length + 1, content.Length);
            Assert.Equal(reference.Width, content.Width, 6);
            Assert.Equal(reference.WidthIncludingTrailingWhitespace, content.WidthIncludingTrailingWhitespace, 6);
            Assert.Equal(reference.Height, content.Height, 6);
            Assert.Equal(reference.Baseline, content.Baseline, 6);
            Assert.Equal(native.TextLayout.MaxWidth, actual.TextLayout.MaxWidth);
            Assert.Equal(native.TextLayout.MaxHeight, actual.TextLayout.MaxHeight);
            for (var position = 0; position <= Source.Length; position++)
            {
                var expected = native.TextLayout.HitTestTextPosition(position);
                var actualHit = actual.TextLayout.HitTestTextPosition(position + 1);
                Assert.Equal(expected.X, actualHit.X, 6);
                Assert.Equal(expected.Width, actualHit.Width, 6);
                Assert.Equal(expected.Height, actualHit.Height, 6);
            }
            Assert.Equal(source, actual.Text);
            Assert.Equal(0, actual.Inlines?.Count ?? 0);
        });

    [Fact]
    public Task InlineMeasureThenCollectionReplacementPreservesTheFaceAlreadyUsedByNativeArrange() =>
        HeadlessTestScope.Run(() =>
        {
            using var fonts = new ReplacementFonts();
            var actual = CreateInline<MarkdownTextPresenter>(fonts.Family);
            var native = CreateInline<TextBlock>(fonts.Family);
            actual.Measure(new Size(800, double.PositiveInfinity));
            native.Measure(new Size(800, double.PositiveInfinity));
            AssertFace(native, fonts.Regular);
            AssertFace(actual, fonts.Regular);

            fonts.ReplaceWithBold();
            // Native Arrange reuses the measured inline TextRunProperties (A)
            // while its new default paragraph properties may resolve face B.
            // Cloning those already used inline properties would change behavior.
            actual.Arrange(new Rect(0, 0, 800, 200));
            native.Arrange(new Rect(0, 0, 800, 200));
            AssertFaceAndGeometry(native, actual, fonts.Regular);
            Assert.True(double.IsFinite(native.TextLayout.MaxHeight));
        });

    private static T Create<T>(FontFamily family) where T : TextBlock, new() => new()
    {
        Text = Source,
        FontFamily = family,
        FontSize = 24,
        FontWeight = FontWeight.Normal,
        FontStyle = FontStyle.Normal,
        FontStretch = FontStretch.Normal,
        TextWrapping = TextWrapping.NoWrap,
    };

    private static T CreateInline<T>(FontFamily family) where T : TextBlock, new()
    {
        var text = Create<T>(family);
        text.Text = null;
        text.Inlines!.Add(new Span
        {
            Inlines =
            {
                new Run("MMMM "),
                new Run("WWWW iii"),
                new Span { Inlines = { new Run(" office 0123456789") } },
            },
        });
        return text;
    }

    private static void Layout(TextBlock text)
    {
        text.Measure(new Size(800, double.PositiveInfinity));
        text.Arrange(new Rect(0, 0, 800, 200));
    }

    private static void AssertFace(TextBlock text, GlyphTypeface expected)
    {
        var runs = text.TextLayout.TextLines.SelectMany(line => line.TextRuns).OfType<ShapedTextRun>().ToArray();
        Assert.NotEmpty(runs);
        Assert.All(runs, run => Assert.Same(expected, run.GlyphRun.GlyphTypeface));
    }

    private static void AssertFaceAndGeometry(TextBlock native, TextBlock actual, GlyphTypeface expected)
    {
        // A changed native face is the primary oracle even on a headless backend
        // where two font faces could happen to report identical dimensions.
        AssertFace(native, expected);
        AssertFace(actual, expected);
        Assert.Equal(native.Bounds.Size, actual.Bounds.Size);
        Assert.Equal(native.DesiredSize, actual.DesiredSize);
        Assert.Equal(native.TextLayout.MaxWidth, actual.TextLayout.MaxWidth);
        Assert.Equal(native.TextLayout.MaxHeight, actual.TextLayout.MaxHeight);
        Assert.Equal(native.TextLayout.Width, actual.TextLayout.Width, 6);
        Assert.Equal(native.TextLayout.WidthIncludingTrailingWhitespace, actual.TextLayout.WidthIncludingTrailingWhitespace, 6);
        Assert.Equal(native.TextLayout.Height, actual.TextLayout.Height, 6);
        Assert.Equal(native.TextLayout.Baseline, actual.TextLayout.Baseline, 6);
        Assert.Equal(native.TextLayout.TextLines.Count, actual.TextLayout.TextLines.Count);
        for (var index = 0; index < native.TextLayout.TextLines.Count; index++)
        {
            var expectedLine = native.TextLayout.TextLines[index];
            var actualLine = actual.TextLayout.TextLines[index];
            Assert.Equal(expectedLine.FirstTextSourceIndex, actualLine.FirstTextSourceIndex);
            Assert.Equal(expectedLine.Length, actualLine.Length);
            Assert.Equal(expectedLine.NewLineLength, actualLine.NewLineLength);
            Assert.Equal(expectedLine.TrailingWhitespaceLength, actualLine.TrailingWhitespaceLength);
            Assert.Equal(expectedLine.Width, actualLine.Width, 6);
            Assert.Equal(expectedLine.Height, actualLine.Height, 6);
            Assert.Equal(expectedLine.Baseline, actualLine.Baseline, 6);
        }
        for (var position = 0; position <= Source.Length; position++)
        {
            var hitRect = native.TextLayout.HitTestTextPosition(position);
            Assert.Equal(hitRect, actual.TextLayout.HitTestTextPosition(position));
            var point = new Point(hitRect.X + .1, hitRect.Y + hitRect.Height / 2);
            Assert.Equal(native.TextLayout.HitTestPoint(point).TextPosition, actual.TextLayout.HitTestPoint(point).TextPosition);
        }
        Assert.Equal(native.TextLayout.HitTestTextRange(2, 12).ToArray(), actual.TextLayout.HitTestTextRange(2, 12).ToArray());
    }

    private sealed class ReplacementFonts : IDisposable
    {
        private readonly FontManager _manager = FontManager.Current;
        private readonly EmbeddedFontCollection _assets;
        private readonly Uri _key = new("fonts:MarkdownDynamic" + Guid.NewGuid().ToString("N"));
        private readonly SingleFaceCollection _regularCollection;

        internal ReplacementFonts()
        {
            // Avalonia.Headless 12.0.3 already brings this asset assembly through
            // its locked transitive dependency. No new package or local font file.
            _assets = new EmbeddedFontCollection(new Uri("fonts:MarkdownDynamicAssets" + Guid.NewGuid().ToString("N")),
                new Uri("avares://Avalonia.Fonts.Inter/Assets"));
            Assert.True(_assets.TryGetGlyphTypeface("Inter", FontStyle.Normal, FontWeight.Normal, FontStretch.Normal, out var regular));
            Assert.True(_assets.TryGetGlyphTypeface("Inter", FontStyle.Normal, FontWeight.Bold, FontStretch.Normal, out var bold));
            Regular = Assert.IsType<GlyphTypeface>(regular);
            Bold = Assert.IsType<GlyphTypeface>(bold);
            Assert.NotSame(Regular, Bold);
            Family = new FontFamily(_key.AbsoluteUri + "#Inter");
            _regularCollection = new(_key, Family, Regular);
            _manager.AddFontCollection(_regularCollection);
        }

        internal FontFamily Family { get; }
        internal GlyphTypeface Regular { get; }
        internal GlyphTypeface Bold { get; }

        internal void ReplaceWithBold()
        {
            _manager.AddFontCollection(new SingleFaceCollection(_key, Family, Bold));
            Assert.True(_regularCollection.Disposed);
            Assert.True(_manager.TryGetGlyphTypeface(new Typeface(Family), out var replacement));
            Assert.Same(Bold, replacement);
        }

        public void Dispose()
        {
            _manager.RemoveFontCollection(_key);
            ((IDisposable)_assets).Dispose();
        }
    }

    // The test owns both faces through the asset collection. Replacing a wrapper
    // must dispose that wrapper while the already measured face remains usable.
    private sealed class SingleFaceCollection(Uri key, FontFamily family, GlyphTypeface face) : IFontCollection
    {
        public Uri Key => key;
        public int Count => 1;
        public FontFamily this[int index] => index == 0 ? family : throw new ArgumentOutOfRangeException(nameof(index));
        internal bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;

        public bool TryGetGlyphTypeface(string familyName, FontStyle style, FontWeight weight, FontStretch stretch,
            [NotNullWhen(true)] out GlyphTypeface? glyphTypeface)
        {
            glyphTypeface = !Disposed && familyName == "Inter" ? face : null;
            return glyphTypeface is not null;
        }

        public bool TryMatchCharacter(int codepoint, FontStyle fontStyle, FontWeight fontWeight, FontStretch fontStretch,
            string? familyName, CultureInfo? culture, out Typeface typeface)
        {
            if (!Disposed && face.CharacterToGlyphMap.TryGetGlyph(codepoint, out _))
            {
                typeface = new(family, fontStyle, fontWeight, fontStretch);
                return true;
            }
            typeface = default;
            return false;
        }

        public bool TryGetFamilyTypefaces(string familyName, [NotNullWhen(true)] out IReadOnlyList<Typeface>? familyTypefaces)
        {
            familyTypefaces = !Disposed && familyName == "Inter" ? [new Typeface(family)] : null;
            return familyTypefaces is not null;
        }

        public bool TryCreateSyntheticGlyphTypeface(GlyphTypeface glyphTypeface, FontStyle style, FontWeight weight,
            FontStretch stretch, [NotNullWhen(true)] out GlyphTypeface? syntheticGlyphTypeface)
        {
            syntheticGlyphTypeface = null;
            return false;
        }

        public bool TryGetNearestMatch(string familyName, FontStyle style, FontWeight weight, FontStretch stretch,
            [NotNullWhen(true)] out GlyphTypeface? glyphTypeface) =>
            TryGetGlyphTypeface(familyName, style, weight, stretch, out glyphTypeface);

        public IEnumerator<FontFamily> GetEnumerator() => ((IEnumerable<FontFamily>)new[] { family }).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
