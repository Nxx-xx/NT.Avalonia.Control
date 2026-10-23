using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed partial class RangeSelectorConsumerView : UserControl
{
    public RangeSelectorConsumerView() => AvaloniaXamlLoader.Load(this);
}

public sealed class RangeSelectorConsumerModel : INotifyPropertyChanged
{
    private ChartRange _range = new(.2, .8);
    public ChartRange Range { get => _range; set { if (_range == value) return; _range = value; PropertyChanged?.Invoke(this, new(nameof(Range))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}
