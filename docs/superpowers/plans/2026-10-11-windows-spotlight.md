# Windows 聚焦搜索工具实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 构建一个可用 `Alt + Space` 全局唤起、可搜索本机应用与文件，并能在组合/分离模式间切换的轻量 Windows 玻璃风格启动器。

**Architecture:** 使用 C# 5 与 .NET Framework 4.8，WinForms 承载 UI，Win32 API 负责热键、窗口和系统材质，Windows Search OLE DB 负责文件查询。应用索引、搜索合并、设置持久化和 UI 通过小型接口隔离；所有后台工作均可取消或有界，不自建全盘数据库。

**Tech Stack:** C# 5、.NET Framework 4.8、Windows Forms、Win32/DWM API、System.Data.OleDb、PowerShell 构建脚本。

**Spec:** `docs/superpowers/specs/2026-10-11-windows-spotlight-design.md`

## Global Constraints

- 目标系统为 Windows 10/11 x64，构建为 AnyCPU 并优先 64 位运行。
- 不使用 NuGet、Node.js、Electron、WebView 或其他第三方依赖。
- 使用 `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` 离线构建。
- 默认搜索快捷键为 `Alt + Space`；分离模式应用网格快捷键为 `Alt + A`。
- 默认使用组合模式，搜索结果最多显示 8 条。
- 不联网，不上传数据；设置和历史只写入当前用户本地应用数据目录。
- 空闲时不轮询；Windows Search 不可用时应用搜索仍必须正常工作。

## Review Focus

- 快捷键已被其他程序占用：提示冲突、保留旧配置且程序仍可从托盘操作。
- 查询包含单引号、通配符或非 ASCII 字符：转义后安全查询，不抛出未处理异常。
- 开始菜单含损坏快捷方式或无权访问目录：跳过单项，继续生成其余索引。
- 设置或历史文件为空、截断或字段缺失：回退默认值，不阻止启动。
- 用户快速连续输入并清空：过期后台结果不得覆盖最新空查询或新查询。

---

## 文件结构

- `src/Models.cs`：结果、设置、热键和历史数据模型。
- `src/FuzzyMatcher.cs`：无副作用的匹配与评分。
- `src/SettingsStore.cs`：本地配置和使用历史持久化。
- `src/AppIndex.cs`：开始菜单与桌面应用索引。
- `src/WindowsSearch.cs`：Windows Search OLE DB 查询及降级。
- `src/SearchEngine.cs`：应用/文件结果合并、去重、排序和取消。
- `src/NativeMethods.cs`：Win32、DWM 和 Shell API 声明。
- `src/GlassEffects.cs`：玻璃材质、圆角和阴影兼容回退。
- `src/SearchWindow.cs`：Spotlight 搜索窗口与结果列表。
- `src/AppGridWindow.cs`：组合/全屏应用网格、分页和键盘导航。
- `src/SettingsWindow.cs`：原生设置窗口和快捷键编辑。
- `src/TrayApplicationContext.cs`：热键、托盘、窗口和生命周期编排。
- `src/Program.cs`：入口、单实例和异常日志。
- `tests/TestRunner.cs`：零依赖断言与测试入口。
- `tests/*Tests.cs`：核心逻辑测试。
- `build.ps1`：编译产品和测试程序。
- `run.ps1`：构建并启动开发版本。
- `README.md`：使用、构建、限制和故障排查。

### Task 1：离线构建骨架与核心模型

**Files:**
- Create: `src/Models.cs`
- Create: `src/FuzzyMatcher.cs`
- Create: `tests/TestRunner.cs`
- Create: `tests/FuzzyMatcherTests.cs`
- Create: `build.ps1`

**Interfaces:**
- Produces: `ResultKind`；`SearchItem`；`AppSettings`；`HotkeySpec`；`UsageRecord`；`FuzzyMatcher.Score(string query, string candidate) -> int`。

- [ ] **Step 1: 写失败测试**：覆盖完全匹配、前缀、连续子串、稀疏字符、不匹配、大小写和中文字符；断言分数顺序为完全匹配 > 前缀 > 子串 > 稀疏匹配 > 不匹配。
- [ ] **Step 2: 执行失败测试**：运行 `powershell -ExecutionPolicy Bypass -File .\build.ps1 -Test`，预期因模型与匹配器缺失而失败。
- [ ] **Step 3: 实现模型与 `FuzzyMatcher.Score`**：使用确定性整数评分，不分配正则表达式，不依赖区域性大小写比较。
- [ ] **Step 4: 完成构建脚本**：分别编译 `build\FocusSearch.exe` 与 `build\FocusSearch.Tests.exe`，引用 `System.Windows.Forms.dll`、`System.Drawing.dll`、`System.Data.dll`、`System.Xml.dll`。
- [ ] **Step 5: 运行测试**：预期全部通过，且两次构建输出一致的文件集合。
- [ ] **Step 6: 提交**：`git commit -m "build: add offline C# foundation"`。

### Task 2：配置与历史持久化

