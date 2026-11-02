using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace LyricsChatbox;

// Column scroller that passes the wheel to the page while the columns stack.
public sealed class ColumnScrollViewer : ScrollViewer
{
    static ColumnScrollViewer() => DefaultStyleKeyProperty.OverrideMetadata(typeof(ColumnScrollViewer), new FrameworkPropertyMetadata(typeof(ScrollViewer)));
    public bool HandlesWheel { get; set; } = true;
    protected override void OnMouseWheel(MouseWheelEventArgs e) { if (HandlesWheel) base.OnMouseWheel(e); }
}

internal static class WindowLayout
{
    internal const double AboutCompactBreakpoint = 1000;
    internal const double AboutContentGutters = 60;
    // The two text columns need this main width beside the 400-DIP panel.
    internal const double AboutColumnsMinWidth = 800;
    internal const double HomeColumnsMinWidth = 840;
    // Width after the rail: the 400-DIP inspector plus 600 DIP for the stage's transport, metadata and lyric.
    internal const double HomeInspectorMinWidth = 1000;
    // Width after the rail: both side columns plus 440 DIP for the editor and its token row.
    internal const double DisplayColumnsMinWidth = 1020;
    // Nine Chatbox lines at the 18-DIP preview size.
    internal const double PreviewNineLines = 216;
    // Width after the rail: the full index beside rows of 240-DIP controls and readable text.
    internal const double SettingsFullIndexMinWidth = 1000;
    internal const double HomeRoomyMinHeight = 760;
    internal const double HomeTallMinHeight = 900;
    internal static bool UsesHomeColumns(double contentWidth) => contentWidth >= HomeColumnsMinWidth;
    internal static bool UsesHomeInspector(double cellWidth) => cellWidth >= HomeInspectorMinWidth;
    internal static bool UsesDisplayColumns(double cellWidth, bool shortWindow) => cellWidth >= DisplayColumnsMinWidth && !shortWindow;
    internal static bool IsAboutCompact(double contentWidth) => contentWidth < AboutCompactBreakpoint;

    // Reserve the scrollbar width even before Auto decides it is needed, so a short
    // viewport cannot squeeze the wide columns after the first layout pass.
    internal static double AboutUsableWidth(double aboutViewportWidth) =>
        Math.Max(0, aboutViewportWidth - AboutContentGutters - SystemParameters.VerticalScrollBarWidth);

    internal static void ConfigureAboutScroll(ScrollViewer scrollViewer)
    {
        scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
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
        var settings = Find<Grid>("SettingsPage").Visibility == Visibility.Visible;
        // The About breakpoint uses usable content width. Derive it synchronously from the
        // post-sidebar cell (stable across navigation) rather than the About viewport,
        // whose ActualWidth is stale on the navigating pass before WPF runs layout.
        var cellWidth = Find<Grid>("ContentRoot").ActualWidth - Find<Border>("Sidebar").ActualWidth;
        if (cellWidth <= 0) cellWidth = Find<Grid>("ContentPanel").ActualWidth;
        if (about) ApplyAboutLayout(scope, cellWidth);
        var displayColumns = display && UsesDisplayColumns(cellWidth, shortWindow);
        Find<ContentControl>("PersistentPreviewHost").Visibility = home || about || settings || displayColumns ? Visibility.Collapsed : Visibility.Visible;
        var preview = Find<Grid>("PreviewPanel");
        var destination = Find<ContentControl>(home ? "HomePreviewHost" : displayColumns ? "DisplayPreviewHost" : settings ? "SettingsPreviewHost" : "PersistentPreviewHost");
        if (preview.Parent != destination)
        {
            ((ContentControl)preview.Parent).Content = null;
            destination.Content = preview;
        }
        preview.Margin = home || displayColumns || settings ? new(0) : new(0, shortWindow ? 8 : 16, 0, 0);
        Find<DockPanel>("PreviewHeading").Visibility = shortWindow && !home ? Visibility.Collapsed : Visibility.Visible;
        Find<ScrollViewer>("PreviewScroller").MaxHeight = home ? 130 : displayColumns ? PreviewNineLines : shortWindow ? 24 : 72;
        Find<Border>("PreviewBubble").VerticalAlignment = displayColumns ? VerticalAlignment.Top : VerticalAlignment.Center;
        Find<TextBlock>("PreviewText").FontSize = displayColumns ? 18 : 17;
        Find<TextBlock>("PreviewProfileText").Visibility = shortWindow && !home ? Visibility.Collapsed : Visibility.Visible;
        Find<DockPanel>("PreviewHeading").Margin = new(0, 0, 0, home ? 8 : 6);
        Find<TextBlock>("PreviewProfileText").Margin = new(0, 0, 0, 2);
        Find<Border>("PreviewFrame").Margin = new(0, 0, 0, home ? 8 : 6);
        if (home) ApplyHomeLayout(scope, cellWidth, contentHeight, shortWindow);
        if (display) ApplyDisplayLayout(scope, displayColumns);
        if (settings) ApplySettingsLayout(scope, cellWidth, shortWindow);
    }

