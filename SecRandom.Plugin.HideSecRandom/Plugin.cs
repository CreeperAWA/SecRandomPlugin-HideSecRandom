using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SecRandom.Core.Abstraction;
using SecRandom.Core.Extensions.Registry;
using SecRandom.Plugin.HideSecRandom.Localization;
using SecRandom.Plugin.HideSecRandom.Services;
using SecRandom.Plugin.HideSecRandom.Views.SettingsPages;
using SecRandom.PluginSdk;

namespace SecRandom.Plugin.HideSecRandom;

/// <summary>
///     HideSecRandom 插件入口：注册配置处理器、窗口特性引擎与插件设置页。
///     窗口特性的运行时逻辑在 <see cref="PluginBase.OnAppStarted"/> 之后启动
///     （此时宿主 Host 与窗口均已就绪）。
/// </summary>
public sealed class Plugin : PluginBase
{
    private WindowFeatureService? _features;

    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        services.AddSingleton<HideSecRandomConfigHandler>();
        services.AddSingleton<WindowFeatureService>();
        services.AddSettingsPage<HideSecRandomSettingsPage>(Strings.PluginName);
    }

    public override void OnAppStarted()
    {
        if (!OperatingSystem.IsWindows())
            return; // 插件特性依赖 Win32 API，仅 Windows 端启用。

        _features = IAppHost.GetService<WindowFeatureService>();
        _features.Start();
    }

    public override void OnAppStopping()
    {
        // 退出前放行一切关闭请求并移除钩子。
        _features?.Stop();
    }

    public override ValueTask DisposeAsync()
    {
        _features?.Dispose();
        _features = null;
        return ValueTask.CompletedTask;
    }
}
