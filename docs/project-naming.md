# 项目命名

项目品牌和代码前缀为 `NT`。

| 项目 | 当前名称或位置 |
| --- | --- |
| 解决方案 | `NT.AvaloniaControl.slnx` |
| 控件库目录 | `src/NT.AvaloniaControl.*` |
| Demo | `samples/NT.AvaloniaControl.Demo/NT.AvaloniaControl.Demo.csproj` |
| 程序集、包与命名空间 | `NT.AvaloniaControl.*` |
| XAML 程序集资源 | `avares://NT.AvaloniaControl.<控件或主题>/...` |
| PDF Viewer XML 命名空间 | `https://schemas.nt-controls.dev/pdfviewer` |
| 包作者 | `NT` |

例如：

```csharp
using NT.AvaloniaControl.Charts;
using NT.AvaloniaControl.TreeDataGrid;
```

```xml
xmlns:charts="clr-namespace:NT.AvaloniaControl.Charts;assembly=NT.AvaloniaControl.Charts"
```

```powershell
dotnet build NT.AvaloniaControl.slnx
dotnet run --project samples/NT.AvaloniaControl.Demo
```
