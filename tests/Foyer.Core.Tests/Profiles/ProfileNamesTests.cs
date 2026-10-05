using Foyer.Core.Entities;
using Foyer.Core.Exceptions;
using Foyer.Core.Profiles;

namespace Foyer.Core.Tests.Profiles;

public sealed class ProfileNamesTests
{
    private static Profile P(string slug, string? owner = null, int id = 0) =>
        new() { Id = id, Name = slug, Slug = slug, OwnerUser = owner };

    [Theory]
    [InlineData("vendor")]
    [InlineData("Work-2")]
    [InlineData("  kitchen  ")]
    public void Validate_AcceptsLettersDigitsAndHyphens(string name)
    {
        ProfileNames.Validate(name).ShouldBe(name.Trim());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("my profile")]
    [InlineData("café")]
    [InlineData("a/b")]
    public void Validate_RefusesOtherNames(string name)
    {
        Should.Throw<InvalidInputException>(() => ProfileNames.Validate(name));
    }

    [Fact]
    public void Validate_RefusesLongNames()
    {
        Should.Throw<InvalidInputException>(() => ProfileNames.Validate(new string('a', 51)));
    }

    [Theory]
    [InlineData("api")]
    [InlineData("assets")]
    [InlineData("Default")]
    [InlineData("healthz")]
    [InlineData("add")]
    [InlineData("openapi")]
    public void Validate_RefusesPathsFoyerServes(string name)
    {
        Should.Throw<InvalidInputException>(() => ProfileNames.Validate(name)).Message.ShouldContain("used by Foyer");
    }

    [Theory]
    [InlineData("cameron@casadecox.org", "cameron-casadecox-org")]
    [InlineData("Cameron Cox", "cameron-cox")]
    [InlineData("--alex--", "alex")]
    [InlineData("@@@", "user")]
    public void Slugify_LowersAndHyphenates(string value, string slug)
    {
        ProfileNames.Slugify(value).ShouldBe(slug);
    }

    [Fact]
    public void Ownerless_MustBeUnusedEverywhere()
    {
        ProfileNames.IsAvailable("work", null, [P("work", "alex")]).ShouldBeFalse();
        ProfileNames.IsAvailable("WORK", null, [P("work")]).ShouldBeFalse();
        ProfileNames.IsAvailable("vendor", null, [P("work", "alex")]).ShouldBeTrue();
    }

    [Fact]
    public void UserProfiles_ClashOnlyWithOwnerlessAndTheirOwn()
    {
        ProfileNames.IsAvailable("work", "cameron", [P("work", "alex")]).ShouldBeTrue();
        ProfileNames.IsAvailable("work", "cameron", [P("work", "CAMERON")]).ShouldBeFalse();
        ProfileNames.IsAvailable("vendor", "cameron", [P("vendor")]).ShouldBeFalse();
        ProfileNames.IsAvailable("default", "cameron", []).ShouldBeFalse();
    }

    [Fact]
    public void IsAvailable_IgnoresTheProfileBeingRenamed()
    {
        ProfileNames.IsAvailable("work", null, [P("work", id: 7)], exceptId: 7).ShouldBeTrue();
    }

    [Fact]
    public void PersonalSlug_GetsASuffix_WhenTaken()
    {
        ProfileNames.PersonalSlug("cameron", []).ShouldBe("cameron");
        ProfileNames.PersonalSlug("cameron", [P("cameron"), P("cameron-2")]).ShouldBe("cameron-3");
        ProfileNames.PersonalSlug("api", []).ShouldBe("api-2");
    }

    [Fact]
    public void PersonalSlug_StaysWithinTheLengthLimit()
    {
        var user = new string('a', 60);

        var slug = ProfileNames.PersonalSlug(user, [P(new string('a', 50))]);

        slug.ShouldBe(new string('a', 48) + "-2");
    }
}