    private static void ApplyDisplayLayout(FrameworkElement scope, bool columns)
    {
        T Find<T>(string name) => (T)scope.FindName(name);
        // One tree: three full-height columns, or one scrolling stack with Preview pinned below it.
        var page = Find<ScrollViewer>("DisplayPage");
        Grid.SetRow(page, columns ? 0 : 1);
        Grid.SetRowSpan(page, columns ? 3 : 1);
        page.VerticalScrollBarVisibility = columns ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Visible;
        Find<Grid>("DisplayCanvas").Margin = columns ? new(0) : new(30, 4, 30 - SystemParameters.VerticalScrollBarWidth, 8);
        Find<ColumnDefinition>("DisplayProfilesColumn").Width = new(columns ? 260 : 0);
        Find<ColumnDefinition>("DisplaySupportWidth").Width = new(columns ? 320 : 0);
        Find<RowDefinition>("DisplayFirstRow").Height = columns ? new(1, GridUnitType.Star) : GridLength.Auto;
        var cells = new (string Panel, int Row, int Column)[] { ("DisplayProfilesPanel", 0, 0), ("DisplayEditorPanel", 1, 1), ("DisplaySupportPanel", 2, 2) };
        foreach (var (name, row, column) in cells)
        {
            var panel = Find<FrameworkElement>(name);
            Grid.SetRow(panel, columns ? 0 : row);
            Grid.SetColumn(panel, columns ? column : 0);
            Grid.SetColumnSpan(panel, columns ? 1 : 3);
        }
        Find<Grid>("DisplayEditorPanel").Margin = columns ? new(24, 40, 24, 16) : new(0, 24, 0, 0);
        foreach (var (panelName, scroller, content, edge) in new[]
        {
            ("DisplayProfilesPanel", "DisplayProfilesScroller", "DisplayProfilesContent", new Thickness(0, 0, 1, 0)),
            ("DisplaySupportPanel", "DisplaySupportScroller", "DisplaySupportColumn", new Thickness(1, 0, 0, 0))
        })
        {
            var panel = Find<Border>(panelName);
            if (columns) panel.SetResourceReference(Border.BackgroundProperty, "SurfaceBrush");
            else panel.Background = Brushes.Transparent;
            panel.BorderThickness = columns ? edge : new(0);
            var column = Find<ColumnScrollViewer>(scroller);
            column.HandlesWheel = columns;
            // Column content stays below the caption area; the panel background extends behind it.
            // Stacked, the scroller keeps room for list rows that reach past their text.
            column.Margin = columns ? new(0, 36, 0, 0) : new(-12, 0, -12, 0);
            Find<StackPanel>(content).Margin = columns ? new(20, 4, 20, 16) : new(12, panelName == "DisplaySupportPanel" ? 24 : 0, 12, 0);
        }
    }

    private static void ApplySettingsLayout(FrameworkElement scope, double cellWidth, bool shortWindow)
    {
        T Find<T>(string name) => (T)scope.FindName(name);
        var full = cellWidth >= SettingsFullIndexMinWidth;
        Find<ColumnDefinition>("SettingsIndexColumn").Width = new(full ? 280 : 180);
        var (left, right) = full ? (56, 32) : (24, 16);
        // Content and Preview share one capped width from the same left edge.
        var width = Math.Min(860, Math.Max(0, cellWidth - (full ? 280 : 180) - left - right - SystemParameters.VerticalScrollBarWidth));
        var content = Find<StackPanel>("SettingsContent");
        content.Margin = new(left, 4, 0, 8);
        content.Width = width;
        var host = Find<ContentControl>("SettingsPreviewHost");
        host.Margin = new(left, shortWindow ? 6 : 12, 0, shortWindow ? 6 : 12);
        host.Width = width;
    }

