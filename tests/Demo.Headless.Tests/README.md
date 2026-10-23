# Demo 导航回归

覆盖主页卡片、各控件分类、Charts 的全部 36 个示例入口、返回与面包屑、TreeDataGrid / MVVM 切换、Markdown 场景、PDF 布局、虚拟键盘离开后的焦点释放，以及亮暗主题和自适应卡片布局。

```powershell
dotnet test tests/Demo.Headless.Tests/Demo.Headless.Tests.csproj
```

也可以使用 xUnit 进程内运行器，避免受限环境中的测试宿主进程通信问题：

```powershell
dotnet build tests/Demo.Headless.Tests/Demo.Headless.Tests.csproj
dotnet tests/Demo.Headless.Tests/bin/Debug/net10.0/Demo.Headless.Tests.dll -parallel none
```

设置 `DEMO_GALLERY_SCREENSHOTS` 为输出目录，可在布局测试中生成主页和 Charts 分类页的亮暗主题截图。图片为真实 Avalonia 控件的离屏渲染。
