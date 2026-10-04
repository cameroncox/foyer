using Foyer.Core.Entities;

namespace Foyer.Core.Tests.Services;

public sealed class BookmarkStatusTests
{
    private static Bookmark Docker(string state, ContainerHealth health = ContainerHealth.None) =>
        new() { Source = BookmarkSource.Docker, Name = "x", Url = "https://x.lan", ContainerState = state, Health = health };

    [Theory]
    [InlineData("running", ContainerHealth.None, DockerStatus.Running)]
    [InlineData("running", ContainerHealth.Healthy, DockerStatus.Running)]
    [InlineData("running", ContainerHealth.Starting, DockerStatus.Warning)]
    [InlineData("running", ContainerHealth.Unhealthy, DockerStatus.Warning)]
    [InlineData("paused", ContainerHealth.None, DockerStatus.Warning)]
    [InlineData("restarting", ContainerHealth.None, DockerStatus.Warning)]
    [InlineData("exited", ContainerHealth.None, DockerStatus.Stopped)]
    [InlineData("dead", ContainerHealth.None, DockerStatus.Stopped)]
    [InlineData("created", ContainerHealth.None, DockerStatus.Stopped)]
    public void DockerStatus_FollowsStateAndHealth(string state, ContainerHealth health, DockerStatus expected) =>
        Docker(state, health).Status.ShouldBe(expected);

    [Fact]
    public void ManualBookmarks_HaveNoDot() =>
        new Bookmark { Name = "x", Url = "https://x.lan" }.Status.ShouldBeNull();
}
