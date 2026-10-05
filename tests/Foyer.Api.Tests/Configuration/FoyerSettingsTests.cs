using Foyer.Api.Configuration;
using Foyer.Core.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Foyer.Api.Tests.Configuration;

public sealed class FoyerSettingsTests
{
    private static FoyerSettings Load(params (string Key, string Value)[] values) =>
        FoyerSettings.Load(
            new ConfigurationBuilder()
                .AddInMemoryCollection(values.Select(v => KeyValuePair.Create(v.Key, (string?)v.Value)))
                .Build(),
            new TestEnvironment());

    [Theory]
    [InlineData("ftp://search.example/?q=")]
    [InlineData("not a url")]
    [InlineData("/search?q=%s")]
    public void SearchUrl_ThatIsntHttp_IsAStartupProblem(string url)
    {
        var ex = Should.Throw<FoyerConfigurationException>(() => Load(("FOYER_SEARCH_URL", url)));

        ex.Problems.ShouldHaveSingleItem().ShouldContain("FOYER_SEARCH_URL");
    }

    [Fact]
    public void Title_OverSixtyCharacters_IsAStartupProblem()
    {
        var ex = Should.Throw<FoyerConfigurationException>(() => Load(("FOYER_TITLE", new string('x', 61))));

        ex.Problems.ShouldHaveSingleItem().ShouldContain("FOYER_TITLE");
    }

    [Fact]
    public void ProfileSettings_AreRead_AndBadOnesReportedWithTheRest()
    {
        Load(("FOYER_PROFILES", "true"), ("FOYER_TRUSTED_PROXIES", "192.0.2.10")).Profiles.Enabled.ShouldBeTrue();

        var ex = Should.Throw<FoyerConfigurationException>(() => Load(
            ("FOYER_TRUSTED_PROXIES", "traefik"),
            ("FOYER_TITLE", new string('x', 61))));

        ex.Problems.Count.ShouldBe(2);
        ex.Problems.ShouldContain(p => p.Contains("'traefik'"));
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "Foyer.Api.Tests";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