    private static void ApplyHomeLayout(FrameworkElement scope, double cellWidth, double contentHeight, bool shortWindow)
    {
        T Find<T>(string name) => (T)scope.FindName(name);
        var inspector = UsesHomeInspector(cellWidth);
        var roomy = contentHeight >= HomeRoomyMinHeight;
        var (artwork, title, artist, lyric, lyricLine, lyricLines) = shortWindow ? (60, 18, 13, 20, 26, 2)
            : !inspector ? roomy ? (128, 28, 15, 36, 44, 3) : (96, 24, 15, 32, 40, 2)
            : contentHeight >= HomeTallMinHeight ? (288, 40, 18, 64, 76, 3)
            : roomy ? (208, 34, 16, 48, 58, 3) : (148, 30, 15, 38, 46, 2);
        var gutter = inspector ? 40 : 24;
        // The artwork pool stays clear of the text: upper right beside the artwork, or under the compact lyric.
        var pool = Find<RadialGradientBrush>("HeroPoolMask");
        pool.Center = pool.GradientOrigin = new(inspector ? .72 : .84, inspector ? .3 : .78);

        // The stage spans its column, or sits above the sections without the inspector.
        Find<ColumnDefinition>("HomeInspectorColumn").Width = new(inspector ? 400 : 0);
        Find<RowDefinition>("HomeStageRow").Height = inspector ? new(1, GridUnitType.Star) : GridLength.Auto;
        Find<RowDefinition>("HomeSectionsRow").Height = inspector ? GridLength.Auto : new(1, GridUnitType.Star);
        var stage = Find<Grid>("HomeCanvas");
        Grid.SetRowSpan(stage, inspector ? 2 : 1);
        Grid.SetColumnSpan(stage, inspector ? 1 : 2);
        var panel = Find<Grid>("HomeSectionsPanel");
        Grid.SetColumn(panel, inspector ? 1 : 0);
        Grid.SetRow(panel, inspector ? 0 : 1);
        Grid.SetRowSpan(panel, inspector ? 2 : 1);
        Grid.SetColumnSpan(panel, inspector ? 1 : 2);
        Find<Border>("HomeInspectorEdge").BorderThickness = inspector ? new(1, 0, 0, 0) : new(0, 1, 0, 0);

        // The status row starts below the caption area.
        Find<Grid>("HeroStatusRow").Margin = new(gutter, 40, gutter, 0);
        Find<Grid>("HeroGrid").Margin = new(inspector && !shortWindow ? gutter + 32 : gutter, shortWindow || !inspector ? 10 : 16, gutter, shortWindow || !inspector ? 10 : 16);
        var frame = Find<Border>("ArtworkFrame");
        frame.Width = frame.Height = artwork;
        Find<ColumnDefinition>("ArtworkColumn").Width = new(artwork + (inspector ? 28 : 18));
        Find<TextBlock>("ArtFallback").FontSize = artwork * .3;
        Find<TextBlock>("TrackText").FontSize = title;
        Find<TextBlock>("ArtistText").FontSize = artist;
        Find<TextBlock>("AlbumText").Visibility = shortWindow ? Visibility.Collapsed : Visibility.Visible;
        var lyricText = Find<TextBlock>("LyricText");
        lyricText.FontSize = lyric;
        lyricText.LineHeight = lyricLine;
        lyricText.MaxHeight = lyricLine * lyricLines;
        lyricText.Margin = new(0, shortWindow || !inspector ? 10 : 24, 0, 0);
        Find<StackPanel>("HeroTransport").Margin = new(gutter, shortWindow ? 6 : 10, gutter, shortWindow ? 4 : 8);
        foreach (var name in new[] { "ShuffleButton", "PreviousButton", "PlayPauseButton", "NextButton", "RepeatButton" })
        {
            var button = Find<Button>(name);
            button.Width = button.Height = shortWindow ? 30 : name == "PlayPauseButton" ? 44 : 36;
            button.MinHeight = shortWindow ? 30 : 36;
        }

        var sections = new[] { "HomePreviewHost", "ProfilesSection", "ContextCard", "InspectorSection", "HomeToolsSection" }
            .Select(Find<FrameworkElement>).ToArray();
        // One tree at every width: sections stay in place and only change cell or collapse.
        var selector = Find<ListBox>("HomeSectionBox");
        if (!inspector && Array.FindIndex(sections, section => section.IsKeyboardFocusWithin) is >= 0 and var focused)
            selector.SelectedIndex = focused;
        selector.Visibility = inspector ? Visibility.Collapsed : Visibility.Visible;
        for (var index = 0; index < sections.Length; index++)
        {
            sections[index].Visibility = inspector || index == selector.SelectedIndex ? Visibility.Visible : Visibility.Collapsed;
            sections[index].Margin = new(inspector ? 20 : 24, 18, inspector ? 20 : 24, 18);
        }
        foreach (var separator in Find<StackPanel>("HomeColumns").Children.OfType<Border>())
            separator.Visibility = inspector ? Visibility.Visible : Visibility.Collapsed;
        // Inspector content stays below the caption area; its background extends behind it.
        Find<ScrollViewer>("HomeSectionsScroller").Margin = new(0, inspector ? 36 : 0, 0, 0);
    }

