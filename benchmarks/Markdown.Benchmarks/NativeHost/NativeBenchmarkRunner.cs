using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Markdown;
using NT.AvaloniaControl.Markdown.TextMate;
using TextMateSharp.Grammars;
using Reader = NT.AvaloniaControl.Markdown.Markdown;

namespace MarkdownNativeBenchmarks;

internal sealed partial class NativeBenchmarkRunner
{
    private readonly Window _window;
    private readonly Reader _reader = new();
    private readonly FrameWitness _witness;
    private readonly NativeReport _report;
    private readonly string _output;
    private readonly int _samples;
    private readonly int _warmup;
    private readonly int _streamSessions;
    private readonly int _longStreamSessions;
    private readonly string? _only;
    private readonly bool _longWithShortStream;
    private MarkdownUpdateMetrics _lastMetric;
    private readonly List<string> _errors = [];

    public NativeBenchmarkRunner(Window window, string[] args)
    {
        _window = window;
        string? Argument(string name) { var index = Array.IndexOf(args, name); return index >= 0 && index + 1 < args.Length ? args[index + 1] : null; }
        var pilot = args.Contains("--pilot", StringComparer.Ordinal);
        _samples = int.TryParse(Argument("--samples"), out var samples) ? samples : pilot ? 3 : 100;
        _warmup = pilot ? 1 : 10;
        _streamSessions = int.TryParse(Argument("--stream-sessions"), out var sessions) ? sessions : pilot ? 1 : 100;
        _longStreamSessions = int.TryParse(Argument("--long-stream-sessions"), out var longSessions) ? longSessions : pilot ? 1 : 100;
        if (_samples <= 0 || _streamSessions <= 0 || _longStreamSessions <= 0 || !pilot && (_samples < 100 || _streamSessions < 100 || _longStreamSessions < 100)) throw new ArgumentOutOfRangeException(nameof(args));
        _only = Argument("--only");
        _longWithShortStream = args.Contains("--stream-long", StringComparer.Ordinal);
        _output = Argument("--output") ?? "artifacts/benchmarks/markdown/native-pilot.json";
        _report = new NativeReport { 正式采样 = !pilot, 轮次 = int.TryParse(Argument("--round"), out var round) ? round : 1 };
        _witness = new FrameWitness(_reader, () => _lastMetric) { IsHitTestVisible = false, ZIndex = 1000 };
        _window.Content = new Grid { Children = { _reader, _witness } };
        _reader.UpdateMeasured += (_, metric) => _lastMetric = metric;
        _reader.Error += (_, error) => _errors.Add(error.Category + ": " + error.Error.Message);
    }

