using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Charts.Headless.Tests;

public sealed partial class TemplatePresentationView : UserControl
{
    public TemplatePresentationView() => AvaloniaXamlLoader.Load(this);
}

public sealed record TooltipTemplateRow(double X, double Y, string Name);

public sealed class TemplatePresentationModel
{
    public string Title { get; init; } = "业务读数";
    public ObservableCollection<TooltipTemplateRow> Records { get; init; } =
        [new(2, 20, "原始项一"), new(8, 80, "原始项二")];
}
