using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using NT.AvaloniaControl.Markdown;

namespace Markdown.Headless.Tests;

public sealed class TextPropertyReuseDifferentialTests
{
    private const string Text = "标题 大😀e\u0301 العربية אבג office fi end";

    [Fact]
    public Task PlainNativeGeometrySurvivesRepeatedLayoutAndLiveFontChanges() => HeadlessTestScope.Run(() =>
    {
        var actual = new MarkdownTextPresenter { Text = Text };
        var expected = new TextBlock { Text = Text };
        foreach (var family in new[] { FontFamily.Default, new FontFamily("Segoe UI, Noto Sans CJK SC, Microsoft YaHei, sans-serif") })
        foreach (var weight in new[] { FontWeight.Normal, FontWeight.SemiBold, FontWeight.Bold })
        foreach (var style in new[] { FontStyle.Normal, FontStyle.Italic })
        {
            foreach (var target in new TextBlock[] { actual, expected })
            {
                target.FontFamily = family;
                target.FontWeight = weight;
                target.FontStyle = style;
                target.FontSize = weight == FontWeight.SemiBold ? 26 : 16;
                target.FontStretch = style == FontStyle.Italic ? FontStretch.Condensed : FontStretch.Normal;
                target.TextWrapping = TextWrapping.Wrap;
                target.Padding = new Thickness(3, 5, 7, 2);
            }
            foreach (var width in new[] { 800d, 140d, 800d }) Compare(actual, expected, width);
        }
    });

    [Fact]
    public Task InlineBaselineAndMutableStyleChangesKeepNativeGeometryAndRunProperties() => HeadlessTestScope.Run(() =>
    {
        var actual = new MarkdownTextPresenter();
        var expected = new TextBlock();
        var actualRun = Populate(actual);
        var expectedRun = Populate(expected);
        foreach (var baseline in new[] { BaselineAlignment.Baseline, BaselineAlignment.Superscript, BaselineAlignment.Subscript, BaselineAlignment.Baseline })
        foreach (var size in new[] { 16d, 31d, 16d })
        {
            foreach (var run in new[] { actualRun, expectedRun })
            {
                run.BaselineAlignment = baseline;
                run.FontSize = size;
                run.FontWeight = size == 31 ? FontWeight.SemiBold : FontWeight.Normal;
                run.Foreground = size == 31 ? Brushes.HotPink : Brushes.DarkBlue;
                run.TextDecorations = size == 31 ? TextDecorations.Underline : null;
            }
            foreach (var width in new[] { 600d, 120d, 600d }) Compare(actual, expected, width);
        }
    });

    [Fact]
    public Task ParagraphOptionsAndRunReplacementUseCurrentNativeValues() => HeadlessTestScope.Run(() =>
    {
        var actual = new MarkdownTextPresenter();
        var expected = new TextBlock();
        Populate(actual); Populate(expected);
        foreach (var alignment in new[] { TextAlignment.Left, TextAlignment.Center, TextAlignment.Right })
        foreach (var spacing in new[] { 0d, 5d })
        {
            foreach (var target in new TextBlock[] { actual, expected })
            {
                target.TextAlignment = alignment;
                target.LineSpacing = spacing;
                target.LineHeight = spacing == 0 ? double.NaN : 35;
                target.MaxLines = spacing == 0 ? 0 : 2;
                target.TextTrimming = spacing == 0 ? TextTrimming.None : TextTrimming.CharacterEllipsis;
                target.Inlines![1] = new Run(Text) { FontSize = spacing == 0 ? 20 : 24, FontWeight = FontWeight.SemiBold };
            }
            Compare(actual, expected, 180);
        }
    });

    private static Run Populate(TextBlock target)
    {
        target.TextWrapping = TextWrapping.Wrap;
        target.FontSize = 18;
        var changing = new Run(Text);
        target.Inlines!.Add(new Run("prefix "));
        target.Inlines.Add(changing);
        target.Inlines.Add(new Run(" tail") { FontStyle = FontStyle.Italic });
        return changing;
    }

    private static void Compare(MarkdownTextPresenter actual, TextBlock expected, double width)
    {
        foreach (var target in new TextBlock[] { actual, expected })
        {
            target.InvalidateMeasure();
            target.Measure(new Size(width, double.PositiveInfinity));
            target.Arrange(new Rect(0, 0, width, target.DesiredSize.Height));
        }
        var left = actual.TextLayout;
        var right = expected.TextLayout;
        Assert.Equal(expected.DesiredSize, actual.DesiredSize);
        Assert.Equal(right.Width, left.Width, 6);
        Assert.Equal(right.Height, left.Height, 6);
        Assert.Equal(right.TextLines.Count, left.TextLines.Count);
        for (var index = 0; index < right.TextLines.Count; index++)
        {
            var wanted = right.TextLines[index]; var found = left.TextLines[index];
            Assert.Equal(wanted.FirstTextSourceIndex, found.FirstTextSourceIndex);
            Assert.Equal(wanted.Length, found.Length);
            Assert.Equal(wanted.Height, found.Height, 6);
            Assert.Equal(wanted.WidthIncludingTrailingWhitespace, found.WidthIncludingTrailingWhitespace, 6);
            Assert.Equal(wanted.TextRuns.Select(run => run.Properties?.BaselineAlignment), found.TextRuns.Select(run => run.Properties?.BaselineAlignment));
            Assert.Equal(wanted.TextRuns.Select(run => run.Properties?.Typeface), found.TextRuns.Select(run => run.Properties?.Typeface));
            Assert.Equal(wanted.TextRuns.Select(run => run.Properties?.FontRenderingEmSize), found.TextRuns.Select(run => run.Properties?.FontRenderingEmSize));
        }
        var length = right.TextLines.Sum(line => line.Length);
        for (var position = 0; position <= length; position++)
        {
            var wanted = right.HitTestTextPosition(position);
            Assert.Equal(wanted, left.HitTestTextPosition(position));
            var point = new Point(wanted.X + .25, wanted.Y + wanted.Height / 2);
            Assert.Equal(right.HitTestPoint(point), left.HitTestPoint(point));
        }
        Assert.Equal(right.HitTestTextRange(0, length), left.HitTestTextRange(0, length));
    }
}
