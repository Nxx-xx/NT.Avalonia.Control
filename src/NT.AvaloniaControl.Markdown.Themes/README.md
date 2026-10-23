# NT.AvaloniaControl.Markdown.Themes

Optional default native reader template and semantic styles for `NT.AvaloniaControl.Markdown`.

Add `new MarkdownTheme()` after your application theme, or include
`avares://NT.AvaloniaControl.Markdown.Themes/Themes/Generic.axaml`.

All theme resources use the `Markdown` prefix. Realized block styles target
`MarkdownBlockPresenter` and its semantic classes; inline styles target real
`MarkdownSpan` / `MarkdownRun` nodes. Recycling does not change the logical document
or selection. The theme package has a one-way dependency on the functional package.
