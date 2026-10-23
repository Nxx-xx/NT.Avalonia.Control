using NT.AvaloniaControl.Markdown.TextMate;
using TextMateSharp.Grammars;

namespace Markdown.TextMate.Tests;

public class TextMateCodeHighlighterTests
{
    [Theory]
    [InlineData("csharp")]
    [InlineData("cs")]
    [InlineData(".cs")]
    [InlineData("C#")]
    [InlineData("source.cs")]
    public async Task Csharp_grammar_produces_valid_colored_ranges(string language)
    {
        var highlighter = new TextMateCodeHighlighter();
        const string code = "using System;\r\nvar text = \"中文 😀\";\r\n// comment\r\n";
        var spans = await highlighter.HighlightAsync(code, language, CancellationToken.None);
        Assert.NotEmpty(spans);
        Assert.Contains(spans, span => span.Foreground is not null &&
            code.Substring(span.Start, span.Length).Contains("using", StringComparison.Ordinal));
        Assert.Contains(spans, span => code.Substring(span.Start, span.Length).Contains("comment", StringComparison.Ordinal));
        var previousEnd = 0;
        foreach (var span in spans)
        {
            Assert.InRange(span.Start, previousEnd, code.Length - 1);
            Assert.InRange(span.Length, 1, code.Length - span.Start);
            Assert.DoesNotContain('\r', code.Substring(span.Start, span.Length));
            Assert.DoesNotContain('\n', code.Substring(span.Start, span.Length));
            Assert.Matches("^#[0-9A-F]{6}([0-9A-F]{2})?$", span.Foreground!);
            previousEnd = span.Start + span.Length;
        }
    }

    [Fact]
    public async Task Multiline_comment_state_is_retained_only_within_its_request()
    {
        var highlighter = new TextMateCodeHighlighter();
        const string code = "/* first\nsecond\n*/\nvar value = 1;";
        var spans = await highlighter.HighlightAsync(code, "csharp", CancellationToken.None);
        var first = Assert.Single(spans, span => code.Substring(span.Start, span.Length).Contains("first", StringComparison.Ordinal));
        var second = Assert.Single(spans, span => code.Substring(span.Start, span.Length).Contains("second", StringComparison.Ordinal));
        Assert.Equal(first.Foreground, second.Foreground);

        var separate = await highlighter.HighlightAsync("var value = 1;", "csharp", CancellationToken.None);
        var keyword = separate.First(span => span.Start == 0);
        Assert.NotEqual(first.Foreground, keyword.Foreground);
    }

    [Fact]
    public async Task Theme_change_invalidates_once_and_changes_comment_color()
    {
        var highlighter = new TextMateCodeHighlighter(ThemeName.DarkPlus);
        var invalidations = 0;
        highlighter.Invalidated += (_, _) => invalidations++;
        var dark = await highlighter.HighlightAsync("// comment", "cs", CancellationToken.None);
        highlighter.SetTheme(ThemeName.LightPlus);
        highlighter.SetTheme(ThemeName.LightPlus);
        var light = await highlighter.HighlightAsync("// comment", "cs", CancellationToken.None);
        Assert.Equal(1, invalidations);
        Assert.Equal(ThemeName.LightPlus, highlighter.Theme);
        Assert.NotEqual(Assert.Single(dark).Foreground, Assert.Single(light).Foreground);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-such-language-rt")]
    public async Task Unknown_language_returns_no_styles(string? language)
    {
        var highlighter = new TextMateCodeHighlighter();
        Assert.Empty(await highlighter.HighlightAsync("\t原文 😀\r\n", language, CancellationToken.None));
    }

    [Fact]
    public async Task Pre_cancelled_request_is_observed_even_without_a_grammar()
    {
        var highlighter = new TextMateCodeHighlighter();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await highlighter.HighlightAsync("value", "unknown", cancellation.Token));
    }

    [Fact]
    public async Task Shared_adapter_keeps_concurrent_documents_and_themes_isolated()
    {
        var highlighter = new TextMateCodeHighlighter();
        var pending = Enumerable.Range(0, 12).Select(index =>
            highlighter.HighlightAsync(index % 2 == 0 ? "/* unterminated" : "var value = 1;", "cs", CancellationToken.None).AsTask()).ToArray();
        var results = await Task.WhenAll(pending);
        Assert.All(results, Assert.NotEmpty);
        Assert.NotEqual(results[0][0].Foreground, results[1][0].Foreground);
        Assert.Equal(results[1], results[3]);
    }

    [Fact]
    public void Invalid_theme_is_rejected_without_invalidating()
    {
        var highlighter = new TextMateCodeHighlighter();
        var invalidations = 0;
        highlighter.Invalidated += (_, _) => invalidations++;
        Assert.Throws<ArgumentOutOfRangeException>(() => highlighter.SetTheme((ThemeName)int.MaxValue));
        Assert.Equal(ThemeName.DarkPlus, highlighter.Theme);
        Assert.Equal(0, invalidations);
    }
}
