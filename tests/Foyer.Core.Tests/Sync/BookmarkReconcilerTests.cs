using Foyer.Core.Models;
using Foyer.Core.Sync;
using Foyer.Core.Tests.Support;

namespace Foyer.Core.Tests.Sync;

public sealed class BookmarkReconcilerTests
{
    private static KnownDockerBookmark Known(int id, string container, bool present = true) =>
        new(id, container, container, $"https://{container}.lan", null, "Uncategorized", null, [],
            "running", null, present, false, false);

    private static ReconcilePlan Reconcile(IEnumerable<ContainerInfo> containers, params KnownDockerBookmark[] known) =>
        BookmarkReconciler.ReconcileHost("docker-1", containers, known, homepageFallback: true);

    [Fact]
    public void NewLabeledContainer_IsCreated()
    {
        var plan = Reconcile([Containers.Labeled("sonarr", "Media", state: "running", health: "healthy")]);

        var create = plan.Creates.ShouldHaveSingleItem();
        create.ContainerName.ShouldBe("sonarr");
        create.Labels.Category.ShouldBe("Media");
        create.Health.ShouldBe("healthy");
        plan.Host.ShouldBe("docker-1");
    }

    [Fact]
    public void UnlabeledContainer_IsIgnored()
    {
        var plan = Reconcile([Containers.Unlabeled("postgres")]);

        plan.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void KnownAndUnchanged_GivesAnEmptyPlan()
    {
        var plan = Reconcile([Containers.Labeled("sonarr")], Known(1, "sonarr"));

        plan.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void KnownWithNewState_IsUpdated()
    {
        var plan = Reconcile([Containers.Labeled("sonarr", state: "exited")], Known(1, "sonarr"));

        plan.Updates.ShouldHaveSingleItem().ContainerState.ShouldBe("exited");
    }

    [Fact]
    public void RemovedContainer_IsHidden()
    {
        var plan = Reconcile([], Known(1, "sonarr"));

        plan.Hides.ShouldBe([1]);
    }

    [Fact]
    public void ContainerThatLostItsLabels_IsHidden()
    {
        var plan = Reconcile([Containers.Unlabeled("sonarr")], Known(1, "sonarr"));

        plan.Hides.ShouldBe([1]);
    }

    [Fact]
    public void AlreadyHidden_AndStillGone_IsLeftAlone()
    {
        var plan = Reconcile([], Known(1, "sonarr", present: false));

        plan.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void HiddenContainerThatComesBack_IsUpdated_NotCreated()
    {
        var plan = Reconcile([Containers.Labeled("sonarr")], Known(1, "sonarr", present: false));

        plan.Creates.ShouldBeEmpty();
        plan.Updates.ShouldHaveSingleItem().Id.ShouldBe(1);
    }

    [Fact]
    public void ContainerNamesMatchExactly()
    {
        var plan = Reconcile([Containers.Labeled("Sonarr")], Known(1, "sonarr"));

        plan.Creates.ShouldHaveSingleItem().ContainerName.ShouldBe("Sonarr");
        plan.Hides.ShouldBe([1]);
    }

    [Fact]
    public void DuplicateNamesInAListing_AreCreatedOnce()
    {
        var plan = Reconcile([Containers.Labeled("sonarr"), Containers.Labeled("sonarr")]);

        plan.Creates.ShouldHaveSingleItem();
    }
}