**Files:**
- Create: `src/SettingsStore.cs`
- Create: `tests/SettingsStoreTests.cs`
- Modify: `src/Models.cs`
- Modify: `build.ps1`

**Interfaces:**
- Consumes: `AppSettings`、`UsageRecord`、`HotkeySpec`。
- Produces: `SettingsStore(string baseDirectory)`；`LoadSettings() -> AppSettings`；`SaveSettings(AppSettings)`；`LoadHistory() -> IDictionary<string, UsageRecord>`；`RecordLaunch(string path)`。

- [ ] **Step 1: 写失败测试**：临时目录中验证默认组合模式、`Alt + Space`、`Alt + A`、保存往返、截断 XML 回退和启动次数累加。
- [ ] **Step 2: 运行测试确认失败**。
- [ ] **Step 3: 实现原子保存**：先写同目录临时文件再替换；读取失败记录日志并返回默认值，路径键使用不区分大小写字典。
- [ ] **Step 4: 运行测试确认通过**。
- [ ] **Step 5: 提交**：`git commit -m "feat: persist settings and usage history"`。

### Task 3：应用程序索引

**Files:**
- Create: `src/AppIndex.cs`
- Create: `tests/AppIndexTests.cs`
- Modify: `build.ps1`

**Interfaces:**
- Consumes: `SearchItem`。
- Produces: `IAppIndex.Items -> IList<SearchItem>`；`AppIndex(IEnumerable<string> roots)`；`Refresh()`；`Search(string query, int limit) -> IList<SearchItem>`；`Changed` 事件。

- [ ] **Step 1: 写失败测试**：临时目录中放置 `.lnk`、`.url`、`.appref-ms`、`.exe`、无关文件、重复目标和不可访问子目录，断言过滤、去重、稳定排序和单项错误隔离。
- [ ] **Step 2: 运行测试确认失败**。
- [ ] **Step 3: 实现索引**：默认根目录包含用户/公共开始菜单与桌面；递归枚举时逐目录捕获权限和 IO 异常；通过 `FileSystemWatcher` 防抖刷新。
- [ ] **Step 4: 运行测试确认通过**。
- [ ] **Step 5: 提交**：`git commit -m "feat: index installed applications"`。

### Task 4：Windows Search 文件查询适配器

**Files:**
- Create: `src/WindowsSearch.cs`
- Create: `tests/WindowsSearchTests.cs`
- Modify: `build.ps1`

**Interfaces:**
- Produces: `IFileSearchProvider.Search(string query, int limit) -> IList<SearchItem>`；`WindowsSearchProvider`；内部 `WindowsSearchProvider.EscapeContainsValue(string) -> string`。

- [ ] **Step 1: 写失败测试**：覆盖空查询、单引号、百分号、下划线、中文和服务不可用异常；断言空查询不访问数据库、特殊字符被安全转义、故障返回空集合与状态信息。
- [ ] **Step 2: 运行测试确认失败**。
- [ ] **Step 3: 实现查询**：通过 `Search.CollatorDSO.1` 查询 `SYSTEMINDEX`，仅选择名称、路径、扩展名和目录标识，限制数量并使用参数化/严格转义条件。
- [ ] **Step 4: 在启用 Windows Search 的当前机器运行真实查询冒烟测试，并运行完整测试集**。
- [ ] **Step 5: 提交**：`git commit -m "feat: query files through Windows Search"`。

### Task 5：搜索合并、排序与并发取消

**Files:**
- Create: `src/SearchEngine.cs`
- Create: `tests/SearchEngineTests.cs`
- Modify: `build.ps1`

**Interfaces:**
- Consumes: `IAppIndex`、`IFileSearchProvider`、历史字典。
- Produces: `SearchEngine.SearchAsync(string query, int limit, CancellationToken token) -> Task<IList<SearchItem>>`；`Recent(int limit) -> IList<SearchItem>`。

- [ ] **Step 1: 写失败测试**：覆盖应用优先级、历史加权、路径去重、8 条上限、文件服务故障和取消；添加快速连续查询测试，断言旧任务不能产生可应用结果。
- [ ] **Step 2: 运行测试确认失败**。
- [ ] **Step 3: 实现搜索引擎**：应用结果同步产生，长度至少 2 时后台查询文件，合并后按分数、历史和名称排序；每次查询接受调用方取消令牌。
- [ ] **Step 4: 运行测试确认通过**。
- [ ] **Step 5: 提交**：`git commit -m "feat: merge and rank search results"`。

### Task 6：原生窗口能力与玻璃效果

**Files:**
- Create: `src/NativeMethods.cs`
- Create: `src/GlassEffects.cs`
- Create: `tests/GlassEffectsTests.cs`
- Modify: `build.ps1`

**Interfaces:**
- Produces: `NativeMethods.TryRegisterHotKey(IntPtr handle, int id, HotkeySpec hotkey) -> bool`；`UnregisterHotKey`；`GetCursorScreen()`；`GlassEffects.Apply(Form form, bool fullScreen) -> GlassMode`。

