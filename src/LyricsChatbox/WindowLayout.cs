using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Shapes;

namespace LyricsChatbox;

internal static class WindowLayout
{
    // Keep a useful page viewport when high scaling leaves little vertical work area.
    // An ambiguous source selection keeps its status row so the guidance stays visible.
    public static void Apply(FrameworkElement scope, double contentHeight, bool showSourceStatus = false)
    {
        var shortWindow = contentHeight < 540;
        T Find<T>(string name) => (T)scope.FindName(name);
        Find<Border>("Sidebar").Padding = shortWindow ? new(12, 16, 12, 12) : new(20, 28, 20, 16);
        Find<StackPanel>("BrandPanel").Margin = shortWindow ? new(0, 0, 0, 16) : new(0, 6, 0, 30);
        Find<Image>("BrandIcon").Width = Find<Image>("BrandIcon").Height = shortWindow ? 38 : 52;
        Find<TextBlock>("VersionCaption").Visibility = shortWindow ? Visibility.Collapsed : Visibility.Visible;
        foreach (var item in Find<StackPanel>("NavigationPanel").Children.OfType<RadioButton>())
            item.Padding = shortWindow ? new(12, 8, 12, 8) : new(16, 11, 16, 11);
        // Decoration hides first at cramped heights; Output text and controls stay accessible.
        Find<Grid>("OutputVisualizer").Visibility = shortWindow ? Visibility.Collapsed : Visibility.Visible;
        Find<Grid>("ContentPanel").Margin = shortWindow ? new(16, 38, 16, 10) : new(28, 48, 28, 16);
        Find<StackPanel>("HeadingPanel").Margin = new(0, 0, 0, shortWindow ? 8 : 16);
        var home = Find<ScrollViewer>("HomePage").Visibility == Visibility.Visible;
        var about = Find<Grid>("AboutPage").Visibility == Visibility.Visible;
        Find<ContentControl>("PersistentPreviewHost").Visibility = about ? Visibility.Collapsed : Visibility.Visible;
        ApplyAboutLayout(scope, Find<Grid>("ContentPanel").ActualWidth, contentHeight);
        var preview = Find<Border>("PreviewCard");
        var destination = Find<ContentControl>(home ? "HomePreviewHost" : "PersistentPreviewHost");
        if (preview.Parent != destination)
        {
            ((ContentControl)preview.Parent).Content = null;
            destination.Content = preview;
        }
        preview.Padding = shortWindow && !home ? new(10, 6, 10, 6) : new(18);
        preview.Margin = home ? new(0) : new(0, shortWindow ? 8 : 16, 0, 0);
        Find<DockPanel>("PreviewHeading").Visibility = shortWindow && !home ? Visibility.Collapsed : Visibility.Visible;
        Find<ScrollViewer>("PreviewScroller").MaxHeight = home ? 130 : shortWindow ? 24 : 72;
        Find<TextBlock>("PreviewProfileText").Visibility = shortWindow && !home ? Visibility.Collapsed : Visibility.Visible;
        Find<DockPanel>("PreviewHeading").Margin = new(0, 0, 0, home ? 12 : 6);
        Find<TextBlock>("PreviewProfileText").Margin = new(0, 0, 0, home ? 12 : 6);
        Find<Border>("PreviewFrame").Padding = home ? new(12, 8, 12, 8) : new(8, 2, 8, 2);
        Find<Border>("PreviewFrame").Margin = new(0, 0, 0, home ? 12 : 6);
        Find<StackPanel>("PreviewStatus").Orientation = home ? Orientation.Vertical : Orientation.Horizontal;
        Find<TextBlock>("OscText").Margin = home ? new(0, 4, 0, 0) : new(12, 0, 0, 0);
        var compactDashboard = home && (contentHeight < 760 || Find<Grid>("ContentPanel").ActualWidth < 900);
        var tightHome = compactDashboard && shortWindow;
        Find<TextBlock>("ArtistText").FontSize = tightHome ? 12 : 15;
        Find<TextBlock>("ArtistText").Margin = new(0, tightHome ? 2 : 5, 0, 0);
        var transport = (StackPanel)Find<ProgressBar>("PositionProgress").Parent;
        transport.Margin = new(0, tightHome ? 5 : 8, 0, 0);
        foreach (var name in new[] { "ShuffleButton", "PreviousButton", "PlayPauseButton", "NextButton", "RepeatButton" })
        {
            var button = Find<Button>(name);
            button.Width = button.Height = tightHome ? 28 : name == "PlayPauseButton" ? 46 : 36;
            button.MinHeight = tightHome ? 28 : 36;
        }
        Find<TextBlock>("PreviewText").FontSize = tightHome ? 16 : 20;
        if (tightHome)
        {
            preview.Padding = new(10, 8, 10, 8);
            Find<DockPanel>("PreviewHeading").Visibility = Visibility.Collapsed;
            Find<TextBlock>("PreviewProfileText").Visibility = Visibility.Collapsed;
            Find<Border>("PreviewFrame").Padding = new(8, 2, 8, 2);
            Find<Border>("PreviewFrame").Margin = new(0, 0, 0, 4);
            Find<StackPanel>("PreviewStatus").Orientation = Orientation.Horizontal;
            Find<TextBlock>("OscText").Margin = new(12, 0, 0, 0);
        }
        Find<Border>("HomeFit").Visibility = compactDashboard ? Visibility.Collapsed : Visibility.Visible;
        Find<Grid>("HomeCompact").Visibility = compactDashboard ? Visibility.Visible : Visibility.Collapsed;
        Find<StackPanel>("HeadingPanel").Visibility = about || compactDashboard && shortWindow ? Visibility.Collapsed : Visibility.Visible;
        Find<DockPanel>("HeroStatusRow").Visibility = compactDashboard && !showSourceStatus ? Visibility.Collapsed : Visibility.Visible;
        Find<Border>("HeroProviderBadge").Visibility = compactDashboard ? Visibility.Collapsed : Visibility.Visible;
        Find<TextBlock>("PositionText").Visibility = compactDashboard ? Visibility.Collapsed : Visibility.Visible;
        void Move(FrameworkElement item, FrameworkElement target, bool first = false)
        {
            if (item.Parent == target) return;
            if (item.Parent is Panel panel) panel.Children.Remove(item);
            else if (item.Parent is ContentControl owner) owner.Content = null;
            if (target is Panel destinationPanel)
            {
                if (first) destinationPanel.Children.Insert(0, item); else destinationPanel.Children.Add(item);
            }
            else ((ContentControl)target).Content = item;
        }
        var dashboard = Find<Grid>("HomeDashboard");
        var sectionHost = Find<ContentControl>("CompactSectionHost");
        var sections = new[] { "HomePreviewHost", "ProfilesCard", "ContextCard", "InspectorCard", "QuickMessagesCard" };
        foreach (var name in sections)
        {
            var target = Find<Grid>(name is "ProfilesCard" or "ContextCard" ? "HomePreferencesGrid" : "HomeDetailsGrid");
            Move(Find<FrameworkElement>(name), target);
        }
        Move(Find<Border>("NowPlayingCard"), compactDashboard ? Find<ContentControl>("CompactHeroHost") : dashboard, true);
        var selectedSection = Find<ComboBox>("HomeSectionBox").SelectedIndex;
        var toolsHost = Find<StackPanel>("CompactToolsHost");
        Move(Find<Border>("HomeToolsCard"), compactDashboard ? toolsHost : dashboard);
        foreach (var name in new[] { "RecoveryCard", "MatchCard" })
            Move(Find<Border>(name), compactDashboard ? toolsHost : Find<StackPanel>("HomeDetailsStack"));
        toolsHost.Visibility = compactDashboard && selectedSection == 5 ? Visibility.Visible : Visibility.Collapsed;
        sectionHost.Visibility = selectedSection == 5 ? Visibility.Collapsed : Visibility.Visible;
        if (compactDashboard && selectedSection >= 0 && selectedSection < sections.Length)
            Move(Find<FrameworkElement>(sections[selectedSection]), sectionHost);
        // The ordinary dashboard fits at native scale; compact work areas use individual readable cards.
        var availableWidth = Find<Grid>("ContentPanel").ActualWidth;
        var width = availableWidth;
        var narrow = compactDashboard || width < 800;
        void Place(string name, int column, int row, int span, Thickness margin)
        {
            var item = Find<FrameworkElement>(name);
            Grid.SetColumn(item, column); Grid.SetRow(item, row); Grid.SetColumnSpan(item, span); item.Margin = margin;
        }
        Place("ProfilesCard", 0, 0, narrow ? 2 : 1, new(0, 0, narrow ? 0 : 12, narrow ? 12 : 0));
        Place("ContextCard", narrow ? 0 : 1, narrow ? 1 : 0, narrow ? 2 : 1, new(0));
        Place("HomePreviewHost", 0, 0, narrow ? 3 : 1, new(0, 0, narrow ? 0 : 12, narrow ? 12 : 0));
        Place("InspectorCard", narrow ? 0 : 1, narrow ? 1 : 0, narrow ? 3 : 1, new(0, 0, narrow ? 0 : 12, narrow ? 12 : 0));
        Place("QuickMessagesCard", narrow ? 0 : 2, narrow ? 2 : 0, narrow ? 3 : 1, new(0));
        var compactHero = !compactDashboard && width < 900;
        Place("TrackMetadata", 0, 0, compactHero ? 2 : 1, new(0, 0, compactHero ? 0 : 20, 0));
        Place("HeroLyricPanel", compactHero ? 0 : 1, compactHero ? 1 : 0, compactHero ? 2 : 1, new(0, compactHero ? 16 : 0, 0, 0));
        Find<Border>("HeroLyricPanel").BorderThickness = new(0);
        Find<Border>("HeroLyricPanel").Padding = compactHero ? new(0) : new(22, 4, 0, 4);
        Find<ColumnDefinition>("ArtworkColumn").Width = new(narrow ? 128 : 194);
        Find<Border>("ArtworkFrame").Width = Find<Border>("ArtworkFrame").Height = narrow ? 110 : 172;
        Find<TextBlock>("TrackText").FontSize = tightHome ? 16 : compactDashboard ? 20 : narrow ? 24 : 28;
        Find<Border>("HeroLyricPanel").Visibility = compactDashboard ? Visibility.Collapsed : Visibility.Visible;
        Find<TextBlock>("AlbumText").Visibility = compactDashboard ? Visibility.Collapsed : Visibility.Visible;
        Find<Grid>("HeroGrid").Margin = tightHome ? new(8) : compactDashboard ? new(12) : new(14);
        if (compactDashboard)
        {
            Grid.SetColumnSpan(Find<StackPanel>("TrackMetadata"), 2);
            Find<Border>("ArtworkFrame").Width = Find<Border>("ArtworkFrame").Height = 72;
            Find<ColumnDefinition>("ArtworkColumn").Width = new(84);
            if (sectionHost.Content is FrameworkElement selected) selected.Margin = new(0);
        }
    }

