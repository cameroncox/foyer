using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Foyer.Api;
using Foyer.Api.Configuration;
using Foyer.Api.Endpoints;
using Foyer.Api.Errors;
using Foyer.Core;
using Foyer.Core.Data;
using Foyer.Core.Events;
using Foyer.Core.Exceptions;
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

builder.Services.AddFoyerCore(settings.DataDir);
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

var version = typeof(Program).Assembly
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
StartupLog.Starting(app.Logger, version, settings.Hosts.Count);

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<FoyerDbContext>().Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapOpenApi();

app.UseDefaultFiles();
app.UseStaticFiles();

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
