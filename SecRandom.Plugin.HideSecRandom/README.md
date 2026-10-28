# HideSecRandom — SecRandom 窗口隐藏与随机特征插件

适用于 **SecRandom Windows 端** 的进程内插件（SRPX），为 SecRandom 的弹出窗口提供对抗弹窗拦截程序的能力：

| 功能 | 说明 | 默认 |
|------|------|------|
| 防止窗口被捕获 | 调用 Windows `SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE)`，把弹出窗口标记为不可捕获——屏幕共享、录屏与常规截图中不可见 | 开 |
| 随机窗口名称 | 为窗口设置随机名称：无边框弹出窗口始终随机化（仅系统 API 可见）；开启「扩展到所有窗口」后，主窗口、设置窗口的可见标题栏文字（含任务栏、Alt+Tab）也会随机化 | 开 |
| 随机窗口大小 | 每个窗口打开时随机增大尺寸：**原本长宽不等**的窗口，长、宽各随机增大 1–4px；**原本长宽相等**的窗口，长、宽一起随机增大相同的 1–4px | 开 |
| 拦截 WM_CLOSE | 通过窗口过程钩子吞掉外部发送的 `WM_CLOSE`，弹窗拦截程序无法借此关闭窗口；界面按钮与 Alt+F4（先产生 `WM_SYSCOMMAND SC_CLOSE`）的正常关闭不受影响 | 开 |
| 层级变化立即置顶 | 监听 `EVENT_OBJECT_REORDER` / `EVENT_SYSTEM_FOREGROUND` / `EVENT_OBJECT_SHOW`，本软件窗口被其他程序的窗口覆盖时立即移回所属层带顶端（`SetWindowPos`），并辅以 500ms 轮询兜底 | 开 |

所有功能均可独立开关，另有一个总开关与“扩展到所有窗口”作用范围开关。

## 系统要求

- SecRandom 桌面版（宿主插件 API 3.x，插件清单声明 `apiVersion: 3.1.5`，主版本 3 兼容所有 3.x 宿主）。
- Windows 10 2004（build 19041）或更高版本才能获得“防捕获”能力；其余功能兼容更早的 Windows 10/11。
- 插件清单已声明 `supportedPlatforms: [windows]`；非 Windows 宿主上插件加载后不会启动任何窗口特性。

## 安装

- **正式安装**：把 `srpx/SecRandom.Plugin.HideSecRandom.srpx` 放入 SecRandom 的 `data/cache/plugin-packages/` 目录，重启 SecRandom 完成安装。
- **开发调试**：使用启动参数 `--epp <包含本插件目录的父目录>`（可重复），插件原位加载，修改后重启应用生效。例如：

  ```powershell
  SecRandom.Desktop.exe --epp D:\Projects\SecRandomPlugin-HideSecRandom
  ```

  （`--epp` 指向的目录下需要存在 `SecRandom.Plugin.HideSecRandom/` 子目录，即“包含插件目录的父目录”。）

安装后，在 **SecRandom 设置** 中会出现新的设置条目「HideSecRandom 窗口保护」，可在其中修改本插件的全部设置。

## 配置存储

配置文件位于 **SecRandom 配置文件（`settings.json`）同目录**下：

```
<data>/config/hide-secrandom.json
```

- 通过 SecRandom 共享的 `Utils.GetFilePath("config", "hide-secrandom.json")` 解析路径，因此与主程序一致地支持便携版包根目录与安装版回退目录。
- 序列化复用宿主 `ConfigServiceBase.JsonOptions`（snake_case 字段名、缩进、宽松转义），风格、格式与 SecRandom 主程序配置完全相同。
- **不写入主程序的 `settings.json`**。官方开发文档（`SecRandom-docs-main/docs/dev/plugins/basics.md`）中“插件私有配置”的默认建议是 `data/config/plugins/<id>`；本插件按需求显式选择与 `settings.json` 同目录，如需改回官方默认位置，仅需调整 `HideSecRandomConfig.ConfigFilePath`。

配置字段（均为布尔值）：

| JSON 字段 | 含义 |
|-----------|------|
| `enabled` | 总开关 |
| `exclude_from_capture` | 功能一：防止窗口被捕获 |
| `random_window_names` | 功能二：随机窗口名称 |
| `random_window_size` | 功能三：随机窗口大小 |
| `block_wm_close` | 功能四：拦截 WM_CLOSE |
| `keep_on_top` | 功能五：层级变化时立即恢复顶层 |
| `include_all_windows` | 作用范围扩展到所有窗口（见下） |

设置变更即时生效（≤500ms）：关闭某个开关后，插件会自动还原对应的窗口特征（`WDA_NONE`、恢复原始标题、恢复原始尺寸、放行 WM_CLOSE、停止置顶恢复）。

## 作用范围

