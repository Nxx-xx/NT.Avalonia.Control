using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    internal async Task RunChartsSmokeAsync(string[] args)
    {
        var output = SmokeArgument(args, "--smoke-output") ?? Path.Combine("artifacts", "platform", "windows-charts-smoke.json");
        var image = Path.GetFullPath(SmokeArgument(args, "--smoke-image") ?? Path.Combine("artifacts", "platform", "windows-charts-light.png"));
        var darkImage = Path.Combine(Path.GetDirectoryName(image)!, Path.GetFileNameWithoutExtension(image) + "-dark.png");
        var zoomedImage = Path.Combine(Path.GetDirectoryName(image)!, Path.GetFileNameWithoutExtension(image) + "-zoomed.png");
        var tooltipImage = Path.Combine(Path.GetDirectoryName(image)!, Path.GetFileNameWithoutExtension(image) + "-tooltip.png");
        var darkTooltipImage = Path.Combine(Path.GetDirectoryName(image)!, Path.GetFileNameWithoutExtension(image) + "-tooltip-dark.png");
        var advancedImage = Path.Combine(Path.GetDirectoryName(image)!, Path.GetFileNameWithoutExtension(image) + "-c1-advanced.png");
        var axisImage = Path.Combine(Path.GetDirectoryName(image)!, Path.GetFileNameWithoutExtension(image) + "-c1-axes.png");
        var axisDarkImage = Path.Combine(Path.GetDirectoryName(image)!, Path.GetFileNameWithoutExtension(image) + "-c1-axes-dark.png");
        var breakPixelsImage = Path.Combine(Path.GetDirectoryName(image)!, Path.GetFileNameWithoutExtension(image) + "-break-pixels.png");
        var breakPixelsDarkImage = Path.Combine(Path.GetDirectoryName(image)!, Path.GetFileNameWithoutExtension(image) + "-break-pixels-dark.png");
        var commonImage = Path.Combine(Path.GetDirectoryName(image)!, Path.GetFileNameWithoutExtension(image) + "-c2-common.png");
        var commonDarkImage = Path.Combine(Path.GetDirectoryName(image)!, Path.GetFileNameWithoutExtension(image) + "-c2-common-dark.png");
        var commonExportImage = Path.Combine(Path.GetDirectoryName(image)!, Path.GetFileNameWithoutExtension(image) + "-c2-export.png");
        var commonDarkExportImage = Path.Combine(Path.GetDirectoryName(image)!, Path.GetFileNameWithoutExtension(image) + "-c2-export-dark.png");
        var checks = new Dictionary<string, bool>();
        _animationDiagnosticEnabled = args.Contains("--charts-animation-diagnostic", StringComparer.Ordinal);
        string? error = null;
        try
        {
            if (args.Contains("--charts-animation-only", StringComparer.Ordinal))
            {
                await RunAnimationSmokeAsync(checks, image);
            }
            else if (args.Contains("--charts-c5-completion-only", StringComparer.Ordinal))
            {
                await RunC5CompletionSmokeAsync(checks, image);
            }
            else if (args.Contains("--charts-c6-only", StringComparer.Ordinal))
            {
                await RunC6CompletionSmokeAsync(checks, image);
            }
            else if (args.Contains("--charts-maps-only", StringComparer.Ordinal))
            {
                await RunMapsSmokeAsync(checks, image);
            }
            else if (args.Contains("--charts-relations-only", StringComparer.Ordinal))
            {
                await RunRelationsSmokeAsync(checks, image);
            }
            else if (args.Contains("--charts-hierarchy-only", StringComparer.Ordinal))
            {
                await RunHierarchySmokeAsync(checks, image);
            }
            else if (args.Contains("--charts-comparison-only", StringComparer.Ordinal))
            {
                await RunAdvancedComparisonSmokeAsync(checks, image);
            }
            else if (args.Contains("--charts-engineering-only", StringComparer.Ordinal))
            {
                await RunEngineeringSmokeAsync(checks, image);
            }
            else if (args.Contains("--charts-technical-only", StringComparer.Ordinal))
            {
                await RunTechnicalTrendsSmokeAsync(checks, image);
            }
            else if (args.Contains("--charts-financial-only", StringComparer.Ordinal))
            {
                await SmokeFlushAsync();
                await RunFinancialSmokeAsync(checks, image);
            }
            else if (args.Contains("--charts-statistics-only", StringComparer.Ordinal))
            {
                await SmokeFlushAsync();
                await RunStatisticsSmokeAsync(checks, image);
            }
            else if (args.Contains("--charts-multichart-only", StringComparer.Ordinal))
            {
                await SmokeFlushAsync();
                await RunMultiChartSmokeAsync(checks, image);
            }
            else if (args.Contains("--charts-resources-only", StringComparer.Ordinal))
            {
                await SmokeFlushAsync();
                await RunRenderResourceSmokeAsync(checks, image);
            }
            else if (args.Contains("--charts-streaming-only", StringComparer.Ordinal))
            {
                await SmokeFlushAsync();
                await RunIncrementalStreamingSmokeAsync(checks, image);
            }
            else if (args.Contains("--charts-hit-only", StringComparer.Ordinal))
            {
                await SmokeFlushAsync();
                await RunEfficientHitSmokeAsync(checks, image);
            }
            else if (args.Contains("--charts-lod-only", StringComparer.Ordinal))
            {
                await SmokeFlushAsync();
                await RunLevelOfDetailSmokeAsync(checks, image);
            }
            else
            {
            await SmokeFlushAsync();
            checks["原生窗口已创建"] = _hostWindow.TryGetPlatformHandle() is { Handle: not 0 };
            checks["Charts页面已选择"] = _hostWindow is MainWindow main && main.SelectedPageId == "charts";
            checks["三种图表已布局"] = AllCharts.All(chart => chart.Bounds.Width > 250 && chart.Bounds.Height >= 240);
            checks["三种图表均有两组数据"] = AllCharts.All(chart => chart.Series.Count == 2);
            checks["三种图表默认启用提示缩放及平移"] = AllCharts.All(chart => chart.IsTooltipEnabled && chart.IsZoomEnabled && chart.IsPanEnabled && chart.InteractionAxes == ChartInteractionAxes.Both);

            _hostWindow.RequestedThemeVariant = ThemeVariant.Light;
            await SmokeFlushAsync();
            var initialChart = CaptureChartHash();
            RefreshData();
            await SmokeFlushAsync();
            checks["集合刷新改变图形"] = CaptureChartHash() != initialChart;
            var refreshedChart = CaptureChartHash();
            AppendMonth();
            await SmokeFlushAsync();
            checks["追加数据扩展横坐标"] = _visits.Count == 13 && _lineChart.XAxis.Labels?.Count == 13 && CaptureChartHash() != refreshedChart;

            SetGridLines(false);
            SetLegends(false);
            await SmokeFlushAsync();
            checks["网格及图例可以关闭"] = AllCharts.All(chart => !chart.ShowGridLines && !chart.ShowLegend);
            SetGridLines(true);
            SetLegends(true);
            ResetData();
            await SmokeFlushAsync();

            _tooltipOption.IsChecked = false;
            _zoomOption.IsChecked = false;
            _panOption.IsChecked = false;
            checks["交互开关可以关闭"] = AllCharts.All(chart => !chart.IsTooltipEnabled && !chart.IsZoomEnabled && !chart.IsPanEnabled);
            _tooltipOption.IsChecked = true;
            _zoomOption.IsChecked = true;
            _panOption.IsChecked = true;
            checks["交互开关可以恢复"] = AllCharts.All(chart => chart.IsTooltipEnabled && chart.IsZoomEnabled && chart.IsPanEnabled);
            _interactionAxes.SelectedIndex = 0;
            checks["可以仅操作横轴"] = AllCharts.All(chart => chart.InteractionAxes == ChartInteractionAxes.X);
            _interactionAxes.SelectedIndex = 1;
            checks["可以仅操作纵轴"] = AllCharts.All(chart => chart.InteractionAxes == ChartInteractionAxes.Y);
            _interactionAxes.SelectedIndex = 2;
            checks["可以操作双轴"] = AllCharts.All(chart => chart.InteractionAxes == ChartInteractionAxes.Both);

            var automaticChart = CaptureChartHash();
            var automaticRange = _lineChart.VisibleRange;
            var viewportEvents = 0;
            void ViewportChanged(object? sender, EventArgs eventArgs) => viewportEvents++;
            foreach (var chart in AllCharts) chart.ViewportChanged += ViewportChanged;
            try
            {
                SetSmokeViewports();
                await SmokeFlushAsync();
                checks["程序视图改变图形"] = CaptureChartHash() != automaticChart;
                checks["三种图表可见范围符合视图"] = HasVisibleRange(_lineChart, new ChartViewport(2, 8, 30, 100)) &&
                    HasVisibleRange(_barChart, new ChartViewport(1, 4, 0, 80)) && HasVisibleRange(_scatterChart, new ChartViewport(30, 85, 40, 200));
                checks["三种图表绘图区有效"] = AllCharts.All(chart => chart.PlotArea.Width > 100 && chart.PlotArea.Height > 100);
                checks["当前视图状态已更新"] = _viewportStatus.Text?.Contains("视图：X", StringComparison.Ordinal) == true;
                _scroll.Offset = default;
                _status.Text = "视图效果示例 · 已放大三张图中的局部数据，可使用“重置视图”返回全部数据。";
                CaptureSmokeImage(zoomedImage);
                var lightTooltip = CaptureTooltipSmokeImage(tooltipImage);
                checks["亮色路由悬停命中目标数据点"] = lightTooltip.Hit;
                checks["亮色悬停提示改变实际渲染"] = lightTooltip.RenderChanged;

                ResetViews();
                await SmokeFlushAsync();
                checks["重置恢复自动视图"] = AllCharts.All(chart => chart.Viewport is null) &&
                    _viewportStatus.Text == "视图：自动适配全部数据" && automaticRange is { } range && HasVisibleRange(_lineChart, range);
                checks["视图变化事件覆盖设置与重置"] = viewportEvents >= 6;

                SetSmokeViewports();
                RefreshData();
                checks["刷新数据同时重置视图"] = AllCharts.All(chart => chart.Viewport is null);
                SetSmokeViewports();
                ResetData();
                checks["恢复数据同时重置视图"] = AllCharts.All(chart => chart.Viewport is null);
                await SmokeFlushAsync();
            }
            finally
            {
                foreach (var chart in AllCharts) chart.ViewportChanged -= ViewportChanged;
            }

            var originalWidth = _hostWindow.Width;
            _hostWindow.Width = 1080;
            await SmokeFlushAsync();
            checks["窄窗口纵向排列"] = !_threeColumns && _barCard.Bounds.Top > _lineCard.Bounds.Top && _scatterCard.Bounds.Top > _barCard.Bounds.Top && AllCharts.All(chart => chart.Bounds.Width > 250);
            _hostWindow.Width = originalWidth;
            await SmokeFlushAsync();
            checks["宽窗口三列排列"] = _threeColumns && _barCard.Bounds.Left > _lineCard.Bounds.Left && _scatterCard.Bounds.Left > _barCard.Bounds.Left;

            if (_hostWindow is MainWindow mainWindow)
            {
                mainWindow.SelectPage("overview");
                checks["离开页面释放活动状态"] = !_active;
                mainWindow.SelectPage("charts");
                await SmokeFlushAsync();
                checks["回到页面保留数据并恢复"] = _active && _visits.Count == 12 && !_disposed;
            }

            _scroll.Offset = default;
            var light = CaptureSmokeImage(image);
            var lightCardBrush = _cards[0].Background;
            _hostWindow.RequestedThemeVariant = ThemeVariant.Dark;
            await SmokeFlushAsync();
            SetSmokeViewports();
            var darkTooltip = CaptureTooltipSmokeImage(darkTooltipImage);
            checks["暗色路由悬停命中目标数据点"] = darkTooltip.Hit;
            checks["暗色悬停提示改变实际渲染"] = darkTooltip.RenderChanged;
            ResetViews();
            _hostWindow.UpdateLayout();
            var dark = CaptureSmokeImage(darkImage);
            checks["亮暗主题更新卡片及图形"] = _hostWindow.ActualThemeVariant == ThemeVariant.Dark &&
                !Equals(lightCardBrush, _cards[0].Background) && light != dark;
            checks["亮暗主题保留数据"] = _visits.Count == 12 && _online.Count == 6 && _normalLatency.Count == 24;
            _hostWindow.RequestedThemeVariant = ThemeVariant.Light;
            await RunDataAndAxesSmokeAsync(checks, advancedImage);
            await RunAxisFeaturesSmokeAsync(checks, axisImage, axisDarkImage, breakPixelsImage, breakPixelsDarkImage);
            await RunCommonFeaturesSmokeAsync(checks, commonImage, commonDarkImage, commonExportImage, commonDarkExportImage);
            await RunShapeAnnotationsSmokeAsync(checks, image);
            await RunTooltipTemplateSmokeAsync(checks, image);
            await RunControlTemplateSmokeAsync(checks, image);
            await RunTrackballSmokeAsync(checks, image);
            await RunSelectionRangeSmokeAsync(checks, image);
            await RunInputGesturesSmokeAsync(checks, image);
            await RunAnnotationCompletionSmokeAsync(checks, image);
            await RunAnimationSmokeAsync(checks, image);
            await RunExportCompletionSmokeAsync(checks, image);
            await RunAccessibilitySmokeAsync(checks, image);
            await RunLineAreaSmokeAsync(checks, image);
            await RunBarComparisonSmokeAsync(checks, image);
            await RunScatterBubbleSmokeAsync(checks, image);
            await RunCircularSmokeAsync(checks, image);
            await RunPolarSmokeAsync(checks, image);
            await RunGaugeMicroSmokeAsync(checks, image);
            await RunAnalysisSmokeAsync(checks, image);
            await RunLevelOfDetailSmokeAsync(checks, image);
            await RunEfficientHitSmokeAsync(checks, image);
            await RunIncrementalStreamingSmokeAsync(checks, image);
            await RunRenderResourceSmokeAsync(checks, image);
            await RunMultiChartSmokeAsync(checks, image);
            await RunStatisticsSmokeAsync(checks, image);
            await RunFinancialSmokeAsync(checks, image);
            await RunTechnicalTrendsSmokeAsync(checks, image);
            await RunEngineeringSmokeAsync(checks, image);
            await RunAdvancedComparisonSmokeAsync(checks, image);
            await RunHierarchySmokeAsync(checks, image);
            await RunRelationsSmokeAsync(checks, image);
            await RunMapsSmokeAsync(checks, image);
            await RunC6CompletionSmokeAsync(checks, image);
            await RunC5CompletionSmokeAsync(checks, image);
            }
        }
        catch (Exception exception) { error = exception.ToString(); }

        var passed = error is null && checks.Count > 0 && checks.Values.All(value => value);
        try
        {
            var fullOutput = Path.GetFullPath(output);
            Directory.CreateDirectory(Path.GetDirectoryName(fullOutput)!);
            File.WriteAllText(fullOutput, JsonSerializer.Serialize(new
            {
                通过 = passed,
                验证范围 = args.Contains("--charts-c5-completion-only", StringComparer.Ordinal) ? "c5-completion-only" : args.Contains("--charts-c6-only", StringComparer.Ordinal) ? "c6-main-completion-only" : args.Contains("--charts-comparison-only", StringComparer.Ordinal) ? "advanced-comparison-only" : args.Contains("--charts-engineering-only", StringComparer.Ordinal) ? "engineering-projections-only" : args.Contains("--charts-technical-only", StringComparer.Ordinal) ? "technical-trends-only" : args.Contains("--charts-financial-only", StringComparer.Ordinal) ? "financial-prices-only" : args.Contains("--charts-statistics-only", StringComparer.Ordinal) ? "statistics-only" : args.Contains("--charts-multichart-only", StringComparer.Ordinal) ? "multi-chart-only" : args.Contains("--charts-resources-only", StringComparer.Ordinal) ? "render-resource-only" : args.Contains("--charts-streaming-only", StringComparer.Ordinal) ? "incremental-streaming-only" : args.Contains("--charts-hit-only", StringComparer.Ordinal) ? "efficient-hit-only" : args.Contains("--charts-lod-only", StringComparer.Ordinal) ? "level-of-detail-only" : "complete-charts-demo",
                时间UTC = DateTimeOffset.UtcNow,
                平台 = RuntimeInformation.OSDescription,
                运行时 = Environment.Version.ToString(),
                Avalonia版本 = typeof(Application).Assembly.GetName().Version?.ToString(),
                窗口宽DIP = _hostWindow.ClientSize.Width,
                窗口高DIP = _hostWindow.ClientSize.Height,
                缩放 = _hostWindow.RenderScaling,
                检查 = checks,
                错误 = error,
                亮色图片 = image,
                暗色图片 = darkImage,
                视图效果图片 = zoomedImage,
                亮色提示图片 = tooltipImage,
                暗色提示图片 = darkTooltipImage,
                业务坐标轴图片 = advancedImage,
                进阶坐标轴图片 = axisImage,
                进阶坐标轴暗色图片 = axisDarkImage,
                断轴像素图片 = breakPixelsImage,
                断轴像素暗色图片 = breakPixelsDarkImage,
                断轴像素证据 = _breakPixelEvidence,
                共用交互图片 = commonImage,
                共用交互暗色图片 = commonDarkImage,
                PNG导出图片 = commonExportImage,
                PNG暗色导出图片 = commonDarkExportImage,
                PNG导出证据 = _commonExportEvidence,
                浮动图例证据 = _legendLayoutEvidence,
                形状标注图片 = _shapeSmokeEvidence?.LightImage,
                形状标注暗色图片 = _shapeSmokeEvidence?.DarkImage,
                形状断轴像素图片 = _shapeSmokeEvidence?.LightPixelsImage,
                形状断轴像素暗色图片 = _shapeSmokeEvidence?.DarkPixelsImage,
                形状PNG导出图片 = _shapeSmokeEvidence?.LightExportImage,
                形状PNG暗色导出图片 = _shapeSmokeEvidence?.DarkExportImage,
                形状标注证据 = _shapeSmokeEvidence,
                业务提示模板证据 = _tooltipTemplateEvidence,
                图例与数据标签模板证据 = _controlTemplateEvidence,
                Trackball证据 = _trackballEvidence,
                选择范围证据 = _selectionRangeEvidence,
                输入完整性证据 = _inputGesturesEvidence,
                标注参考完整性证据 = _annotationCompletionEvidence,
                动画完整性证据 = _animationEvidence,
                animationDiagnosticEvidence = _animationDiagnosticEvidence,
                导出完整性证据 = _exportCompletionEvidence,
                辅助功能完整性证据 = _accessibilityEvidence,
                折线面积完整性证据 = _lineAreaEvidence,
                柱条比较完整性证据 = _barComparisonEvidence,
                scatterBubbleEvidence = _scatterBubbleEvidence,
                circularEvidence = _circularEvidence,
                polarEvidence = _polarEvidence,
                gaugeMicroEvidence = _gaugeMicroEvidence,
                analysisEvidence = _analysisEvidence,
                levelOfDetailEvidence = _levelOfDetailEvidence,
                efficientHitEvidence = _efficientHitEvidence,
                incrementalStreamingEvidence = _incrementalStreamingEvidence,
                renderResourceEvidence = _renderResourceEvidence,
                multiChartEvidence = _multiChartEvidence,
                statisticsEvidence = _statisticsEvidence,
                financialEvidence = _financialEvidence,
                technicalEvidence = _technicalEvidence,
                engineeringProbes = _engineeringProbes,
                comparisonProbes = _comparisonProbes,
                hierarchyProbes = _hierarchyProbes, hierarchyAnimationEvidence = _hierarchyAnimationEvidence,
                relationsProbes = _relationsProbes, relationsAsyncEvidence = _relationsAsyncEvidence,
                mapProbes = _mapProbes, mapAsyncEvidence = _mapAsyncEvidence,
                c6Probes = _c6Probes,
                c5Probes = _c5CompletionProbes,
                输入完整性亮色图片 = _inputGesturesEvidence.FirstOrDefault()?.PageImage,
                输入完整性暗色图片 = _inputGesturesEvidence.LastOrDefault()?.PageImage,
                选择范围亮色图片 = _selectionRangeEvidence.FirstOrDefault()?.PageImage,
                选择范围暗色图片 = _selectionRangeEvidence.LastOrDefault()?.PageImage,
                系列选择亮色导出图片 = _selectionRangeEvidence.FirstOrDefault()?.ExportImage,
                系列选择暗色导出图片 = _selectionRangeEvidence.LastOrDefault()?.ExportImage,
                Trackball亮色图片 = _trackballEvidence.FirstOrDefault()?.PageImage,
                Trackball暗色图片 = _trackballEvidence.LastOrDefault()?.PageImage,
                Trackball亮色像素图片 = _trackballEvidence.FirstOrDefault()?.PixelImage,
                Trackball暗色像素图片 = _trackballEvidence.LastOrDefault()?.PixelImage,
                Trackball亮色导出图片 = _trackballEvidence.FirstOrDefault()?.ExportImage,
                Trackball暗色导出图片 = _trackballEvidence.LastOrDefault()?.ExportImage,
                图例标签模板图片 = _controlTemplateEvidence.FirstOrDefault()?.PageImage,
                图例标签模板暗色图片 = _controlTemplateEvidence.LastOrDefault()?.PageImage,
                图例标签模板像素图片 = _controlTemplateEvidence.FirstOrDefault()?.PixelImage,
                图例标签模板暗色像素图片 = _controlTemplateEvidence.LastOrDefault()?.PixelImage,
                图例标签模板PNG图片 = _controlTemplateEvidence.FirstOrDefault()?.ExportImage,
                图例标签模板暗色PNG图片 = _controlTemplateEvidence.LastOrDefault()?.ExportImage,
                输入验证方式 = "synthetic-routed-pointer-and-key-and-programmatic-selection-viewport-and-export",
                限制 = new[]
                {
                    "图像由可见原生 Window 使用 Avalonia RenderTargetBitmap 渲染，不是桌面截图。",
                    "检查覆盖示例数据更新、页面生命周期、两种布局、亮暗主题、交互选项与程序视图；视图效果图由程序设置视图生成。真实鼠标、触屏、辅助工具和其他平台另行验收。",
                    "提示图通过公开 Pointer 与 PointerMovedEvent 合成路由悬停，核对 HoveredPoint 并验证渲染变化；不是物理鼠标输入。",
                    "断轴像素检查在原生窗口内渲染固定背景的独立折线图，读取RenderTargetBitmap像素；覆盖横纵断轴亮暗四种情况，不代表桌面合成器呈现或性能验收。",
                    "C2 图例点击、共享悬停与方向/Home/Esc键使用合成路由输入；矩形选择与PNG导出调用公开API。PNG使用控件导出接口，不是窗口截图。",
                    "形状标注检查使用程序属性更新与独立原生窗口RenderTargetBitmap；亮暗主题分别验证矩形、椭圆、双端箭头跨横纵断轴的两侧墨迹与缝内背景，并检查椭圆角落和箭头端部像素。不是物理输入或桌面合成器截图。",
                    "业务提示模板使用独立原生窗口和合成路由悬停，核对实际控件、原始业务对象与逐轴反算；像素验证参考线方向、非空提示徽标和三种DPI PNG内容，不代表物理输入或桌面合成器截图。",
                    "图例和数据标签模板使用独立原生窗口及合成路由滚轮/点击；分别读取业务编号和滚动末项文字区域的RenderTargetBitmap及PNG像素，不代表物理输入、桌面合成器呈现或性能验收。",
                    "Trackball使用合成路由悬停与独立原生窗口RenderTargetBitmap，覆盖方向/显示/snap组合、插值数学及两个原始端点；文字用隐藏恢复逐像素正负对照，PNG覆盖三种DPI，不代表物理输入或性能验收。",
                    "系列选择、横竖范围条和多图范围联动使用原生窗口内合成路由点击/拖动/滚轮/键盘，核对实际端点、捕获与目标范围；淡化和三种DPI导出有逐像素正负证据。本期不含跨图游标/选择同步，不代表物理输入、Linux实机或性能验收。",
                    "输入完整性使用原生窗口内合成多触点Pointer事件及连续小数XY滚轮事件，独立计算捏合比例、质心、平移和缩放锚点，并记录图表处理阶段、真实ScrollViewer偏移、捕获释放与三种DPI正负导出。平台未提供可靠硬件种类标识，合成滚轮不能证明物理触控板、触屏、系统手势路由或Linux实机行为。",
                    "辅助功能完整性在原生窗口内核对点级辅助提供器、数据摘要与合成键盘路由，并对高对比系列投影和三种DPI PNG/JPEG执行独立数值与像素检查。此Demo记录本身不代表外部UIA/AT-SPI桥接、真实读屏器播报或物理键盘验收；平台桥接探针应单独记录。",
                    "折线面积完整性使用原生窗口内RenderTargetBitmap，核对五种路径的墨迹与空白、面积基线、范围上下界、正负堆叠及百分比贡献，并比较三种DPI PNG。选择、视图及数据开关通过公开API或控件程序值执行，不代表物理输入或性能验收。",
                    "柱条比较完整性使用原生窗口内RenderTargetBitmap，按预注册数学坐标核对横竖柱、正负堆叠与百分比、范围、瀑布、Dot、Lollipop、Variance、Pareto的墨迹和空白；三种DPI PNG要求同尺寸精确像素及密度。交互通过公开API和控件程序值，不代表物理输入或性能验收。",
                },
            }, new JsonSerializerOptions { WriteIndented = true, NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals }));
        }
        finally
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.Shutdown(passed ? 0 : 1);
            else _hostWindow.Close();
        }
    }

    private void SetSmokeViewports()
    {
        _lineChart.Viewport = new ChartViewport(2, 8, 30, 100);
        _barChart.Viewport = new ChartViewport(1, 4, 0, 80);
        _scatterChart.Viewport = new ChartViewport(30, 85, 40, 200);
    }

    private static bool HasVisibleRange(CartesianChart chart, ChartViewport expected) => chart.VisibleRange is { } actual &&
        Math.Abs(actual.XMinimum - expected.XMinimum) < .0001 && Math.Abs(actual.XMaximum - expected.XMaximum) < .0001 &&
        Math.Abs(actual.YMinimum - expected.YMinimum) < .0001 && Math.Abs(actual.YMaximum - expected.YMaximum) < .0001;

    private (bool Hit, bool RenderChanged) CaptureTooltipSmokeImage(string path)
    {
        _hostWindow.UpdateLayout();
        var before = CaptureChartHash();
        var range = _lineChart.VisibleRange ?? throw new InvalidOperationException("折线图尚未生成可见范围。");
        var plot = _lineChart.PlotArea;
        const int index = 5;
        var point = _visits[index];
        var position = new Point(
            plot.Left + (point.X - range.XMinimum) / (range.XMaximum - range.XMinimum) * plot.Width,
            plot.Bottom - (point.Y - range.YMinimum) / (range.YMaximum - range.YMinimum) * plot.Height);
        var windowPoint = _lineChart.TranslatePoint(position, _hostWindow)
            ?? throw new InvalidOperationException("折线图数据点无法转换为原生窗口坐标。");
        using var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        _lineChart.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, _lineChart, pointer,
            _hostWindow, windowPoint, 1, default, KeyModifiers.None));
        var hit = _lineChart.HoveredPoint;
        var hitExpected = hit is not null && ReferenceEquals(hit.Series, _lineChart.Series[0]) && hit.Index == index && hit.Point == point;
        var rendered = CaptureChartHash() != before;
        CaptureSmokeImage(path);
        return (hitExpected, rendered);
    }

    private string CaptureChartHash()
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(_lineChart.Bounds.Width), (int)Math.Ceiling(_lineChart.Bounds.Height)), new Vector(96, 96));
        bitmap.Render(_lineChart);
        using var stream = new MemoryStream();
        bitmap.Save(stream);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private string CaptureSmokeImage(string path)
    {
        var size = _hostWindow.ClientSize;
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height)), new Vector(96, 96));
        bitmap.Render(_hostWindow);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        bitmap.Save(path);
        using var stream = new MemoryStream();
        bitmap.Save(stream);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private async Task SmokeFlushAsync() { await Task.Delay(100); _hostWindow.UpdateLayout(); }
    private static string? SmokeArgument(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
