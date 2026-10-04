using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Foyer.Core.Entities;

namespace Foyer.Api.Tests.Support;

/// <summary>JSON helpers matching the API's settings (camelCase, enums as strings).</summary>
public static class TestApi
{
    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;

    public static Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PostAsJsonAsync(url, body, Json);

    public static Task<HttpResponseMessage> PutJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PutAsJsonAsync(url, body, Json);

    public static ContainerInfo Labeled(string name, string? category = null, string? tags = null)
    {
        var labels = new Dictionary<string, string>
        {
            ["coxdev.bookmark.enabled"] = "true",
            ["coxdev.bookmark.url"] = $"https://{name}.lan",
        };
        if (category is not null)
        {
            labels["coxdev.bookmark.category"] = category;
        }

        if (tags is not null)
        {
            labels["coxdev.bookmark.tags"] = tags;
        }

        return new ContainerInfo(name, "running", ContainerHealth.Healthy, labels);
    }
}
