using Foyer.Core.Entities;
using Foyer.Core.Sharing;

namespace Foyer.Core.Tests.Sharing;

public sealed class SharedPlacementRulesTests
{
    [Fact]
    public void TargetCategoryName_IsTheOwnersName_OrUncategorized()
    {
        SharedPlacementRules.TargetCategoryName(new Category { Name = "Media" }).ShouldBe("Media");
        SharedPlacementRules.TargetCategoryName(new Category { Name = Category.UncategorizedName, IsSystem = true }).ShouldBeNull();
    }

    [Fact]
    public void OwnerLabel_IsTheUser_OrTheProfileWithoutOne()
    {
        SharedPlacementRules.OwnerLabel(new Profile { Name = "alex", Slug = "alex", OwnerUser = "alex@example.com" }).ShouldBe("alex@example.com");
        SharedPlacementRules.OwnerLabel(new Profile { Name = "vendor", Slug = "vendor" }).ShouldBe("vendor");
    }

    [Theory]
    [InlineData(1, new[] { "alex" }, "Read Later holds 1 bookmark shared by alex, so it can't be deleted. Move your own bookmarks out, or ask alex to unshare.")]
    [InlineData(3, new[] { "alex", "Default" }, "Read Later holds 3 bookmarks shared by alex and Default, so it can't be deleted. Move your own bookmarks out, or ask alex and Default to unshare.")]
    [InlineData(3, new[] { "a", "b", "c" }, "Read Later holds 3 bookmarks shared by a, b and c, so it can't be deleted. Move your own bookmarks out, or ask a, b and c to unshare.")]
    public void BlockedDeleteMessage_NamesTheOwners(int count, string[] owners, string message)
    {
        SharedPlacementRules.BlockedDeleteMessage("Read Later", count, owners).ShouldBe(message);
    }
}
