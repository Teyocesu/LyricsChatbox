using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace LyricsChatbox;

internal static class WindowLayout
{
    internal const double AboutCompactBreakpoint = 1000;
    internal const double AboutUpdatesCompactColumnsBreakpoint = 560;
    internal const double AboutUpdatesWideBreakpoint = 1120;
    internal const double AboutContentGutters = 60;
    internal enum AboutUpdatesMode { Wide, Medium, CompactColumns, Compact }
    internal static bool IsAboutCompact(double contentWidth) => contentWidth < AboutCompactBreakpoint;
    internal static AboutUpdatesMode SelectAboutUpdatesMode(double contentWidth) =>
        contentWidth < AboutUpdatesCompactColumnsBreakpoint ? AboutUpdatesMode.Compact :
        contentWidth < AboutCompactBreakpoint ? AboutUpdatesMode.CompactColumns :
        contentWidth < AboutUpdatesWideBreakpoint ? AboutUpdatesMode.Medium : AboutUpdatesMode.Wide;
    internal static bool UsesAboutUpdatesTwoColumnLayout(AboutUpdatesMode mode) =>
        mode is AboutUpdatesMode.CompactColumns or AboutUpdatesMode.Medium;

    // Reserve the scrollbar width even before Auto decides it is needed, so a short
    // viewport cannot squeeze the wide columns after the first layout pass.
    internal static double AboutUsableWidth(double aboutViewportWidth) =>
        Math.Max(0, aboutViewportWidth - AboutContentGutters - SystemParameters.VerticalScrollBarWidth);

    internal static void ConfigureAboutScroll(ScrollViewer scrollViewer)
    {
        scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
    }

    internal static bool ReparentAboutSections(bool compact, StackPanel overview, StackPanel community,
        StackPanel project, StackPanel privacy, StackPanel left, StackPanel right, StackPanel cascade)
    {
        var placements = compact
            ? new[] { (overview, cascade), (community, cascade), (project, cascade), (privacy, cascade) }
            : new[] { (overview, left), (project, left), (community, right), (privacy, right) };
        var moved = false;
        foreach (var (section, target) in placements)
        {
            if (section.Parent == target) continue;
            if (section.Parent is Panel oldParent) oldParent.Children.Remove(section);
            target.Children.Add(section);
            moved = true;
        }
        return moved;
    }

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
        var home = Find<ScrollViewer>("HomePage").Visibility == Visibility.Visible;
        var about = Find<Grid>("AboutPage").Visibility == Visibility.Visible;
        // About is full-bleed: it fills the entire post-sidebar cell, so the shared
        // ContentPanel margin is removed while About is active. Other pages keep it.
        Find<Grid>("ContentPanel").Margin = about ? new(0) : shortWindow ? new(16, 38, 16, 10) : new(28, 48, 28, 16);
        Find<TextBlock>("ErrorText").Margin = about ? new(30, 8, 30, 8) : new(0, 8, 0, 0);
        Find<StackPanel>("HeadingPanel").Margin = new(0, 0, 0, shortWindow ? 8 : 16);
        Find<ContentControl>("PersistentPreviewHost").Visibility = about ? Visibility.Collapsed : Visibility.Visible;
        // The About breakpoint uses usable content width. Derive it synchronously from the
        // post-sidebar cell (stable across navigation) rather than the About viewport,
        // whose ActualWidth is stale on the navigating pass before WPF runs layout.
        var cellWidth = Find<Grid>("ContentRoot").ActualWidth - Find<Border>("Sidebar").ActualWidth;
        if (cellWidth <= 0) cellWidth = Find<Grid>("ContentPanel").ActualWidth;
        ApplyAboutLayout(scope, AboutUsableWidth(cellWidth));
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
        var compact = IsAboutCompact(contentWidth);
        var updatesMode = SelectAboutUpdatesMode(contentWidth);
        var scrollViewer = Find<ScrollViewer>("AboutScrollViewer");
        ConfigureAboutScroll(scrollViewer);
        var wasCompact = scrollViewer.Tag is bool previous && previous;
        var modeChanged = scrollViewer.Tag is null || wasCompact != compact;
        scrollViewer.Tag = compact;
        if (modeChanged)
            scrollViewer.ScrollToTop();

