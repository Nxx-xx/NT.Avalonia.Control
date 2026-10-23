using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NT.AvaloniaControl.Demo.Pages.TreeDataGrid;

internal enum TemplateDemoKind { Buttons, Colors, Clock, Composite }

/// <summary>
/// Stable row identity. Mutable template state lives one level below the row so a clock tick or
/// slider drag updates its bindings without rebuilding the source projection and its controls.
/// </summary>
internal sealed class TemplateDemoItem
{
    public TemplateDemoItem(int id)
    {
        Id = id;
        Name = $"设备 {id:00}";
        State = new TemplateDemoState(Name, id);
    }

    public int Id { get; }
    public string Name { get; }
    public TemplateDemoState State { get; }
}

internal readonly record struct TemplatePaletteColor(string Name, string Hex);

internal sealed partial class TemplateDemoState : ObservableObject
{
    public static IReadOnlyList<TemplatePaletteColor> Palette { get; } =
    [
        new("海蓝", "#3B82F6"), new("青绿", "#14B8A6"), new("草绿", "#22C55E"), new("金黄", "#EAB308"),
        new("橙色", "#F97316"), new("玫红", "#EC4899"), new("紫色", "#8B5CF6"), new("石灰", "#64748B"),
    ];

    private bool _isRunning = true;
    private int _count;
    private int _colorIndex;
    private double _progress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TargetTimeText), nameof(Summary))]
    private TimeSpan? _targetTime;

    private TimeSpan _clockTime;

    public TemplateDemoState(string name, int index)
    {
        Name = name;
        _colorIndex = (index - 1) % Palette.Count;
        _progress = (index * 13) % 101;
        _targetTime = TimeSpan.FromHours(8 + index % 10) + TimeSpan.FromMinutes(index % 4 * 15);
        _clockTime = DateTime.Now.TimeOfDay;
    }

    public string Name { get; }
    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (!SetProperty(ref _isRunning, value)) return;
            if (value) Tick(DateTime.Now);
            OnPropertyChanged(nameof(RunActionText));
            OnPropertyChanged(nameof(RunStatus));
            OnPropertyChanged(nameof(Summary));
            IncrementCommand.NotifyCanExecuteChanged();
        }
    }

    public int Count => _count;
    public string CounterText => $"已执行 {_count} 次";
    public string RunActionText => IsRunning ? "暂停" : "启动";
    public string RunStatus => IsRunning ? "运行中" : "已暂停";
    public string ColorHex => Palette[_colorIndex].Hex;
    public string ColorName => Palette[_colorIndex].Name;
    public IBrush ColorBrush => new SolidColorBrush(Color.Parse(ColorHex));
    public string ColorDescription => $"{ColorName} · {ColorHex}";

    public double Progress
    {
        get => _progress;
        set
        {
            if (!SetProperty(ref _progress, Math.Clamp(value, 0, 100))) return;
            OnPropertyChanged(nameof(ProgressText));
            OnPropertyChanged(nameof(Summary));
        }
    }

    public string ProgressText => $"完成 {Progress:0}%";
    public string TargetTimeText => TargetTime is { } time ? $"计划 {time:hh\\:mm}" : "尚未设置计划时间";
    public TimeSpan ClockTime => _clockTime;
    public string CurrentTimeText => $"{ClockTime:hh\\:mm\\:ss}";
    public string Summary => $"{RunStatus} · {CounterText}\n{ColorDescription}\n{TargetTimeText} · {ProgressText}";

    [RelayCommand]
    public void ToggleRunning() => IsRunning = !IsRunning;

    [RelayCommand(CanExecute = nameof(IsRunning))]
    public void Increment()
    {
        if (!IsRunning) return;
        _count++;
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(CounterText));
        OnPropertyChanged(nameof(Summary));
    }

    [RelayCommand]
    public void Reset()
    {
        _count = 0;
        Progress = 0;
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(CounterText));
        OnPropertyChanged(nameof(Summary));
    }

    [RelayCommand]
    public void SelectColor(int index)
    {
        if (index < 0 || index >= Palette.Count || !SetProperty(ref _colorIndex, index, nameof(ColorHex))) return;
        OnPropertyChanged(nameof(ColorName));
        OnPropertyChanged(nameof(ColorBrush));
        OnPropertyChanged(nameof(ColorDescription));
        OnPropertyChanged(nameof(Summary));
    }

    public void Tick(DateTime now)
    {
        if (!IsRunning) return;
        var time = TimeSpan.FromSeconds(Math.Floor(now.TimeOfDay.TotalSeconds));
        if (!SetProperty(ref _clockTime, time, nameof(ClockTime))) return;
        OnPropertyChanged(nameof(CurrentTimeText));
    }
}
