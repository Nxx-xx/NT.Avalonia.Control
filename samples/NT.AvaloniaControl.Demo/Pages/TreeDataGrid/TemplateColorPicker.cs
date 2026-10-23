using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;

namespace NT.AvaloniaControl.Demo.Pages.TreeDataGrid;

/// <summary>A compact preset color picker used inside display templates; no ColorPicker package required.</summary>
internal sealed class TemplateColorPicker : UserControl
{
    private readonly StackPanel _paletteContent;
    private readonly Flyout _flyout;

    public TemplateColorPicker()
    {
        var selectedSwatch = new Border { Width = 18, Height = 18, CornerRadius = new CornerRadius(3) };
        selectedSwatch.Bind(Border.BackgroundProperty, new ReflectionBinding(nameof(TemplateDemoState.ColorBrush)));
        var selectedText = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        selectedText.Bind(TextBlock.TextProperty, new ReflectionBinding(nameof(TemplateDemoState.ColorHex)));
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
        content.Children.Add(selectedSwatch);
        content.Children.Add(selectedText);
        content.Children.Add(new TextBlock { Text = "▾", VerticalAlignment = VerticalAlignment.Center });

        _paletteContent = new StackPanel { Spacing = 10, Width = 196 };
        _paletteContent.Children.Add(new TextBlock { Text = "选择标签颜色", FontWeight = FontWeight.SemiBold });
        var colors = new WrapPanel { ItemSpacing = 8, LineSpacing = 8 };
        _flyout = new Flyout { Content = _paletteContent };
        for (var index = 0; index < TemplateDemoState.Palette.Count; index++)
        {
            var entry = TemplateDemoState.Palette[index];
            var swatch = new Button
            {
                Width = 40, Height = 34, MinWidth = 0, MinHeight = 0, Padding = new Thickness(4),
                CommandParameter = index,
                Content = new Border
                {
                    Background = new SolidColorBrush(Color.Parse(entry.Hex)),
                    Width = 24, Height = 20, CornerRadius = new CornerRadius(3),
                },
            };
            swatch.Bind(Button.CommandProperty, new ReflectionBinding(nameof(TemplateDemoState.SelectColorCommand)));
            ToolTip.SetTip(swatch, $"{entry.Name} · {entry.Hex}");
            swatch.Click += (_, _) => _flyout.Hide();
            colors.Children.Add(swatch);
        }
        _paletteContent.Children.Add(colors);
        var currentColor = new TextBlock { FontSize = 12 };
        currentColor.Bind(TextBlock.TextProperty, new ReflectionBinding(nameof(TemplateDemoState.ColorDescription)));
        _paletteContent.Children.Add(currentColor);
        var button = new Button { Content = content, Flyout = _flyout, Padding = new Thickness(8, 4), MinHeight = 30 };
        ToolTip.SetTip(button, "点击打开选色盘；选择后立即更新这一行的标签颜色");
        Content = button;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _paletteContent.DataContext = DataContext;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _flyout.Hide();
        base.OnDetachedFromVisualTree(e);
    }
}
