using Foyer.Core.Sync;

namespace Foyer.Core.Tests.Sync;

public sealed class LabelParserTests
{
    private static Dictionary<string, string> Labels(params (string Key, string Value)[] pairs) =>
        pairs.ToDictionary(p => p.Key, p => p.Value);

    [Fact]
    public void Coxdev_ReadsEveryField()
    {
        var parsed = LabelParser.Parse(Labels(
            ("coxdev.bookmark.enabled", "true"),
            ("coxdev.bookmark.name", "Sonarr"),
            ("coxdev.bookmark.category", "Media"),
            ("coxdev.bookmark.url", "https://sonarr.lan"),
            ("coxdev.bookmark.icon", "sonarr.svg"),
            ("coxdev.bookmark.tags", "arr, tv")), "sonarr", homepageFallback: true);

        parsed.ShouldNotBeNull();
        parsed.Name.ShouldBe("Sonarr");
        parsed.Category.ShouldBe("Media");
        parsed.Url.ShouldBe("https://sonarr.lan");
        parsed.Icon.ShouldBe("sonarr.svg");
        parsed.Tags.ShouldBe(["arr", "tv"]);
    }

    [Fact]
    public void HomepageOnly_WithFallback_ReadsHomepageLabels()
    {
        var parsed = LabelParser.Parse(Labels(
            ("homepage.name", "Jellyfin"),
            ("homepage.group", "Media"),
            ("homepage.href", "https://jellyfin.lan"),
            ("homepage.icon", "jellyfin.svg"),
            ("homepage.description", "ignored"),
            ("homepage.widget.type", "jellyfin")), "jellyfin", homepageFallback: true);

        parsed.ShouldBe(new(
            "Jellyfin", "https://jellyfin.lan", "jellyfin.svg", "Media", parsed!.Tags));
        parsed.Tags.ShouldBeEmpty();
    }

    [Fact]
    public void HomepageOnly_WithoutFallback_IsNotABookmark()
    {
        var parsed = LabelParser.Parse(
            Labels(("homepage.href", "https://jellyfin.lan")), "jellyfin", homepageFallback: false);

        parsed.ShouldBeNull();
    }

    [Fact]
    public void EnabledFalse_KeepsContainerOff_EvenWithHomepageLabels()
    {
        var parsed = LabelParser.Parse(Labels(
            ("coxdev.bookmark.enabled", "false"),
            ("homepage.href", "https://jellyfin.lan")), "jellyfin", homepageFallback: true);

        parsed.ShouldBeNull();
    }

    [Fact]
    public void Coxdev_WinsFieldByField_FallingBackToHomepageForTheRest()
    {
        var parsed = LabelParser.Parse(Labels(
            ("coxdev.bookmark.name", "Radarr"),
            ("homepage.name", "radarr-old"),
            ("homepage.group", "Media"),
            ("homepage.href", "https://radarr.lan")), "radarr", homepageFallback: true);

        parsed.ShouldNotBeNull();
        parsed.Name.ShouldBe("Radarr");
        parsed.Category.ShouldBe("Media");
        parsed.Url.ShouldBe("https://radarr.lan");
    }

    [Fact]
    public void FallbackOff_IgnoresHomepageFields_OnAnOptedInContainer()
    {
        var parsed = LabelParser.Parse(Labels(
            ("coxdev.bookmark.enabled", "true"),
            ("coxdev.bookmark.url", "https://radarr.lan"),
            ("homepage.name", "Radarr"),
            ("homepage.group", "Media")), "radarr", homepageFallback: false);

        parsed.ShouldNotBeNull();
        parsed.Name.ShouldBe("radarr");
        parsed.Category.ShouldBeNull();
    }

    [Fact]
    public void Defaults_NameToContainerName_AndCategoryToUncategorized()
    {
        var parsed = LabelParser.Parse(Labels(
            ("coxdev.bookmark.enabled", "true"),
            ("coxdev.bookmark.url", "https://whoami.lan")), "whoami", homepageFallback: true);

        parsed.ShouldNotBeNull();
        parsed.Name.ShouldBe("whoami");
        parsed.Category.ShouldBeNull();
        parsed.Icon.ShouldBeNull();
    }

    [Fact]
    public void Enabled_WithNoUrl_IsNotABookmark()
    {
        var parsed = LabelParser.Parse(
            Labels(("coxdev.bookmark.enabled", "true"), ("coxdev.bookmark.name", "Ghost")), "ghost", homepageFallback: true);

        parsed.ShouldBeNull();
    }

    [Theory]
    [InlineData("TRUE", true)]
    [InlineData(" true ", true)]
    [InlineData("False", false)]
    public void EnabledValue_IgnoresCaseAndWhitespace(string value, bool expected)
    {
        var parsed = LabelParser.Parse(
            Labels(("coxdev.bookmark.enabled", value), ("coxdev.bookmark.url", "https://x.lan")), "x", homepageFallback: true);

        (parsed is not null).ShouldBe(expected);
    }

    [Fact]
    public void UnparseableEnabled_IsTreatedAsMissing()
    {
        var withHref = LabelParser.Parse(
            Labels(("coxdev.bookmark.enabled", "yes"), ("homepage.href", "https://x.lan")), "x", homepageFallback: true);
        var withoutHref = LabelParser.Parse(
            Labels(("coxdev.bookmark.enabled", "yes"), ("coxdev.bookmark.url", "https://x.lan")), "x", homepageFallback: true);

        withHref.ShouldNotBeNull();
        withoutHref.ShouldBeNull();
    }

    [Fact]
    public void BlankLabels_AreTreatedAsMissing()
    {
        var parsed = LabelParser.Parse(Labels(
            ("coxdev.bookmark.enabled", "true"),
            ("coxdev.bookmark.name", "  "),
            ("coxdev.bookmark.url", ""),
            ("homepage.name", "Fallback"),
            ("homepage.href", "https://fallback.lan")), "c", homepageFallback: true);

        parsed.ShouldNotBeNull();
        parsed.Name.ShouldBe("Fallback");
        parsed.Url.ShouldBe("https://fallback.lan");
    }

    [Fact]
    public void Tags_AreSplitAndNormalized()
    {
        var parsed = LabelParser.Parse(Labels(
            ("coxdev.bookmark.enabled", "true"),
            ("coxdev.bookmark.url", "https://x.lan"),
            ("coxdev.bookmark.tags", " media, arr,,#tv, Media ")), "x", homepageFallback: true);

        parsed!.Tags.ShouldBe(["media", "arr", "tv"]);
    }

    [Fact]
    public void OverlongValues_AreCut_NotRejected()
    {
        var parsed = LabelParser.Parse(Labels(
            ("coxdev.bookmark.enabled", "true"),
            ("coxdev.bookmark.url", "https://x.lan"),
            ("coxdev.bookmark.name", new string('n', 300)),
            ("coxdev.bookmark.category", new string('c', 150)),
            ("coxdev.bookmark.tags", new string('t', 80))), "x", homepageFallback: true);

        parsed.ShouldNotBeNull();
        parsed.Name.Length.ShouldBe(200);
        parsed.Category!.Length.ShouldBe(100);
        parsed.Tags.ShouldHaveSingleItem().Length.ShouldBe(50);
    }

    [Fact]
    public void NoLabels_IsNotABookmark() =>
        LabelParser.Parse(new Dictionary<string, string>(), "x", homepageFallback: true).ShouldBeNull();
}
