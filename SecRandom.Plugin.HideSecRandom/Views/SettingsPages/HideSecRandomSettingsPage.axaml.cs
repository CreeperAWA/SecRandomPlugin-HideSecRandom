using Avalonia.Controls;
using Avalonia.Interactivity;
using SecRandom.Core.Abstraction;
using SecRandom.Core.Attributes;
using SecRandom.Core.Icons;
using SecRandom.Plugin.HideSecRandom.Configuration;

namespace SecRandom.Plugin.HideSecRandom.Views.SettingsPages;

[PageInfo("plugin.secrandom.hidesecrandom.settings", FluentIcons.EyeOffFilled)]
public partial class HideSecRandomSettingsPage : UserControl
{
    private readonly HideSecRandomConfigHandler _configHandler;

    public HideSecRandomSettingsPage()
    {
        // 页面由宿主在运行时创建，此时 Host 已构建，按宿主视图约定用 IAppHost 解析服务。
        _configHandler = IAppHost.GetService<HideSecRandomConfigHandler>();
        InitializeComponent();
        GreetingSentencesBox.Text = string.Join("\n", _configHandler.Data.GreetingSentences);
        DataContext = this;
    }

    /// <summary>插件配置对象；属性变更经 ConfigHandlerBase 自动保存。</summary>
    public HideSecRandomConfig Settings => _configHandler.Data;

    public string ConfigFilePath => _configHandler.Data.ConfigFilePath;

    /// <summary>
    ///     保存自定义句子（一行一句）：失焦时才写回，避免每次按键都写盘；
    ///     整体替换列表以触发配置自动保存，并让引擎在下一轮刷新已打开窗口的标题。
    /// </summary>
    private void GreetingSentencesBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        var sentences = GreetingSentencesBox.Text?
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList() ?? [];

        if (!sentences.SequenceEqual(Settings.GreetingSentences, StringComparer.Ordinal))
            Settings.GreetingSentences = sentences;
    }
}
