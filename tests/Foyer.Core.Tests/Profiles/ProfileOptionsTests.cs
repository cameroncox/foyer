using System.Net;
using Foyer.Core.Exceptions;
using Foyer.Core.Profiles;

namespace Foyer.Core.Tests.Profiles;

public sealed class ProfileOptionsTests
{
    private static ProfileOptions Parse(params (string Key, string Value)[] values) =>
        ProfileOptions.Parse(values.Select(v => KeyValuePair.Create(v.Key, (string?)v.Value)));

    [Fact]
    public void Defaults_AreOnWithTinyauthHeadersAndNoEditors()
    {
        var options = Parse();

        options.Enabled.ShouldBeTrue();
        options.UserHeader.ShouldBe("Remote-User");
        options.GroupsHeader.ShouldBe("Remote-Groups");
        options.TrustedProxies.ShouldBeEmpty();
        options.HasDefaultEditors.ShouldBeFalse();
    }

    [Fact]
    public void Settings_AreRead()
    {
        var options = Parse(
            ("FOYER_PROFILES", "true"),
            ("FOYER_PROFILE_HEADER", "X-Forwarded-User"),
            ("FOYER_GROUPS_HEADER", "X-Forwarded-Groups"),
            ("FOYER_TRUSTED_PROXIES", "192.0.2.10, 198.51.100.0/24,2001:db8::1"),
            ("FOYER_DEFAULT_REMOTE_USERS", "cameron, alex"),
            ("FOYER_DEFAULT_REMOTE_GROUPS", "admins"));

        options.Enabled.ShouldBeTrue();
        options.UserHeader.ShouldBe("X-Forwarded-User");
        options.GroupsHeader.ShouldBe("X-Forwarded-Groups");
        options.TrustedProxies.Select(n => n.ToString()).ShouldBe(["192.0.2.10/32", "198.51.100.0/24", "2001:db8::1/128"]);
        options.DefaultEditorUsers.ShouldBe(["cameron", "alex"]);
        options.DefaultEditorGroups.ShouldBe(["admins"]);
        options.HasDefaultEditors.ShouldBeTrue();
    }

    [Fact]
    public void BadValues_AreAllReported_NamingTheEntry()
    {
        var ex = Should.Throw<FoyerConfigurationException>(() => Parse(
            ("FOYER_PROFILES", "yes"),
            ("FOYER_TRUSTED_PROXIES", "192.0.2.10, traefik, 198.51.100.0/40")));

        ex.Problems.Count.ShouldBe(3);
        ex.Problems.ShouldContain(p => p.Contains("FOYER_PROFILES"));
        ex.Problems.ShouldContain(p => p.Contains("'traefik'"));
        ex.Problems.ShouldContain(p => p.Contains("'198.51.100.0/40'"));
    }

    [Theory]
    [InlineData("cameroncox@gmail.com", "cameroncox_gmail.com")]
    [InlineData("cameroncox_gmail.com", "cameroncox@gmail.com")]
    [InlineData("CameronCox@Gmail.com", "cameroncox@gmail.com")]
    public void DefaultEditorUsers_MatchWithAtAndUnderscoreTheSame(string listed, string sent)
    {
        Parse(("FOYER_DEFAULT_REMOTE_USERS", listed)).IsDefaultEditorUser(sent).ShouldBeTrue();
    }

    [Fact]
    public void DefaultEditorUsers_DontMatchOtherUsers()
    {
        Parse(("FOYER_DEFAULT_REMOTE_USERS", "cameron@example.com")).IsDefaultEditorUser("cameron").ShouldBeFalse();
    }

    [Theory]
    [InlineData("198.51.100.7", true)]
    [InlineData("::ffff:198.51.100.7", true)]
    [InlineData("192.0.2.10", true)]
    [InlineData("192.0.2.11", false)]
    [InlineData(null, false)]
    public void Trusts_ListedAddressesOnly(string? address, bool trusted)
    {
        var options = Parse(("FOYER_TRUSTED_PROXIES", "192.0.2.10,198.51.100.0/24"));

        options.Trusts(address is null ? null : IPAddress.Parse(address)).ShouldBe(trusted);
    }

    [Fact]
    public void Trusts_EveryAddress_WhenNoneAreListed()
    {
        Parse().Trusts(IPAddress.Parse("203.0.113.5")).ShouldBeTrue();
        Parse().Trusts(null).ShouldBeTrue();
    }
}
