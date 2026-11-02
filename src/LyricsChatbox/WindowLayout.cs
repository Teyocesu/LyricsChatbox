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
    internal const double HomeColumnsMinWidth = 840;
    internal const double HomeRoomyMinHeight = 760;
    internal const double HomeTallMinHeight = 900;
    internal enum AboutUpdatesMode { Wide, Medium, CompactColumns, Compact }
    internal static bool UsesHomeColumns(double contentWidth) => contentWidth >= HomeColumnsMinWidth;
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
    public static void Apply(FrameworkElement scope, double contentHeight)
    {
        var shortWindow = contentHeight < 540;
        T Find<T>(string name) => (T)scope.FindName(name);
        Find<StackPanel>("BrandPanel").Margin = shortWindow ? new(0, 6, 0, 8) : new(0, 14, 0, 20);
        foreach (var item in Find<StackPanel>("NavigationPanel").Children.OfType<RadioButton>())
            item.Padding = shortWindow ? new(0, 6, 0, 6) : new(0, 10, 0, 10);
        var home = Find<Grid>("HomePage").Visibility == Visibility.Visible;
        var about = Find<Grid>("AboutPage").Visibility == Visibility.Visible;
        var display = Find<ScrollViewer>("DisplayPage").Visibility == Visibility.Visible;
        var settings = Find<ScrollViewer>("SettingsPage").Visibility == Visibility.Visible;
        Find<ContentControl>("PersistentPreviewHost").Visibility = home || about ? Visibility.Collapsed : Visibility.Visible;
        // The About breakpoint uses usable content width. Derive it synchronously from the
        // post-sidebar cell (stable across navigation) rather than the About viewport,
        // whose ActualWidth is stale on the navigating pass before WPF runs layout.
        var cellWidth = Find<Grid>("ContentRoot").ActualWidth - Find<Border>("Sidebar").ActualWidth;
        if (cellWidth <= 0) cellWidth = Find<Grid>("ContentPanel").ActualWidth;
        ApplyAboutLayout(scope, AboutUsableWidth(cellWidth));
        var preview = Find<Grid>("PreviewPanel");
        var destination = Find<ContentControl>(home ? "HomePreviewHost" : "PersistentPreviewHost");
        if (preview.Parent != destination)
        {
            ((ContentControl)preview.Parent).Content = null;
            destination.Content = preview;
        }
        preview.Margin = home ? new(0, 0, 0, 24) : new(0, shortWindow ? 8 : 16, 0, 0);
        Find<DockPanel>("PreviewHeading").Visibility = shortWindow && !home ? Visibility.Collapsed : Visibility.Visible;
        Find<ScrollViewer>("PreviewScroller").MaxHeight = home ? 130 : shortWindow ? 24 : 72;
        Find<TextBlock>("PreviewProfileText").Visibility = shortWindow && !home ? Visibility.Collapsed : Visibility.Visible;
        Find<DockPanel>("PreviewHeading").Margin = new(0, 0, 0, home ? 8 : 6);
        Find<TextBlock>("PreviewProfileText").Margin = new(0, 0, 0, home ? 10 : 6);
        Find<Border>("PreviewFrame").Padding = home ? new(12, 8, 12, 8) : new(8, 2, 8, 2);
        Find<Border>("PreviewFrame").Margin = new(0, 0, 0, home ? 10 : 6);
        if (home) ApplyHomeLayout(scope, cellWidth - AboutContentGutters, contentHeight, shortWindow);
        if (display) ApplyWorkspaceLayout(scope, "Display", cellWidth - AboutContentGutters);
        if (settings) ApplyWorkspaceLayout(scope, "Settings", cellWidth - AboutContentGutters);
    }

    private static void ApplyWorkspaceLayout(FrameworkElement scope, string page, double contentWidth)
    {
        T Find<T>(string part) => (T)scope.FindName(page + part);
        // The scrollbar column is always reserved, so content keeps Preview's right edge and never reflows.
        Find<StackPanel>("Canvas").Margin = new(30, 4, 30 - SystemParameters.VerticalScrollBarWidth, 8);
        var columns = UsesHomeColumns(contentWidth);
        var support = Find<StackPanel>("SupportColumn");
        Grid.SetColumnSpan(Find<StackPanel>("PrimaryColumn"), columns ? 1 : 3);
        Grid.SetColumn(support, columns ? 2 : 0);
        Grid.SetColumnSpan(support, columns ? 1 : 3);
        Grid.SetRow(support, columns ? 0 : 1);
        support.Margin = new(0, columns ? 0 : 24, 0, 0);
        Find<Border>("ColumnDivider").Visibility = columns ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void ApplyHomeLayout(FrameworkElement scope, double contentWidth, double contentHeight, bool shortWindow)
    {
        T Find<T>(string name) => (T)scope.FindName(name);
        var columns = UsesHomeColumns(contentWidth);
        var roomy = contentHeight >= HomeRoomyMinHeight;
        var (artwork, title, lyric, lyricLine, lyricLines) = shortWindow ? (60, 18, 20, 26, 2)
            : columns ? contentHeight >= HomeTallMinHeight ? (240, 36, 40, 50, 5) : roomy ? (208, 32, 34, 44, 4) : (160, 26, 30, 38, 3)
            : roomy ? (128, 26, 30, 38, 4) : (84, 20, 24, 30, 2);
        var spacious = columns && roomy;
        var lyricBesideArtwork = columns && !shortWindow;

        var hero = Find<Border>("NowPlayingPanel");
        hero.Padding = new(0, shortWindow ? 34 : spacious ? 40 : 36, 0, shortWindow ? 10 : spacious ? 20 : 14);

        // Beside the lyric, the artwork sets the hero's height without taking part in row sizing,
        // so ordinary lines keep the layout still and only an unusually long line needs more room.
        var frame = Find<Border>("ArtworkFrame");
        frame.Width = frame.Height = artwork;
        frame.Margin = new(0, 0, 0, lyricBesideArtwork ? -artwork : 0);
        Grid.SetRowSpan(frame, lyricBesideArtwork ? 2 : 1);
        Find<Grid>("HeroGrid").MinHeight = lyricBesideArtwork ? artwork : 0;
        Find<ColumnDefinition>("ArtworkColumn").Width = new(artwork + (lyricBesideArtwork ? 28 : 18));
        Find<TextBlock>("ArtFallback").FontSize = artwork * .3;
        Find<TextBlock>("TrackText").FontSize = title;
        Find<TextBlock>("ArtistText").FontSize = shortWindow ? 13 : spacious ? 16 : 14;
        Find<TextBlock>("AlbumText").Visibility = shortWindow ? Visibility.Collapsed : Visibility.Visible;
        var lyricText = Find<TextBlock>("LyricText");
        lyricText.FontSize = lyric;
        lyricText.LineHeight = lyricLine;
        lyricText.MaxHeight = lyricLine * lyricLines;
        lyricText.Margin = new(0, shortWindow || lyricBesideArtwork ? 6 : 12, 0, 0);
        Grid.SetColumn(lyricText, lyricBesideArtwork ? 1 : 0);
        Grid.SetColumnSpan(lyricText, lyricBesideArtwork ? 1 : 2);
        Find<StackPanel>("HeroTransport").Margin = new(0, shortWindow ? 8 : spacious ? 18 : 12, 0, 0);
        foreach (var name in new[] { "ShuffleButton", "PreviousButton", "PlayPauseButton", "NextButton", "RepeatButton" })
        {
            var button = Find<Button>(name);
            button.Width = button.Height = shortWindow ? 30 : name == "PlayPauseButton" ? 46 : 36;
            button.MinHeight = shortWindow ? 30 : 36;
        }

        var sections = new[] { "HomePreviewHost", "ProfilesSection", "ContextCard", "InspectorSection", "HomeToolsSection" }
            .Select(Find<FrameworkElement>).ToArray();
        // One tree at every width: sections stay in place and only change cell or collapse.
        var selector = Find<ListBox>("HomeSectionBox");
        if (!columns && Array.FindIndex(sections, section => section.IsKeyboardFocusWithin) is >= 0 and var focused)
            selector.SelectedIndex = focused;
        selector.Visibility = columns ? Visibility.Collapsed : Visibility.Visible;
        for (var index = 0; index < sections.Length; index++)
        {
            sections[index].Visibility = columns || index == selector.SelectedIndex ? Visibility.Visible : Visibility.Collapsed;
            sections[index].Margin = new(0, columns && index is not (0 or 1) ? 24 : 0, 0, 0);
        }
        Find<Border>("HomeColumnDivider").Visibility = columns ? Visibility.Visible : Visibility.Collapsed;
        var left = Find<StackPanel>("HomeLeftColumn");
        var right = Find<StackPanel>("HomeRightColumn");
        Grid.SetColumnSpan(left, columns ? 1 : 3);
        Grid.SetColumn(right, columns ? 2 : 0);
        Grid.SetColumnSpan(right, columns ? 1 : 3);
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

        Find<Grid>("AboutPage").Margin = compact ? new(30, 4, 42, 24) : new(30, 4, 30, 24);

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
