using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace NT.AvaloniaControl.Demo.Navigation;

internal static class DemoGallery
{
    internal static Control Create(string title, string description, IReadOnlyList<DemoNavigationItem> items, Action<DemoNavigationItem> navigate)
    {
        var cards = new GalleryPanel { Name = "GalleryCards" };
        foreach (var item in items)
        {
            var card = new Button
            {
                Classes = { "gallery-card" },
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                Padding = new Thickness(0),
                Content = new StackPanel
                {
                    Children =
                    {
                        new Border
                        {
                            Classes = { "gallery-preview" }, Height = 120, Margin = new Thickness(10, 10, 10, 0),
                            CornerRadius = new CornerRadius(9), Padding = new Thickness(24, 16),
                            Child = new GalleryPreview(item.Preview),
                        },
                        new Grid
                        {
                            Margin = new Thickness(20, 16, 20, 20), ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                            Children =
                            {
                                new StackPanel
                                {
                                    Spacing = 6,
                                    Children =
                                    {
                                        new TextBlock { Text = item.Title, FontSize = 18, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                                        new TextBlock { Text = item.Description, FontSize = 12, Opacity = .7, TextWrapping = TextWrapping.Wrap },
                                    },
                                },
                                Arrow(),
                            },
                        },
                    },
                },
            };
            AutomationProperties.SetName(card, item.Title);
            ToolTip.SetTip(card, item.Description);
            card.Click += (_, _) => navigate(item);
            cards.Children.Add(card);
        }
        return new ScrollViewer
        {
            Name = "GalleryScroll", HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new StackPanel
            {
                Margin = new Thickness(24, 24, 24, 28), Spacing = 24,
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 8,
                        Children =
                        {
                            new TextBlock { Text = "COMPONENT GALLERY", FontSize = 11, Opacity = .65 },
                            new TextBlock { Text = title, FontSize = 30, FontWeight = FontWeight.SemiBold },
                            new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap, Opacity = .7 },
                            new TextBlock { Text = $"{items.Count} 个{(title == "探索控件" ? "控件" : "入口")}", FontSize = 12, Opacity = .6 },
                        },
                    },
                    cards,
                },
            },
        };
    }

    private static TextBlock Arrow()
    {
        var arrow = new TextBlock { Text = "↗", FontSize = 22, Opacity = .65, Margin = new Thickness(12, 0, 0, 0) };
        Grid.SetColumn(arrow, 1);
        return arrow;
    }

    internal static void AddStyles(Styles styles)
    {
        foreach (var (theme, background, surface, line, preview, accent) in new[]
        {
            (ThemeVariant.Light, "#F6F7F9", "#FFFFFF", "#E1E5EC", "#F0F2FA", "#5363CE"),
            (ThemeVariant.Dark, "#181B21", "#22262E", "#363D49", "#292E40", "#A4ADFF"),
        })
        {
            styles.Resources.ThemeDictionaries[theme] = new ResourceDictionary
            {
                ["GalleryBackground"] = Brush.Parse(background), ["GallerySurface"] = Brush.Parse(surface),
                ["GalleryLine"] = Brush.Parse(line), ["GalleryPreview"] = Brush.Parse(preview), ["GalleryAccent"] = Brush.Parse(accent),
            };
        }
        styles.Add(new Style(s => s.OfType<Button>().Class("gallery-card"))
        {
            Setters =
            {
                new Setter(TemplatedControl.BackgroundProperty, new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("GallerySurface")),
                new Setter(TemplatedControl.BorderBrushProperty, new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("GalleryLine")),
                new Setter(TemplatedControl.BorderThicknessProperty, new Thickness(1)),
                new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(14)),
            },
        });
        styles.Add(new Style(s => s.OfType<Button>().Class("gallery-card").Class(":pointerover"))
        {
            Setters = { new Setter(TemplatedControl.BorderBrushProperty, new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("GalleryAccent")) },
        });
        styles.Add(new Style(s => s.OfType<Border>().Class("gallery-preview"))
        {
            Setters = { new Setter(Border.BackgroundProperty, new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("GalleryPreview")) },
        });
    }
}

/// <summary>Equal width cards that wrap without horizontal scrolling.</summary>
internal sealed class GalleryPanel : Panel
{
    private const double Gap = 18;
    private int Columns(double width) => Math.Max(1, Math.Min(3, (int)((width + Gap) / 300)));

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 960;
        var columns = Columns(width);
        var cardWidth = Math.Max(0, (width - (columns - 1) * Gap) / columns);
        foreach (var child in Children) child.Measure(new Size(cardWidth, double.PositiveInfinity));
        var height = Children.Count == 0 ? 0 : Children.Max(child => child.DesiredSize.Height);
        var rows = (Children.Count + columns - 1) / columns;
        return new Size(width, rows * height + Math.Max(0, rows - 1) * Gap);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = Columns(finalSize.Width);
        var width = Math.Max(0, (finalSize.Width - (columns - 1) * Gap) / columns);
        var height = Children.Count == 0 ? 0 : Children.Max(child => child.DesiredSize.Height);
        for (var i = 0; i < Children.Count; i++)
            Children[i].Arrange(new Rect(i % columns * (width + Gap), i / columns * (height + Gap), width, height));
        return finalSize;
    }
}
