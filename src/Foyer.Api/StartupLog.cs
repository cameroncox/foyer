namespace Foyer.Api;

internal static partial class StartupLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Foyer {Version} starting with {HostCount} Docker host(s)")]
    public static partial void Starting(ILogger logger, string version, int hostCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Profiles off: every request uses Default")]
    public static partial void ProfilesOff(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Profiles on: users from {Header}, trusted from {Proxies}; Default editors: {Editors}")]
    public static partial void ProfilesOn(ILogger logger, string header, string proxies, string editors);
}
