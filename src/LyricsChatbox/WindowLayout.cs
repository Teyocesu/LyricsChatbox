using System.Windows;
using System.Windows.Controls;

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
        var about = Find<ScrollViewer>("AboutPage").Visibility == Visibility.Visible;
        Find<ContentControl>("PersistentPreviewHost").Visibility = about ? Visibility.Collapsed : Visibility.Visible;
        ApplyAboutLayout(scope, Find<Grid>("ContentPanel").ActualWidth);
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

    private static void ApplyAboutLayout(FrameworkElement scope, double contentWidth)
    {
        T Find<T>(string name) => (T)scope.FindName(name);
        var wide = contentWidth >= 900;
        var sections = Find<Grid>("AboutSections");
        sections.ColumnDefinitions[0].Width = new(1, GridUnitType.Star);
        sections.ColumnDefinitions[1].Width = new(wide ? 48 : 0);
        sections.ColumnDefinitions[2].Width = wide ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        void Place(string name, int row, int column, int span = 1)
        {
            var section = Find<FrameworkElement>(name);
            Grid.SetRow(section, row);
            Grid.SetColumn(section, column);
            Grid.SetColumnSpan(section, span);
        }
        if (wide)
        {
            Place("AboutOverview", 0, 0);
            Place("AboutCommunity", 0, 2);
            Place("AboutProject", 1, 0);
            Place("AboutPrivacy", 1, 2);
            Place("AboutUpdates", 2, 0, 3);
            Find<Border>("AboutCenterDivider").Visibility = Visibility.Visible;
            Find<StackPanel>("AboutCommunity").Margin = new(0);
            Find<StackPanel>("AboutProject").Margin = new(0, 22, 0, 0);
            Find<StackPanel>("AboutPrivacy").Margin = new(0, 22, 0, 0);
            Find<StackPanel>("AboutUpdates").Margin = new(0, 28, 0, 0);
            Find<TextBlock>("AboutHeroTitle").FontSize = 68;
            Find<TextBlock>("AboutHeroNote").FontSize = 96;
            Find<StackPanel>("AboutHeroCopy").Margin = new(0, 18, 18, 0);
            Find<TextBlock>("AboutHeroDescription").MaxWidth = 520;
            Find<TextBlock>("AboutHeroDescription").Margin = new(0, 14, 0, 0);
            Find<TextBlock>("AboutOriginText").Margin = new(0, 28, 0, 0);
        }
        else
        {
            Place("AboutOverview", 0, 0);
            Place("AboutCommunity", 1, 0);
            Place("AboutProject", 2, 0);
            Place("AboutPrivacy", 3, 0);
            Place("AboutUpdates", 4, 0);
            Find<Border>("AboutCenterDivider").Visibility = Visibility.Collapsed;
            Find<StackPanel>("AboutCommunity").Margin = new(0, 28, 0, 0);
            Find<StackPanel>("AboutProject").Margin = new(0, 28, 0, 0);
            Find<StackPanel>("AboutPrivacy").Margin = new(0, 28, 0, 0);
            Find<StackPanel>("AboutUpdates").Margin = new(0, 28, 0, 0);
            Find<TextBlock>("AboutHeroTitle").FontSize = 42;
            Find<TextBlock>("AboutHeroNote").FontSize = 68;
            Find<StackPanel>("AboutHeroCopy").Margin = new(0, 40, 18, 0);
            Find<TextBlock>("AboutHeroDescription").MaxWidth = 620;
            Find<TextBlock>("AboutHeroDescription").Margin = new(0, 2, 0, 0);
            Find<TextBlock>("AboutOriginText").Margin = new(0, 12, 0, 0);
        }
        var summary = Find<Grid>("UpdateSummary");
        summary.ColumnDefinitions[0].Width = new(wide ? 48 : 32);
        summary.ColumnDefinitions[1].Width = new(1, GridUnitType.Star);
        summary.ColumnDefinitions[2].Width = GridLength.Auto;
        summary.ColumnDefinitions[3].Width = wide ? new(130) : new(0);
        summary.ColumnDefinitions[4].Width = wide ? GridLength.Auto : new GridLength(0);
        summary.ColumnDefinitions[5].Width = wide ? new(190) : new(0);
        summary.ColumnDefinitions[6].Width = wide ? GridLength.Auto : new GridLength(0);
        var icon = Find<TextBlock>("UpdateSummaryIcon");
        Grid.SetColumn(icon, 0); Grid.SetRow(icon, 0); Grid.SetRowSpan(icon, wide ? 3 : 1);
        var status = Find<StackPanel>("UpdateStatusGroup");
        Grid.SetColumn(status, 1); Grid.SetRow(status, 0); Grid.SetColumnSpan(status, 1);
        var current = Find<StackPanel>("UpdateCurrentGroup");
        Grid.SetColumn(current, wide ? 3 : 1); Grid.SetRow(current, wide ? 0 : 1); Grid.SetColumnSpan(current, wide ? 1 : 2);
        var checkedGroup = Find<StackPanel>("UpdateLastCheckedGroup");
        Grid.SetColumn(checkedGroup, wide ? 5 : 1); Grid.SetRow(checkedGroup, wide ? 0 : 2); Grid.SetColumnSpan(checkedGroup, wide ? 1 : 2);
        var check = Find<Button>("CheckUpdatesButton");
        Grid.SetColumn(check, wide ? 6 : 2); Grid.SetRow(check, 0);
        Find<Border>("UpdateCurrentDivider").Visibility = wide ? Visibility.Visible : Visibility.Collapsed;
        Find<Border>("UpdateLastCheckedDivider").Visibility = wide ? Visibility.Visible : Visibility.Collapsed;
        status.Margin = wide ? new(6, 0, 16, 0) : new(4, 0, 8, 0);
        current.Margin = wide ? new(12, 0, 12, 0) : new(4, 4, 0, 0);
        checkedGroup.Margin = wide ? new(12, 0, 12, 0) : new(4, 4, 0, 0);
    }
}
