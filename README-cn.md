# EVE-Preview

Windows x64 的 EVE Online 多客户端原始预览与窗口管理工具。为配置指定的 EVE 客户端显示未经修改的实时缩略图，支持客户端切换，并可保存和恢复客户端窗口布局。

[English README](README.md)

## 项目状态

- **许可证：** [Unlicense](LICENSE)，即公有领域。任何人都可以复制、修改、再分发或销售本项目，无需额外授权或署名。
- **AI 生成：** 本项目包含大量由 AI 生成的代码和文档。在关键环境中使用前请自行审查和测试。
- **欢迎 Fork：** 可以自由 Fork、修改并依据 Unlicense 再发布，无需额外申请许可。

## 功能

- 进程名监控、多客户端发现、窗口标题跟踪和安全清理。
- DWM 实时缩略图，并可回退到 GDI 兼容预览。
- 物理像素尺寸、不透明度、置顶、位置锁定、网格/边缘吸附、悬停缩放、活动客户端高亮和隐私遮挡。
- 每个客户端独立配置别名、全局热键、循环组排除、隐私遮挡和预览开关。
- 按显示器 DPI 处理缩略图，支持混合 DPI 多显示器和按中心点选择显示器。
- 单独或批量保存、应用 EVE 客户端窗口位置与尺寸，并可在启动时自动还原。
- 动态数量循环组，支持全局热键、组热键和客户端独立热键。
- 深色 WPF 管理界面、系统托盘、暂停/恢复、原子配置保存和无效配置自动修复。
- 简体中文和英文界面。

## 交互边界

EVE-Preview 只读取配置指定的客户端进程 ID、主窗口句柄、标题和窗口位置，并显示未经修改的 DWM/GDI 原始画面。它**不会**修改 EVE 界面或游戏文件、注入 DLL、读写游戏内存、Hook 渲染或网络、发送键盘或鼠标输入，也不执行游戏自动化。

对 EVE 客户端的唯一写操作是允许的窗口管理：前台切换、恢复、移动、调整大小、最小化和最大化。

## 系统要求

- Windows 10 1809（10.0.17763）x64 或更高版本。
- 从源码构建需要 .NET 10 SDK。
- 发布版为自包含程序，目标机无需安装 .NET。

## 下载

请从 [Releases](https://github.com/extrame911/EVE-Preview/releases) 页面下载最新单文件版本。

单文件版本为自包含程序，直接运行 `EVE-Preview.exe`。目录版本包含可执行文件和运行时依赖。

## 构建与测试

```powershell
dotnet restore EVE-Preview.sln
dotnet build EVE-Preview.sln -c Release -warnaserror
dotnet test EVE-Preview.sln -c Release
```

发布自包含目录版：

```powershell
dotnet publish src/EVE-Preview.App/EVE-Preview.App.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=false -o dist/folder
```

发布自包含单文件版：

```powershell
dotnet publish src/EVE-Preview.App/EVE-Preview.App.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true -o dist/single
```

## Mock 客户端

`mock/EVE-Preview.Mock` 构建为 `ExeFile.exe`，显示动态时钟，仅用于验证预览渲染和允许的窗口管理操作。先启动 Mock，再启动 EVE-Preview：

```powershell
dotnet build mock/EVE-Preview.Mock/EVE-Preview.Mock.csproj -c Debug
start mock/EVE-Preview.Mock/bin/Debug/net10.0-windows10.0.17763.0/ExeFile.exe
start src/EVE-Preview.App/bin/Debug/net10.0-windows10.0.17763.0/win-x64/EVE-Preview.exe
```

## 配置

默认配置文件为可执行文件同目录下的 `EVE-Preview.json`。普通设置变更后 500 ms 防抖保存，退出时还会同步保存最新布局。保存流程使用同目录临时文件、`.bak` 备份和原子替换。

无效配置按顶层模块修复并自动回写，原文件保留为 `.corrupt-*.bak`。无效客户端条目会逐条丢弃或降级，其他客户端的设置不受影响。

## 项目结构

```text
src/EVE-Preview.Core/
  Configuration/       配置模型、修复、原子存储、旧版导入
  Clients/             客户端身份、标题、快照和进程名处理
  Layout/              网格吸附与布局约束
  Hotkeys/             热键解析
  CycleGroups/         循环组模型
src/EVE-Preview.Windows/
  Hotkeys/  Previews/  Processes/  WindowManagement/
src/EVE-Preview.App/
  Services/Runtime/          预览运行时及生命周期/轮询/持久化分片
  Services/Previews/         预览窗口、覆盖层、DPI、显示器和吸附几何
  Services/Localization/     本地化和视觉文本刷新
  Services/Infrastructure/   托盘、单实例、配置档案、字体和构建信息
  ViewModels/Shell/          主窗口、总览和页面状态
  ViewModels/Clients/        客户端列表与属性
  ViewModels/CycleGroups/    循环组
  ViewModels/Settings/       系统、缩略图和热键设置
  Views/                     XAML 页面（保持路径以兼容 pack URI）
  Themes/                    调色板、控件样式、图标和合并主题入口
tests/
  EVE-Preview.Core.Tests/
  EVE-Preview.Windows.Tests/
  EVE-Preview.App.Tests/
```

详细设计和行为约束见 [DESIGN.md](DESIGN.md)。

## 许可证

本项目依据 [Unlicense](LICENSE) 发布到公有领域，属于自由且无约束的软件。

任何人都可以以源码或编译后二进制形式复制、修改、发布、使用、编译、销售或分发本软件，可用于商业或非商业目的，也可采用任何方式分发。

## Fork 与贡献

欢迎 Fork、修改、制作衍生项目或提交 Pull Request。依据 Unlicense，发布修改版本无需授权或署名，但保留指向原项目的链接会有所帮助。
