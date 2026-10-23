using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using NT.AvaloniaControl.Markdown;

namespace Markdown.Headless.Tests;

public sealed class PlainWrappedLineBreakLayoutTests
{
    [Fact]
    public Task PlainLeadingConsecutiveAndTrailingLfKeepNativeMetricsAndEverySourcePosition() =>
        HeadlessTestScope.Run(() =>
        {
            foreach (var wrapping in new[] { TextWrapping.Wrap, TextWrapping.WrapWithOverflow })
            foreach (var source in new[] { "\nfirst", "first\n\nlast", "first\n", "\nfirst\n\nlast\n", "\n", "\n\n" })
            foreach (var (height, spacing) in new[] { (double.NaN, 0d), (27d, 5d) })
            {
                var actual = Plain(source, wrapping);
                actual.LineHeight = height;
                actual.LineSpacing = spacing;
                Layout(actual, 470);
                AssertNativeSegments(source, actual, 470);
            }
        });

    [Fact]
    public Task PublicPlainCrLfAndCrRetainTheirOriginalUtf16Positions() =>
        HeadlessTestScope.Run(() =>
        {
            foreach (var wrapping in new[] { TextWrapping.Wrap, TextWrapping.WrapWithOverflow })
            foreach (var source in new[] { "\r\nfirst\r\n\r\n😀e\u0301 אבג\r\n", "\rfirst\r\rlast\r", "\r\n", "\r", "\n\r\n\rlast\n" })
            {
                var actual = Plain(source, wrapping);
                Layout(actual, 470);
                AssertNativeSegments(source, actual, 470);
                Assert.Equal(source, actual.Text);
            }
        });

    [Fact]
    public Task PlainUnicodeReflowAndLiveFontChangesKeepNativeLineAndHitGeometry() =>
        HeadlessTestScope.Run(() =>
        {
            const string source = "\nalpha 😀e\u0301 中文 beta gamma delta epsilon\n\nאבג العربية end\n";
            var actual = Plain(source, TextWrapping.Wrap);
            actual.Padding = new Thickness(7, 9, 11, 13);
            Layout(actual, 800);
            AssertNativeSegments(source, actual, 800);
            var original = Ranges(actual);
            var originalHeight = actual.TextLayout.Height;

            Layout(actual, 140);
            AssertNativeSegments(source, actual, 140);
            Assert.True(actual.TextLayout.TextLines.Count > original.Length);

            actual.FontSize = 29;
            actual.FontWeight = FontWeight.Bold;
            actual.FontStyle = FontStyle.Italic;
            actual.Foreground = Brushes.DarkBlue;
            actual.LetterSpacing = 1.5;
            Layout(actual, 140);
            AssertNativeSegments(source, actual, 140);

            actual.FontSize = 16;
            actual.FontWeight = FontWeight.Normal;
            actual.FontStyle = FontStyle.Normal;
            actual.LetterSpacing = 0;
            Layout(actual, 800);
            AssertNativeSegments(source, actual, 800);
            Assert.Equal(original, Ranges(actual));
            Assert.Equal(originalHeight, actual.TextLayout.Height, 6);
        });

    [Fact]
    public Task PlainBreakAdaptationKeepsFiniteArrangeTrimmingAndMaxLinesOptions() =>
        HeadlessTestScope.Run(() =>
        {
            const string source = "\nalpha beta gamma delta epsilon zeta\n\nlast\n";
            var actual = Plain(source, TextWrapping.Wrap);
            actual.MaxLines = 2;
            actual.TextTrimming = TextTrimming.CharacterEllipsis;
            actual.LineHeight = 25;
            actual.LineSpacing = 3;
            actual.Padding = new Thickness(4, 6);
            actual.Measure(new Size(160, double.PositiveInfinity));
            Assert.True(double.IsPositiveInfinity(actual.TextLayout.MaxHeight));
            actual.Arrange(new Rect(0, 0, 160, 80));
            Assert.Equal(152, actual.TextLayout.MaxWidth);
            Assert.Equal(68, actual.TextLayout.MaxHeight);
            Assert.Equal(2, actual.TextLayout.MaxLines);
            Assert.True(actual.TextLayout.TextLines.Count <= 2);
            Assert.Equal(25, actual.TextLayout.LineHeight);
            Assert.Equal(TextTrimming.CharacterEllipsis, actual.TextTrimming);
            Assert.Equal(source, actual.Text);
            Assert.Equal(0, actual.Inlines?.Count ?? 0);
        });

