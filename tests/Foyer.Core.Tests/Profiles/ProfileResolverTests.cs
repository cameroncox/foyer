using System.Net;
using Foyer.Core.Entities;
using Foyer.Core.Profiles;

namespace Foyer.Core.Tests.Profiles;

public sealed class ProfileResolverTests
{
    private static readonly IPAddress Proxy = IPAddress.Parse("192.0.2.10");
    private static readonly IPAddress Elsewhere = IPAddress.Parse("203.0.113.5");

    private static readonly ProfileOptions On = ProfileOptions.Default with { Enabled = true };

    private static readonly Profile Default = new() { Id = 1, Name = "Default", Slug = "default", IsSystem = true };
    private static readonly Profile Cameron = new() { Id = 2, Name = "cameron@casadecox.org", Slug = "cameron-casadecox-org", OwnerUser = "cameron@casadecox.org", IsPersonal = true };
    private static readonly Profile Work = new() { Id = 3, Name = "work", Slug = "work", OwnerUser = "cameron@casadecox.org" };
    private static readonly Profile Vendor = new() { Id = 4, Name = "vendor", Slug = "vendor" };
    private static readonly Profile Alex = new() { Id = 5, Name = "alex", Slug = "alex", OwnerUser = "alex", IsPersonal = true };
    private static readonly Profile[] All = [Default, Cameron, Work, Vendor, Alex];

    private static readonly Caller Anonymous = Caller.Anonymous;
    private static readonly Caller CameronCaller = new("cameron@casadecox.org", ["family"]);

    private static ProfileOptions Trusting(string proxies) =>
        ProfileOptions.Parse([KeyValuePair.Create<string, string?>("FOYER_PROFILES", "true"), KeyValuePair.Create<string, string?>("FOYER_TRUSTED_PROXIES", proxies)]);

    [Fact]
    public void Identify_IgnoresHeaders_WhenProfilesAreOff()
    {
        var result = ProfileResolver.Identify(ProfileOptions.Default with { Enabled = false }, new RequestFacts(Elsewhere, "", null));

        result.Caller.ShouldBe(Caller.Anonymous);
    }

    [Fact]
    public void Identify_NoHeader_IsAnonymous()
    {
        ProfileResolver.Identify(Trusting("192.0.2.10"), new RequestFacts(Elsewhere, null, null)).Caller.ShouldBe(Caller.Anonymous);
    }

    [Fact]
    public void Identify_ReadsUserAndGroups_FromATrustedProxy()
    {
        var result = ProfileResolver.Identify(Trusting("192.0.2.10"), new RequestFacts(Proxy, " cameron ", "family, admins,"));

        result.Caller!.User.ShouldBe("cameron");
        result.Caller.Groups.ShouldBe(["family", "admins"]);
    }

    [Fact]
    public void Identify_TrustsEveryAddress_WithNoProxiesListed()
    {
        ProfileResolver.Identify(On, new RequestFacts(Elsewhere, "cameron", null)).Caller!.User.ShouldBe("cameron");
    }

    [Theory]
    [InlineData("cameron", null)]
    [InlineData(null, "admins")]
    public void Identify_RefusesHeadersFromUntrustedAddresses(string? user, string? groups)
    {
        var result = ProfileResolver.Identify(Trusting("192.0.2.10"), new RequestFacts(Elsewhere, user, groups));

        result.Caller.ShouldBeNull();
        result.FromUntrustedAddress.ShouldBeTrue();
    }

    [Fact]
    public void Identify_RefusesAnEmptyUserHeader()
    {
        var result = ProfileResolver.Identify(On, new RequestFacts(Proxy, "  ", null));

        result.Caller.ShouldBeNull();
        result.FromUntrustedAddress.ShouldBeFalse();
        result.Refusal!.ShouldContain("empty");
    }

