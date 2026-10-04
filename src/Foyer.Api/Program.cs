using Foyer.Api;
using Foyer.Api.Configuration;
using Foyer.Api.Events;
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
builder.Services.AddSingleton<IChangeNotifier, NullChangeNotifier>();
builder.Services.AddOpenApi();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<FoyerDbContext>().Database.MigrateAsync();
}

app.MapOpenApi();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/healthz", () => Results.Ok("ok"))
    .ExcludeFromDescription();

app.MapFallbackToFile("index.html");

await app.RunAsync();
return 0;