        var eyebrow = Find<Grid>("AboutEyebrow");
        eyebrow.Height = 22;
        eyebrow.Margin = new(0, 0, 0, compact ? 8 : 12);

        Find<Grid>("AboutPage").Margin = compact ? new(30, 40, 42, 24) : new(30, 40, 30, 24);

        var hero = Find<Grid>("AboutHero");
        hero.MinHeight = compact ? 0 : 248;
        hero.Margin = new(0, 0, 0, compact ? 20 : 14);
        var heroContent = Find<Grid>("AboutHeroContent");
        var note = Find<Border>("AboutNoteMark");
        heroContent.ColumnDefinitions[0].Width = new(compact ? 72 : 108);
        Grid.SetRow(note, 0);
        Grid.SetRowSpan(note, compact ? 1 : 3);
        note.Width = compact ? 54 : 108;
        note.Height = compact ? 60 : 124;
        note.VerticalAlignment = compact ? VerticalAlignment.Center : VerticalAlignment.Top;
        note.Margin = compact ? new(0) : new(0, 4, 0, 0);
        note.Opacity = compact ? .80 : .82;
        var title = Find<TextBlock>("AboutHeroTitle");
        var description = Find<TextBlock>("AboutHeroDescription");
        var origin = Find<TextBlock>("AboutOriginText");
        Grid.SetRow(title, 0); Grid.SetColumn(title, 1);
        Grid.SetRow(description, 1); Grid.SetColumn(description, compact ? 0 : 1); Grid.SetColumnSpan(description, compact ? 2 : 1);
        Grid.SetRow(origin, 2); Grid.SetColumn(origin, compact ? 0 : 1); Grid.SetColumnSpan(origin, compact ? 2 : 1);
        title.Margin = new(0, compact ? 0 : 16, 12, 10);
        description.Margin = new(0, compact ? 10 : 0, 0, 12);
        Find<Grid>("AboutHeroArtLayer").Height = compact ? 320 : 420;
        Find<Border>("AboutHeroBackdrop").Opacity = compact ? 0.38 : 0.82;
        title.FontSize = compact ? 34 : 66;
        description.FontSize = 18;
        description.LineHeight = 24;
        description.MaxWidth = 620;
        origin.FontSize = 14;
        origin.LineHeight = 20;
        origin.MaxWidth = 700;

        var wideColumns = Find<Grid>("AboutWideColumns");
        wideColumns.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        Find<StackPanel>("AboutCompactCascade").Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
        wideColumns.ColumnDefinitions[0].Width = new(1, GridUnitType.Star);
        wideColumns.ColumnDefinitions[1].Width = new(48);
        wideColumns.ColumnDefinitions[2].Width = new(1, GridUnitType.Star);
        var focusedElement = Keyboard.FocusedElement;
        var sectionsReparented = ReparentAboutSections(compact,
            Find<StackPanel>("AboutOverview"), Find<StackPanel>("AboutCommunity"),
            Find<StackPanel>("AboutProject"), Find<StackPanel>("AboutPrivacy"),
            Find<StackPanel>("AboutLeftColumn"), Find<StackPanel>("AboutRightColumn"),
            Find<StackPanel>("AboutCompactCascade"));
        if (sectionsReparented && focusedElement is UIElement focusElement && focusElement.IsVisible)
            Keyboard.Focus(focusElement);

        Find<StackPanel>("AboutProject").Margin = new(0, compact ? 26 : 22, 0, 0);
        Find<StackPanel>("AboutCommunity").Margin = new(0, compact ? 26 : 0, 0, 0);
        Find<StackPanel>("AboutPrivacy").Margin = new(0, compact ? 26 : 22, 0, 0);
        Find<StackPanel>("AboutUpdates").Margin = new(0, compact ? 26 : 20, 0, 0);

