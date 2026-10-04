using System.Net;

namespace Foyer.Api.Tests;

public sealed class HealthzTests(FoyerApiFactory factory)
    : IClassFixture<FoyerApiFactory>
{
    [Fact]
    public async Task Healthz_ReturnsOk()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/healthz", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
