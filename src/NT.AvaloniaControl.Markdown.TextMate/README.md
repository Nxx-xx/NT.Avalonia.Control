# NT.AvaloniaControl.Markdown.TextMate

Optional TextMate grammar highlighting for the independent native Markdown reader.
Install this package only when highlighting is needed. The core and default theme packages do not depend on it.

```csharp
using NT.AvaloniaControl.Markdown.TextMate;
using TextMateSharp.Grammars;

var highlighter = new TextMateCodeHighlighter(ThemeName.DarkPlus);
markdown.CodeHighlighter = highlighter;
highlighter.SetTheme(ThemeName.LightPlus); // Invalidates the control's code styling.
```

The host owns the highlighter and may share it across controls. The adapter recognizes
grammar language IDs, aliases, file extensions such as `cs` / `.cs`, and scope names.
Unknown languages return no styling and preserve the original code.

Each call returns UTF-16 ranges over the unchanged input. Multiline grammar state stays
within the request; CRLF, tabs, Unicode and final newlines are retained. Work is serialized
per adapter and runs in the background. A theme change raises `Invalidated`; an in-flight
request still uses the captured theme, so consumers must reject results from older service
versions. Cancellation is checked between lines and tokens; a running native regex cannot
be synchronously interrupted. Tokenization uses a 100 ms per-line time limit and unstyled
text remains visible if the grammar cannot fully classify a line.

TextMateSharp 2.0.4 and TextMateSharp.Grammars 2.0.4 bring Onigwrap 1.0.11 native assets.
See `THIRD-PARTY-NOTICES.md` and the project dependency record for licensing and platform
evidence. A listed runtime asset is not a tested-device support statement.
