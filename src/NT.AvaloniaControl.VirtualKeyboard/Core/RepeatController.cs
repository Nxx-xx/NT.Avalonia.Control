namespace NT.AvaloniaControl.VirtualKeyboard;

/// <summary>Schedules one repeat per due tick without a catch-up burst; time is supplied by its caller.</summary>
internal sealed class RepeatController
{
    private long _session;
    private TimeSpan _next;
    private TimeSpan _interval;
    internal bool IsActive { get; private set; }
    internal void Begin(long session, TimeSpan now, TimeSpan delay, TimeSpan interval)
    {
        Validate(delay, interval);
        _session = session; _interval = interval; _next = now + (delay == TimeSpan.Zero ? interval : delay); IsActive = true;
    }
    internal bool TryTick(long session, TimeSpan now)
    {
        if (!IsActive || session != _session || now < _next) return false;
        _next = now + _interval;
        return true;
    }
    internal TimeSpan? GetNextTickDelay(long session, TimeSpan now)
    {
        if (!IsActive || session != _session) return null;
        // Dispatcher timers have millisecond deadlines. Round upwards and keep
        // an early tick from either spinning at zero or restarting the full delay.
        var remainingMilliseconds = (_next - now).TotalMilliseconds;
        return TimeSpan.FromMilliseconds(Math.Max(1, Math.Ceiling(remainingMilliseconds)));
    }
    internal void Cancel() => IsActive = false;
    internal static void Validate(TimeSpan delay, TimeSpan interval)
    {
        if (delay < TimeSpan.Zero || delay > TimeSpan.FromSeconds(10)) throw new ArgumentOutOfRangeException(nameof(delay));
        if (interval < TimeSpan.FromMilliseconds(10) || interval > TimeSpan.FromSeconds(10)) throw new ArgumentOutOfRangeException(nameof(interval));
    }
}