- 默认仅作用于 **无边框弹出窗口**（`WindowDecorations == None`）：悬浮窗、快速抽奖窗口、独立通知窗口等——它们正是弹窗拦截程序的目标。
- 开启「扩展到所有窗口」后，功能同时作用于主窗口、设置窗口等带标题栏的窗口；随机窗口名称也会包含它们——这些窗口的**可见标题栏文字**将变为随机字符串（设置窗口与主窗口的标题栏、任务栏、Alt+Tab 都显示随机名）。

## 注意事项与边界

1. **防捕获**是 Windows 提供的标记，能对抗屏幕共享与常规捕获 API（BitBlt/PrintWindow/DWM 捕获），不能对抗物理拍摄屏幕。
2. **随机窗口大小**针对 Avalonia 逻辑像素（DIP）：100% 缩放下即物理像素；高 DPI 下增量按逻辑像素应用。
3. 主窗口/设置窗口若启用了「自动保存窗口大小」，且开启了「扩展到所有窗口」，随机增量可能被主程序记忆，多次启动后窗口会缓慢变大；默认作用范围（仅弹出窗口）不存在该问题，弹出窗口尺寸通常由内容决定且不会被持久化。
4. **拦截 WM_CLOSE** 基于 Avalonia 官方钩子 `Win32Properties.AddWndProcHookCallback`（在 Avalonia 窗口过程之前被调用、可置 `handled` 吞掉消息；不更换 GWLP_WNDPROC，无还原竞态，IME/键盘/触摸/UIA 消息全部原样放行）。拦截策略：凡未经 `WM_SYSCOMMAND SC_CLOSE`（标题栏 X、Alt+F4、系统菜单）或 Avalonia `Closing` 事件放行、也非应用退出阶段发来的 `WM_CLOSE` 一律吞掉——这正是外部弹窗拦截程序使用的通道；SecRandom 程序内的关闭走 Avalonia `Window.Close()`（已验证：直接 `DestroyWindow`，不经过 WM_CLOSE），完全不受影响。无边框弹出窗口没有 `WS_SYSMENU`，Alt+F4 通常也不产生 WM_CLOSE，因此拦截对弹窗几乎是纯外部防护。
5. **层级置顶恢复**只在存在与本软件窗口相交的外部窗口覆盖时动作，不会在窗口互不遮挡时抢焦点（`SWP_NOACTIVATE`，不抢输入焦点）。实现依据逆向确认：Avalonia 12.1.1 从不使用 `SetWindowDisplayAffinity`、从不读写 `GWLP_WNDPROC`、除 `Title` 属性变化外不会重设窗口标题，因此本插件的窗口操作与宿主行为互不干扰。
6. 插件在宿主进程内运行且所有窗口操作均有异常保护，不会向宿主 UI 线程抛出异常。

## 构建

```powershell
dotnet build -c Release SecRandom.Plugin.HideSecRandom/SecRandom.Plugin.HideSecRandom.csproj
```

- 通过 NuGet 引用 `SecRandom.PluginSdk 3.1.5`（`ExcludeAssets="runtime;native"`，运行时由宿主提供 `SecRandom.Core`/`SecRandom.Shared`/Avalonia 等程序集，插件包内同名 DLL 会被宿主忽略）。
- `<CreateSrpx>true</CreateSrpx>` 使每次构建自动产出 `srpx/SecRandom.Plugin.HideSecRandom.srpx`。
- 多语言（zh-CN / en-US / ja-JP）内嵌在主程序集中（SRPX 打包会剔除附属资源程序集，故不使用 resx 卫星程序集），按启动时的 `CurrentUICulture` 选择语言。

## 目录结构

```
SecRandom.Plugin.HideSecRandom/
├── SecRandom.Plugin.HideSecRandom.csproj
├── manifest.yml                     # 插件清单（id: secrandom.hidesecrandom）
├── Plugin.cs                        # 插件入口（PluginBase）
├── HideSecRandomConfigHandler.cs    # 配置处理器（ConfigHandlerBase，自动保存）
├── Configuration/
│   └── HideSecRandomConfig.cs       # 配置模型（hide-secrandom.json）
├── Localization/
│   └── Strings.cs                   # zh-CN / en-US / ja-JP 内置字符串
├── Native/
│   └── NativeMethods.cs             # user32/dwmapi P/Invoke
├── Services/
│   ├── WindowFeatureService.cs      # 五大特性引擎（定时器 + WinEvent + 窗口过程）
│   └── TrackedWindow.cs             # 单窗口状态快照
├── Views/SettingsPages/
│   └── HideSecRandomSettingsPage.axaml(.cs)   # 设置页（设置中的新条目）
├── icon.png
└── README.md
```

## 许可

遵循 SecRandom 的 GPL-3.0-only 许可证。