    [Fact]
    public void Identify_GroupsWithoutAUser_AreAnonymous()
    {
        ProfileResolver.Identify(On, new RequestFacts(Proxy, null, "admins")).Caller.ShouldBe(Caller.Anonymous);
    }

    [Fact]
    public void Pick_NoSlug_IsDefaultWithoutAUser_AndThePersonalProfileWithOne()
    {
        ProfileResolver.Pick(Anonymous, null, All).ShouldBe(Default);
        ProfileResolver.Pick(CameronCaller, "", All).ShouldBe(Cameron);
    }

    [Theory]
    [InlineData("default", 1)]
    [InlineData("VENDOR", 4)]
    [InlineData("work", 3)]
    [InlineData("Cameron-Casadecox-Org", 2)]
    public void Pick_FindsVisibleProfilesBySlug_IgnoringCase(string slug, int id)
    {
        ProfileResolver.Pick(CameronCaller, slug, All)!.Id.ShouldBe(id);
    }

    [Theory]
    [InlineData("alex")]
    [InlineData("nope")]
    public void Pick_SomeoneElsesProfile_IsAsGoodAsMissing(string slug)
    {
        ProfileResolver.Pick(CameronCaller, slug, All).ShouldBeNull();
    }

    [Fact]
    public void Pick_WithoutAUser_SeesOnlyDefaultAndOwnerless()
    {
        ProfileResolver.Pick(Anonymous, "vendor", All).ShouldBe(Vendor);
        ProfileResolver.Pick(Anonymous, "work", All).ShouldBeNull();
    }

    [Fact]
    public void CanEdit_OwnAndOwnerlessProfiles_Always()
    {
        var editors = On with { DefaultEditorUsers = ["someone"] };

        ProfileResolver.CanEdit(editors, CameronCaller, Work).ShouldBeTrue();
        ProfileResolver.CanEdit(editors, CameronCaller, Vendor).ShouldBeTrue();
        ProfileResolver.CanEdit(editors, Anonymous, Vendor).ShouldBeTrue();
    }

    [Fact]
    public void CanEdit_Default_WithNoEditorsListed_IsHeaderlessOnly()
    {
        ProfileResolver.CanEdit(On, Anonymous, Default).ShouldBeTrue();
        ProfileResolver.CanEdit(On, CameronCaller, Default).ShouldBeFalse();
    }

    [Fact]
    public void CanEdit_Default_WithEditorsListed_IsThemOnly()
    {
        var byUser = On with { DefaultEditorUsers = ["CAMERON@casadecox.org"] };
        var byGroup = On with { DefaultEditorGroups = ["Family"] };
        var neither = On with { DefaultEditorUsers = ["alex"], DefaultEditorGroups = ["admins"] };

        ProfileResolver.CanEdit(byUser, CameronCaller, Default).ShouldBeTrue();
        ProfileResolver.CanEdit(byGroup, CameronCaller, Default).ShouldBeTrue();
        ProfileResolver.CanEdit(neither, CameronCaller, Default).ShouldBeFalse();
        ProfileResolver.CanEdit(byUser, Anonymous, Default).ShouldBeFalse();
    }

    [Fact]
    public void CanEdit_Everything_WhenProfilesAreOff()
    {
        var off = ProfileOptions.Default with { Enabled = false, DefaultEditorUsers = ["alex"] };

        ProfileResolver.CanEdit(off, CameronCaller, Default).ShouldBeTrue();
    }

    [Fact]
    public void CanManage_NeverDefaultOrAPersonalProfile()
    {
        ProfileResolver.CanManage(On, Default).ShouldBeFalse();
        ProfileResolver.CanManage(On, Cameron).ShouldBeFalse();
        ProfileResolver.CanManage(On, Work).ShouldBeTrue();
        ProfileResolver.CanManage(On, Vendor).ShouldBeTrue();
        ProfileResolver.CanManage(ProfileOptions.Default with { Enabled = false }, Vendor).ShouldBeFalse();
    }
}
