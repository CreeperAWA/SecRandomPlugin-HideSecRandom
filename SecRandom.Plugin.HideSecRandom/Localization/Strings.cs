using System.Globalization;

namespace SecRandom.Plugin.HideSecRandom.Localization;

/// <summary>
///     插件自带的多语言字符串（zh-CN / en-US / ja-JP）。
///     SRPX 打包会剔除附属资源程序集，因此不使用 resx 卫星程序集，
///     改为把三种语言内嵌在本程序集中，按启动时的 CurrentUICulture 选择。
///     宿主切换语言会请求重启，重启后按新语言重新选择。
/// </summary>
public static class Strings
{
    private static readonly IReadOnlyDictionary<string, string> Chinese = new Dictionary<string, string>
    {
        ["PluginName"] = "HideSecRandom 窗口保护",
        ["S_Intro"] = "为 SecRandom 的弹出窗口提供防截屏捕获、随机窗口特征与防关闭保护，用于对抗弹窗拦截程序。默认只作用于无边框弹出窗口（悬浮窗、快速抽奖窗口等）；所有改动在关闭对应开关或插件后自动还原。",
        ["S_Master"] = "启用 HideSecRandom",
        ["S_Master_D"] = "总开关：关闭后所有功能立即停用，并恢复已修改的窗口特征。",
        ["S_Capture"] = "防止窗口被捕获",
        ["S_Capture_D"] = "调用 SetWindowDisplayAffinity 将弹出窗口标记为不可捕获：屏幕共享、录屏与常规截图中不可见（需要 Windows 10 2004 或更高版本）。",
        ["S_RandomName"] = "随机窗口名称",
        ["S_RandomName_D"] = "为窗口设置随机名称：无边框弹出窗口始终随机化（仅系统 API 可见）；开启「扩展到所有窗口」后，主窗口、设置窗口的标题栏文字也会随机化为随机字符串。标题内容来源可在「窗口问候与嘲讽」中自定义。",
        ["S_RandomSize"] = "随机窗口大小",
        ["S_RandomSize_D"] = "每个窗口打开时随机增大尺寸：原本长宽不等的窗口长、宽各随机增大 1-4px；原本长宽相等的窗口长、宽一起随机增大相同的 1-4px。",
        ["S_BlockClose"] = "拦截 WM_CLOSE 关闭请求",
        ["S_BlockClose_D"] = "吞掉外部发送的 WM_CLOSE 消息，弹窗拦截程序无法借此关闭窗口；通过界面按钮或 Alt+F4 的正常关闭不受影响。",
        ["S_KeepTop"] = "层级与显示保护",
        ["S_KeepTop_D"] = "监听窗口层级与状态变化：弹窗被其他窗口（含本软件其他窗口）覆盖时立即移回顶层；Topmost 置顶层带被剥夺（如被压到 Z 序底部）时立即恢复；窗口被外部隐藏、最小化、禁用或移出屏幕时延迟恢复（以软件自身的显示意图为准，不影响正常的最小化与后台驻留）。",
        ["S_Toolwindow"] = "弹窗工具窗口伪装",
        ["S_Toolwindow_D"] = "把无边框弹窗标记为 WS_EX_TOOLWINDOW 工具窗口：弹窗拦截软件对工具窗口不弹“确认拦截”提示条，弹窗同时从 Alt+Tab 隐藏。",
        ["S_Scope"] = "扩展到所有窗口",
        ["S_Scope_D"] = "开启后上述功能同时作用于主窗口、设置窗口等带标题栏的窗口，随机窗口名称也会包含它们（标题栏可见文字将变为随机字符串）。注意：主窗口尺寸的随机增量可能被“自动保存窗口大小”记忆。",
        ["S_ConfigPath"] = "配置文件位置",
        ["S_ConfigPath_D"] = "本插件设置保存在 SecRandom 配置文件同目录下的 hide-secrandom.json，风格与主程序配置一致。",
        ["S_Greeting"] = "窗口问候与嘲讽",
        ["S_Greeting_D"] = "把随机窗口标题的内容来源换成自定义句子，向弹窗拦截程序问好或嘲讽它，并可叠加随机前缀、随机后缀与随机插入字母，避免句子被原样匹配。",
        ["S_Greeting_Enabled"] = "启用窗口问候与嘲讽",
        ["S_Greeting_Enabled_D"] = "总开关：关闭后标题内容回到随机字母数字字符串，所有装饰立即停用。",
        ["S_Greeting_Sentences"] = "自定义句子",
        ["S_Greeting_Sentences_D"] = "随机窗口标题的内容来源，一行一句；留空或没有可用句子时回退为随机字符串，修改后已打开的窗口也会刷新标题。",
        ["S_Greeting_Sentences_Watermark"] = "一行一句，例如：你抓不到我~",
        ["S_Greeting_Prefix"] = "随机前缀",
        ["S_Greeting_Prefix_D"] = "在标题开头随机添加 2-4 个字母或数字。",
        ["S_Greeting_Suffix"] = "随机后缀",
        ["S_Greeting_Suffix_D"] = "在标题结尾随机添加 2-4 个字母或数字。",
        ["S_Greeting_Insert"] = "随机插入字母",
        ["S_Greeting_Insert_D"] = "在内容中随机位置插入 1-3 个字母，句子仍大致可读，但不会被精确匹配。",
        ["C_Greeting_Scope"] = "需要先开启「随机窗口名称」才会生效；无边框弹窗的随机标题仅系统 API 可见，开启「扩展到所有窗口」后标题栏也会显示。",
    };

