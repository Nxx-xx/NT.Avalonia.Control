using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Markdown;
using NT.AvaloniaControl.Markdown.Themes;
using MarkdownControl = NT.AvaloniaControl.Markdown.Markdown;

namespace Markdown.Themes.Tests;

public sealed class ThemeContractTests
{
    [Fact]
    public void ProductAssembliesHaveOneWayDependency()
    {
        Assert.DoesNotContain(typeof(MarkdownControl).Assembly.GetReferencedAssemblies(), name => name.Name == "NT.AvaloniaControl.Markdown.Themes");
        Assert.Contains(typeof(MarkdownTheme).Assembly.GetReferencedAssemblies(), name => name.Name == "NT.AvaloniaControl.Markdown");
    }

    [Fact]
    public Task TemplateCreatesBothPublicPartsAndRendersSemanticFonts() => HeadlessTestScope.Run(TemplateCreatesBothPublicPartsAndRendersSemanticFontsCore);

    private async Task TemplateCreatesBothPublicPartsAndRendersSemanticFontsCore()
    {
        var markdown = new MarkdownControl { Text = "# Heading\n\ntext **bold** *italic*\n\n```cs\ncode\n```" };
        var window = Show(markdown);
        try
        {
            await markdown.WhenUpdatedAsync(); window.UpdateLayout();
            Assert.True(markdown.HasPresentationEnvironment);
            var heading = markdown.GetVisualDescendants().OfType<MarkdownBlockPresenter>().Single(block => block.Classes.Contains("h1"));
            Assert.Equal(32d, heading.GetVisualDescendants().OfType<MarkdownTextPresenter>().Single().FontSize);
            var code = markdown.GetVisualDescendants().OfType<MarkdownTextPresenter>().Single(text => text.Classes.Contains("code"));
            Assert.Equal(14d, code.FontSize);
            var paragraph = markdown.GetVisualDescendants().OfType<MarkdownTextPresenter>().Single(text => text.Inlines?.OfType<MarkdownSpan>().Any(span => span.Classes.Contains("strong")) == true);
            var strong = paragraph.Inlines!.OfType<MarkdownSpan>().Single(span => span.Classes.Contains("strong"));
            var emphasis = paragraph.Inlines!.OfType<MarkdownSpan>().Single(span => span.Classes.Contains("emphasis"));
            Assert.Equal(FontWeight.Bold, strong.FontWeight);
            Assert.Equal(FontStyle.Italic, emphasis.FontStyle);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task LightDarkAndResourceOverridesPreserveDocumentAndSelection() => HeadlessTestScope.Run(LightDarkAndResourceOverridesPreserveDocumentAndSelectionCore);

    private async Task LightDarkAndResourceOverridesPreserveDocumentAndSelectionCore()
    {
        var markdown = new MarkdownControl { Text = "# Heading\n\nkeep selection" };
        var window = Show(markdown);
        try
        {
            await markdown.WhenUpdatedAsync(); window.UpdateLayout(); markdown.SetSelection(0, 4);
            var document = markdown.Document;
            window.RequestedThemeVariant = ThemeVariant.Light; window.UpdateLayout();
            var light = Assert.IsAssignableFrom<ISolidColorBrush>(markdown.Background).Color;
            window.RequestedThemeVariant = ThemeVariant.Dark; window.UpdateLayout();
            Assert.NotEqual(light, Assert.IsAssignableFrom<ISolidColorBrush>(markdown.Background).Color);
            markdown.Resources["MarkdownH1FontSize"] = 41d;
            markdown.Resources["MarkdownBlockMargin"] = new Thickness(0, 0, 0, 23);
            markdown.Resources["MarkdownBackgroundBrush"] = Brushes.MediumPurple;
            window.UpdateLayout();
            Assert.Same(Brushes.MediumPurple, markdown.Background);
            Assert.Equal(41d, markdown.GetVisualDescendants().OfType<MarkdownTextPresenter>().First().FontSize);
            Assert.Same(document, markdown.Document);
            Assert.Equal("Head", markdown.SelectedText);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task PublicResourceUriLoadsFromAnotherAssembly() => HeadlessTestScope.Run(PublicResourceUriLoadsFromAnotherAssemblyCore);

    private async Task PublicResourceUriLoadsFromAnotherAssemblyCore()
    {
        var styles = Application.Current!.Styles;
        var original = styles.OfType<MarkdownTheme>().Single();
        var include = new StyleInclude(new Uri("avares://Markdown.Themes.Tests/"))
        { Source = new Uri("avares://NT.AvaloniaControl.Markdown.Themes/Themes/Generic.axaml") };
        styles.Remove(original); styles.Add(include);
        var markdown = new MarkdownControl { Text = "hello" };
        Window? window = null;
        try
        {
            window = Show(markdown); await markdown.WhenUpdatedAsync(); window.UpdateLayout();
            Assert.True(markdown.HasPresentationEnvironment);
            Assert.Equal("hello", markdown.Document.Text);
        }
        finally { window?.Close(); styles.Remove(include); styles.Add(original); }
    }

    [Fact]
    public Task FunctionalPackageAcceptsAnApplicationTemplate() => HeadlessTestScope.Run(FunctionalPackageAcceptsAnApplicationTemplateCore);

    private async Task FunctionalPackageAcceptsAnApplicationTemplateCore()
    {
        var markdown = new MarkdownControl { Text = "custom reader" };
        markdown.Template = new FuncControlTemplate<MarkdownControl>((_, scope) =>
        {
            var presenter = new MarkdownDocumentPresenter { Name = "PART_DocumentPresenter" };
            scope.Register(presenter.Name, presenter);
            var scroll = new ScrollViewer { Name = "PART_ScrollViewer", Content = presenter };
            scope.Register(scroll.Name, scroll);
            return scroll;
        });
        var window = Show(markdown);
        try
        {
            await markdown.WhenUpdatedAsync(); window.UpdateLayout(); markdown.SelectAll();
            Assert.True(markdown.HasPresentationEnvironment);
            Assert.Equal("custom reader", markdown.SelectedText);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task GlobalNestedInlineSelectorStylesActualSemanticSpanAndLayout() => HeadlessTestScope.Run(GlobalNestedInlineSelectorStylesActualSemanticSpanAndLayoutCore);

    private async Task GlobalNestedInlineSelectorStylesActualSemanticSpanAndLayoutCore()
    {
        var style = new Style(selector => selector.OfType<MarkdownBlockPresenter>().Class("quote").Descendant()
            .OfType<MarkdownSpan>().Class("strong").Descendant().OfType<MarkdownSpan>().Class("emphasis"));
        style.Setters.Add(new Setter(TextElement.ForegroundProperty, Brushes.HotPink));
        style.Setters.Add(new Setter(TextElement.FontSizeProperty, 27d));
        Application.Current!.Styles.Add(style);
        var markdown = new MarkdownControl { Text = "> **bold *nested italic***" };
        Window? window = null;
        try
        {
            window = Show(markdown); await markdown.WhenUpdatedAsync(); window.UpdateLayout();
            var text = markdown.GetVisualDescendants().OfType<MarkdownTextPresenter>().Single();
            var strong = text.Inlines!.OfType<MarkdownSpan>().Single(span => span.Classes.Contains("strong"));
            var nested = strong.Inlines.OfType<MarkdownSpan>().Single(span => span.Classes.Contains("emphasis"));
            Assert.Same(Brushes.HotPink, nested.Foreground);
            Assert.Equal(27d, nested.FontSize);
            var height = text.TextLayout.Height;
            nested.FontSize = 45; window.UpdateLayout();
            Assert.True(text.TextLayout.Height > height);
        }
        finally { window?.Close(); Application.Current!.Styles.Remove(style); }
    }

    [Fact]
    public Task TableCellAndAlertResourcesApplyToActualBlockNodes() => HeadlessTestScope.Run(TableCellAndAlertResourcesApplyToActualBlockNodesCore);

    private async Task TableCellAndAlertResourcesApplyToActualBlockNodesCore()
    {
        var markdown = new MarkdownControl { Text = "> [!TIP]\n> content\n\n| A | B |\n| - | - |\n| a | b |" };
        markdown.Resources["MarkdownTipBrush"] = Brushes.HotPink;
        markdown.Resources["MarkdownTableCellPadding"] = new Thickness(19);
        var window = Show(markdown);
        try
        {
            await markdown.WhenUpdatedAsync(); window.UpdateLayout();
            var nodes = markdown.GetVisualDescendants().OfType<MarkdownBlockPresenter>().ToArray();
            Assert.Same(Brushes.HotPink, nodes.Single(node => node.Classes.Contains("tip")).BorderBrush);
            Assert.All(nodes.Where(node => node.Classes.Contains("tablecell")), node => Assert.Equal(new Thickness(19), node.Padding));
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task AddingAndRemovingALocalNestedInlineStyleUpdatesGeometryAndKeepsSelection() => HeadlessTestScope.Run(AddingAndRemovingALocalNestedInlineStyleUpdatesGeometryAndKeepsSelectionCore);

    private async Task AddingAndRemovingALocalNestedInlineStyleUpdatesGeometryAndKeepsSelectionCore()
    {
        var markdown = new MarkdownControl { Text = "> **bold *nested italic***" };
        var window = Show(markdown);
        try
        {
            await markdown.WhenUpdatedAsync(); window.UpdateLayout(); markdown.SelectAll();
            var document = markdown.Document;
            var selected = markdown.SelectedText;
            var text = markdown.GetVisualDescendants().OfType<MarkdownTextPresenter>().Single();
            var strong = text.Inlines!.OfType<MarkdownSpan>().Single(span => span.Classes.Contains("strong"));
            var nested = strong.Inlines.OfType<MarkdownSpan>().Single(span => span.Classes.Contains("emphasis"));
            var originalSize = nested.FontSize;
            var originalHeight = text.TextLayout.Height;
            var style = new Style(selector => selector.OfType<MarkdownBlockPresenter>().Class("quote").Descendant()
                .OfType<MarkdownSpan>().Class("strong").Descendant().OfType<MarkdownSpan>().Class("emphasis"));
            style.Setters.Add(new Setter(TextElement.FontSizeProperty, 43d));
            style.Setters.Add(new Setter(TextElement.ForegroundProperty, Brushes.HotPink));
            markdown.Styles.Add(style); window.UpdateLayout();
            Assert.Equal(43d, nested.FontSize);
            Assert.Same(Brushes.HotPink, nested.Foreground);
            Assert.True(text.TextLayout.Height > originalHeight);
            Assert.Same(document, markdown.Document); Assert.Equal(selected, markdown.SelectedText);
            markdown.Styles.Remove(style); window.UpdateLayout();
            Assert.Equal(originalSize, nested.FontSize);
            Assert.Equal(originalHeight, text.TextLayout.Height, 4);
            Assert.Equal(selected, markdown.SelectedText);
        }
        finally { window.Close(); }
    }

    private static Window Show(Control control) { var window = new Window { Width = 800, Height = 700, Content = control }; window.Show(); window.UpdateLayout(); return window; }
}

