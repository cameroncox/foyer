using System.Net;
using Foyer.Api.Tests.Support;

namespace Foyer.Api.Tests;

public sealed class HealthzTests
{
    [Fact]
    public async Task Healthz_ReturnsOk()
    {
        await using var app = new FoyerApiFactory();
        using var client = app.CreateClient();

        using var response = await client.GetAsync(new Uri("/healthz", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
