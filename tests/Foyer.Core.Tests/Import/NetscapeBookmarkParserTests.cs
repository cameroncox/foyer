using Foyer.Core.Entities;
using Foyer.Core.Import;

namespace Foyer.Core.Tests.Import;

public sealed class NetscapeBookmarkParserTests
{
    private static IEnumerable<ImportFolder> All(ImportFolder folder) => [folder, .. folder.Children.SelectMany(All)];

    [Fact]
    public void Chrome_RootFoldersBecomeSections()
    {
        var export = NetscapeBookmarkParser.Parse(Fixtures.Chrome);

        export.Sections.Select(s => s.Name).ShouldBe(["Bookmarks bar", "Other bookmarks"]);
    }

    [Fact]
    public void Chrome_BookmarksDirectlyInARoot_AreLoose()
    {
        var bar = NetscapeBookmarkParser.Parse(Fixtures.Chrome).Sections[0];

        bar.Loose.Name.ShouldBe("Not in a folder");
        bar.Loose.Bookmarks.Select(b => b.Name).ShouldBe(["Hacker News"]);
    }

    [Fact]
    public void Chrome_NestedFolders_HoldOnlyTheirOwnBookmarks()
    {
        var homelab = NetscapeBookmarkParser.Parse(Fixtures.Chrome).Sections[0].Folders[0];

        homelab.Name.ShouldBe("Homelab");
        homelab.Bookmarks.Select(b => b.Name).ShouldBe(["Proxmox", "TrueNAS"]);
        var network = homelab.Children.ShouldHaveSingleItem();
        network.Name.ShouldBe("Network");
        network.Bookmarks.Select(b => b.Name).ShouldBe(["OPNsense", "Switch & AP"]);
    }

    [Fact]
    public void NonHttpLinks_AreDropped()
    {
        var media = NetscapeBookmarkParser.Parse(Fixtures.Chrome).Sections[0].Folders[1];

        media.Bookmarks.Select(b => b.Name).ShouldBe(["Jellyfin"]);
    }

    [Fact]
    public void FolderIds_AreUniqueAndStable()
    {
        var first = NetscapeBookmarkParser.Parse(Fixtures.Chrome);
        var second = NetscapeBookmarkParser.Parse(Fixtures.Chrome);

        var ids = first.Sections.SelectMany(s => All(s.Loose).Concat(s.Folders.SelectMany(All))).Select(f => f.Id).ToList();
        ids.ShouldBeUnique();
        ids.ShouldBe(second.Sections.SelectMany(s => All(s.Loose).Concat(s.Folders.SelectMany(All))).Select(f => f.Id));
    }

    [Fact]
    public void Firefox_MenuItemsAtTheTop_FormTheMenuSection()
    {
        var export = NetscapeBookmarkParser.Parse(Fixtures.Firefox);

        export.Sections.Select(s => s.Name).ShouldBe(["Bookmarks Menu", "Bookmarks Toolbar", "Other Bookmarks"]);
        var menu = export.Sections[0];
        menu.Loose.Bookmarks.Select(b => b.Name).ShouldBe(["Firefox"]);
        menu.Folders.ShouldHaveSingleItem().Name.ShouldBe("Mozilla Firefox");
    }

    [Fact]
    public void Firefox_KeepsTagsAndDataUriIcons()
    {
        var export = NetscapeBookmarkParser.Parse(Fixtures.Firefox);

        export.Sections[1].Folders[0].Bookmarks.ShouldHaveSingleItem().Tags.ShouldBe(["arr", "tv"]);
        export.Sections[0].Loose.Bookmarks[0].Icon.ShouldBe("data:image/png;base64,iVBORw0KGgo=");
    }

    [Fact]
    public void EmptyTitle_FallsBackToHost()
    {
        var export = NetscapeBookmarkParser.Parse("""<DL><p><DT><A HREF="https://grafana.lan/d/x"></A></DL>""");

        export.Sections.ShouldHaveSingleItem().Loose.Bookmarks.ShouldHaveSingleItem().Name.ShouldBe("grafana.lan");
    }

    [Fact]
    public void Garbage_GivesNoSections() =>
        NetscapeBookmarkParser.Parse("not a bookmark file").Sections.ShouldBeEmpty();
}
