using Foyer.Core.Exceptions;
using Foyer.Core.Sync;

namespace Foyer.Core.Tests.Sync;

public sealed class SyncOptionsTests
{
    private static SyncOptions Parse(params (string Key, string? Value)[] settings) =>
        SyncOptions.Parse(settings.Select(s => KeyValuePair.Create(s.Key, s.Value)));

    [Fact]
    public void Defaults_MatchTheSpec()
    {
        var options = Parse();

        options.HomepageLabels.ShouldBeTrue();
        options.ResyncInterval.ShouldBe(TimeSpan.FromSeconds(300));
        options.PollInterval.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void ReadsOverrides()
    {
        var options = Parse(
            ("FOYER_HOMEPAGE_LABELS", "false"),
            ("foyer_resync_interval", "600"),
            ("FOYER_POLL_INTERVAL", " 10 "));

        options.HomepageLabels.ShouldBeFalse();
        options.ResyncInterval.ShouldBe(TimeSpan.FromMinutes(10));
        options.PollInterval.ShouldBe(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void BlankValues_KeepDefaults() =>
        Parse(("FOYER_POLL_INTERVAL", "")).PollInterval.ShouldBe(TimeSpan.FromSeconds(30));

    [Fact]
    public void BadValues_AreAllReported()
    {
        var ex = Should.Throw<FoyerConfigurationException>(() => Parse(
            ("FOYER_HOMEPAGE_LABELS", "yes"),
            ("FOYER_RESYNC_INTERVAL", "0"),
            ("FOYER_POLL_INTERVAL", "5m")));

        ex.Problems.Count.ShouldBe(3);
    }
}
