using NT.AvaloniaControl.Markdown;

namespace Markdown.Core.Tests;

public sealed class MarkdownUriResolverTests
{
    [Theory]
    [InlineData("assets/a b.png", "https://example.test/docs/assets/a%20b.png")]
    [InlineData("../a.png", "https://example.test/a.png")]
    [InlineData("/assets/a.png", "https://example.test/assets/a.png")]
    [InlineData("//cdn.example.test/a.png", "https://cdn.example.test/a.png")]
    [InlineData("#next", "https://example.test/docs/readme.md?q=1#next")]
    [InlineData("?q=2", "https://example.test/docs/readme.md?q=2")]
    public void RelativeWebReferencesUseTheBaseBeforeImplicitFileParsing(string url, string expected)
    {
        var uri = MarkdownUriResolver.Resolve(url, new("https://example.test/docs/readme.md?q=1#old"));
        Assert.Equal(expected, uri!.AbsoluteUri);
        Assert.False(uri.IsFile);
    }

    [Theory]
    [InlineData("https://other.test/a", "https://other.test/a")]
    [InlineData("mailto:name@example.test", "mailto:name@example.test")]
    [InlineData("urn:example:resource", "urn:example:resource")]
    [InlineData("custom+v1.path:value", "custom+v1.path:value")]
    [InlineData("file:///tmp/a%20b.png", "file:///tmp/a%20b.png")]
    [InlineData("C:\\temp\\a b.png", "file:///C:/temp/a%20b.png")]
    [InlineData("C:/temp/a b.png", "file:///C:/temp/a%20b.png")]
    [InlineData("C:\\temp\\a#b.png", "file:///C:/temp/a%23b.png")]
    [InlineData("\\\\server\\share\\a?b.png", "file://server/share/a%3Fb.png")]
    public void ExplicitSchemesAndWindowsFilePathsRemainAbsoluteAcrossPlatforms(string url, string expected)
    {
        var withBase = MarkdownUriResolver.Resolve(url, new("https://example.test/docs/readme.md"));
        var withoutBase = MarkdownUriResolver.Resolve(url, null);
        Assert.Equal(expected, withBase!.AbsoluteUri);
        Assert.Equal(expected, withoutBase!.AbsoluteUri);
    }

    [Theory]
    [InlineData("assets/x.png")]
    [InlineData("/assets/x.png")]
    [InlineData("//cdn.example/x.png")]
    [InlineData("#fragment")]
    [InlineData("")]
    [InlineData("C:relative")]
    public void RelativeDestinationWithoutAnAbsoluteBaseIsUnresolved(string url)
    {
        Assert.Null(MarkdownUriResolver.Resolve(url, null));
        Assert.Null(MarkdownUriResolver.Resolve(url, new("docs/readme.md", UriKind.Relative)));
    }

    [Fact]
    public void EmptyDestinationRefersToTheCurrentDocumentWhenItsBaseIsAbsolute()
    {
        var baseUri = new Uri("https://example.test/docs/readme.md?q=1#current");
        Assert.Same(baseUri, MarkdownUriResolver.Resolve(string.Empty, baseUri));
        Assert.Null(MarkdownUriResolver.Resolve(null, baseUri));
    }

    [Theory]
    [InlineData("avares://Markdown.Assets/docs/readme.md", "assets/x.png", "avares://markdown.assets/docs/assets/x.png")]
    [InlineData("avares://Markdown.Assets/docs/readme.md", "/x.png", "avares://markdown.assets/x.png")]
    [InlineData("avares://Markdown.Assets/docs/readme.md", "#part", "avares://markdown.assets/docs/readme.md#part")]
    [InlineData("file:///tmp/docs/readme.md", "assets/x.png", "file:///tmp/docs/assets/x.png")]
    [InlineData("file:///tmp/docs/readme.md", "/x.png", "file:///x.png")]
    [InlineData("file:///C:/docs/readme.md", "/x.png", "file:///C:/x.png")]
    public void AssetAndFileBasesRetainTheirSchemeAndNormalRelativeRules(string baseUri, string url, string expected)
    {
        Assert.Equal(expected, MarkdownUriResolver.Resolve(url, new(baseUri))!.AbsoluteUri);
    }

    [Theory]
    [InlineData("http://")]
    [InlineData("https://[broken")]
    [InlineData("//[broken/x")]
    [InlineData("\\\\")]
    public void MalformedDestinationsReturnNullInsteadOfThrowing(string url)
    {
        Assert.Null(MarkdownUriResolver.Resolve(url, new("https://example.test/")));
        Assert.Null(MarkdownUriResolver.Resolve(url, null));
    }
}
