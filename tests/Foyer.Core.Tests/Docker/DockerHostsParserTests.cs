using Foyer.Core.Docker;
using Foyer.Core.Exceptions;

namespace Foyer.Core.Tests.Docker;

public sealed class DockerHostsParserTests
{
    private static IReadOnlyList<DockerHostOptions> Parse(params (string Key, string? Value)[] settings) =>
        DockerHostsParser.Parse(settings.Select(s => KeyValuePair.Create(s.Key, s.Value)));

    private static FoyerConfigurationException Fails(params (string Key, string? Value)[] settings) =>
        Should.Throw<FoyerConfigurationException>(() => Parse(settings));

    [Fact]
    public void ReadsTheSpecExample()
    {
        var hosts = Parse(
            ("FOYER_DOCKERHOSTS_DOCKER1_NAME", "docker-1"),
            ("FOYER_DOCKERHOSTS_DOCKER1_URI", "unix:///var/run/docker.sock"),
            ("FOYER_DOCKERHOSTS_DOCKER2_NAME", "docker-2"),
            ("FOYER_DOCKERHOSTS_DOCKER2_URI", "http://172.16.78.22:2375"));

        hosts.ShouldBe([
            new("DOCKER1", "docker-1", new Uri("unix:///var/run/docker.sock")),
            new("DOCKER2", "docker-2", new Uri("http://172.16.78.22:2375")),
        ]);
    }

    [Fact]
    public void NoHosts_IsValid() =>
        Parse(("FOYER_DATA_DIR", "/data"), ("PATH", "/usr/bin")).ShouldBeEmpty();

    [Fact]
    public void Name_DefaultsToKeyLowercased()
    {
        var host = Parse(("FOYER_DOCKERHOSTS_NAS_URI", "tcp://nas:2375")).ShouldHaveSingleItem();

        host.Name.ShouldBe("nas");
        host.Key.ShouldBe("NAS");
    }

    [Fact]
    public void KeysAndFields_IgnoreCase()
    {
        var host = Parse(
            ("foyer_dockerhosts_pve2_uri", "http://pve2:2375"),
            ("FOYER_DOCKERHOSTS_PVE2_Name", "pve-2")).ShouldHaveSingleItem();

        host.Name.ShouldBe("pve-2");
    }

    [Fact]
    public void NameWithoutUri_FailsNamingTheKey()
    {
        var ex = Fails(("FOYER_DOCKERHOSTS_DOCKER3_NAME", "docker-3"));

        ex.Problems.ShouldHaveSingleItem().ShouldStartWith("DOCKER3 has no URI");
    }

    [Theory]
    [InlineData("ssh://docker-2")]
    [InlineData("https://docker-2:2376")]
    [InlineData("docker-2:2375")]
    public void UnsupportedUri_Fails(string uri) =>
        Fails(("FOYER_DOCKERHOSTS_DOCKER2_URI", uri)).Problems.ShouldHaveSingleItem().ShouldContain("DOCKER2_URI");

    [Theory]
    [InlineData("FOYER_DOCKERHOSTS_DOCKER1_URL")]
    [InlineData("FOYER_DOCKERHOSTS_DOCKER-1_URI")]
    [InlineData("FOYER_DOCKERHOSTS_URI")]
    public void MalformedSettingName_Fails(string key) =>
        Fails((key, "http://x:2375")).Problems.ShouldHaveSingleItem().ShouldStartWith(key);

    [Fact]
    public void DuplicateNames_Fail()
    {
        var ex = Fails(
            ("FOYER_DOCKERHOSTS_A_URI", "http://a:2375"),
            ("FOYER_DOCKERHOSTS_A_NAME", "docker"),
            ("FOYER_DOCKERHOSTS_B_URI", "http://b:2375"),
            ("FOYER_DOCKERHOSTS_B_NAME", "Docker"));

        ex.Problems.ShouldHaveSingleItem().ShouldContain("A, B");
    }

    [Fact]
    public void ReportsEveryProblemAtOnce()
    {
        var ex = Fails(
            ("FOYER_DOCKERHOSTS_X_NAME", "x"),
            ("FOYER_DOCKERHOSTS_Y_URI", "ftp://y"));

        ex.Problems.Count.ShouldBe(2);
        ex.Message.ShouldContain("X has no URI");
    }
}
