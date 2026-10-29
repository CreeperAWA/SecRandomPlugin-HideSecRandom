using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using SecRandom.Shared;
using SecRandom.Shared.Abstraction;

namespace SecRandom.Plugin.HideSecRandom.Configuration;

/// <summary>
///     HideSecRandom 插件配置。持久化到 SecRandom 主配置文件（settings.json）同目录下的
///     <c>hide-secrandom.json</c>，JSON 命名/缩进风格与主程序一致
///     （经宿主 <c>ConfigServiceBase.JsonOptions</c>：snake_case、缩进、宽松转义），
///     不写入主程序配置文件本身。
/// </summary>
public partial class HideSecRandomConfig : ConfigBase
{
    /// <summary>总开关：关闭后所有功能立即停用，并恢复已修改的窗口特征。</summary>
    [ObservableProperty] private bool _enabled = true;

    /// <summary>功能一：调用 SetWindowDisplayAffinity 将窗口标记为不可捕获（屏幕共享/录屏中不可见）。</summary>
    [ObservableProperty] private bool _excludeFromCapture = true;

    /// <summary>功能二：随机化窗口名称。无边框弹窗始终随机化（仅系统 API 可见）；开启 include_all_windows 后，带标题栏窗口的可见标题栏文字也会随机化。</summary>
    [ObservableProperty] private bool _randomWindowNames = true;

    /// <summary>功能三：随机增大窗口尺寸（不等长宽各 +1~4px；相等长宽同步 +1~4px）。</summary>
    [ObservableProperty] private bool _randomWindowSize = true;

    /// <summary>功能四：拦截外部发来的 WM_CLOSE，使弹窗拦截程序无法借此关闭窗口。</summary>
    [ObservableProperty] private bool _blockWmClose = true;

    /// <summary>
    ///     功能四（扩展）：拦截弹窗拦截程序的强制关窗动作链。凡带 HWND_BOTTOM 压底签名的
    ///     SetWindowPos 调用，一律拒绝其隐藏标志与压底降级，屏幕内的位置/尺寸改动钉回原值，
    ///     <strong>仅放行“移动到屏幕外”</strong>——放行移动可让拦截程序确认目标已移出屏幕，
    ///     避免触发其兜底的 TerminateProcess 结束进程。同时吞掉外部投递的
    ///     WM_SYSCOMMAND SC_CLOSE 与诱导自隐藏的 WM_SHOWWINDOW 通知。
    /// </summary>
    [ObservableProperty] private bool _blockForceClose = true;

    /// <summary>功能五：窗口保护。监听层级与状态变化：弹窗被覆盖时立即移回顶层；被外部隐藏、最小化、禁用或移出屏幕时立即恢复；Topmost 层带被剥夺（HWND_BOTTOM 降级）时立即恢复置顶。</summary>
    [ObservableProperty] private bool _keepOnTop = true;

    /// <summary>
    ///     弹窗伪装为 WS_EX_TOOLWINDOW 工具窗口：常见弹窗拦截程序对工具窗口不弹本地
    ///     “确认拦截”提示条，窗口同时从 Alt+Tab 隐藏。Avalonia 不会为 ShowInTaskbar=false
    ///     的窗口设置该样式位（它改用离屏属主方案），需要插件自行补上。
    /// </summary>
    [ObservableProperty] private bool _toolwindowDisguise = true;

    /// <summary>
    ///     作用范围扩展：<c>false</c> 时功能仅作用于无边框弹出窗口（悬浮窗、快速抽奖窗口等），
    ///     <c>true</c> 时扩展到本软件全部顶层窗口（主窗口、设置窗口等）。
    ///     开启后随机窗口名称也会作用于带标题栏窗口：其可见标题栏文字（含任务栏、Alt+Tab）
    ///     将变为随机字符串。
    /// </summary>
    [ObservableProperty] private bool _includeAllWindows;

    /// <summary>
    ///     功能六「窗口问候与嘲讽」总开关：开启后随机窗口标题的内容来源改为
    ///     <see cref="GreetingSentences"/> 中随机选取的一句，并按需叠加随机前缀、
    ///     随机后缀与随机插入字母；关闭或句子不可用时内容来源仍为随机字母数字字符串。
    ///     仅在 random_window_names 开启时才有随机标题可替换。
    /// </summary>
    [ObservableProperty] private bool _greetingEnabled;

    /// <summary>问候/嘲讽句子，一行一句对应一个元素；留空（或全部为空白）时回退为随机字符串。</summary>
    [ObservableProperty] private List<string> _greetingSentences = [];

    /// <summary>在标题开头随机添加 2-4 个字母或数字。</summary>
    [ObservableProperty] private bool _greetingRandomPrefix = true;

    /// <summary>在标题结尾随机添加 2-4 个字母或数字。</summary>
    [ObservableProperty] private bool _greetingRandomSuffix = true;

    /// <summary>在内容中随机位置插入 1-3 个字母，保持句子大致可读的同时避免被精确匹配。</summary>
    [ObservableProperty] private bool _greetingRandomInsertLetters = true;

    [JsonIgnore]
    public override string ConfigFilePath => Utils.GetFilePath("config", "hide-secrandom.json");
}
