using TextMateSharp.Grammars;
using TextMateSharp.Themes;
using Registry = TextMateSharp.Registry.Registry;

namespace NT.AvaloniaControl.Markdown.TextMate;

/// <summary>
/// Optional TextMate grammar highlighting. Each request owns its line state and preserves
/// the original UTF-16 text, including CRLF, tabs and a final newline.
/// </summary>
public sealed class TextMateCodeHighlighter : IMarkdownCodeHighlighter
{
    private readonly object _themeGate = new();
    private readonly SemaphoreSlim _workGate = new(1, 1);
    private ThemeState _state;

    public TextMateCodeHighlighter() : this(ThemeName.DarkPlus) { }

    public TextMateCodeHighlighter(ThemeName theme)
    {
        ValidateTheme(theme);
        _state = new ThemeState(theme);
    }

    public event EventHandler? Invalidated;

    public ThemeName Theme => Volatile.Read(ref _state).Name;

    /// <summary>
    /// Changes the theme and invalidates consumers. An already running request continues
    /// using its captured theme; the consumer must reject results from its old service version.
    /// </summary>
    public void SetTheme(ThemeName theme)
    {
        ValidateTheme(theme);
        lock (_themeGate)
        {
            if (_state.Name == theme)
                return;
            Volatile.Write(ref _state, new ThemeState(theme));
        }
        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    public async ValueTask<IReadOnlyList<MarkdownHighlightSpan>> HighlightAsync(
        string code, string? language, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);
        cancellationToken.ThrowIfCancellationRequested();
        if (code.Length == 0 || string.IsNullOrWhiteSpace(language))
            return Array.Empty<MarkdownHighlightSpan>();

        var state = Volatile.Read(ref _state);
        if (!state.Scopes.TryGetValue(language.Trim(), out var scope))
            return Array.Empty<MarkdownHighlightSpan>();

        // Registries and grammar caches are mutable. Serialize tokenizer work, but do not
        // make the UI thread wait for that work when the host switches the theme.
        await _workGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() => Highlight(code, scope, state, cancellationToken), cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _workGate.Release();
        }
    }

    private static IReadOnlyList<MarkdownHighlightSpan> Highlight(
        string code, string scope, ThemeState state, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var grammar = state.Registry.LoadGrammar(scope);
        if (grammar is null)
            return Array.Empty<MarkdownHighlightSpan>();

        var spans = new List<MarkdownHighlightSpan>();
        var theme = state.Registry.GetTheme();
        IStateStack? stack = null;
        var offset = 0;
        while (offset < code.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativeEnd = code.AsSpan(offset).IndexOfAny('\r', '\n');
            var end = relativeEnd < 0 ? code.Length : offset + relativeEnd;
            var lineLength = end - offset;
            var result = grammar.TokenizeLine(new LineText(code.AsMemory(offset, lineLength)), stack,
                TimeSpan.FromMilliseconds(100));
            stack = result.RuleStack;
            foreach (var token in result.Tokens)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var start = Math.Clamp(token.StartIndex, 0, lineLength);
                var tokenEnd = Math.Clamp(token.EndIndex, start, lineLength);
                if (tokenEnd == start)
                    continue;

                string? foreground = null;
                FontStyle? fontStyle = null;
                foreach (var rule in theme.Match(token.Scopes))
                {
                    if (foreground is null && rule.foreground != 0)
                        foreground = NormalizeColor(theme.GetColor(rule.foreground));
                    if (fontStyle is null && rule.fontStyle != FontStyle.NotSet)
                        fontStyle = rule.fontStyle;
                }

                var bold = fontStyle.HasValue && (fontStyle.Value & FontStyle.Bold) != 0;
                var italic = fontStyle.HasValue && (fontStyle.Value & FontStyle.Italic) != 0;
                if (foreground is null && !bold && !italic)
                    continue;

                AddSpan(spans, new MarkdownHighlightSpan(offset + start, tokenEnd - start,
                    foreground, bold, italic));
            }

            offset = end;
            if (offset < code.Length && code[offset] == '\r')
                offset++;
            if (offset < code.Length && code[offset] == '\n')
                offset++;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return Array.AsReadOnly(spans.ToArray());
    }

    private static void AddSpan(List<MarkdownHighlightSpan> spans, MarkdownHighlightSpan span)
    {
        if (spans.Count > 0)
        {
            var previous = spans[^1];
            if (previous.Start + previous.Length == span.Start && previous.Foreground == span.Foreground &&
                previous.Bold == span.Bold && previous.Italic == span.Italic)
            {
                spans[^1] = previous with { Length = previous.Length + span.Length };
                return;
            }
        }
        spans.Add(span);
    }

    private static string? NormalizeColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
            return null;
        var hex = color.AsSpan().Trim();
        if (hex[0] == '#')
            hex = hex[1..];
        foreach (var character in hex)
            if (!char.IsAsciiHexDigit(character))
                return null;

        // TextMate themes use CSS hex (#RRGGBBAA); the control contract uses #AARRGGBB.
        return hex.Length switch
        {
            6 => "#" + hex.ToString().ToUpperInvariant(),
            8 => "#" + hex[6..].ToString().ToUpperInvariant() + hex[..6].ToString().ToUpperInvariant(),
            3 => $"#{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}".ToUpperInvariant(),
            4 => $"#{hex[3]}{hex[3]}{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}".ToUpperInvariant(),
            _ => null
        };
    }

    private static void ValidateTheme(ThemeName theme)
    {
        if (!Enum.IsDefined(theme))
            throw new ArgumentOutOfRangeException(nameof(theme));
    }

    private sealed class ThemeState
    {
        public ThemeState(ThemeName name)
        {
            Name = name;
            var options = new RegistryOptions(name);
            Registry = new Registry(options);
            Scopes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var language in options.GetAvailableLanguages())
            {
                var scope = options.GetScopeByLanguageId(language.Id);
                if (string.IsNullOrEmpty(scope))
                    continue;
                Scopes.TryAdd(language.Id, scope);
                Scopes.TryAdd(scope, scope);
                if (language.Aliases is not null)
                    foreach (var alias in language.Aliases)
                        Scopes.TryAdd(alias, scope);
                if (language.Extensions is not null)
                    foreach (var extension in language.Extensions)
                    {
                        Scopes.TryAdd(extension, scope);
                        Scopes.TryAdd(extension.TrimStart('.'), scope);
                    }
            }
        }

        public ThemeName Name { get; }
        public Registry Registry { get; }
        public Dictionary<string, string> Scopes { get; }
    }
}