    private static void ApplyAboutLayout(FrameworkElement scope, double contentWidth, double contentHeight)
    {
        T Find<T>(string name) => (T)scope.FindName(name);
        var wide = contentWidth >= 1040;
        var compact = contentWidth < 900 || contentHeight < 820;
        var micro = contentWidth < 660 && contentHeight < 700;
        var shortWide = wide && contentHeight < 1060;
        var shortMedium = !wide && contentWidth >= 900 && contentHeight < 960;
        if (micro) Find<Grid>("ContentPanel").Margin = new(16, 48, 16, 4);
        var eyebrow = Find<Grid>("AboutEyebrow");
        eyebrow.Height = micro ? 18 : 22;
        eyebrow.Margin = new(0, 0, 0, micro ? 4 : 8);
        var sections = Find<Grid>("AboutSections");
        sections.ColumnDefinitions[0].Width = new(1, GridUnitType.Star);
        sections.ColumnDefinitions[1].Width = new(micro ? 8 : wide ? 48 : 12);
        sections.ColumnDefinitions[2].Width = new(1, GridUnitType.Star);
        Find<StackPanel>("AboutProject").Margin = new(0, micro ? 4 : compact ? 8 : shortMedium ? 10 : shortWide ? 14 : 22, 0, 0);
        Find<StackPanel>("AboutPrivacy").Margin = new(0, micro ? 4 : compact ? 8 : shortMedium ? 10 : shortWide ? 14 : 22, 0, 0);
        Find<StackPanel>("AboutUpdates").Margin = new(0, micro ? 0 : compact ? 4 : shortMedium ? 6 : shortWide ? 4 : 16, 0, 0);

        var hero = Find<Grid>("AboutHero");
        hero.MinHeight = !compact && contentHeight >= 820 && wide ? shortWide ? 230 : 250 : 0;
        hero.Margin = new(0, 0, 0, micro ? 2 : compact ? 4 : shortMedium ? 6 : shortWide ? 4 : 14);
        Find<Grid>("AboutHeroContent").ColumnDefinitions[0].Width = new(micro ? 72 : 112);
        var note = Find<Viewbox>("AboutNoteMark");
        note.Width = micro ? 52 : 96;
        note.Height = micro ? 60 : 110;
        Find<StackPanel>("AboutHeroCopy").Margin = new(0, micro || compact ? 0 : shortMedium ? 4 : shortWide ? 6 : wide ? 18 : 8, micro ? 2 : compact ? 4 : shortMedium ? 12 : 18, 0);
        Find<TextBlock>("AboutHeroTitle").FontSize = micro ? 24 : compact ? 30 : shortMedium ? 42 : wide ? 66 : 44;
        Find<TextBlock>("AboutHeroDescription").FontSize = micro ? 11.5 : compact ? 12.5 : shortMedium ? 14 : wide ? 18 : 15;
        Find<TextBlock>("AboutHeroDescription").MaxWidth = wide ? 660 : micro ? 510 : 560;
        Find<TextBlock>("AboutHeroDescription").Margin = new(0, micro ? 1 : compact ? 4 : shortMedium ? 4 : shortWide ? 4 : wide ? 10 : 6, 0, 0);
        Find<TextBlock>("AboutOriginText").FontSize = micro ? 10.5 : compact ? 11.5 : shortMedium ? 12 : wide ? 15 : 13;
        Find<TextBlock>("AboutOriginText").MaxWidth = wide ? 720 : micro ? 510 : 560;
        Find<TextBlock>("AboutOriginText").Margin = new(0, micro ? 1 : compact ? 5 : shortMedium ? 6 : shortWide ? 6 : wide ? 20 : 10, 0, 0);

        var sectionWidth = Math.Max(0, (contentWidth - sections.ColumnDefinitions[1].Width.Value) / 2);
        var iconWidth = micro ? 22 : sectionWidth < 350 ? 30 : sectionWidth < 460 ? 40 : 48;
        var labelWidth = micro ? 88 : sectionWidth < 350 ? 94 : sectionWidth < 460 ? 130 : 172;
        foreach (var sectionName in new[] { "AboutOverview", "AboutProject", "AboutCommunity", "AboutPrivacy" })
        {
            var section = Find<StackPanel>(sectionName);
            foreach (var row in section.Children.OfType<Grid>().Where(row => row.ColumnDefinitions.Count >= 3))
            {
                row.MinHeight = micro ? 18 : compact ? 24 : shortMedium ? 30 : wide ? shortWide ? 36 : 42 : 34;
                row.ColumnDefinitions[0].Width = new(iconWidth);
                row.ColumnDefinitions[1].Width = new(labelWidth);
                if (row.ColumnDefinitions.Count == 4) row.ColumnDefinitions[3].Width = new(micro ? 16 : compact ? 20 : 24);
                foreach (var label in row.Children.OfType<TextBlock>().Where(text => Grid.GetColumn(text) == 1))
                {
                    label.Margin = new(micro ? 2 : compact ? 4 : 8, 0, micro ? 2 : 4, 0);
                    label.FontSize = micro ? 10.5 : compact ? 11.5 : shortMedium ? 12 : 13;
                }
                foreach (var value in row.Children.OfType<TextBlock>().Where(text => Grid.GetColumn(text) == 2))
                {
                    value.FontSize = micro ? 11 : compact ? 12 : shortMedium ? 13 : 14;
                    value.Margin = micro ? new(0, 1, 0, 1) : compact ? new(0, 2, 0, 2) : new(0);
                }
                foreach (var textIcon in row.Children.OfType<TextBlock>().Where(text => Grid.GetColumn(text) == 0))
                    textIcon.FontSize = micro ? 15 : compact ? 17 : shortMedium ? 19 : 20;
                foreach (var button in row.Children.OfType<Button>())
                {
                    button.MinHeight = micro ? 20 : compact ? 24 : shortMedium ? 30 : 34;
                    button.Padding = micro ? new(0) : compact ? new(1, 1, 1, 1) : new(4, 4, 4, 4);
                }
            }
        }

        var diagnosticsCopyButton = Find<Button>("DiagnosticsCopyButton");
        diagnosticsCopyButton.MinHeight = micro ? 20 : compact ? 24 : shortMedium ? 30 : 34;
        diagnosticsCopyButton.Padding = micro ? new(0) : compact ? new(1, 1, 1, 1) : new(4, 4, 4, 4);
        if (diagnosticsCopyButton.Content is Grid diagnosticsActionGrid)
        {
            var actionText = diagnosticsActionGrid.Children.OfType<TextBlock>().FirstOrDefault();
            if (actionText is not null) actionText.Text = micro ? "Copy info" : "Generate diagnostic info";
        }

        var headingSize = micro ? 11 : compact ? 11.5 : shortMedium ? 12 : 13;
        foreach (var label in new[] { "Overview", "Community and Contact", "Project and Tools", "Privacy and Data", "Updates" })
        {
            if (scope.FindName(label) is TextBlock sectionLabel) sectionLabel.FontSize = headingSize;
        }
        foreach (var ruleName in new[] { "AboutOverviewRule", "AboutProjectRule", "AboutCommunityRule", "AboutPrivacyRule", "AboutUpdatesRule" })
        {
            var rule = Find<Border>(ruleName);
            rule.Height = micro ? 1 : 2;
            rule.Margin = micro ? new(0, 1, 0, 1) : shortMedium || shortWide ? new(0, 2, 0, 2) : new(0, 4, 0, 4);
        }
        Find<Image>("VrChatLogoMark").Width = micro ? 24 : compact ? 30 : 44;
        Find<Image>("VrChatLogoMark").Height = micro ? 15 : compact ? 18 : 24;
        Find<Path>("DiscordClydeMark").Width = micro ? 22 : compact ? 26 : 32;
        Find<Path>("DiscordClydeMark").Height = micro ? 17 : compact ? 20 : 24;

        var summary = Find<Grid>("UpdateSummary");
        var columns = summary.ColumnDefinitions;
        var icon = Find<TextBlock>("UpdateSummaryIcon");
        var status = Find<StackPanel>("UpdateStatusGroup");
        var current = Find<StackPanel>("UpdateCurrentGroup");
        var checkedGroup = Find<StackPanel>("UpdateLastCheckedGroup");
        var action = Find<StackPanel>("UpdateActionGroup");
        var check = Find<Button>("CheckUpdatesButton");
        if (wide)
        {
            columns[0].Width = new(42);
            columns[1].Width = new(2, GridUnitType.Star);
            columns[2].Width = GridLength.Auto;
            columns[3].Width = new(116);
            columns[4].Width = GridLength.Auto;
            columns[5].Width = new(1.5, GridUnitType.Star);
            columns[6].Width = GridLength.Auto;
            Grid.SetColumn(icon, 0); Grid.SetRow(icon, 0); Grid.SetRowSpan(icon, 3);
            Grid.SetColumn(status, 1); Grid.SetRow(status, 0); Grid.SetColumnSpan(status, 1);
            Grid.SetColumn(current, 3); Grid.SetRow(current, 0); Grid.SetColumnSpan(current, 1);
            Grid.SetColumn(checkedGroup, 5); Grid.SetRow(checkedGroup, 0); Grid.SetColumnSpan(checkedGroup, 1);
            Grid.SetColumn(action, 6); Grid.SetRow(action, 0); Grid.SetRowSpan(action, 3);
            Find<Border>("UpdateCurrentDivider").Visibility = Visibility.Visible;
            Find<Border>("UpdateLastCheckedDivider").Visibility = Visibility.Visible;
            summary.MinHeight = shortWide ? 58 : 62;
            summary.Margin = new(0, shortWide ? 0 : 4, 0, 0);
            status.Margin = new(6, 0, 14, 0);
            current.Margin = new(10, 0, 10, 0);
            checkedGroup.Margin = new(10, 0, 10, 0);
            action.Margin = new(12, 0, 0, 0);
        }
        else
        {
            columns[0].Width = new(32);
            columns[1].Width = new(1, GridUnitType.Star);
            columns[2].Width = GridLength.Auto;
            for (var i = 3; i < columns.Count; i++) columns[i].Width = new(0);
            Grid.SetColumn(icon, 0); Grid.SetRow(icon, 0); Grid.SetRowSpan(icon, 3);
            Grid.SetColumn(status, 1); Grid.SetRow(status, 0); Grid.SetColumnSpan(status, 2);
            Grid.SetColumn(current, 2); Grid.SetRow(current, 0); Grid.SetColumnSpan(current, 1);
            Grid.SetColumn(checkedGroup, 1); Grid.SetRow(checkedGroup, 1); Grid.SetColumnSpan(checkedGroup, 1);
            Grid.SetColumn(action, 2); Grid.SetRow(action, 1); Grid.SetRowSpan(action, 1);
            Find<Border>("UpdateCurrentDivider").Visibility = Visibility.Collapsed;
            Find<Border>("UpdateLastCheckedDivider").Visibility = Visibility.Collapsed;
            summary.MinHeight = micro ? 54 : shortMedium ? 70 : 78;
            summary.Margin = new(0, micro ? 0 : shortMedium ? 2 : 4, 0, 0);
            status.Margin = new(2, 0, 4, 0);
            current.Margin = new(2, 0, 0, 0);
            checkedGroup.Margin = new(2, 2, 4, 0);
            action.Margin = new(0, 0, 0, 0);
        }
        check.MinWidth = micro ? 146 : compact ? 164 : shortMedium ? 172 : 184;
        check.Height = micro ? 28 : double.NaN;
        Find<ToggleButton>("UpdatePreferencesButton").Width = micro ? 26 : compact ? 30 : 34;
        Find<ToggleButton>("UpdatePreferencesButton").Height = micro ? 27 : compact ? 32 : 36;
    }
}