    private static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>
    {
        ["PluginName"] = "HideSecRandom Window Guard",
        ["S_Intro"] = "Protects SecRandom popup windows against screen capture, popup-blocker detection and forced closing. By default only borderless popup windows (floating window, quick-draw window, etc.) are affected; every change is reverted automatically when a switch or the plugin is turned off.",
        ["S_Master"] = "Enable HideSecRandom",
        ["S_Master_D"] = "Master switch: turning it off disables every feature immediately and reverts window traits that were changed.",
        ["S_Capture"] = "Prevent window capture",
        ["S_Capture_D"] = "Calls SetWindowDisplayAffinity to mark popup windows as non-capturable: invisible in screen sharing, recording and common capture APIs (requires Windows 10 2004 or later).",
        ["S_RandomName"] = "Random window names",
        ["S_RandomName_D"] = "Assigns random window names: borderless popup windows are always randomized (only system APIs see it); when “extend to all windows” is enabled, the visible title bars of the main and settings windows become random strings too. The title content source can be customized under “window greetings & taunts”.",
        ["S_RandomSize"] = "Random window size",
        ["S_RandomSize_D"] = "Each window grows randomly when opened: windows whose width and height differ grow 1-4px in each dimension; square windows grow by the same random 1-4px in both dimensions.",
        ["S_BlockClose"] = "Block WM_CLOSE requests",
        ["S_BlockClose_D"] = "Swallows WM_CLOSE messages sent from outside the app, so popup blockers cannot close the window; normal closing through in-app buttons or Alt+F4 keeps working.",
        ["S_KeepTop"] = "Z-order & visibility guard",
        ["S_KeepTop_D"] = "Watches window z-order and state: a popup covered by another window (including the app's own windows) is immediately moved back to the top; a stripped Topmost band (demoted to the z-order bottom) is re-asserted; windows hidden, minimized, disabled or moved off-screen from outside are restored with a short delay (anchored on the app's own display intent, so normal minimizing and background residency still work).",
        ["S_Toolwindow"] = "Popup tool-window disguise",
        ["S_Toolwindow_D"] = "Marks borderless popups as WS_EX_TOOLWINDOW: popup blockers do not show their \"confirm block\" bar for tool windows, and the popups are hidden from Alt+Tab as well.",
        ["S_Scope"] = "Extend to all windows",
        ["S_Scope_D"] = "When enabled, the features above also apply to windows with title bars such as the main and settings windows, and random names extend to them as well (visible title bars become random strings). Note: the random size delta of the main window may be remembered by “auto save window size”.",
        ["S_ConfigPath"] = "Configuration file",
        ["S_ConfigPath_D"] = "Settings are stored in hide-secrandom.json next to the SecRandom configuration file, in the same style as the main configuration.",
        ["S_Greeting"] = "Window greetings & taunts",
        ["S_Greeting_D"] = "Switches the random window title's content source to custom sentences that greet or taunt the popup blocker, with optional random prefix, random suffix and random inserted letters so a sentence is never matched verbatim.",
        ["S_Greeting_Enabled"] = "Enable window greetings & taunts",
        ["S_Greeting_Enabled_D"] = "Master switch: when off, title content falls back to the random alphanumeric string and every decoration is disabled.",
        ["S_Greeting_Sentences"] = "Custom sentences",
        ["S_Greeting_Sentences_D"] = "The content source for random window titles, one sentence per line; empty or unusable lists fall back to the random string, and already-open windows refresh their titles after a change.",
        ["S_Greeting_Sentences_Watermark"] = "One per line, e.g. You can't catch me~",
        ["S_Greeting_Prefix"] = "Random prefix",
        ["S_Greeting_Prefix_D"] = "Prepends 2-4 random letters or digits to the title.",
        ["S_Greeting_Suffix"] = "Random suffix",
        ["S_Greeting_Suffix_D"] = "Appends 2-4 random letters or digits to the title.",
        ["S_Greeting_Insert"] = "Random inserted letters",
        ["S_Greeting_Insert_D"] = "Inserts 1-3 random letters at random positions, keeping the sentence roughly readable while defeating exact matching.",
        ["C_Greeting_Scope"] = "Requires “random window names” to be enabled; the randomized popup title is only visible to system APIs, and with “extend to all windows” on it also shows in the title bar.",
    };

