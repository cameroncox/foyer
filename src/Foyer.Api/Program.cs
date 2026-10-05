using System.Text.Json;
using System.Text.Json.Serialization;
using Foyer.Api;
using Foyer.Api.Configuration;
using Foyer.Api.Endpoints;
using Foyer.Api.Errors;
using Foyer.Api.Profiles;
using Foyer.Core;
using Foyer.Core.Data;
using Foyer.Core.Events;
using Foyer.Core.Exceptions;
using Foyer.Core.Profiles;
using Microsoft.EntityFrameworkCore;

if (args is ["--healthcheck", ..])
{
    return await HealthCheck.RunAsync(args.Length > 1 ? args[1] : "http://localhost:8080/healthz");
}

var builder = WebApplication.CreateBuilder(args);

FoyerSettings settings;
try
{
    settings = FoyerSettings.Load(builder.Configuration, builder.Environment);
}
catch (FoyerConfigurationException ex)
{
    await Console.Error.WriteLineAsync(ex.Message);
    return 1;
}

Directory.CreateDirectory(settings.DataDir);

builder.Services.AddSingleton(settings);
builder.Services.AddFoyerCore(settings.DataDir, settings.Profiles);
builder.Services.AddFoyerDockerSync(settings.Hosts, settings.Sync);
builder.Services.AddSingleton<ChangeBroadcaster>();
builder.Services.AddSingleton(EventStreamOptions.Default);
builder.Services.AddSingleton<IChangeNotifier>(sp => sp.GetRequiredService<ChangeBroadcaster>());

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

    // Numbers are numbers: the web defaults also accept "42", which types ids as number | string.
    o.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<FoyerExceptionHandler>();
builder.Services.AddOpenApi();

var app = builder.Build();

StartupLog.Starting(app.Logger, FoyerVersion.Current, settings.Hosts.Count);
LogProfileMode(app.Logger, settings.Profiles);

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<FoyerDbContext>().Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseWhen(http => http.Request.Path.StartsWithSegments("/api"), api => api.UseMiddleware<ProfileMiddleware>());

app.MapOpenApi();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapSettings();
app.MapManifest();
app.MapProfiles();
app.MapDashboard();
app.MapBookmarks();
app.MapCategories();
app.MapImport();
app.MapIcons();
app.MapEvents();

app.MapGet("/healthz", () => Results.Ok("ok"))
    .ExcludeFromDescription();

// Unknown /api routes are 404s, not the app shell.
app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

await app.RunAsync();
return 0;

static void LogProfileMode(ILogger logger, ProfileOptions profiles)
{
    if (!profiles.Enabled)
    {
        StartupLog.ProfilesOff(logger);
        return;
    }

    var proxies = profiles.TrustedProxies.Count == 0 ? "any address" : string.Join(", ", profiles.TrustedProxies);
    var editors = profiles.HasDefaultEditors
        ? string.Join(", ", profiles.DefaultEditorUsers.Concat(profiles.DefaultEditorGroups.Select(g => $"group {g}")))
        : "none listed, so only requests without a user header";
    StartupLog.ProfilesOn(logger, profiles.UserHeader, proxies, editors);
}
