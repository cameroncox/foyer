using Docker.DotNet.Models;
using Foyer.Core.Docker;
using Foyer.Core.Entities;

namespace Foyer.Core.Tests.Docker;

public sealed class ContainerInfoMapperTests
{
    private static ContainerListResponse Response(
        string[]? names = null,
        string state = "running",
        string status = "Up 5 minutes",
        HealthSummary? health = null,
        Dictionary<string, string>? labels = null) =>
        new()
        {
            ID = "abc123",
            Names = names ?? ["/sonarr"],
            State = state,
            Status = status,
            Health = health,
            Labels = labels!,
        };

    [Fact]
    public void Maps_NameStateAndLabels()
    {
        var info = ContainerInfoMapper.Map(Response(
            state: "Exited", labels: new() { ["coxdev.bookmark.enabled"] = "true" }));

        info.Name.ShouldBe("sonarr");
        info.State.ShouldBe("exited");
        info.Health.ShouldBe(ContainerHealth.None);
        info.Labels["coxdev.bookmark.enabled"].ShouldBe("true");
    }

    [Fact]
    public void Name_SkipsLinkAliases() =>
        ContainerInfoMapper.Map(Response(names: ["/web/db", "/db"])).Name.ShouldBe("db");

    [Fact]
    public void MissingLabels_AreEmpty() =>
        ContainerInfoMapper.Map(Response()).Labels.ShouldBeEmpty();

    [Theory]
    [InlineData("healthy", ContainerHealth.Healthy)]
    [InlineData("Unhealthy", ContainerHealth.Unhealthy)]
    [InlineData("starting", ContainerHealth.Starting)]
    [InlineData("none", ContainerHealth.None)]
    [InlineData("", ContainerHealth.None)]
    public void Health_FromHealthSummary(string status, ContainerHealth expected) =>
        ContainerInfoMapper.Map(Response(health: new HealthSummary { Status = status })).Health.ShouldBe(expected);

    [Theory]
    [InlineData("Up 2 hours (healthy)", ContainerHealth.Healthy)]
    [InlineData("Up 2 hours (unhealthy)", ContainerHealth.Unhealthy)]
    [InlineData("Up 3 seconds (health: starting)", ContainerHealth.Starting)]
    [InlineData("Up 2 hours", ContainerHealth.None)]
    public void Health_FromStatusText_WhenNoSummary(string status, ContainerHealth expected) =>
        ContainerInfoMapper.Map(Response(status: status)).Health.ShouldBe(expected);
}
