using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class ChartUsageViewModel
{
    private ChartHighContrastMode _c7Contrast;
    public ChartHighContrastMode C7Contrast
    {
        get => _c7Contrast;
        set { if (_c7Contrast == value) return; _c7Contrast = value; Notify(); }
    }
}
