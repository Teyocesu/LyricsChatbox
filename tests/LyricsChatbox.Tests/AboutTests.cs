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
    public void ApplicationOutputContainsThirdPartyNotices()
    {
        var applicationDirectory = Path.GetDirectoryName(typeof(ProductIdentity).Assembly.Location)!;
        Assert.True(ExternalLinks.TryResolveExistingNotice(applicationDirectory, out var path));
        Assert.Contains("# Third-party components", File.ReadAllText(path));
    }

    [Fact]
    public void AboutArtworkAndBrandMarksAreBundledAsWpfResources()
    {
        var assembly = typeof(ProductIdentity).Assembly;
        var resourceName = assembly.GetManifestResourceNames().Single(name => name.EndsWith(".g.resources", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var resources = new System.Resources.ResourceReader(stream);
        var keys = resources.Cast<System.Collections.DictionaryEntry>()
            .Select(entry => (string)entry.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("assets/about/aboutheronote.png", keys);
        Assert.Contains("assets/about/aboutherobackdrop.png", keys);
        Assert.Contains("assets/about/aboutoscrouting.png", keys);
        Assert.Contains("assets/brand/discord-symbol-blurple.png", keys);
    }

    [Theory]
    [InlineData(999.9, true)]
    [InlineData(1000, false)]
    [InlineData(1200, false)]
    public void AboutLayoutSwitchesAtUsableContentWidth(double width, bool expectedCompact) =>
        Assert.Equal(expectedCompact, WindowLayout.IsAboutCompact(width));

    [Theory]
    [InlineData(1060, true)]
    [InlineData(1080, false)]
    [InlineData(1448 - 240, false)]
    public void AboutViewportSwitchesAtUsableWidth(double viewport, bool expectedCompact) =>
        Assert.Equal(expectedCompact, WindowLayout.IsAboutCompact(WindowLayout.AboutUsableWidth(viewport)));

    [Fact]
    public void UpdatesReflowsBeforeTheAboutCascade()
    {
        Assert.Equal(WindowLayout.AboutUpdatesMode.Compact, WindowLayout.SelectAboutUpdatesMode(559.9));
        Assert.Equal(WindowLayout.AboutUpdatesMode.CompactColumns, WindowLayout.SelectAboutUpdatesMode(560));
        Assert.Equal(WindowLayout.AboutUpdatesMode.CompactColumns, WindowLayout.SelectAboutUpdatesMode(999.9));
        Assert.Equal(WindowLayout.AboutUpdatesMode.Medium, WindowLayout.SelectAboutUpdatesMode(1000));
        Assert.Equal(WindowLayout.AboutUpdatesMode.Medium, WindowLayout.SelectAboutUpdatesMode(1119.9));
        Assert.Equal(WindowLayout.AboutUpdatesMode.Wide, WindowLayout.SelectAboutUpdatesMode(1120));
    }

    [Fact]
    public void UpdatesUsesTwoColumnsFromCascadeThroughMediumWidths()
    {
        Assert.False(WindowLayout.UsesAboutUpdatesTwoColumnLayout(WindowLayout.SelectAboutUpdatesMode(559.9)));
        Assert.True(WindowLayout.UsesAboutUpdatesTwoColumnLayout(WindowLayout.SelectAboutUpdatesMode(560)));
        Assert.True(WindowLayout.UsesAboutUpdatesTwoColumnLayout(WindowLayout.SelectAboutUpdatesMode(999.9)));
        Assert.True(WindowLayout.UsesAboutUpdatesTwoColumnLayout(WindowLayout.SelectAboutUpdatesMode(1000)));
        Assert.True(WindowLayout.UsesAboutUpdatesTwoColumnLayout(WindowLayout.SelectAboutUpdatesMode(1119.9)));
        Assert.False(WindowLayout.UsesAboutUpdatesTwoColumnLayout(WindowLayout.SelectAboutUpdatesMode(1120)));
    }

    [Fact]
    public void AboutScrollReachesOverflowWithoutHorizontalMovement() => RunOnSta(() =>
    {
        var scroll = new System.Windows.Controls.ScrollViewer
        {
            Content = new System.Windows.Controls.Border { Width = 600, Height = 800 },
            CanContentScroll = false
        };
        WindowLayout.ConfigureAboutScroll(scroll);
        scroll.Measure(new System.Windows.Size(300, 200));
        scroll.Arrange(new System.Windows.Rect(0, 0, 300, 200));
        scroll.UpdateLayout();

        Assert.Equal(System.Windows.Controls.ScrollBarVisibility.Auto, scroll.VerticalScrollBarVisibility);
        Assert.Equal(System.Windows.Controls.ScrollBarVisibility.Disabled, scroll.HorizontalScrollBarVisibility);
        Assert.True(scroll.ScrollableHeight > 0);
        Assert.Equal(System.Windows.Visibility.Visible, scroll.ComputedVerticalScrollBarVisibility);
        scroll.ScrollToVerticalOffset(scroll.ScrollableHeight);
        scroll.ScrollToHorizontalOffset(100);
        scroll.UpdateLayout();
        Assert.Equal(scroll.ScrollableHeight, scroll.VerticalOffset);
        Assert.Equal(0, scroll.HorizontalOffset);
    });

    [Fact]
    public void AboutAutoScrollHidesBarWhenContentFits() => RunOnSta(() =>
    {
        var scroll = new System.Windows.Controls.ScrollViewer
        {
            Content = new System.Windows.Controls.Border { Width = 200, Height = 100 },
            CanContentScroll = false
        };
        WindowLayout.ConfigureAboutScroll(scroll);
        scroll.Measure(new System.Windows.Size(300, 200));
        scroll.Arrange(new System.Windows.Rect(0, 0, 300, 200));
        scroll.UpdateLayout();

        Assert.Equal(0, scroll.ScrollableHeight);
        Assert.Equal(System.Windows.Visibility.Collapsed, scroll.ComputedVerticalScrollBarVisibility);
        Assert.Equal(System.Windows.Controls.ScrollBarVisibility.Disabled, scroll.HorizontalScrollBarVisibility);
    });

    [Fact]
    public void AboutSectionsReuseControlsAcrossRepeatedWidthChanges() => RunOnSta(() =>
    {
        var overview = new System.Windows.Controls.StackPanel();
        var community = new System.Windows.Controls.StackPanel();
        var project = new System.Windows.Controls.StackPanel();
        var privacy = new System.Windows.Controls.StackPanel();
        var left = new System.Windows.Controls.StackPanel();
        var right = new System.Windows.Controls.StackPanel();
        var cascade = new System.Windows.Controls.StackPanel();
        left.Children.Add(overview);
        left.Children.Add(project);
        right.Children.Add(community);
        right.Children.Add(privacy);

        for (var i = 0; i < 2; i++)
        {
            Assert.True(WindowLayout.ReparentAboutSections(true, overview, community, project, privacy, left, right, cascade));
            Assert.Equal(new[] { overview, community, project, privacy }, cascade.Children.Cast<System.Windows.Controls.StackPanel>());
            Assert.Equal(4, left.Children.Count + right.Children.Count + cascade.Children.Count);
            Assert.False(WindowLayout.ReparentAboutSections(true, overview, community, project, privacy, left, right, cascade));

            Assert.True(WindowLayout.ReparentAboutSections(false, overview, community, project, privacy, left, right, cascade));
            Assert.Equal(new[] { overview, project }, left.Children.Cast<System.Windows.Controls.StackPanel>());
            Assert.Equal(new[] { community, privacy }, right.Children.Cast<System.Windows.Controls.StackPanel>());
            Assert.Equal(4, left.Children.Count + right.Children.Count + cascade.Children.Count);
        }
    });

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(5)));
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
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
