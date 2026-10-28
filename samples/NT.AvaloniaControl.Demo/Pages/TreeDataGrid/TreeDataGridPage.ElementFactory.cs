using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Media;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace NT.AvaloniaControl.Demo.Pages.TreeDataGrid;

internal sealed partial class TreeDataGridPage
{
    private Control _elementFactoryTools = null!;
    private readonly TextBlock _elementFactoryStats = new() { TextWrapping = TextWrapping.Wrap };
    private DemoElementFactory? _elementFactoryA, _elementFactoryB;
    private Control CreateElementFactoryTools()
    {
        var actions = new WrapPanel { ItemSpacing = 8, LineSpacing = 6 };
        AddButton(actions, "编辑首行名称", () =>
        {
            if (_source is not null) _grid.BeginEdit(new(_source.Rows[0].Id, _grid.Columns[1].Id));
        }, "输入草稿后切换工厂，草稿仍属于原 NodeId/ColumnId。", "BeginFactoryEditButton");
        AddButton(actions, "使用工厂 A", () => { _grid.ElementFactory = _elementFactoryA; UpdateElementFactoryStats(); }, "青色容器装饰。", "UseFactoryAButton");
        AddButton(actions, "使用工厂 B", () => { _grid.ElementFactory = _elementFactoryB; UpdateElementFactoryStats(); }, "紫色容器装饰；旧工厂容器和订阅释放。", "UseFactoryBButton");
        AddButton(actions, "恢复默认工厂", () => { _grid.ElementFactory = null; UpdateElementFactoryStats(); }, "保留选择和编辑草稿，重新创建默认容器。", "UseDefaultFactoryButton");
        AddButton(actions, "提交草稿", () => _lastAction.Text = _grid.CommitEdit() ? "已提交原编辑目标。" : _grid.EditSession?.ValidationError ?? "没有草稿。", "通过原列的转换和验证再写入。", "CommitFactoryEditButton");
        AddButton(actions, "取消草稿", () => _grid.CancelEdit(), "恢复模型原值。", "CancelFactoryEditButton");
        return new StackPanel { Spacing = 6, Children =
        {
            new TextBlock { Text = "工厂创建行、单元格和列头，定制容器模板并管理订阅；列绑定、编辑与自动化继续由标准容器处理。切换工厂不会提交草稿，旧容器清理后不进入新工厂的池。", TextWrapping = TextWrapping.Wrap },
            actions, _elementFactoryStats,
        } };
    }
    private void ShowElementFactoryScenario()
    {
        ShowEditingScenario();
        _elementFactoryA = new("A", Brushes.Teal, UpdateElementFactoryStats);
        _elementFactoryB = new("B", Brushes.MediumPurple, UpdateElementFactoryStats);
        _p6ScenarioId = "element-factory"; _scenario = "元素工厂 · 三类容器生命周期 · 草稿与身份保留";
        _grid.ElementFactory = _elementFactoryA;
        _lastAction.Text = "编辑首行名称，再切换 A/B 或恢复默认；对比容器装饰、活动订阅和释放计数。";
        UpdateElementFactoryStats(); UpdateScenarioUi();
    }
    private void UpdateElementFactoryStats()
    {
        string Stats(DemoElementFactory? factory) => factory is null ? "" : $"{factory.Label}：创建 {factory.Created} / 准备 {factory.Prepared} / 清理 {factory.Cleared} / 回收 {factory.Recycled} / 释放 {factory.Released} / 活动 {factory.Active} / 订阅 {factory.Subscriptions}";
        _elementFactoryStats.Text = Stats(_elementFactoryA) + "\n" + Stats(_elementFactoryB);
    }
    private sealed class DemoElementFactory(string label, IBrush accent, Action changed) : TreeDataGridElementFactory
    {
        private readonly Dictionary<Control, (INotifyPropertyChanged Model, PropertyChangedEventHandler Handler)> _subscriptions = [];
        public string Label { get; } = label;
        public int Created, Prepared, Cleared, Recycled, Released, Active;
        public HashSet<TreeDataGridElementKind> Kinds { get; } = [];
        public int Subscriptions => _subscriptions.Count;
        public override Control CreateElement(GridControl owner, TreeDataGridElementKind kind)
        {
            var element = base.CreateElement(owner, kind); Created++; Kinds.Add(kind); element.Tag = "factory-" + Label;
            if (element is TreeDataGridCellPresenter cell)
                cell.Template = new FuncControlTemplate<TreeDataGridCellPresenter>((_, _) => new Grid { Children =
                {
                    new Border { Name = "FactoryCellAccent", BorderBrush = accent, BorderThickness = new Thickness(3, 0, 0, 0),
                        Child = new ContentPresenter
                        {
                            [!ContentPresenter.ContentProperty] = new TemplateBinding(ContentControl.ContentProperty),
                            [!ContentPresenter.ContentTemplateProperty] = new TemplateBinding(ContentControl.ContentTemplateProperty),
                            [!ContentPresenter.BackgroundProperty] = new TemplateBinding(ContentControl.BackgroundProperty),
                            [!ContentPresenter.PaddingProperty] = new TemplateBinding(ContentControl.PaddingProperty),
                        } },
                    new Border { Name = "PART_StateIndicator", IsHitTestVisible = false,
                        [!Border.BorderBrushProperty] = new TemplateBinding(ContentControl.ForegroundProperty) },
                } });
            changed(); return element;
        }
        public override void PrepareElement(TreeDataGridElementContext context)
        {
            Prepared++; Active++;
            if (context.Kind == TreeDataGridElementKind.Cell && context.Row!.Model is INotifyPropertyChanged notifying)
            {
                PropertyChangedEventHandler handler = (_, _) => changed();
                _subscriptions.Add(context.Element, (notifying, handler)); notifying.PropertyChanged += handler;
            }
            changed();
        }
        public override void ClearElement(TreeDataGridElementContext context)
        {
            Cleared++; Active--;
            if (_subscriptions.Remove(context.Element, out var subscription)) subscription.Model.PropertyChanged -= subscription.Handler;
            changed();
        }
        public override bool RecycleElement(TreeDataGridElementContext context) { Recycled++; changed(); return true; }
        public override void ReleaseElement(GridControl owner, TreeDataGridElementKind kind, Control element) { Released++; changed(); }
    }
}
