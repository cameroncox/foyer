namespace Foyer.Api;

/// <summary>
/// Backs the image's HEALTHCHECK: the chiseled runtime has no shell or curl,
/// so the app binary probes its own /healthz endpoint.
/// </summary>
internal static class HealthCheck
{
    public static async Task<int> RunAsync(string url)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            using var response = await client.GetAsync(new Uri(url));
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return 1;
        }
    }
}
