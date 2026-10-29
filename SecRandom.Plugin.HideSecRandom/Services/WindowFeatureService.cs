using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using SecRandom.Plugin.HideSecRandom.Configuration;
using SecRandom.Plugin.HideSecRandom.Native;
using static SecRandom.Plugin.HideSecRandom.Native.NativeMethods;

namespace SecRandom.Plugin.HideSecRandom.Services;

/// <summary>
///     HideSecRandom 窗口特性引擎。按配置对宿主顶层窗口应用六个防拦截特性：
///     防捕获（SetWindowDisplayAffinity）、随机窗口名称（内容来源可自定义：窗口问候与嘲讽）、
///     随机窗口大小、拦截 WM_CLOSE、层级置顶恢复。
///     所有窗口操作都在 UI 线程执行；窗口过程与 WinEvent 回调保持无异常、快速返回。
/// </summary>
public sealed partial class WindowFeatureService : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(200);
    private const long EventThrottleMilliseconds = 100;
    private const long ScCloseGraceMilliseconds = 600;
    private const long ProgrammaticCloseGraceMilliseconds = 5000;
    /// <summary>外部隐藏后的恢复延迟：必须大于强制关窗动作链从隐藏到结束进程判定的全部耗时（Sleep(200)+微秒级检查）。</summary>
    private const long HideRestoreDelayMilliseconds = 400;
    private static readonly object InstanceGate = new();
    private static WindowFeatureService? _instance;

    private readonly ILogger<WindowFeatureService> _logger;
    private readonly HideSecRandomConfigHandler _configHandler;
    private readonly ConcurrentDictionary<nint, TrackedWindow> _tracked = new();
    private readonly List<nint> _winEventHooks = [];

    private DispatcherTimer? _timer;
    private volatile bool _active;
    private volatile bool _blockWmCloseActive;
    private volatile bool _blockForceCloseActive;
    private volatile bool _shuttingDown;
    private long _lastEventFixTicks;
    private bool _started;

    public WindowFeatureService(ILogger<WindowFeatureService> logger, HideSecRandomConfigHandler configHandler)
    {
        _logger = logger;
        _configHandler = configHandler;
        lock (InstanceGate)
        {
            // 单例服务；窗口过程需要静态访问点。
            _instance = this;
        }
    }

    private static WindowFeatureService? Instance
    {
        get { lock (InstanceGate) return _instance; }
    }

    public void Start()
    {
        if (_started || !OperatingSystem.IsWindows())
            return;
        _started = true;

        InstallWinEventHooks();
        _configHandler.Data.PropertyChanged += ConfigData_OnPropertyChanged;
        _timer = new DispatcherTimer(PollInterval, DispatcherPriority.Background, (_, _) => Tick());
        _timer.Start();
        Tick();
        _logger.LogInformation("HideSecRandom 窗口特性引擎已启动。");
    }

    public void Stop()
    {
        if (!_started)
            return;
        _started = false;
        _shuttingDown = true;
        _configHandler.Data.PropertyChanged -= ConfigData_OnPropertyChanged;

        try
        {
            _timer?.Stop();
            _timer = null;
            foreach (var hook in _winEventHooks)
                UnhookWinEvent(hook);
            _winEventHooks.Clear();

            foreach (var tracked in _tracked.Values)
            {
                try
                {
                    UnhookClose(tracked);
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "移除窗口过程钩子失败。");
                }
            }
            _tracked.Clear();
            _logger.LogInformation("HideSecRandom 窗口特性引擎已停止。");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "停止窗口特性引擎时出现异常。");
        }
    }

    public void Dispose() => Stop();

    // ---------------------------------------------------------------- 周期处理

    private void Tick()
    {
        try
        {
            var config = _configHandler.Data;
            _active = config.Enabled;
            _blockWmCloseActive = config.Enabled && config.BlockWmClose;
            _blockForceCloseActive = config.Enabled && config.BlockForceClose;

            if (!_active)
            {
                RevertAll();
                return;
            }

            RefreshTracking();
            foreach (var tracked in _tracked.Values)
            {
                if (!tracked.InScope || tracked.Hwnd == 0 || !IsWindow(tracked.Hwnd))
                    continue;

                ApplyCaptureExclusion(tracked, config.ExcludeFromCapture);
                ApplyRandomTitle(tracked, config.RandomWindowNames && (tracked.IsPopup || config.IncludeAllWindows), config);
                ApplyRandomSize(tracked, config.RandomWindowSize);
                ApplyToolwindowDisguise(tracked, config.ToolwindowDisguise);
                EnsureCloseHook(tracked);
            }

            EnforceTopOrder();
        }
        catch (Exception exception)
        {
            // 插件代码不允许把异常抛进宿主 UI 线程（会被归因到插件并自动禁用）。
            _logger.LogError(exception, "HideSecRandom 周期处理失败。");
        }
    }

    /// <summary>对照 Avalonia 生命周期中的窗口集合，增删被跟踪窗口并刷新作用范围。</summary>
    private void RefreshTracking()
    {
        var windows = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Windows
                      ?? [];
        HashSet<nint> alive = [];

        foreach (var window in windows)
        {
            var hwnd = window.TryGetPlatformHandle()?.Handle ?? nint.Zero;
            if (hwnd == nint.Zero)
                continue;

            if (!_tracked.TryGetValue(hwnd, out var tracked))
            {
                tracked = new TrackedWindow
                {
                    Window = window,
                    Hwnd = hwnd,
                    // 无边框窗口即“弹出窗口”：悬浮窗、快速抽奖窗口等防拦截目标。
                    IsPopup = window.WindowDecorations == WindowDecorations.None,
                };
                // 纵深防御：已验证 Avalonia 的程序化 Close() 直接 DestroyWindow（不经 WM_CLOSE），
                // 但若任何关闭路径在 Closing 事件之后仍以 WM_CLOSE 送达（未来行为变化），
                // 这里预先放行，保证拦截只作用于外部发来的关闭请求。
                window.Closing += (_, _) =>
                    tracked.ProgrammaticCloseTicks =
                        Environment.TickCount64 + ProgrammaticCloseGraceMilliseconds;
                _tracked[hwnd] = tracked;
            }

            tracked.InScope = tracked.IsPopup || _configHandler.Data.IncludeAllWindows;
            alive.Add(hwnd);
        }

        foreach (var tracked in _tracked.Values)
        {
            if (alive.Contains(tracked.Hwnd))
                continue;
            if (_tracked.TryRemove(tracked.Hwnd, out _))
            {
                UnhookClose(tracked);
                _logger.LogDebug("窗口已关闭，停止跟踪（HWND {Hwnd}）。", tracked.Hwnd);
            }
        }
    }

    // ---------------------------------------------------------------- 功能一：防捕获

    private void ApplyCaptureExclusion(TrackedWindow tracked, bool enabled)
    {
        if (enabled && !tracked.CaptureExclusionApplied)
        {
            // WDA_EXCLUDEFROMCAPTURE（Win10 2004+）：屏幕共享与常规捕获 API 中完全不可见。
            if (SetWindowDisplayAffinity(tracked.Hwnd, WDA_EXCLUDEFROMCAPTURE))
            {
                tracked.CaptureExclusionApplied = true;
            }
            else
            {
                _logger.LogWarning("SetWindowDisplayAffinity 失败（错误代码 {Error}），窗口保持可捕获。",
                    Marshal.GetLastWin32Error());
            }
        }
        else if (!enabled && tracked.CaptureExclusionApplied)
        {
            SetWindowDisplayAffinity(tracked.Hwnd, WDA_NONE);
            tracked.CaptureExclusionApplied = false;
        }
    }

    // ---------------------------------------------------------------- 功能二：随机窗口名称

    private void ApplyRandomTitle(TrackedWindow tracked, bool enabled, HideSecRandomConfig config)
    {
        // 作用范围由 Tick 决定：无边框弹窗始终随机化；开启“扩展到所有窗口”后，
        // 主窗口、设置窗口等带标题栏窗口的可见标题栏文字也会随机化（用户显式选择）。
        if (enabled)
        {
            tracked.OriginalTitle ??= GetWindowText(tracked.Hwnd);
            tracked.RandomTitle ??= GenerateRandomTitle(config);

            // 宿主若重新设置了 Title，下一轮立即恢复随机名称。
            var current = GetWindowText(tracked.Hwnd);
            if (!string.Equals(current, tracked.RandomTitle, StringComparison.Ordinal))
                SetWindowTextW(tracked.Hwnd, tracked.RandomTitle);
        }
        else if (tracked.RandomTitle is not null)
        {
            SetWindowTextW(tracked.Hwnd, tracked.OriginalTitle ?? string.Empty);
            tracked.RandomTitle = null;
            tracked.OriginalTitle = null;
        }
    }

    private const string TitleAlphabet = "abcdefghjkmnpqrstuvwxyz23456789";

    /// <summary>生成随机窗口标题：内容来源加问候/嘲讽装饰（前缀、后缀、插入字母）。</summary>
    private static string GenerateRandomTitle(HideSecRandomConfig config)
        => ApplyGreetingDecorations(GenerateTitleContent(config), config);

    /// <summary>
    ///     标题内容来源：问候/嘲讽开启且有可用句子时随机取一句，否则回退为
    ///     10 位随机字母数字字符串（原默认行为）。
    /// </summary>
    private static string GenerateTitleContent(HideSecRandomConfig config)
    {
        if (config.GreetingEnabled)
        {
            var sentences = config.GreetingSentences;
            if (sentences is { Count: > 0 })
            {
                var sentence = sentences[Random.Shared.Next(sentences.Count)];
                if (!string.IsNullOrWhiteSpace(sentence))
                    return sentence.Trim();
            }
        }

        return RandomTitleChunk(10);
    }

    /// <summary>按配置在内容外叠加随机前缀/后缀，并向内容中插入随机字母。</summary>
    private static string ApplyGreetingDecorations(string content, HideSecRandomConfig config)
    {
        if (!config.GreetingEnabled)
            return content;

        if (config.GreetingRandomPrefix)
            content = RandomTitleChunk(Random.Shared.Next(2, 5)) + content;
        if (config.GreetingRandomSuffix)
            content += RandomTitleChunk(Random.Shared.Next(2, 5));
        if (config.GreetingRandomInsertLetters)
            content = InsertRandomLetters(content);
        return content;
    }

    private static string InsertRandomLetters(string content)
    {
        var builder = new StringBuilder(content);
        var count = Random.Shared.Next(1, 4);
        for (var index = 0; index < count; index++)
            builder.Insert(
                Random.Shared.Next(builder.Length + 1),
                TitleAlphabet[Random.Shared.Next(TitleAlphabet.Length)]);
        return builder.ToString();
    }

    private static string RandomTitleChunk(int length)
    {
        Span<char> chars = stackalloc char[length];
        for (var index = 0; index < chars.Length; index++)
            chars[index] = TitleAlphabet[Random.Shared.Next(TitleAlphabet.Length)];
        return new string(chars);
    }

    // ---------------------------------------------------------------- 功能三：随机窗口大小

    private void ApplyRandomSize(TrackedWindow tracked, bool enabled)
    {
        var window = tracked.Window;
        if (enabled && !tracked.SizeApplied)
        {
            if (window.WindowState != WindowState.Normal)
                return; // 最大化/全屏窗口不动，恢复 Normal 后下一轮再应用。
            if (!TryComputeSizeDelta(window, out var deltaX, out var deltaY))
                return; // 尺寸尚未就绪（未显示），下一轮重试。
            if (window.SizeToContent == SizeToContent.Manual
                && !double.IsNaN(window.Width) && !double.IsNaN(window.Height))
            {
                tracked.OriginalWidth = window.Width;
                tracked.OriginalHeight = window.Height;
                window.Width = tracked.OriginalWidth.Value + deltaX;
                window.Height = tracked.OriginalHeight.Value + deltaY;
            }
            else
            {
                // SizeToContent / 自动尺寸窗口：用最小尺寸钳制，随内容重测仍保持增量。
                tracked.OriginalMinWidth = window.MinWidth;
                tracked.OriginalMinHeight = window.MinHeight;
                window.MinWidth = window.ClientSize.Width + deltaX;
                window.MinHeight = window.ClientSize.Height + deltaY;
            }

            tracked.SizeApplied = true;
            _logger.LogDebug("已为窗口增加随机尺寸（+{Dx}, +{Dy}）。", deltaX, deltaY);
        }
        else if (!enabled && tracked.SizeApplied)
        {
            if (tracked.OriginalWidth is { } width && tracked.OriginalHeight is { } height)
            {
                window.Width = width;
                window.Height = height;
            }

            if (tracked.OriginalMinWidth is { } minWidth)
                window.MinWidth = minWidth;
            if (tracked.OriginalMinHeight is { } minHeight)
                window.MinHeight = minHeight;

            tracked.SizeApplied = false;
            tracked.OriginalWidth = null;
            tracked.OriginalHeight = null;
            tracked.OriginalMinWidth = null;
            tracked.OriginalMinHeight = null;
        }
    }

    /// <summary>
    ///     依据需求计算随机增量：原本长宽不等的窗口，长、宽各随机增大 1~4px；
    ///     原本长宽相等的窗口，长、宽一起随机增大相同的 1~4px。
    ///     需求中的像素指实际像素，Avalonia 的宽高为逻辑像素（DIP），按窗口缩放换算。
    /// </summary>
    private static bool TryComputeSizeDelta(Window window, out double deltaX, out double deltaY)
    {
        deltaX = deltaY = 0;
        var width = window.ClientSize.Width;
        var height = window.ClientSize.Height;
        if (width < 1 || height < 1)
            return false;

        var scaling = window.RenderScaling;
        if (scaling <= 0)
            scaling = 1;

        int physicalX, physicalY;
        if (Math.Abs(width - height) < 0.5)
        {
            physicalX = physicalY = Random.Shared.Next(1, 5);
        }
        else
        {
            physicalX = Random.Shared.Next(1, 5);
            physicalY = Random.Shared.Next(1, 5);
        }

        deltaX = physicalX / scaling;
        deltaY = physicalY / scaling;
        return true;
    }

    // ---------------------------------------------------------------- 功能四：拦截 WM_CLOSE

    /// <summary>
    ///     通过 Avalonia 官方钩子 <see cref="Win32Properties.AddWndProcHookCallback"/> 注册 WM_CLOSE
    ///     拦截（幂等）。回调在 Avalonia 窗口过程之前被调用，可置 handled 吞掉消息；
    ///     无需更换 GWLP_WNDPROC，也不存在还原窗口过程的竞态。
    /// </summary>
    private void EnsureCloseHook(TrackedWindow tracked)
    {
        if (tracked.CloseHookInstalled)
            return;

        Win32Properties.AddWndProcHookCallback(tracked.Window, WndProcHook);
        tracked.CloseHookInstalled = true;
    }

    private void UnhookClose(TrackedWindow tracked)
    {
        if (!tracked.CloseHookInstalled)
            return;
        tracked.CloseHookInstalled = false;
        try
        {
            Win32Properties.RemoveWndProcHookCallback(tracked.Window, WndProcHook);
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "移除 WM_CLOSE 钩子回调失败（窗口可能已销毁）。");
        }
    }

    private bool ShouldSwallowWmClose(TrackedWindow tracked)
    {
        if (!_blockWmCloseActive || _shuttingDown)
            return false;
        var now = Environment.TickCount64;
        // 标题栏关闭按钮 / Alt+F4 / 系统菜单会先发出 SC_CLOSE，短暂放行窗口内关闭路径。
        if (now < tracked.AllowCloseUntilTicks)
            return false;
        // Avalonia 程序化关闭（Closing 已触发）同样放行，且为一次性放行。
        if (now < tracked.ProgrammaticCloseTicks)
        {
            tracked.ProgrammaticCloseTicks = 0;
            return false;
        }
        return true;
    }

    // ---------------------------------------------------------------- 功能五：窗口保护（层级/显示/禁用/位置自愈）

    private void EnforceTopOrder()
    {
        if (!_configHandler.Data.Enabled || !_configHandler.Data.KeepOnTop)
            return;

        foreach (var tracked in _tracked.Values)
        {
            if (!tracked.InScope || tracked.Hwnd == 0 || !IsWindow(tracked.Hwnd))
                continue;

            ApplySelfHeal(tracked);

            // Z 序恢复只作用于无边框弹窗：带标题栏窗口被用户主动压后是正常使用，不去争抢。
            if (!tracked.IsPopup)
                continue;
            if (!IsWindowVisible(tracked.Hwnd) || IsIconic(tracked.Hwnd))
                continue;
            if (!IsWindowCovered(tracked.Hwnd))
                continue;
            if (BringWindowToTopOfOrder(tracked.Hwnd))
                _logger.LogDebug("检测到窗口被覆盖，已恢复到顶层（HWND {Hwnd}）。", tracked.Hwnd);
        }
    }

    /// <summary>
    ///     自愈：对抗外部程序对窗口的隐藏、最小化、禁用与移出屏幕。
    ///     以 Avalonia 的窗口状态为“宿主本意”基准——只有当宿主认为窗口应显示而原生窗口不可见、
    ///     认为未最小化而原生已最小化时才恢复，避免破坏宿主自身的 Hide()（后台驻留、课程联动隐藏）。
    ///
    ///     隐藏恢复刻意<strong>延迟</strong>：弹窗拦截程序的强制关窗动作链在每步隐藏后立即
    ///     检查 IsWindowVisible 决定是否升级 TerminateProcess；立即恢复会撞进其判定窗口。让隐藏
    ///     “成功”、延迟到其动作序列结束后再恢复，全程不会触发强杀分支。位置恢复同理延迟：
    ///     先让“已移出屏幕”判定成立，序列结束后再把窗口送回原位。
    /// </summary>
    private void ApplySelfHeal(TrackedWindow tracked)
    {
        var window = tracked.Window;
        var nativeVisible = IsWindowVisible(tracked.Hwnd);
        var now = Environment.TickCount64;

        if (!window.IsVisible)
        {
            // 宿主本意就是隐藏：清掉抑制状态，等待宿主重新显示。
            tracked.SuppressRestoreUntilTicks = 0;
            tracked.SuppressPositionRestoreUntilTicks = 0;
        }
        else if (!nativeVisible)
        {
            if (tracked.SuppressRestoreUntilTicks == 0)
                tracked.SuppressRestoreUntilTicks = now + HideRestoreDelayMilliseconds;
        }
        else
        {
            tracked.SuppressRestoreUntilTicks = 0;
        }

        // Topmost 层带恢复：强制关窗动作链的 SetWindowPos(HWND_BOTTOM) 会把 Topmost 窗口
        // 降级到普通带最底（“被盖住、回到桌面上方”的根源）。层带恢复不影响其位置/可见性判定，
        // 因此在隐藏抑制期内也可立即执行。
        if (tracked.HasLastGoodRect && tracked.LastGoodTopmost
            && (GetWindowLongPtr(tracked.Hwnd, GWL_EXSTYLE) & WS_EX_TOPMOST) == 0)
        {
            SetWindowPos(tracked.Hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            _logger.LogInformation("检测到窗口 Topmost 层带被剥夺，已恢复置顶（HWND {Hwnd}）。", tracked.Hwnd);
        }

        if (now < tracked.SuppressRestoreUntilTicks)
            return; // 隐藏对抗进行中：等对方动作序列结束，期间不做任何自愈动作。

        if (window.IsVisible && !nativeVisible)
        {
            ShowWindow(tracked.Hwnd, SW_SHOW);
            _logger.LogInformation("检测到窗口被外部隐藏，已恢复显示（HWND {Hwnd}）。", tracked.Hwnd);
        }

        // 宿主未请求最小化但原生已最小化：CanMinimize=false 的窗口（如悬浮窗）用户无法最小化，
        // 一旦 iconic 必为外部所为；可最小化窗口则尊重用户的主动最小化。
        if (IsIconic(tracked.Hwnd)
            && (window.WindowState != WindowState.Minimized || !window.CanMinimize))
        {
            ShowWindow(tracked.Hwnd, SW_RESTORE);
            _logger.LogInformation("检测到窗口被外部最小化，已恢复（HWND {Hwnd}）。", tracked.Hwnd);
        }

        if (!IsWindowEnabled(tracked.Hwnd))
        {
            EnableWindow(tracked.Hwnd, true);
            _logger.LogInformation("检测到窗口被外部禁用，已恢复（HWND {Hwnd}）。", tracked.Hwnd);
        }

        // 屏幕外位置恢复：与隐藏恢复同理刻意延迟——强制关窗动作链把窗口移出屏幕后仍会
        // 立即复查，“始终可见 + 未移出屏幕”是其升级结束进程的条件，立即拉回会撞进其判定
        // 窗口；先让“已移出屏幕”成立、动作序列结束后再送回原位，进程安全与可用性兼顾。
        if (window.WindowState == WindowState.Normal && nativeVisible && tracked.HasLastGoodRect
            && !IntersectsAnyMonitor(tracked.Hwnd))
        {
            if (tracked.SuppressPositionRestoreUntilTicks == 0)
                tracked.SuppressPositionRestoreUntilTicks = now + HideRestoreDelayMilliseconds;
        }
        else
        {
            tracked.SuppressPositionRestoreUntilTicks = 0;
        }

        if (now < tracked.SuppressPositionRestoreUntilTicks)
            return; // 位置对抗进行中：等对方动作序列结束，期间不做任何自愈动作。

        if (window.WindowState == WindowState.Normal && nativeVisible && tracked.HasLastGoodRect
            && !IntersectsAnyMonitor(tracked.Hwnd))
        {
            SetWindowPos(tracked.Hwnd, nint.Zero,
                tracked.LastGoodLeft, tracked.LastGoodTop,
                tracked.LastGoodRight - tracked.LastGoodLeft,
                tracked.LastGoodBottom - tracked.LastGoodTop,
                SWP_NOZORDER | SWP_NOACTIVATE);
            _logger.LogInformation("检测到窗口被移出屏幕，已恢复位置（HWND {Hwnd}）。", tracked.Hwnd);
        }

        UpdateLastGoodRect(tracked);
    }

    /// <summary>窗口处于可见、未最小化、未禁用且与虚拟屏幕相交的“正常”状态时记录矩形与层带基线。</summary>
    private void UpdateLastGoodRect(TrackedWindow tracked)
    {
        if (!IsWindowVisible(tracked.Hwnd) || IsIconic(tracked.Hwnd) || !IsWindowEnabled(tracked.Hwnd))
            return;
        if (!GetWindowRect(tracked.Hwnd, out var rect) || rect.IsEmpty)
            return;
        if (!IntersectsAnyMonitor(tracked.Hwnd))
            return;

        tracked.HasLastGoodRect = true;
        tracked.LastGoodLeft = rect.Left;
        tracked.LastGoodTop = rect.Top;
        tracked.LastGoodRight = rect.Right;
        tracked.LastGoodBottom = rect.Bottom;
        tracked.LastGoodTopmost = (GetWindowLongPtr(tracked.Hwnd, GWL_EXSTYLE) & WS_EX_TOPMOST) != 0;
    }

    private static bool IntersectsAnyMonitor(nint hwnd)
        => GetWindowRect(hwnd, out var rect) && !rect.IsEmpty && RectIntersectsVirtualScreen(in rect);

    /// <summary>矩形是否与整个虚拟屏幕（所有显示器的并集）相交。</summary>
    private static bool RectIntersectsVirtualScreen(in RECT rect)
    {
        var left = GetSystemMetrics(SM_XVIRTUALSCREEN);
        var top = GetSystemMetrics(SM_YVIRTUALSCREEN);
        var virtualScreen = new RECT
        {
            Left = left,
            Top = top,
            Right = left + GetSystemMetrics(SM_CXVIRTUALSCREEN),
            Bottom = top + GetSystemMetrics(SM_CYVIRTUALSCREEN),
        };
        return IntersectRect(out var intersection, in rect, in virtualScreen) && !intersection.IsEmpty;
    }

    /// <summary>
    ///     从 Z 顶向下扫描：是否存在位于本窗口之上、同层带、与本窗口相交的可见窗口。
    ///     弹窗拦截程序强制关窗时的 SetWindowPos(HWND_BOTTOM) 会把窗口压到普通带最底，本进程窗口
    ///     （如主界面盖住悬浮窗）同样算覆盖；但前台窗口与属主关联窗口压在上面视为正常
    ///     （如正在操作的快速抽奖窗口），不同层带（Topmost/普通）的窗口不参与比较。
    /// </summary>
    private static bool IsWindowCovered(nint hwnd)
    {
        if (!GetWindowRect(hwnd, out var myRect) || myRect.IsEmpty)
            return false;

        var myTopmost = (GetWindowLongPtr(hwnd, GWL_EXSTYLE) & WS_EX_TOPMOST) != 0;
        var myOwner = GetWindow(hwnd, GW_OWNER);
        var foreground = GetForegroundWindow();

        var current = GetTopWindow(nint.Zero);
        while (current != nint.Zero)
        {
            if (current == hwnd)
                return false;

            if (IsWindowVisible(current) && !IsIconic(current) && !IsWindowCloaked(current))
            {
                var otherTopmost = (GetWindowLongPtr(current, GWL_EXSTYLE) & WS_EX_TOPMOST) != 0;
                if (otherTopmost == myTopmost)
                {
                    _ = GetWindowThreadProcessId(current, out var processId);
                    var sameProcess = processId == (uint)Environment.ProcessId;
                    var related = GetWindow(current, GW_OWNER) == hwnd || current == myOwner;
                    if ((!sameProcess || (!related && current != foreground))
                        && GetWindowRect(current, out var otherRect)
                        && !otherRect.IsEmpty
                        && IntersectRect(out var intersection, in myRect, in otherRect)
                        && !intersection.IsEmpty)
                    {
                        return true;
                    }
                }
            }

            current = GetWindow(current, GW_HWNDNEXT);
        }

        return false;
    }

    /// <summary>把窗口移回所属层带的顶端；本身是 Topmost 的窗口恢复 Topmost 带。</summary>
    private static bool BringWindowToTopOfOrder(nint hwnd)
    {
        var exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        var insertAfter = (exStyle & WS_EX_TOPMOST) != 0 ? HWND_TOPMOST : HWND_TOP;
        return SetWindowPos(hwnd, insertAfter, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    /// <summary>
    ///     弹窗伪装为 WS_EX_TOOLWINDOW 工具窗口：弹窗拦截程序的窗口分类
    ///     对工具窗口不弹本地“确认拦截”提示条，窗口同时从 Alt+Tab 隐藏。
    ///     Avalonia 12 对 ShowInTaskbar=false 的窗口不设置该样式位（改用离屏属主方案），
    ///     需要插件自行补上。
    /// </summary>
    private void ApplyToolwindowDisguise(TrackedWindow tracked, bool enabled)
    {
        if (!tracked.IsPopup)
            return;

        var exStyle = GetWindowLongPtr(tracked.Hwnd, GWL_EXSTYLE);
        var hasBit = (exStyle & WS_EX_TOOLWINDOW) != 0;
        if (enabled && !hasBit)
        {
            SetWindowLongPtr(tracked.Hwnd, GWL_EXSTYLE, exStyle | (nint)WS_EX_TOOLWINDOW);
            tracked.ToolwindowApplied = true;
            _logger.LogDebug("已为弹窗应用 WS_EX_TOOLWINDOW 伪装（HWND {Hwnd}）。", tracked.Hwnd);
        }
        else if (!enabled && hasBit && tracked.ToolwindowApplied)
        {
            SetWindowLongPtr(tracked.Hwnd, GWL_EXSTYLE, exStyle & ~(nint)WS_EX_TOOLWINDOW);
            tracked.ToolwindowApplied = false;
        }
    }

    private void RevertAll()
    {
        var config = _configHandler.Data;
        foreach (var tracked in _tracked.Values)
        {
            if (!tracked.InScope || tracked.Hwnd == 0 || !IsWindow(tracked.Hwnd))
                continue;
            ApplyCaptureExclusion(tracked, enabled: false);
            ApplyRandomTitle(tracked, enabled: false, config);
            ApplyRandomSize(tracked, enabled: false);
            ApplyToolwindowDisguise(tracked, enabled: false);
        }
    }

    // ---------------------------------------------------------------- WinEvent 与 WM_CLOSE 钩子

    /// <summary>
    ///     问候/嘲讽相关配置变化后，把已打开窗口的随机标题置空：
    ///     下一轮 <see cref="ApplyRandomTitle"/> 会以新内容重新随机（原始标题基线保留）。
    ///     与 Tick 同在 UI 线程执行，无需额外同步。
    /// </summary>
    private void ConfigData_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(HideSecRandomConfig.GreetingEnabled)
            or nameof(HideSecRandomConfig.GreetingSentences)
            or nameof(HideSecRandomConfig.GreetingRandomPrefix)
            or nameof(HideSecRandomConfig.GreetingRandomSuffix)
            or nameof(HideSecRandomConfig.GreetingRandomInsertLetters)))
            return;

        foreach (var tracked in _tracked.Values)
            tracked.RandomTitle = null;
    }

    private static readonly WinEventDelegate WinEventProc = OnWinEvent;
    private static readonly Win32Properties.CustomWndProcHookCallback WndProcHook = OnWndProcHook;

    private void InstallWinEventHooks()
    {
        foreach (var eventType in (uint[])[EVENT_SYSTEM_FOREGROUND, EVENT_OBJECT_SHOW, EVENT_OBJECT_HIDE, EVENT_OBJECT_REORDER])
        {
            var hook = SetWinEventHook(
                eventType, eventType, nint.Zero, WinEventProc, 0, 0, WINEVENT_OUTOFCONTEXT);
            if (hook != nint.Zero)
                _winEventHooks.Add(hook);
        }
    }

    private static void OnWinEvent(
        nint hook, uint eventType, nint hwnd, int idObject, int idChild, uint thread, uint time)
    {
        var instance = Instance;
        if (instance is null || idObject != OBJID_WINDOW || idChild != 0)
            return;

        try
        {
            var now = Environment.TickCount64;
            var last = Interlocked.Read(ref instance._lastEventFixTicks);
            if (now - last < EventThrottleMilliseconds)
                return;
            Interlocked.Exchange(ref instance._lastEventFixTicks, now);

            // 前台切换 / 窗口显示 / Z 序变化都可能让本软件窗口被覆盖，立即检查并恢复。
            instance.EnforceTopOrder();
        }
        catch
        {
            // WinEvent 回调绝不允许把异常抛回消息循环。
        }
    }

    /// <summary>
    ///     官方窗口过程钩子回调：仅在需要拦截时置 handled 吞掉消息，其余一切消息
    ///     原样交回 Avalonia 的窗口过程（IME、键盘、触摸、UIA 等不受影响）。
    /// </summary>
    private static nint OnWndProcHook(nint hwnd, uint msg, nint wParam, nint lParam, ref bool handled)
    {
        var instance = Instance;
        if (instance is null || !instance._tracked.TryGetValue(hwnd, out var tracked))
            return nint.Zero;

        try
        {
            switch (msg)
            {
                case WM_SYSCOMMAND:
                    // 弹窗拦截程序以 PostMessageW(WM_SYSCOMMAND, SC_CLOSE, lParam=0) 强制关窗。
                    // 用户真实触发的 SC_CLOSE 携带光标坐标或 -1（lParam != 0），且只存在于带
                    // WS_SYSMENU 的窗口：
                    // - 无边框弹窗：不存在用户触发的 SC_CLOSE，一律吞掉；
                    // - 带标题栏窗口：仅吞 lParam == 0 的外部投递，真实的 X/Alt+F4 放行。
                    if (((ulong)(long)wParam & 0xFFF0UL) == SC_CLOSE)
                    {
                        var hasSysMenu = (GetWindowLongPtr(hwnd, GWL_STYLE) & WS_SYSMENU) != 0;
                        if (instance._blockForceCloseActive && (!hasSysMenu || lParam == nint.Zero))
                        {
                            handled = true;
                            return nint.Zero;
                        }
                        tracked.AllowCloseUntilTicks = Environment.TickCount64 + ScCloseGraceMilliseconds;
                    }
                    break;

                case WM_WINDOWPOSCHANGING:
                    // 强制关窗动作链的每次 SetWindowPos 都带 HWND_BOTTOM 压底签名（隐藏、
                    // 移出屏幕、压底打包在同一次调用中）；宿主与用户操作不会使用 HWND_BOTTOM，
                    // 以此区分内外来源，宿主自身的显示/隐藏/布局不受影响。
                    // 策略：仅放行“移动到屏幕外”一步（让拦截程序确认目标已移出屏幕，避免其
                    // 升级为结束进程）；隐藏标志、压底降级、屏幕内移动与捆绑的尺寸改动一律拒绝。
                    if (instance._blockForceCloseActive && lParam != nint.Zero
                        && GetWindowRect(hwnd, out var current))
                    {
                        var pos = Marshal.PtrToStructure<WINDOWPOS>(lParam);
                        var isForcedClose = pos.HwndInsertAfter == HWND_BOTTOM;
                        var modified = false;

                        if (isForcedClose)
                        {
                            pos.Flags &= ~SWP_HIDEWINDOW; // 拒绝隐藏
                            pos.Flags |= SWP_NOZORDER;    // 拒绝压底，保持当前层带
                            modified = true;
                        }

                        if ((pos.Flags & SWP_NOMOVE) == 0)
                        {
                            var hasSize = (pos.Flags & SWP_NOSIZE) == 0;
                            var width = hasSize ? pos.Cx : current.Width;
                            var height = hasSize ? pos.Cy : current.Height;
                            var target = new RECT
                            {
                                Left = pos.X,
                                Top = pos.Y,
                                Right = pos.X + width,
                                Bottom = pos.Y + height,
                            };

                            if (!RectIntersectsVirtualScreen(in target))
                            {
                                // 移动到屏幕外：放行，但拒绝捆绑的尺寸改动（如 0×0），保持窗口完整。
                                if (hasSize)
                                {
                                    pos.Flags |= SWP_NOSIZE;
                                    modified = true;
                                }
                            }
                            else if (isForcedClose)
                            {
                                // 强制关窗签名下的屏幕内移动：钉回原位，同一调用中的尺寸改动一并拒绝。
                                pos.X = current.Left;
                                pos.Y = current.Top;
                                pos.Flags |= SWP_NOSIZE;
                                modified = true;
                            }
                        }
                        else if (isForcedClose && (pos.Flags & SWP_NOSIZE) == 0)
                        {
                            pos.Flags |= SWP_NOSIZE; // 无移动的强制关窗调用：尺寸改动同样拒绝。
                            modified = true;
                        }

                        if (modified)
                            Marshal.StructureToPtr(pos, lParam, fDeleteOld: false);
                    }
                    break;

                case WM_SHOWWINDOW:
                    // 动作链里 ShowWindow(SW_HIDE) 失败时会补发 SendMessage(WM_SHOWWINDOW, 0, 0)
                    // 诱导应用“自行隐藏”。宿主 Avalonia 不处理本消息（反编译确认无引用），
                    // 吞掉不影响任何宿主行为。
                    if (instance._blockForceCloseActive && wParam == nint.Zero && lParam == nint.Zero)
                    {
                        handled = true;
                        return nint.Zero;
                    }
                    break;

                case WM_CLOSE:
                    if (instance.ShouldSwallowWmClose(tracked))
                    {
                        handled = true; // 吞掉外部 WM_CLOSE，Avalonia 不再处理，窗口保持打开。
                        return nint.Zero;
                    }
                    break;
            }
        }
        catch
        {
            // 窗口消息回调绝不允许把托管异常抛进消息循环。
        }

        return nint.Zero; // handled=false：交回 Avalonia 正常处理。
    }
}
