using Foyer.Api;
using Foyer.Api.Configuration;
using Foyer.Api.Events;
using Foyer.Core;
using Foyer.Core.Data;
using Foyer.Core.Events;
using Microsoft.EntityFrameworkCore;

if (args is ["--healthcheck", ..])
{
    return await HealthCheck.RunAsync(args.Length > 1 ? args[1] : "http://localhost:8080/healthz");
}

var builder = WebApplication.CreateBuilder(args);

var dataDir = FoyerSettings.DataDir(builder.Configuration, builder.Environment);
Directory.CreateDirectory(dataDir);

builder.Services.AddFoyerCore(dataDir);
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