        var iconWidth = compact ? 44 : 48;
        var labelWidth = compact ? 156 : 172;
        foreach (var sectionName in new[] { "AboutOverview", "AboutProject", "AboutCommunity", "AboutPrivacy" })
        {
            var section = Find<StackPanel>(sectionName);
            foreach (var row in section.Children.OfType<Grid>().Where(row => row.ColumnDefinitions.Count >= 3))
            {
                row.MinHeight = compact ? 40 : 42;
                row.ColumnDefinitions[0].Width = new(iconWidth);
                row.ColumnDefinitions[1].Width = new(labelWidth);
                if (row.ColumnDefinitions.Count == 4) row.ColumnDefinitions[3].Width = new(24);
                foreach (var label in row.Children.OfType<TextBlock>().Where(text => Grid.GetColumn(text) == 1))
                {
                    label.Margin = new(compact ? 6 : 8, 0, 4, 0);
                    label.FontSize = 14;
                    label.LineHeight = 19;
                }
                foreach (var value in row.Children.OfType<TextBlock>().Where(text => Grid.GetColumn(text) == 2))
                {
                    value.FontSize = 14;
                    value.LineHeight = 20;
                    value.Margin = new(0, 2, 0, 2);
                }
                foreach (var textIcon in row.Children.OfType<TextBlock>().Where(text => Grid.GetColumn(text) == 0))
                    textIcon.FontSize = 20;
                foreach (var button in row.Children.OfType<Button>())
                {
                    button.MinHeight = compact ? 34 : 36;
                    button.Padding = new(4);
                }
            }
        }
        Find<Grid>("AboutDiagnosticsRow").ColumnDefinitions[3].Width = GridLength.Auto;
        var diagnosticsDescription = Find<TextBlock>("DiagnosticsDescription");
        diagnosticsDescription.Visibility = updatesMode == AboutUpdatesMode.Wide ? Visibility.Visible : Visibility.Collapsed;
        diagnosticsDescription.Margin = new(0, 2, 8, 2);

        foreach (var name in new[] { "AboutOverviewTitle", "AboutCommunityTitle", "AboutProjectTitle", "AboutPrivacyTitle", "AboutUpdatesTitle" })
            Find<TextBlock>(name).FontSize = 13;
        foreach (var ruleName in new[] { "AboutOverviewRule", "AboutProjectRule", "AboutCommunityRule", "AboutPrivacyRule", "AboutUpdatesRule" })
        {
            var rule = Find<Border>(ruleName);
            rule.Height = 2;
            rule.Margin = new(0, 4, 0, 4);
        }
        Find<Image>("VrChatLogoMark").Width = compact ? 42 : 48;
        Find<Image>("VrChatLogoMark").Height = compact ? 21 : 24;
        Find<Image>("DiscordClydeMark").Width = compact ? 28 : 32;
        Find<Image>("DiscordClydeMark").Height = compact ? 21 : 24;

