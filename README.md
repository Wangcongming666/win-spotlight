# FocusSearch · 聚焦搜索

轻量 Windows 桌面启动器，C# / .NET Framework 4.8 原生实现，无第三方依赖。

## 下载与运行

普通用户在仓库的 **Releases** 页面下载 `FocusSearch-版本-windows-portable.zip`，解压后双击 `FocusSearch.exe` 即可使用，无需下载源码或安装开发工具。也可下载单独的 exe。初次发布前，可用本地 `dist` 中的同名便携包。

运行后，右下角通知区域会出现蓝色放大镜图标；右键可以打开设置或退出。如果图标被 Windows 折叠，点击托盘箭头即可找到。

详细说明见 [便携版使用说明](docs/INSTALL.md)，版本变化见 [更新记录](CHANGELOG.md)。

## 使用

双击 `build/FocusSearch.exe`。启动后按 **Alt + Space** 显示或收起搜索面板。

- 默认组合模式：搜索框下方显示应用网格；输入后切换到搜索结果。
- 托盘右键 → 设置：取消“搜索框与应用程序一起显示”后，搜索框独立显示；**Alt + A** 打开全屏应用网格。
- 方向键选择，Enter 打开，Esc 收起；网格可使用滚轮或 Page Up / Page Down 翻页。
- 点击应用图标直接启动。设置中可以修改快捷键、文件搜索、失焦收起和开机启动。
- 程序常驻托盘，右键 → 退出可释放全局快捷键。

## 文件搜索范围

优先搜索 Windows Search 已索引的文件名。未索引的目录不会自动加入系统索引。

如果系统未安装或未注册 Windows Search 提供程序，回退搜索桌面、文档和用户 Downloads 目录；最多访问 5000 个文件，深入两级子目录，时间预算 250 ms。这个回退是有限范围搜索，不能代替全盘检索。当前检查环境缺少 `Search.CollatorDSO.1`，已验证回退行为。

系统搜索通配符语法参考 [Microsoft Windows Search LIKE 文档](https://learn.microsoft.com/en-us/windows/win32/search/-search-sql-like)。

## 构建与验证

Windows 10/11，需 .NET Framework 4.8。使用 Windows 自带 C# 编译器，无需 SDK、NuGet 或联网。

```powershell
pwsh -NoProfile -File .\build.ps1
pwsh -NoProfile -File .\build.ps1 -Test
pwsh -NoProfile -File .\run.ps1
pwsh -NoProfile -File .\package.ps1
```

没有 PowerShell 7 时可将 `pwsh` 替换为 `powershell -ExecutionPolicy Bypass`。也可以双击 `启动.cmd`。

核心测试覆盖匹配排序、配置往返、损坏配置、历史计数、索引过滤与去重、搜索转义、取消、热键冲突回滚和原生输入框消息。UI 冒烟可运行 `build/FocusSearch.Tests.exe --ui-smoke`，将布局截图和真实屏幕截图保存到 build 目录；这些本机截图不纳入发布包。`--perf-smoke` 可测量翻页事件处理耗时。

## 外观与限制

原生圆角、深色模糊玻璃面板；每次唤起采集窗口覆盖区域并生成模糊快照，收起后释放，不持续截屏或保存屏幕内容。使用单一背景渲染路径，避免 Acrylic 与 WinForms 编辑框叠加产生白条。应用图标从 Windows Shell 读取，应用自身与托盘使用专用多尺寸放大镜图标。

图标优先读取 [Shell 的 Jumbo 图像列表](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shgetimagelist)，去除透明留白后按统一比例绘制；缺失图标使用圆角字母标识。窗口以 180 ms 淡入上移、120 ms 淡出收起，悬停反馈约 130 ms。支持在设置关闭动画，也遵循 Windows 的客户端动画偏好；动画定时器在过渡结束后停止。

Shell 图标由单独的 STA 后台线程提取，绘制与滚轮事件不再执行文件/COM 图标读取。优先加载并保护当前页，按剩余缓存容量预取相邻页，缓存最多 96 项；高速滚轮事件按短间隔合并，悬停只重绘受影响区域。首次进入未缓存页面可能短暂显示占位图标。隐藏后暂停尚未开始的图标任务。

应用来自当前用户及公共开始菜单和桌面。快捷方式可解析时按真实目标、启动参数和工作目录去重；无法解析时保留独立路径，避免隐藏不同目标。第一版不枚举没有开始菜单入口的商店应用，不提供图标拖拽重排、应用文件夹或文件全文检索。

设置及历史位于 `%LOCALAPPDATA%\FocusSearch`。卸载时先取消开机启动，再从托盘退出，删除项目文件夹即可；本地历史目录可按需保留或自行删除。

## 当前验证结果

24 项核心测试通过，窗口开闭、中断重开与原生热键冒烟通过。冷翻页事件处理在同机同组测量中由平均 198.83 ms / 最大 373.17 ms 降到平均 0.06 ms / 最大 0.46 ms；这是事件处理时间，不代表整个页面图标加载时间。之前版本的空闲一分钟 CPU 时间增量为 0 秒、工作集约 41 MB；新版的内存会受异步图标缓存影响。Windows Search 系统提供程序查询仍需在接口可用的桌面环境中验收。

重新编译前请从托盘退出，或运行 `build/FocusSearch.exe --quit`；正在运行的 exe 无法被编译器覆盖。

## GitHub 发布

主分支推送或手动触发 `.github/workflows/release.yml` 后，Windows 工作流执行测试、打包，并创建带 zip、exe 和 SHA-256 校验文件的 Release。不会覆盖已发布的同版本资产；发布新版本时请修改 `src/AssemblyInfo.cs` 的版本号。

发布脚本使用 [GitHub Releases API](https://docs.github.com/en/rest/releases/releases) 和 Actions 的仓库令牌，不在源码保存个人访问令牌。仓库需要允许 Actions 使用 `contents: write`；第一次远程发布只有在仓库存在并允许写入后才能执行。
