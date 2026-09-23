namespace LyricsChatbox.Tests;

public sealed class AboutTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "lyricschatbox-about-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    [Fact]
    public void CanonicalAboutIdentities()
    {
        Assert.Equal("https://github.com/Teyocesu", ProductIdentity.AuthorGitHubUrl);
        Assert.Equal("https://github.com/Teyocesu/LyricsChatbox", ProductIdentity.Repository);
        Assert.Equal("https://github.com/Teyocesu/LyricsChatbox/releases", ProductIdentity.ReleasesUrl);
        Assert.Equal("https://github.com/Teyocesu/LyricsChatbox/issues/new", ProductIdentity.ReportIssueUrl);
        Assert.Equal("https://github.com/Teyocesu/LyricsChatbox/blob/main/THIRD_PARTY_NOTICES.md", ProductIdentity.ThirdPartyNoticesUrl);
        Assert.Equal("https://vrchat.com/home/user/usr_5560dde5-e00b-4784-a0ef-c6a6f36130d1", ProductIdentity.VrChatProfileUrl);
        Assert.Equal("teyocesu", ProductIdentity.DiscordUsername);
    }

    [Theory]
    [InlineData("https://github.com/Teyocesu", true)]
    [InlineData("https://github.com/Teyocesu/LyricsChatbox/releases", true)]
    [InlineData("https://github.com/Teyocesu/LyricsChatbox/issues/new", true)]
    [InlineData("https://vrchat.com/home/user/usr_5560dde5-e00b-4784-a0ef-c6a6f36130d1", true)]
    [InlineData("http://github.com/Teyocesu", false)]
    [InlineData("https://example.com/profile", false)]
    [InlineData("https://discord.gg/invite", false)]
    [InlineData("not a uri", false)]
    [InlineData(null, false)]
    public void ExternalLinkAllowlist(string? url, bool expected) =>
        Assert.Equal(expected, ExternalLinks.IsAllowed(url));

    [Fact]
    public void RejectedExternalLinkReportsWithoutLaunching()
    {
        var reported = "";
        Assert.False(ExternalLinks.TryOpen("https://example.com/profile", message => reported = message));
        Assert.NotEmpty(reported);
    }

    [Fact]
    public void BundledNoticeResolvesOnlyToKnownFile()
    {
        var resolved = ExternalLinks.ResolveBundledNoticePath(root);
        Assert.Equal(Path.Combine(root, "THIRD_PARTY_NOTICES.md"), resolved);
        Assert.False(ExternalLinks.TryResolveExistingNotice(root, out _));
        Directory.CreateDirectory(root);
        File.WriteAllText(resolved, "notices");
        Assert.True(ExternalLinks.TryResolveExistingNotice(root, out var existing));
        Assert.Equal(resolved, existing);
    }

    [Fact]
    public void ActiveOutputShowsPauseWithoutResume()
    {
        var state = OutputSidebarPresentation.Describe(true, false, "Active");
        Assert.Equal("Active", state.Primary);
        Assert.True(state.ShowPause); Assert.Equal("Pause", state.PauseContent);
        Assert.False(state.ShowResume); Assert.Equal("", state.Secondary);
    }

    [Fact]
    public void PausedOutputShowsResumeAndChange()
    {
        var state = OutputSidebarPresentation.Describe(true, true, "Paused · Until resumed");
        Assert.Equal("Paused · Until resumed", state.Primary);
        Assert.True(state.ShowPause); Assert.Equal("Change", state.PauseContent);
        Assert.True(state.ShowResume); Assert.Equal("", state.Secondary);
    }

    [Fact]
    public void DisabledOutputHidesActionsWithoutSecondaryHint()
    {
        var state = OutputSidebarPresentation.Describe(false, false, "Active");
        Assert.Equal("Off", state.Primary); Assert.Equal("", state.Secondary);
        Assert.False(state.ShowPause); Assert.False(state.ShowResume);
    }

    [Theory]
    [InlineData(true, false, true, true, true)]
    [InlineData(true, false, true, false, false)]
    [InlineData(true, false, false, true, false)]
    [InlineData(true, true, true, true, false)]
    [InlineData(false, false, true, true, false)]
    [InlineData(false, true, true, true, false)]
    public void OutputSendingRequiresEnabledUnpausedPlayingAutomatic(
        bool enabled, bool paused, bool playing, bool automatic, bool expected) =>
        Assert.Equal(expected, OutputSidebarPresentation.IsSending(enabled, paused, playing, automatic));

    [Theory]
    [InlineData("127.0.0.1", 9000, "To 127.0.0.1:9000")]
    [InlineData("localhost", 9000, "To localhost:9000")]
    [InlineData("192.168.1.10", 9001, "To 192.168.1.10:9001")]
    [InlineData("::1", 9000, "To [::1]:9000")]
    [InlineData("fe80::1", 9000, "To [fe80::1]:9000")]
    public void OutputDestinationFormatsActualDestinationWithoutClaimingDelivery(string host, int port, string expected) =>
        Assert.Equal(expected, OutputSidebarPresentation.FormatDestination(host, port));

    [Fact]
    public void DisabledOutputWithScheduledPauseKeepsSecondaryHint()
    {
        var state = OutputSidebarPresentation.Describe(false, true, "Paused · Until resumed");
        Assert.Equal("Off", state.Primary);
        Assert.Equal(OutputSidebarPresentation.ScheduledHint, state.Secondary);
        Assert.False(state.ShowPause); Assert.False(state.ShowResume);
    }
}
