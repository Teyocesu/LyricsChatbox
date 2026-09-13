using System.Windows;
using System.Windows.Controls;

namespace LyricsChatbox;

internal static class WindowLayout
{
    // Keep a useful page viewport when high scaling leaves little vertical work area.
    public static void Apply(FrameworkElement scope, double contentHeight)
    {
        var shortWindow = contentHeight < 540;
        T Find<T>(string name) => (T)scope.FindName(name);
        Find<Border>("Sidebar").Padding = shortWindow ? new(12, 16, 12, 12) : new(12, 26, 12, 20);
        Find<StackPanel>("BrandPanel").Margin = new(0, 0, 0, shortWindow ? 18 : 28);
        Find<TextBlock>("VersionCaption").Visibility = shortWindow ? Visibility.Collapsed : Visibility.Visible;
        foreach (var item in Find<StackPanel>("NavigationPanel").Children.OfType<RadioButton>())
            item.Padding = shortWindow ? new(12, 8, 12, 8) : new(16, 11, 16, 11);
        Find<Grid>("ContentPanel").Margin = shortWindow ? new(16, 38, 16, 10) : new(28, 48, 28, 16);
        Find<StackPanel>("HeadingPanel").Margin = new(0, 0, 0, shortWindow ? 8 : 16);
        var home = Find<ScrollViewer>("HomePage").Visibility == Visibility.Visible;
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
        Find<StackPanel>("HeadingPanel").Visibility = compactDashboard && shortWindow ? Visibility.Collapsed : Visibility.Visible;
        Find<DockPanel>("HeroStatusRow").Visibility = compactDashboard ? Visibility.Collapsed : Visibility.Visible;
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
}
