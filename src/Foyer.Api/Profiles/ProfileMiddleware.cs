using Foyer.Core.Exceptions;
using Foyer.Core.Profiles;

namespace Foyer.Api.Profiles;

/// <summary>
/// Resolves the caller and the profile once per /api request into <see cref="ProfileContext"/>,
/// or ends the request: 403 for a bad or untrusted user header, 404 for a profile the caller
/// can't see (whether or not it exists). A user seen for the first time gets a personal profile.
/// </summary>
internal sealed partial class ProfileMiddleware(RequestDelegate next, ProfileOptions options, ILogger<ProfileMiddleware> logger)
{
    /// <summary>The profile to act on, sent by the page from its URL.</summary>
    public const string ProfileHeader = "X-Foyer-Profile";

    /// <summary>The same for the event stream, since EventSource can't send headers.</summary>
    public const string ProfileQuery = "profile";

    private int _warnedNoDefaultEditors;

    public async Task InvokeAsync(HttpContext http, ProfileContext context, ProfileService profiles)
    {
        // Off, every request is Default and may edit it, which is the context's unset state.
        if (!options.Enabled)
        {
            await next(http);
            return;
        }

        var identification = ProfileResolver.Identify(options, new RequestFacts(
            http.Connection.RemoteIpAddress,
            Header(http, options.UserHeader),
            Header(http, options.GroupsHeader)));
        if (identification.Caller is not { } caller)
        {
            if (identification.FromUntrustedAddress)
            {
                UntrustedHeader(logger, options.UserHeader, http.Connection.RemoteIpAddress?.ToString() ?? "unknown");
            }

            throw new ForbiddenException(identification.Refusal!);
        }

        if (caller.User is not null)
        {
            WarnOnceIfNobodyCanEditDefault();
            await profiles.EnsurePersonalAsync(caller.User, http.RequestAborted);
        }

        var slug = Header(http, ProfileHeader) ?? http.Request.Query[ProfileQuery].FirstOrDefault();
        var profile = ProfileResolver.Pick(caller, slug, await profiles.VisibleAsync(caller, http.RequestAborted))
            ?? throw new NotFoundException($"There's no profile '{slug}'.");

        context.Use(profile, caller, ProfileResolver.CanEdit(options, caller, profile));
        await next(http);
    }

    private static string? Header(HttpContext http, string name) =>
        http.Request.Headers.TryGetValue(name, out var value) ? value.ToString() : null;

    private void WarnOnceIfNobodyCanEditDefault()
    {
        if (!options.HasDefaultEditors && Interlocked.Exchange(ref _warnedNoDefaultEditors, 1) == 0)
        {
            NobodyCanEditDefault(logger, options.UserHeader, ProfileOptions.DefaultEditorUsersKey, ProfileOptions.DefaultEditorGroupsKey);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refused a {Header} header from {Address}, which isn't in FOYER_TRUSTED_PROXIES")]
    private static partial void UntrustedHeader(ILogger logger, string header, string address);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Requests are arriving with {Header}, but no Default editors are listed, so nobody behind the proxy can edit Default. Set {UsersKey} or {GroupsKey}")]
    private static partial void NobodyCanEditDefault(ILogger logger, string header, string usersKey, string groupsKey);
}
