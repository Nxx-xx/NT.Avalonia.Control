using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml;

namespace Charts.Headless.Tests;

public sealed partial class TemplateFeaturesConsumerView : UserControl
{
    public TemplateFeaturesConsumerView() => AvaloniaXamlLoader.Load(this);
}

public sealed class TemplateFeatureRow(string id, double x, double y, string name) : INotifyPropertyChanged
{
    private double _y = y;
    private string _name = name;
    public string Id { get; } = id;
    public double X { get; } = x;
    public double Y { get => _y; set { if (_y.Equals(value)) return; _y = value; PropertyChanged?.Invoke(this, new(nameof(Y))); } }
    public string Name { get => _name; set { if (_name == value) return; _name = value; PropertyChanged?.Invoke(this, new(nameof(Name))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class TemplateFeaturesModel : INotifyPropertyChanged
{
    private string _title = "original series";
    private IDataTemplate? _legendTemplate;
    private IDataTemplate? _labelTemplate;
    public string Title { get => _title; set { if (_title == value) return; _title = value; PropertyChanged?.Invoke(this, new(nameof(Title))); } }
    public IDataTemplate? LegendTemplate { get => _legendTemplate; set { if (ReferenceEquals(_legendTemplate, value)) return; _legendTemplate = value; PropertyChanged?.Invoke(this, new(nameof(LegendTemplate))); } }
    public IDataTemplate? LabelTemplate { get => _labelTemplate; set { if (ReferenceEquals(_labelTemplate, value)) return; _labelTemplate = value; PropertyChanged?.Invoke(this, new(nameof(LabelTemplate))); } }
    public ObservableCollection<TemplateFeatureRow> Records { get; init; } = [new("original-id", 4, 40, "original item")];
    public event PropertyChangedEventHandler? PropertyChanged;
}
