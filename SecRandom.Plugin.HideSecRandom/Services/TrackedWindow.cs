using Avalonia.Controls;

namespace SecRandom.Plugin.HideSecRandom.Services;

/// <summary>
///     单个被跟踪窗口的状态：原始窗口特征快照 + 各功能的应用标记。
///     全部字段只在 UI 线程访问（定时器、WinEvent 回调与窗口过程同属 UI 线程）。
/// </summary>
internal sealed class TrackedWindow
{
    public required Window Window { get; init; }

    /// <summary>Win32 句柄；窗口尚未创建平台句柄时为 0，待下一轮重试。</summary>
    public nint Hwnd { get; set; }

    /// <summary>无边框弹出窗口（WindowDecorations == None）。</summary>
    public bool IsPopup { get; init; }

    /// <summary>是否在当前作用范围内（弹出窗口，或用户开启了“扩展到所有窗口”）。</summary>
    public bool InScope { get; set; }

    // 防捕获
    public bool CaptureExclusionApplied { get; set; }

    // 随机窗口名称
    public string? OriginalTitle { get; set; }
    public string? RandomTitle { get; set; }

    // 随机窗口大小（两套策略：显式宽高 / SizeToContent 下用最小尺寸钳制）
    public bool SizeApplied { get; set; }
    public double? OriginalWidth { get; set; }
    public double? OriginalHeight { get; set; }
    public double? OriginalMinWidth { get; set; }
    public double? OriginalMinHeight { get; set; }

    // WM_CLOSE 拦截（Win32Properties.AddWndProcHookCallback，官方钩子）
    public bool CloseHookInstalled { get; set; }

    /// <summary>用户通过标题栏关闭按钮 / Alt+F4 触发 SC_CLOSE 后的短暂放行截止时刻（TickCount64 毫秒）。</summary>
    public long AllowCloseUntilTicks { get; set; }

    /// <summary>Avalonia 触发 Closing（程序化或用户关闭路径）后的放行截止时刻；一次性使用。</summary>
    public long ProgrammaticCloseTicks { get; set; }

    /// <summary>
    ///     检测到外部隐藏后的恢复抑制截止时刻（TickCount64 毫秒）。
    ///     用于避开强制关窗动作链隐藏后的可见性判定窗口，防止升级为 TerminateProcess。
    /// </summary>
    public long SuppressRestoreUntilTicks { get; set; }

    /// <summary>
    ///     检测到窗口被移出屏幕后的位置恢复抑制截止时刻（TickCount64 毫秒）。
    ///     与隐藏恢复同理：先让强制关窗动作链的“已移出屏幕”判定成立，序列结束后再送回原位。
    /// </summary>
    public long SuppressPositionRestoreUntilTicks { get; set; }

    // 自愈基线：最近一次“正常”状态下的原生窗口矩形（物理像素）。
    public bool HasLastGoodRect { get; set; }
    public int LastGoodLeft { get; set; }
    public int LastGoodTop { get; set; }
    public int LastGoodRight { get; set; }
    public int LastGoodBottom { get; set; }

    /// <summary>基线时刻窗口是否处于 Topmost 层带（被 HWND_BOTTOM 降级后据此恢复置顶）。</summary>
    public bool LastGoodTopmost { get; set; }

    /// <summary>是否已应用 WS_EX_TOOLWINDOW 伪装（弹窗免弹窗拦截程序的本地确认条，同时从 Alt+Tab 隐藏）。</summary>
    public bool ToolwindowApplied { get; set; }
}