    private static readonly IReadOnlyDictionary<string, string> Japanese = new Dictionary<string, string>
    {
        ["PluginName"] = "HideSecRandom ウィンドウ保護",
        ["S_Intro"] = "SecRandom のポップアップウィンドウを、画面キャプチャやポップアップブロッカーの検出、強制終了から保護します。既定では枠なしポップアップウィンドウ（フローティングウィンドウ、クイック抽選ウィンドウなど）のみが対象で、スイッチやプラグインを無効にすると変更は自動的に元に戻ります。",
        ["S_Master"] = "HideSecRandom を有効にする",
        ["S_Master_D"] = "マスタースイッチ：オフにすると全機能が即時に無効になり、変更されたウィンドウ特性も元に戻ります。",
        ["S_Capture"] = "ウィンドウのキャプチャを防ぐ",
        ["S_Capture_D"] = "SetWindowDisplayAffinity を呼び出してポップアップをキャプチャ不可にします。画面共有・録画・一般的なキャプチャ API に表示されません（Windows 10 2004 以降が必要）。",
        ["S_RandomName"] = "ランダムなウィンドウ名",
        ["S_RandomName_D"] = "ウィンドウにランダムな名前を設定します：枠なしポップアップは常にランダム化され（システム API からのみ見えます）、「すべてのウィンドウに拡張」を有効にすると、メインウィンドウや設定ウィンドウの表示タイトルバーもランダムな文字列になります。タイトルの内容は「ウィンドウの挨拶と煽り」でカスタマイズできます。",
        ["S_RandomSize"] = "ランダムなウィンドウサイズ",
        ["S_RandomSize_D"] = "各ウィンドウは開くときにランダムに大きくなります：幅と高さが異なるウィンドウは各 1〜4px、正方形のウィンドウは同じランダム値 1〜4px ずつ大きくなります。",
        ["S_BlockClose"] = "WM_CLOSE を遮断",
        ["S_BlockClose_D"] = "外部から送られた WM_CLOSE メッセージを握りつぶし、ポップアップブロッカーによる強制終了を防ぎます。アプリ内ボタンや Alt+F4 による通常の終了は影響を受けません。",
        ["S_KeepTop"] = "Z 順・表示保護",
        ["S_KeepTop_D"] = "ウィンドウの Z 順と状態を監視します：他のウィンドウ（アプリ自身のウィンドウを含む）に覆われたポップアップは即時に最前面へ戻し、Topmost 層を剥奪されて Z 順の最底へ移動された場合は即時に再適用します。外部から隠された・最小化された・無効化された・画面外へ移動されたウィンドウは短い遅延を置いて復元します（アプリ自身の表示意図を基準にするため、通常の最小化やバックグラウンド常駐には影響しません）。",
        ["S_Toolwindow"] = "ポップアップのツールウィンドウ偽装",
        ["S_Toolwindow_D"] = "枠なしポップアップを WS_EX_TOOLWINDOW として扱います：ポップアップブロッカーはツールウィンドウに対して「確認してブロック」のバーを表示せず、Alt+Tab にも表示されなくなります。",
        ["S_Scope"] = "すべてのウィンドウに拡張",
        ["S_Scope_D"] = "有効にすると、上記の機能がメインウィンドウや設定ウィンドウなどタイトルバー付きのウィンドウにも適用され、ランダムなウィンドウ名も含まれます（表示タイトルバーはランダムな文字列になります）。メインウィンドウのサイズ増分は「ウィンドウサイズの自動保存」に記録される場合があります。",
        ["S_ConfigPath"] = "設定ファイルの場所",
        ["S_ConfigPath_D"] = "設定は SecRandom の設定ファイルと同じフォルダーにある hide-secrandom.json に、本体と同じスタイルで保存されます。",
        ["S_Greeting"] = "ウィンドウの挨拶と煽り",
        ["S_Greeting_D"] = "ランダムなウィンドウタイトルの内容をカスタム文に置き換え、ポップアップブロッカーに挨拶や煽りを返します。ランダムな接頭辞・接尾辞・文字挿入を組み合わせ、文がそのまま一致されるのを防ぎます。",
        ["S_Greeting_Enabled"] = "ウィンドウの挨拶と煽りを有効にする",
        ["S_Greeting_Enabled_D"] = "マスタースイッチ：オフにするとタイトルの内容はランダムな英数字に戻り、すべての装飾も即時に無効になります。",
        ["S_Greeting_Sentences"] = "カスタム文",
        ["S_Greeting_Sentences_D"] = "ランダムなウィンドウタイトルの内容源です。1 行に 1 文。空または使用できる文がない場合はランダム文字列にフォールバックし、変更後は開いているウィンドウもタイトルを更新します。",
        ["S_Greeting_Sentences_Watermark"] = "1 行に 1 文、例：捕まえられないよ〜",
        ["S_Greeting_Prefix"] = "ランダム接頭辞",
        ["S_Greeting_Prefix_D"] = "タイトルの先頭にランダムな英数字 2〜4 文字を追加します。",
        ["S_Greeting_Suffix"] = "ランダム接尾辞",
        ["S_Greeting_Suffix_D"] = "タイトルの末尾にランダムな英数字 2〜4 文字を追加します。",
        ["S_Greeting_Insert"] = "ランダム文字挿入",
        ["S_Greeting_Insert_D"] = "内容の中のランダムな位置に 1〜3 文字を挿入します。文はおおむね読めるまま、完全一致は防げます。",
        ["C_Greeting_Scope"] = "「ランダムなウィンドウ名」の有効化が必要です。枠なしポップアップのランダムタイトルはシステム API からのみ見え、「すべてのウィンドウに拡張」を有効にするとタイトルバーにも表示されます。",
    };

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Catalog =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["zh-CN"] = Chinese,
            ["en-US"] = English,
            ["ja-JP"] = Japanese,
        };

    private static readonly IReadOnlyDictionary<string, string> Active = ResolveActive();

    private static IReadOnlyDictionary<string, string> ResolveActive()
    {
        var culture = CultureInfo.CurrentUICulture;
        if (culture.Name.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
            return Japanese;
        if (culture.Name.StartsWith("en", StringComparison.OrdinalIgnoreCase))
            return English;
        return Chinese; // 缺失语言回退到简体中文（主程序默认语言）。
    }

    public static string Get(string key)
        => Active.TryGetValue(key, out var value) ? value : Catalog["zh-CN"][key];

    public static string PluginName => Get("PluginName");
    public static string S_Intro => Get("S_Intro");
    public static string S_Master => Get("S_Master");
    public static string S_Master_D => Get("S_Master_D");
    public static string S_Capture => Get("S_Capture");
    public static string S_Capture_D => Get("S_Capture_D");
    public static string S_RandomName => Get("S_RandomName");
    public static string S_RandomName_D => Get("S_RandomName_D");
    public static string S_RandomSize => Get("S_RandomSize");
    public static string S_RandomSize_D => Get("S_RandomSize_D");
    public static string S_BlockClose => Get("S_BlockClose");
    public static string S_BlockClose_D => Get("S_BlockClose_D");
    public static string S_KeepTop => Get("S_KeepTop");
    public static string S_KeepTop_D => Get("S_KeepTop_D");
    public static string S_Toolwindow => Get("S_Toolwindow");
    public static string S_Toolwindow_D => Get("S_Toolwindow_D");
    public static string S_Scope => Get("S_Scope");
    public static string S_Scope_D => Get("S_Scope_D");
    public static string S_ConfigPath => Get("S_ConfigPath");
    public static string S_ConfigPath_D => Get("S_ConfigPath_D");
    public static string S_Greeting => Get("S_Greeting");
    public static string S_Greeting_D => Get("S_Greeting_D");
    public static string S_Greeting_Enabled => Get("S_Greeting_Enabled");
    public static string S_Greeting_Enabled_D => Get("S_Greeting_Enabled_D");
    public static string S_Greeting_Sentences => Get("S_Greeting_Sentences");
    public static string S_Greeting_Sentences_D => Get("S_Greeting_Sentences_D");
    public static string S_Greeting_Sentences_Watermark => Get("S_Greeting_Sentences_Watermark");
    public static string S_Greeting_Prefix => Get("S_Greeting_Prefix");
    public static string S_Greeting_Prefix_D => Get("S_Greeting_Prefix_D");
    public static string S_Greeting_Suffix => Get("S_Greeting_Suffix");
    public static string S_Greeting_Suffix_D => Get("S_Greeting_Suffix_D");
    public static string S_Greeting_Insert => Get("S_Greeting_Insert");
    public static string S_Greeting_Insert_D => Get("S_Greeting_Insert_D");
    public static string C_Greeting_Scope => Get("C_Greeting_Scope");
}
