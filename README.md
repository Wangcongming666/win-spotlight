# 聚焦搜索

轻量 Windows 桌面启动器，C# / .NET Framework 4.8 原生实现，无第三方依赖。

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
```

没有 PowerShell 7 时可将 `pwsh` 替换为 `powershell -ExecutionPolicy Bypass`。也可以双击 `启动.cmd`。

核心测试覆盖匹配排序、配置往返、损坏配置、历史计数、索引过滤与去重、搜索转义、取消和热键冲突回滚。UI 冒烟可运行 `build/FocusSearch.Tests.exe --ui-smoke`，将两种界面的布局截图保存到 build 目录。截图使用 DrawToBitmap，不体现系统合成器的真实模糊背景。

## 外观与限制

原生圆角、系统模糊材质、深色玻璃面板；每次唤起采集窗口覆盖区域的模糊快照作为兼容背景，收起后释放，不持续截屏或保存屏幕内容。视觉效果受 Windows 版本和远程桌面影响。应用图标从 Windows Shell 读取。

应用来自当前用户及公共开始菜单和桌面。快捷方式可解析时按真实目标、启动参数和工作目录去重；无法解析时保留独立路径，避免隐藏不同目标。第一版不枚举没有开始菜单入口的商店应用，不提供图标拖拽重排、应用文件夹或文件全文检索。

设置及历史位于 `%LOCALAPPDATA%\FocusSearch`。卸载时先取消开机启动，再从托盘退出，删除项目文件夹即可；本地历史目录可按需保留或自行删除。

## 当前验证结果

主程序约 60 KB。后台索引完成后，实测静置 60 秒 CPU 时间增量 0 秒，工作集约 41 MB，内存未增长；应用数量和图标缓存会影响实际内存。已验证重复启动仅保留一个实例，`--quit` 可正常退出，并通过两种模式的窗口布局冒烟。Windows Search 系统提供程序查询需在提供程序可用的桌面环境中验收。

重新编译前请从托盘退出，或运行 `build/FocusSearch.exe --quit`；正在运行的 exe 无法被编译器覆盖。