    public async Task<int> RunAsync()
    {
        var allocatedAtStart = GC.GetTotalAllocatedBytes(false);
        var initialCollections = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
        try
        {
            var debugBuild = false;
#if DEBUG
            debugBuild = true;
#endif
            _report.检查["Release且无调试器"] = !debugBuild && !Debugger.IsAttached;
            await Dispatcher.UIThread.InvokeAsync(_window.UpdateLayout, DispatcherPriority.Loaded);
            var handle = _window.TryGetPlatformHandle();
            _report.检查["原生窗口句柄"] = handle is { Handle: not 0 };
            _report.检查["固定缩放1"] = Math.Abs(_window.RenderScaling - 1) < .001;
            _report.检查["固定1200x700视口"] = Math.Abs(_reader.Bounds.Width - 1200) < 1 && Math.Abs(_reader.Bounds.Height - 700) < 1;
            _report.环境["操作系统"] = RuntimeInformation.OSDescription;
            _report.环境["进程架构"] = RuntimeInformation.ProcessArchitecture.ToString();
            _report.环境["运行时"] = RuntimeInformation.FrameworkDescription;
            _report.环境["StopwatchFrequencyHz"] = Stopwatch.Frequency;
            _report.环境["配置"] = debugBuild ? "Debug" : "Release";
            _report.环境["NativeHandle类型"] = handle?.HandleDescriptor;
            _report.环境["NativeBackend"] = OperatingSystem.IsWindows() ? "Avalonia.Win32" : OperatingSystem.IsLinux() ? "Avalonia.X11" : "other";
            _report.环境["窗口宽DIP"] = _reader.Bounds.Width;
            _report.环境["窗口高DIP"] = _reader.Bounds.Height;
            _report.环境["缩放"] = _window.RenderScaling;
            _report.环境["字体"] = _reader.FontFamily.Name;
            _report.环境["字体大小"] = _reader.FontSize;
            _report.环境["CPU标识"] = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER");
            if (_report.环境["CPU标识"] is null && OperatingSystem.IsLinux())
            {
                try { _report.环境["CPU标识"] = File.ReadLines("/proc/cpuinfo").FirstOrDefault(line => line.StartsWith("model name", StringComparison.Ordinal))?.Split(':', 2)[1].Trim(); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            _report.环境["逻辑CPU数"] = Environment.ProcessorCount;
            _report.环境["FONTCONFIG_FILE"] = Environment.GetEnvironmentVariable("FONTCONFIG_FILE");
            foreach (var assembly in new[] { typeof(Reader).Assembly, typeof(NT.AvaloniaControl.Markdown.Themes.MarkdownTheme).Assembly, typeof(TextMateCodeHighlighter).Assembly, typeof(FrameWitness).Assembly })
                _report.环境[assembly.GetName().Name + "SHA256"] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location)));
            foreach (var codepoint in new[] { 0x41, 0x4e2d, 0x1f600 })
                _report.环境["字符字体U+" + codepoint.ToString("X")] = FontManager.Current.TryMatchCharacter(codepoint, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal, _reader.FontFamily, null, out var face) ? face.FontFamily.Name : null;
            if (!_report.检查.Values.All(value => value)) throw new InvalidOperationException("Native fixture mismatch; budgets must not be silently changed.");

            if (Run("reading"))
                foreach (var highlight in new[] { false, true })
                    foreach (var bytes in new[] { 100 * 1024, 1024 * 1024 })
                        await ReadGroupAsync(CorpusFactory.Mixed(bytes), $"{bytes / 1024}KiB混合首屏-" + (highlight ? "TextMate" : "纯文本"), highlight, bytes == 1024 * 1024 ? 1500 : 300, bytes == 1024 * 1024 ? 50 : null);
            if (Run("long-block"))
            {
                await ReadGroupAsync(CorpusFactory.Code(100 * 1024), "100KiB单代码长行", false, 300, null, true);
                await ReadGroupAsync(CorpusFactory.Paragraph(100 * 1024), "100KiB单段落", false, 300, null, true);
                await ReadGroupAsync(CorpusFactory.WideTable(), "30x30宽表", false, 1500, null, true);
            }
            if (Run("streaming")) await StreamAsync(100 * 1024, false);
            if (Run("streaming-long") || _only == "streaming" && _longWithShortStream) await StreamAsync(1024 * 1024, true);
            if (Run("virtualization")) await VirtualizationAsync();
            if (Run("resources")) await ResourcesAsync();
            _report.检查["无诊断错误"] = _errors.Count == 0;
            _report.环境["诊断错误"] = _errors.ToArray();
        }
        catch (Exception error) { _report.错误 = error.ToString(); Console.Error.WriteLine(error); }
        finally
        {
            _report.内存与GC["累计进程分配字节数"] = GC.GetTotalAllocatedBytes(false) - allocatedAtStart;
            for (var generation = 0; generation < 3; generation++)
                _report.内存与GC["第" + generation + "代回收次数"] = GC.CollectionCount(generation) - initialCollections[generation];
            try
            {
                using var process = Process.GetCurrentProcess();
                _report.内存与GC["采样结束工作集字节数"] = process.WorkingSet64;
                _report.内存与GC["进程历史峰值工作集字节数"] = process.PeakWorkingSet64;
            }
            catch (System.ComponentModel.Win32Exception) { _report.内存与GC["工作集读取可用"] = false; }
            catch (PlatformNotSupportedException) { _report.内存与GC["工作集读取可用"] = false; }
            _report.内存与GC["说明"] = "整个宿主累计分配、GC及OS进程工作集；包括所有场景、预热、资源探针主动GC，不是单文档或受控堆的峰值。";
            _report.Save(_output);
        }
        return _report.全部通过 ? 0 : 1;
    }
    private bool Run(string group) => _only is null || _only == group;
    private async Task<long> ResetAsync()
    {
        _reader.Text = string.Empty;
        var result = await _reader.WhenUpdatedAsync().WaitAsync(TimeSpan.FromSeconds(30));
        if (result.Status != MarkdownUpdateStatus.Completed) throw new InvalidOperationException("Reset failed.");
        await _witness.ObserveAsync();
        return result.SourceVersion;
    }
    private async Task ReadGroupAsync(string source, string name, bool highlight, double? budget, double? uiBudget, bool interactions = false)
    {
        _reader.CodeHighlighter = highlight ? new TextMateCodeHighlighter(ThemeName.LightPlus) : null;
        var group = new NativeGroup { 名称 = name, 语料SHA256 = Hash(source), UTF8字节数 = Encoding.UTF8.GetByteCount(source), 预热次数 = _warmup, 总耗时P95预算毫秒 = budget, UI批P95预算毫秒 = uiBudget,
            布局P95预算毫秒 = interactions ? name == "30x30宽表" ? 500 : 150 : null,
            选择P95预算毫秒 = interactions ? 150 : null, 滚动P95预算毫秒 = interactions ? 150 : null,
            单文档累计分配上限字节数 = interactions ? 128L * 1024 * 1024 : null };
        _report.场景.Add(group);
        for (var iteration = -_warmup; iteration < _samples; iteration++)
        {
            await ResetAsync();
            var allocation = GC.GetTotalAllocatedBytes(false);
            var start = Stopwatch.GetTimestamp();
            _reader.Text = source;
            var result = await _reader.WhenUpdatedAsync().WaitAsync(TimeSpan.FromSeconds(60));
            var metric = _lastMetric;
            var frame = await _witness.ObserveAsync();
            var elapsed = Stopwatch.GetElapsedTime(start, frame.RenderedStamp).TotalMilliseconds;
            var allocated = GC.GetTotalAllocatedBytes(false) - allocation;
            var correct = result.Status == MarkdownUpdateStatus.Completed && _reader.Document.Source == source && frame.Length == source.Length &&
                metric.SourceLength == source.Length && metric.SourceVersion == result.SourceVersion && frame.Metric.SourceVersion == result.SourceVersion && _reader.DocumentPresenter is { RealizedBlockCount: > 0 };
            if (iteration >= 0) group.样本.Add(new(iteration, elapsed, metric.ParseElapsedMilliseconds, metric.UiApplyElapsedMilliseconds, metric.LayoutElapsedMilliseconds, allocated, correct));
            if (!correct) throw new InvalidOperationException("Native first-frame semantic mismatch: " + name);
            if (highlight)
            {
                var timeout = Stopwatch.StartNew();
                while (_reader.ActiveResourceRequestCount > 0 && timeout.Elapsed < TimeSpan.FromSeconds(30)) await Task.Delay(1);
                if (_reader.ActiveResourceRequestCount != 0) throw new TimeoutException("Visible highlighting did not settle.");
                var highlighted = await _witness.ObserveAsync();
                if (iteration >= 0) group.样本[^1] = group.样本[^1] with { 高亮完成至绘制总毫秒 = Stopwatch.GetElapsedTime(start, highlighted.RenderedStamp).TotalMilliseconds };
            }
            if (interactions)
            {
                var selectedAt = Stopwatch.GetTimestamp();
                _reader.SelectAll();
                var selected = await _witness.ObserveAsync();
                if (_reader.SelectedText != _reader.Document.Text) throw new InvalidOperationException("Long-block selection projection differs.");
                // Observe every actual nested ScrollViewer; long code may scroll horizontally.
                var viewers = _reader.GetVisualDescendants().OfType<ScrollViewer>().ToArray();
                var scrolledAt = Stopwatch.GetTimestamp();
                foreach (var viewer in viewers)
                    viewer.Offset = new Vector(Math.Max(0, viewer.Extent.Width - viewer.Viewport.Width) * .5, Math.Max(0, viewer.Extent.Height - viewer.Viewport.Height) * .5);
                _window.UpdateLayout();
                var scrolled = await _witness.ObserveAsync();
                if (iteration >= 0) group.样本[^1] = group.样本[^1] with
                {
                    选择至绘制毫秒 = Stopwatch.GetElapsedTime(selectedAt, selected.RenderedStamp).TotalMilliseconds,
                    滚动至绘制毫秒 = Stopwatch.GetElapsedTime(scrolledAt, scrolled.RenderedStamp).TotalMilliseconds,
                    // The per-document bound covers loading and both measured interactions.
                    分配字节数 = GC.GetTotalAllocatedBytes(false) - allocation
                };
            }
        }
        Console.WriteLine($"{name}: samples={group.样本数}, P95={group.总耗时P95毫秒:F3}ms, UI-P95={group.UI批P95毫秒:F3}ms, passed={group.是否通过}");
    }

    private async Task VirtualizationAsync()
    {
        _reader.CodeHighlighter = null;
        _reader.Text = CorpusFactory.Blocks();
        await _reader.WhenUpdatedAsync();
        await _witness.ObserveAsync();
        var scroll = _reader.GetVisualDescendants().OfType<ScrollViewer>().Single(control => control.Name == "PART_ScrollViewer");
        var passed = true;
        for (var iteration = 0; iteration < _samples; iteration++)
        {
            scroll.Offset = new Vector(0, Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height) * iteration / Math.Max(1, _samples - 1));
            _window.UpdateLayout();
            await _witness.ObserveAsync();
            var presenter = _reader.DocumentPresenter!;
            var visible = presenter.Children.Count(child => child.Bounds.Bottom > scroll.Offset.Y && child.Bounds.Y < scroll.Offset.Y + scroll.Viewport.Height);
            var valid = presenter.RealizedBlockCount <= visible + 16 && presenter.RetainedBlockCount <= 16;
            passed &= valid;
            _report.虚拟化.Add(new { 序号 = iteration, Visible = visible, Realized = presenter.RealizedBlockCount, Retained = presenter.RetainedBlockCount, OffsetY = scroll.Offset.Y, 是否通过 = valid });
        }
        _reader.SelectAll();
        _report.检查["10000块回收守卫"] = passed && _reader.Document.Blocks.Length == 10_000 && _reader.SelectedText == _reader.Document.Text;
    }
    private static string Hash(string source) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
}