    private static void ApplyAboutLayout(FrameworkElement scope, double cellWidth)
    {
        T Find<T>(string name) => (T)scope.FindName(name);
        // One tree: main column and panel side by side, or one scrolling stack in the same reading order.
        var compact = IsAboutCompact(AboutUsableWidth(cellWidth));
        var scrollViewer = Find<ScrollViewer>("AboutScrollViewer");
        var modeChanged = scrollViewer.Tag is not bool previous || previous != compact;
        scrollViewer.Tag = compact;
        if (compact) ConfigureAboutScroll(scrollViewer);
        else scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        if (modeChanged) scrollViewer.ScrollToTop();
        scrollViewer.Margin = new(0, compact ? 36 : 0, 0, 0);
        Find<ColumnDefinition>("AboutPanelColumn").Width = new(compact ? 0 : 400);
        Find<RowDefinition>("AboutFirstRow").Height = compact ? GridLength.Auto : new(1, GridUnitType.Star);
        var panel = Find<Border>("AboutPanel");
        Grid.SetRow(panel, compact ? 1 : 0);
        Grid.SetColumn(panel, compact ? 0 : 1);
        if (compact) panel.Background = Brushes.Transparent;
        else panel.SetResourceReference(Border.BackgroundProperty, "SurfaceBrush");
        panel.BorderThickness = compact ? new(0) : new(1, 0, 0, 0);
        var main = Find<ColumnScrollViewer>("AboutMainScroller");
        var side = Find<ColumnScrollViewer>("AboutPanelScroller");
        main.HandlesWheel = side.HandlesWheel = !compact;
        main.Margin = side.Margin = new(0, compact ? 0 : 36, 0, 0);
        Find<StackPanel>("AboutBody").Margin = compact ? new(30, 8, 30, 0) : new(64, 16, 48, 24);
        Find<StackPanel>("AboutPanelContent").Margin = compact ? new(30, 28, 30, 24) : new(20, 4, 20, 16);

        var logo = Find<Image>("AboutLogo");
        logo.Width = logo.Height = compact ? 64 : 88;
        logo.Margin = new(0, 0, compact ? 16 : 20, 0);
        Find<TextBlock>("AboutHeroTitle").FontSize = compact ? 44 : 60;

        var columns = !compact && cellWidth - 400 >= AboutColumnsMinWidth;
        Find<ColumnDefinition>("AboutColumnGap").Width = new(columns ? 48 : 0);
        Find<ColumnDefinition>("AboutSecondColumn").Width = columns ? new(1, GridUnitType.Star) : new(0);
        var right = Find<StackPanel>("AboutRightColumn");
        Grid.SetRow(right, columns ? 0 : 1);
        Grid.SetColumn(right, columns ? 2 : 0);
        right.Margin = new(0, columns ? 0 : 28, 0, 0);
    }
}
