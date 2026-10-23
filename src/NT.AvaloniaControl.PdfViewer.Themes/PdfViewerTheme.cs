using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;

namespace NT.AvaloniaControl.PdfViewer.Themes;

/// <summary>The optional PDF viewer template and light/dark resources.</summary>
public sealed class PdfViewerTheme : Styles
{
    /// <summary>Standalone palette: fixed light/dark colors, no Fluent dependency.</summary>
    public PdfViewerTheme() : this(false)
    {
    }

    /// <summary>
    /// When <paramref name="useFluentResources"/> is true, the viewer palette is additionally
    /// mapped onto Avalonia Fluent System*Color tokens (Resources/Fluent.axaml), so the reader
    /// follows the host's Fluent theme variant. The standalone colors remain as fallbacks.
    /// </summary>
    public PdfViewerTheme(bool useFluentResources)
    {
        Add(new StyleInclude(new Uri("avares://NT.AvaloniaControl.PdfViewer.Themes/"))
        {
            Source = new Uri("avares://NT.AvaloniaControl.PdfViewer.Themes/Themes/Generic.axaml"),
        });
        if (useFluentResources)
            Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://NT.AvaloniaControl.PdfViewer.Themes/"))
            {
                Source = new Uri("avares://NT.AvaloniaControl.PdfViewer.Themes/Resources/Fluent.axaml"),
            });
    }
}