        var summary = Find<Grid>("UpdateSummary");
        var columns = summary.ColumnDefinitions;
        var icon = Find<TextBlock>("UpdateSummaryIcon");
        var status = Find<StackPanel>("UpdateStatusGroup");
        var current = Find<StackPanel>("UpdateCurrentGroup");
        var checkedGroup = Find<StackPanel>("UpdateLastCheckedGroup");
        var action = Find<StackPanel>("UpdateActionGroup");
        var check = Find<Button>("CheckUpdatesButton");
        if (updatesMode == AboutUpdatesMode.Wide)
        {
            columns[0].Width = new(42);
            columns[1].Width = new(1.6, GridUnitType.Star);
            columns[2].Width = GridLength.Auto;
            columns[3].Width = new(0.8, GridUnitType.Star);
            columns[4].Width = GridLength.Auto;
            columns[5].Width = new(1, GridUnitType.Star);
            columns[6].Width = GridLength.Auto;
            Grid.SetColumn(icon, 0); Grid.SetRow(icon, 0); Grid.SetRowSpan(icon, 1);
            Grid.SetColumn(status, 1); Grid.SetRow(status, 0); Grid.SetColumnSpan(status, 1); Grid.SetRowSpan(status, 1);
            Grid.SetColumn(current, 3); Grid.SetRow(current, 0); Grid.SetColumnSpan(current, 1); Grid.SetRowSpan(current, 1);
            Grid.SetColumn(checkedGroup, 5); Grid.SetRow(checkedGroup, 0); Grid.SetColumnSpan(checkedGroup, 1); Grid.SetRowSpan(checkedGroup, 1);
            Grid.SetColumn(action, 6); Grid.SetRow(action, 0); Grid.SetColumnSpan(action, 1); Grid.SetRowSpan(action, 1);
            Find<Border>("UpdateCurrentDivider").Visibility = Visibility.Visible;
            Find<Border>("UpdateLastCheckedDivider").Visibility = Visibility.Visible;
            summary.MinHeight = 62;
            summary.Margin = new(0, 4, 0, 0);
            status.Margin = new(6, 0, 12, 0);
            current.Margin = new(8, 0, 8, 0);
            checkedGroup.Margin = new(8, 0, 8, 0);
            action.Margin = new(8, 0, 0, 0);
        }
        else if (UsesAboutUpdatesTwoColumnLayout(updatesMode))
        {
            columns[0].Width = new(40);
            columns[1].Width = new(1, GridUnitType.Star);
            columns[2].Width = new(36);
            columns[3].Width = new(1, GridUnitType.Star);
            for (var i = 4; i < columns.Count; i++) columns[i].Width = new(0);
            Grid.SetColumn(icon, 0); Grid.SetRow(icon, 0); Grid.SetRowSpan(icon, 1);
            Grid.SetColumn(status, 1); Grid.SetRow(status, 0); Grid.SetColumnSpan(status, 1); Grid.SetRowSpan(status, 1);
            Grid.SetColumn(checkedGroup, 3); Grid.SetRow(checkedGroup, 0); Grid.SetColumnSpan(checkedGroup, 1); Grid.SetRowSpan(checkedGroup, 1);
            Grid.SetColumn(current, 3); Grid.SetRow(current, 1); Grid.SetColumnSpan(current, 1); Grid.SetRowSpan(current, 1);
            Grid.SetColumn(action, 1); Grid.SetRow(action, 1); Grid.SetColumnSpan(action, 1); Grid.SetRowSpan(action, 1);
            Find<Border>("UpdateCurrentDivider").Visibility = Visibility.Collapsed;
            Find<Border>("UpdateLastCheckedDivider").Visibility = Visibility.Collapsed;
            summary.MinHeight = 0;
            summary.Margin = new(0, 4, 0, 0);
            status.VerticalAlignment = VerticalAlignment.Top;
            status.Margin = new(8, 0, 8, 0);
            checkedGroup.VerticalAlignment = VerticalAlignment.Top;
            checkedGroup.Margin = new(0, 0, 4, 0);
            current.VerticalAlignment = VerticalAlignment.Top;
            current.Margin = new(0, 12, 4, 0);
            action.VerticalAlignment = VerticalAlignment.Top;
            action.Margin = new(8, 16, 0, 0);
        }
        else
        {
            columns[0].Width = new(32);
            columns[1].Width = new(1, GridUnitType.Star);
            for (var i = 2; i < columns.Count; i++) columns[i].Width = new(0);
            Grid.SetColumn(icon, 0); Grid.SetRow(icon, 0); Grid.SetRowSpan(icon, 1);
            Grid.SetColumn(status, 1); Grid.SetRow(status, 0); Grid.SetColumnSpan(status, 1); Grid.SetRowSpan(status, 1);
            Grid.SetColumn(current, 1); Grid.SetRow(current, 1); Grid.SetColumnSpan(current, 1); Grid.SetRowSpan(current, 1);
            Grid.SetColumn(checkedGroup, 1); Grid.SetRow(checkedGroup, 2); Grid.SetColumnSpan(checkedGroup, 1); Grid.SetRowSpan(checkedGroup, 1);
            Grid.SetColumn(action, 1); Grid.SetRow(action, 3); Grid.SetColumnSpan(action, 1); Grid.SetRowSpan(action, 1);
            Find<Border>("UpdateCurrentDivider").Visibility = Visibility.Collapsed;
            Find<Border>("UpdateLastCheckedDivider").Visibility = Visibility.Collapsed;
            summary.MinHeight = 0;
            summary.Margin = new(0, 4, 0, 0);
            status.Margin = new(2, 0, 4, 0);
            current.Margin = new(2, 16, 0, 0);
            checkedGroup.Margin = new(2, 8, 0, 0);
            action.Margin = new(2, 18, 0, 0);
        }
        check.MinWidth = updatesMode is AboutUpdatesMode.CompactColumns or AboutUpdatesMode.Compact ? 164 : 184;
        check.Height = double.NaN;
        Find<ToggleButton>("UpdatePreferencesButton").Width = 38;
        Find<ToggleButton>("UpdatePreferencesButton").Height = 38;
    }
}
