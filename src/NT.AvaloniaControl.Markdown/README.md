# NT.AvaloniaControl.Markdown

Independent read-only Markdown control using Markdig and Avalonia native text layout.
Load NT.AvaloniaControl.Markdown.Themes explicitly, or provide an application template
with PART_ScrollViewer and PART_DocumentPresenter. Optional highlighting is provided
by NT.AvaloniaControl.Markdown.TextMate.

Text, selection and streaming use one immutable document projection. Await
WhenUpdatedAsync() or a streaming session's CompleteAsync() for the final model/layout
boundary. Image loading and highlighting complete independently.

ImageLoader, CodeHighlighter and absolute BaseUri inherit into realized elements.
ElementServices supplies persistent per-element overrides; an explicit null disables
a service, while ClearValue restores inheritance. The host owns shared services; the
control owns returned image leases and releases stale results.

MarkdownParserOptions.Converters maps custom AST nodes to immutable data.
MarkdownRendererRegistry supplies native block/inline renderers. Use the context's
CreateDefault/CreateText helpers to retain selection, copy and hit testing geometry.
Invalid extensions produce diagnostics and a native fallback.

Public contracts, usage and current stage/platform evidence are maintained in the
repository docs/markdown-contracts.md, docs/markdown-usage.md and docs/markdown-status.md.
Package versions follow the repository's prerelease policy; supported platforms are
declared only from recorded native verification.
