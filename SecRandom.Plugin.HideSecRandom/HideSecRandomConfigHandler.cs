using Microsoft.Extensions.Logging;
using SecRandom.Core.Abstraction;
using SecRandom.Plugin.HideSecRandom.Configuration;

namespace SecRandom.Plugin.HideSecRandom;

/// <summary>
///     插件配置处理器。与主程序 <c>MainConfigHandler</c> 同构：
///     复用宿主的 <c>ConfigServiceBase</c>（原子写盘 + snake_case JSON），
///     配置对象属性变更时自动保存。
/// </summary>
public sealed class HideSecRandomConfigHandler(
    ILogger<HideSecRandomConfigHandler> logger,
    ConfigServiceBase configService)
    : ConfigHandlerBase<HideSecRandomConfig>(logger, configService, () => new HideSecRandomConfig());