    private static MarkdownTextPresenter Plain(string source, TextWrapping wrapping) => new()
    {
        Text = source, TextWrapping = wrapping, FontSize = 16,
    };

    private static void Layout(TextBlock text, double width)
    {
        text.Measure(new Size(width, double.PositiveInfinity));
        text.Arrange(new Rect(0, 0, width, text.DesiredSize.Height));
    }

    private static (int Start, int Length)[] Ranges(TextBlock text) => text.TextLayout.TextLines
        .Select(line => (line.FirstTextSourceIndex, line.Length)).ToArray();

    private static void AssertNativeSegments(string source, MarkdownTextPresenter actual, double width)
    {
        // Native plain wrapping can hang on empty hard lines in 12.0.3. Use
        // independently laid-out newline-free TextBlocks as the shaping oracle.
        // Their terminal one-unit EOP is replaced only in the expected source
        // ranges by the original CRLF/CR/LF length. No production helper is used.
        var expectedRanges = new List<(int Start, int Length)>();
        var nextLine = 0;
        var position = 0;
        var expectedHeight = 0d;
        foreach (var piece in source.Split(["\r\n", "\r", "\n"], StringSplitOptions.None))
        {
            var reference = new TextBlock
            {
                Text = piece, TextWrapping = actual.TextWrapping, FontSize = actual.FontSize,
                FontFamily = actual.FontFamily, FontWeight = actual.FontWeight,
                FontStyle = actual.FontStyle, FontStretch = actual.FontStretch,
                FontFeatures = actual.FontFeatures, TextDecorations = actual.TextDecorations,
                Foreground = actual.Foreground, LetterSpacing = actual.LetterSpacing,
                LineHeight = actual.LineHeight, LineSpacing = actual.LineSpacing,
                Padding = actual.Padding, FlowDirection = actual.FlowDirection,
                TextAlignment = actual.TextAlignment,
            };
            Layout(reference, width);
            var breakPosition = position + piece.Length;
            var breakLength = breakPosition == source.Length ? 0 :
                source.AsSpan(breakPosition).StartsWith("\r\n") ? 2 : 1;
            for (var index = 0; index < reference.TextLayout.TextLines.Count; index++)
            {
                var expected = reference.TextLayout.TextLines[index];
                var line = actual.TextLayout.TextLines[nextLine++];
                var endAdjustment = index + 1 == reference.TextLayout.TextLines.Count && breakLength != 0
                    ? breakLength - 1 : 0;
                expectedRanges.Add((position + expected.FirstTextSourceIndex, expected.Length + endAdjustment));
                Assert.Equal(expected.Width, line.Width, 6);
                Assert.Equal(expected.Height, line.Height, 6);
                Assert.Equal(expected.Baseline, line.Baseline, 6);
                expectedHeight += expected.Height;

                // Compare public caret geometry within the native logical line;
                // the position immediately after its terminal EOP belongs to the
                // next paragraph and is deliberately checked by that paragraph.
                for (var local = expected.FirstTextSourceIndex;
                     local < Math.Min(piece.Length, expected.FirstTextSourceIndex + expected.Length); local++)
                {
                    var expectedHit = reference.TextLayout.HitTestTextPosition(local);
                    var actualHit = actual.TextLayout.HitTestTextPosition(position + local);
                    Assert.Equal(expectedHit.X, actualHit.X, 6);
                    Assert.Equal(expectedHit.Width, actualHit.Width, 6);
                    Assert.Equal(expectedHit.Height, actualHit.Height, 6);
                }
            }
            position += piece.Length + breakLength;
        }
        Assert.Equal(source.Length, position);
        Assert.Equal(expectedRanges, Ranges(actual));
        Assert.Equal(nextLine, actual.TextLayout.TextLines.Count);
        Assert.Equal(expectedHeight, actual.TextLayout.Height, 6);
        Assert.Equal(source, actual.Text);
        Assert.Equal(0, actual.Inlines?.Count ?? 0);
        Assert.True(double.IsFinite(actual.DesiredSize.Width) && double.IsFinite(actual.DesiredSize.Height));
    }
}
