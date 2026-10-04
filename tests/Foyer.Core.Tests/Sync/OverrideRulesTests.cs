using Foyer.Core.Models;
using Foyer.Core.Sync;
using Foyer.Core.Tests.Support;

namespace Foyer.Core.Tests.Sync;

public sealed class OverrideRulesTests
{
    private static KnownDockerBookmark Known(
        string category = "Media",
        string? labelCategory = "Media",
        bool present = true,
        bool categoryOverridden = false,
        bool tagsOverridden = false,
        string state = "running") =>
        new(
            Id: 7,
            ContainerName: "sonarr",
            Name: "sonarr",
            Url: "https://sonarr.lan",
            Icon: null,
            CategoryName: category,
            LabelCategory: labelCategory,
            LabelTags: ["arr"],
            ContainerState: state,
            Health: null,
            IsPresent: present,
            CategoryOverridden: categoryOverridden,
            TagsOverridden: tagsOverridden);

    private static (BookmarkLabels Labels, ContainerInfo Container) Seen(
        string? category = "Media", string tags = "arr", string state = "running")
    {
        var container = Containers.Labeled("sonarr", category, tags, state);
        return (LabelParser.Parse(container.Labels, container.Name, homepageFallback: true)!, container);
    }

    [Fact]
    public void ApplyLabels_NothingChanged_ReturnsNull()
    {
        var (labels, container) = Seen();

        OverrideRules.ApplyLabels(Known(), labels, container).ShouldBeNull();
    }

    [Fact]
    public void ApplyLabels_StateChange_Updates_WithoutMoving()
    {
        var (labels, container) = Seen(state: "exited");

        var update = OverrideRules.ApplyLabels(Known(), labels, container);

        update.ShouldNotBeNull();
        update.ContainerState.ShouldBe("exited");
        update.MoveToCategory.ShouldBeNull();
        update.IsPresent.ShouldBeTrue();
    }

    [Fact]
    public void ApplyLabels_NewLabelCategory_MovesWhenNotOverridden()
    {
        var (labels, container) = Seen(category: "TV");

        var update = OverrideRules.ApplyLabels(Known(), labels, container);

        update!.MoveToCategory.ShouldBe("TV");
        update.LabelCategory.ShouldBe("TV");
    }

    [Fact]
    public void ApplyLabels_NewLabelCategory_IgnoredWhenOverridden_ButRemembered()
    {
        var (labels, container) = Seen(category: "TV");

        var update = OverrideRules.ApplyLabels(
            Known(category: "Mine", categoryOverridden: true), labels, container);

        update!.MoveToCategory.ShouldBeNull();
        update.LabelCategory.ShouldBe("TV");
    }

    [Fact]
    public void ApplyLabels_CategoryDiffersOnlyInCase_DoesNotMove()
    {
        var (labels, container) = Seen(category: "media");

        var update = OverrideRules.ApplyLabels(Known(labelCategory: "media"), labels, container);

        update.ShouldBeNull();
    }

    [Fact]
    public void ApplyLabels_NoLabelCategory_TargetsUncategorized()
    {
        var (labels, container) = Seen(category: null);

        var stays = OverrideRules.ApplyLabels(Known("Uncategorized", null), labels, container);
        var moves = OverrideRules.ApplyLabels(Known(), labels, container);

        stays.ShouldBeNull();
        moves!.MoveToCategory.ShouldBe("Uncategorized");
    }

    [Fact]
    public void ApplyLabels_HiddenBookmarkComesBack_EvenWithSameLabels()
    {
        var (labels, container) = Seen();

        var update = OverrideRules.ApplyLabels(Known(present: false), labels, container);

        update.ShouldNotBeNull();
        update.IsPresent.ShouldBeTrue();
        update.MoveToCategory.ShouldBeNull();
    }

    [Fact]
    public void ApplyLabels_NewLabelTags_AreStored_RegardlessOfOverride()
    {
        var (labels, container) = Seen(tags: "arr, tv");

        var update = OverrideRules.ApplyLabels(Known(tagsOverridden: true), labels, container);

        update!.LabelTags.ShouldBe(["arr", "tv"]);
        update.ClearOverrides.ShouldBeFalse();
    }

    [Fact]
    public void Reset_ClearsOverrides_AndMovesBackToLabelCategory()
    {
        var update = OverrideRules.Reset(Known(category: "Mine", categoryOverridden: true, tagsOverridden: true));

        update.ClearOverrides.ShouldBeTrue();
        update.MoveToCategory.ShouldBe("Media");
        update.IsPresent.ShouldBeTrue();
    }

    [Fact]
    public void Reset_AlreadyInLabelCategory_DoesNotMove()
    {
        var update = OverrideRules.Reset(Known(tagsOverridden: true));

        update.MoveToCategory.ShouldBeNull();
    }

    [Fact]
    public void Reset_HiddenBookmark_StaysHidden()
    {
        var update = OverrideRules.Reset(Known(present: false, categoryOverridden: true));

        update.IsPresent.ShouldBeFalse();
    }
}