- [ ] **Step 1: 写失败测试**：将 Windows 构建号与 DWM 调用抽象为可注入探针，验证 Windows 11 选择系统背板、旧系统/调用失败选择透明纯色回退。
- [ ] **Step 2: 运行测试确认失败**。
- [ ] **Step 3: 实现 Win32/DWM 封装**：热键注册、前台激活、圆角、阴影、沉浸式深色和系统背板均返回明确成功状态，不让入口点缺失异常逃逸。
- [ ] **Step 4: 运行测试，并用最小测试窗体验证玻璃回退不崩溃**。
- [ ] **Step 5: 提交**：`git commit -m "feat: add native hotkeys and glass effects"`。

### Task 7：Spotlight 搜索窗口与应用网格

**Files:**
- Create: `src/SearchWindow.cs`
- Create: `src/AppGridWindow.cs`
- Create: `tests/AppGridLayoutTests.cs`
- Modify: `build.ps1`

**Interfaces:**
- Consumes: `SearchEngine`、`IAppIndex`、`AppSettings`、`GlassEffects`。
- Produces: `SearchWindow.ShowLauncher(bool includeGrid)`；`HideLauncher()`；`LaunchRequested(SearchItem)` 事件；`AppGridWindow.ShowGrid(Screen target)`；`HideGrid()`。

- [ ] **Step 1: 写失败测试**：将网格分页提取为纯函数，验证 0/1/整页/跨页项目、窗口缩放和方向键边界；验证空查询显示网格、非空查询显示结果的状态转换。
- [ ] **Step 2: 运行测试确认失败**。
- [ ] **Step 3: 实现自绘控件与窗口**：原生图标按需加载并设有固定缓存上限；组合模式使用紧凑玻璃面板，分离模式使用独立全屏玻璃网格。
- [ ] **Step 4: 实现输入防抖和取消**：每次文本变化取消旧令牌；只有当前查询版本可更新 UI。
- [ ] **Step 5: 运行测试并手动验证鼠标、上下左右、Enter、Esc、滚轮和分页**。
- [ ] **Step 6: 提交**：`git commit -m "feat: build spotlight and app grid interfaces"`。

### Task 8：设置、托盘、单实例与启动编排

**Files:**
- Create: `src/SettingsWindow.cs`
- Create: `src/TrayApplicationContext.cs`
- Create: `src/Program.cs`
- Create: `tests/HotkeyCoordinatorTests.cs`
- Modify: `build.ps1`

**Interfaces:**
- Consumes: 前述全部组件。
- Produces: `HotkeyCoordinator.Apply(AppSettings next) -> HotkeyApplyResult`；可执行的 `FocusSearch.exe`。

- [ ] **Step 1: 写失败测试**：注入热键注册器，验证冲突时旧快捷键保持有效、组合/分离切换即时生效、退出时全部释放；验证单实例通知路径。
- [ ] **Step 2: 运行测试确认失败**。
- [ ] **Step 3: 实现设置窗与热键事务**：先试注册新组合，成功后释放旧组合并保存；失败则清理新注册并保留旧值。
- [ ] **Step 4: 实现托盘与生命周期**：显示、设置、开机启动、关于、退出；命名互斥体保证单实例，第二实例通知第一实例显示。
- [ ] **Step 5: 实现启动目标与异常日志**：使用系统默认关联打开，成功后记录历史；失败显示非阻塞提示；顶层异常写入 `%LOCALAPPDATA%\FocusSearch\logs`。
- [ ] **Step 6: 运行完整测试并启动两次验证单实例**。
- [ ] **Step 7: 提交**：`git commit -m "feat: wire tray settings and application lifecycle"`。

### Task 9：交付验证与文档

**Files:**
- Create: `run.ps1`
- Create: `README.md`
- Modify: `build.ps1`

**Interfaces:**
- Consumes: `FocusSearch.exe` 与全部测试。
- Produces: 可复现的构建、运行和故障排查说明。

- [ ] **Step 1: 完善构建参数**：支持 `-Clean`、`-Test` 和 Release 默认构建，任何编译或测试失败均返回非零退出码。
- [ ] **Step 2: 编写 README**：记录 `Alt + Space`、`Alt + A`、两种显示模式、Windows Search 依赖、开机启动、构建命令和卸载方式。
- [ ] **Step 3: 运行 `powershell -ExecutionPolicy Bypass -File .\build.ps1 -Clean -Test`**：预期所有测试通过并生成单个主程序。
- [ ] **Step 4: 实机验收**：在其他前台应用中唤起；搜索并启动应用/文件；切换两种模式；测试快捷键冲突；验证退出后快捷键释放。
- [ ] **Step 5: 观察空闲进程一分钟**：确认无持续 CPU 活动、内存不持续增长，并记录基线到 README。
- [ ] **Step 6: 运行 `git diff --check` 和 `git status --short`，审查无临时文件或日志进入版本库**。
- [ ] **Step 7: 提交**：`git commit -m "docs: complete FocusSearch delivery"`。
