using System.Globalization;
using System.Reflection;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using NT.AvaloniaControl.Markdown;

namespace Markdown.Headless.Tests;

public sealed class TextPropertyCacheLifecycleTests
{
    [Fact]
    public Task PropertiesShareOnlyWithinOneCreateAndCannotReintroduceEarlierGlyphCaches() => HeadlessTestScope.Run(() =>
    {
        // Inspect only our own internal helper. Framework internals and font tables
        // stay untouched, and this test uses the session's actual FontManager.
        var type = typeof(MarkdownTextPresenter).Assembly.GetType(
            "NT.AvaloniaControl.Markdown.MarkdownTextRunPropertiesCache", throwOnError: true)!;
        var cache = Activator.CreateInstance(type, nonPublic: true)!;
        object? Invoke(string name, params object?[] values) => type.GetMethod(name,
            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(cache, values);
        var original = new GenericTextRunProperties(
            new Typeface(new FontFamily("Segoe UI, Noto Sans CJK SC, Microsoft YaHei, sans-serif"),
                FontStyle.Italic, FontWeight.SemiBold, FontStretch.Condensed),
            26, TextDecorations.Underline, Brushes.DarkBlue, Brushes.LightYellow,
            BaselineAlignment.Superscript, CultureInfo.GetCultureInfo("ar-SA"));
        var firstDocument = new object();
        var secondDocument = new object();
        Invoke("BeginRequest", FontManager.Current, firstDocument);
        var first = Assert.IsType<GenericTextRunProperties>(Invoke("GetOrAdd", original));
        Assert.Same(first, Invoke("GetOrAdd", original));
        Invoke("EndRequest");
        Invoke("BeginRequest", FontManager.Current, firstDocument);
        var sameDocumentNextCall = Assert.IsType<GenericTextRunProperties>(Invoke("GetOrAdd", original));
        Assert.NotSame(first, sameDocumentNextCall);
        Assert.Same(sameDocumentNextCall, Invoke("GetOrAdd", original));
        Invoke("EndRequest");

        // Even an accidentally supplied old run cannot reintroduce an earlier
        // request's private glyph-face cache. Presenter Arrange itself uses base.
        Invoke("BeginRequest", FontManager.Current, secondDocument);
        const string content = "标题 😀e\u0301 العربية";
        var runs = Assert.IsAssignableFrom<IReadOnlyList<TextRun>>(Invoke("ReuseInlineProperties",
            new object[] { new TextRun[] { new TextCharacters(content, first) } }));
        var rewritten = Assert.IsType<TextCharacters>(Assert.Single(runs));
        var second = Assert.IsType<GenericTextRunProperties>(rewritten.Properties);
        Assert.NotSame(first, second);
        Assert.Equal(content, rewritten.Text.ToString());
        Assert.Equal(first.Typeface, second.Typeface);
        Assert.Equal(first.FontRenderingEmSize, second.FontRenderingEmSize);
        Assert.Equal(first.BaselineAlignment, second.BaselineAlignment);
        Assert.Same(first.TextDecorations, second.TextDecorations);
        Assert.Same(first.ForegroundBrush, second.ForegroundBrush);
        Assert.Same(first.BackgroundBrush, second.BackgroundBrush);
        Assert.Same(first.CultureInfo, second.CultureInfo);
        Assert.Same(first.FontFeatures, second.FontFeatures);
        Invoke("EndRequest");
        Invoke("Clear");
        Invoke("BeginRequest", FontManager.Current, secondDocument);
        Assert.NotSame(second, Invoke("GetOrAdd", second));
        Invoke("EndRequest");
    });
}
