using System.Runtime.CompilerServices;
using Avalonia.Metadata;

[assembly: XmlnsDefinition("https://schemas.nt-controls.dev/pdfviewer", "NT.AvaloniaControl.PdfViewer")]
[assembly: XmlnsPrefix("https://schemas.nt-controls.dev/pdfviewer", "pdf")]
[assembly: InternalsVisibleTo("PdfViewer.Core.Tests")]
[assembly: InternalsVisibleTo("PdfViewer.Headless.Tests")]
[assembly: InternalsVisibleTo("PdfViewer.Themes.Tests")]
