using Foyer.Api;

if (args is ["--healthcheck", ..])
{
    return await HealthCheck.RunAsync(args.Length > 1 ? args[1] : "http://localhost:8080/healthz");
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

app.MapOpenApi();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/healthz", () => Results.Ok("ok"))
    .ExcludeFromDescription();

app.MapFallbackToFile("index.html");

await app.RunAsync();
return 0;
