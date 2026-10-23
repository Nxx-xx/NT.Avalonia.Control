using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Markdown.Core.Tests")]

namespace NT.AvaloniaControl.Markdown;

/// <summary>Resolves Markdown destinations without treating web root paths as implicit file URIs.</summary>
internal static class MarkdownUriResolver
{
    public static Uri? Resolve(string? url, Uri? absoluteBase)
    {
        if (url is null) return null;
        url = url.Trim();
        var baseUri = absoluteBase is { IsAbsoluteUri: true } ? absoluteBase : null;
        if (url.Length == 0) return baseUri;

        // These are explicit Windows file paths even when a web/asset BaseUri is available.
        // Let the implicit-file parser escape filename '#'/'?'/'%' rather than treating them as
        // URI fragment/query delimiters. Its DOS/UNC recognition is independent of the host OS.
        if (IsWindowsDrivePath(url) || url.StartsWith("\\\\", StringComparison.Ordinal))
            return Uri.TryCreate(url, UriKind.Absolute, out var file) && file.IsFile ? file : null;
        if (HasScheme(url))
            return Uri.TryCreate(url, UriKind.Absolute, out var absolute) ? absolute : null;

        // In particular, //host/path is an authority-relative URI, unlike backslash UNC above.
        return baseUri is not null && Uri.TryCreate(baseUri, url, out var resolved) ? resolved : null;
    }

    private static bool IsWindowsDrivePath(string value) => value.Length >= 3 &&
        IsAsciiLetter(value[0]) && value[1] == ':' && value[2] is '/' or '\\';

    private static bool HasScheme(string value)
    {
        var colon = value.IndexOf(':');
        if (colon <= 0 || !IsAsciiLetter(value[0])) return false;
        for (var index = 1; index < colon; index++)
        {
            var character = value[index];
            if (!IsAsciiLetter(character) && !char.IsAsciiDigit(character) && character is not '+' and not '-' and not '.') return false;
        }
        return true;
    }

    private static bool IsAsciiLetter(char value) => value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
}
