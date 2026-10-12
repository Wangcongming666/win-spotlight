# FocusSearch

**聚焦搜索 — 面向 Windows 的轻量级桌面搜索与应用启动器。**

FocusSearch 借鉴 macOS Spotlight 与 Launchpad 的交互方式，通过全局快捷键快速查找应用和本机文件。提供玻璃质感搜索面板与应用网格，支持组合显示或独立使用。

[下载版本](https://github.com/Wangcongming666/win-spotlight/releases) · [使用说明](docs/INSTALL.md) · [更新记录](CHANGELOG.md) · [问题反馈](https://github.com/Wangcongming666/win-spotlight/issues)

## 功能特性

- **快捷搜索**：使用 `Alt + Space` 唤起搜索面板，查找应用和文件。
- **应用网格**：以图标网格浏览应用，支持鼠标滚轮翻页与键盘导航。
- **灵活布局**：搜索框与应用网格可组合显示；分离模式下，使用 `Alt + A` 打开全屏应用网格。
- **玻璃质感**：深色模糊背景、圆角面板与过渡动画；动画可在设置中关闭。
- **托盘常驻**：通过通知区域图标访问设置、打开面板或退出程序。
- **本地运行**：基于 C#、WinForms 与 .NET Framework 4.8，无第三方运行库依赖，不上传搜索数据。

## 系统要求

- Windows 10 / Windows 11（x64）。
- .NET Framework 4.8 或 4.8.1。

使用便携版无需安装 .NET SDK、Node.js 或其他开发工具。

## 下载与运行

1. 前往 [Releases](https://github.com/Wangcongming666/win-spotlight/releases)，下载 `FocusSearch-版本-windows-portable.zip`。
2. 将压缩包解压到可写目录，双击 `FocusSearch.exe`。
3. 按 `Alt + Space` 打开搜索面板。

启动后，Windows 通知区域会显示蓝色放大镜图标。右键该图标可打开设置或退出；若图标被折叠，可在隐藏图标菜单中找到。

发布包提供 `SHA256SUMS.txt` 供校验文件完整性。当前可执行文件未进行商业代码签名，请从本仓库的发布页面获取程序。

## 使用方式

| 操作 | 默认快捷键 / 方式 |
| --- | --- |
| 显示或收起搜索面板 | `Alt + Space` |
| 打开独立应用网格 | `Alt + A`（分离模式且启用应用网格时） |
| 移动选择 | 方向键 |
| 打开选中项 | `Enter` |
| 收起面板 | `Esc` |
| 应用网格翻页 | 鼠标滚轮、`Page Up` / `Page Down` |
| 打开设置或退出 | 右键通知区域图标 |

默认使用组合模式：未输入内容时显示应用网格，输入关键词后显示搜索结果。点击应用图标可直接启动应用。

在设置中可以调整显示模式、全局快捷键、文件搜索、动画、失焦收起与开机启动。若快捷键已被其他程序占用，请更换组合键。

## 搜索范围与限制

### 应用

应用列表来自当前用户及公共开始菜单、桌面中的程序入口。可解析的快捷方式按目标、启动参数和工作目录去重。

当前版本不枚举没有开始菜单入口的商店应用，不支持图标拖拽排序或应用文件夹。

### 文件

优先使用 Windows Search 的已有索引，按文件名及路径搜索；不会自动扩大系统索引范围，也不提供文件内容全文检索。

当 Windows Search 接口不可用时，使用有限范围搜索：桌面、文档及 Downloads 目录，最多访问 5,000 个文件，深入两级子目录，时间预算为 250 ms。未索引或超出回退范围的文件可能不会出现在结果中；本工具并非全盘文件检索引擎。

## 隐私与本地数据

程序运行时不联网，不上传搜索内容。设置与使用历史保存在 `%LOCALAPPDATA%\FocusSearch`。

玻璃背景在面板唤起时读取其覆盖的屏幕区域，生成临时模糊图像；收起后释放，不持续截屏或保存屏幕内容。

更新前请先退出程序，再替换文件。移除程序前请取消开机启动并从托盘退出，然后删除程序所在目录；本地设置与历史可按需保留或删除。

## 从源码构建

构建使用系统目录中的 C# 编译器，无需 .NET SDK、NuGet 或联网下载依赖。在项目根目录运行：

```powershell
# 构建程序
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1

# 构建并运行测试
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test

# 启动程序
powershell -NoProfile -ExecutionPolicy Bypass -File .\run.ps1

# 生成便携发布包
powershell -NoProfile -ExecutionPolicy Bypass -File .\package.ps1
```

也可使用 PowerShell 7（`pwsh`）运行上述脚本，或双击 `启动.cmd` 构建并启动。

构建输出位于 `build`，发布包位于 `dist`。重新编译前请退出正在运行的程序，避免可执行文件被占用。

### 测试

自动化测试覆盖搜索匹配与排序、设置读写、历史记录、应用去重、搜索取消、热键冲突处理及输入框消息处理。

```powershell
# 窗口、布局与热键冒烟测试
.\build\FocusSearch.Tests.exe --ui-smoke

# 翻页事件处理性能测试
.\build\FocusSearch.Tests.exe --perf-smoke
```

界面测试生成的本机截图保存在 `build`，不纳入发布包。性能测试测量翻页事件处理耗时，不代表页面图标全部加载时间或渲染帧率。

## 发布流程

推送至 `main` / `master` 或手动触发 [发布工作流](https://github.com/Wangcongming666/win-spotlight/actions/workflows/release.yml)，将依次完成构建、测试、打包和 GitHub Release 发布。

发布资产包括便携版 ZIP、独立可执行文件和 SHA-256 校验文件。同版本的已发布资产不会被覆盖；发布新版本前，请更新 `src/AssemblyInfo.cs` 中的版本号。工作流需要仓库允许 Actions 使用 `contents: write` 权限。

## 问题反馈

欢迎通过 [GitHub Issues](https://github.com/Wangcongming666/win-spotlight/issues) 提交缺陷或功能建议。报告问题时，请提供系统版本、程序版本、复现步骤与预期行为；附加截图或日志前，请移除个人信息。
