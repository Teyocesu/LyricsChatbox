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
        var width = Find<Grid>("ContentPanel").ActualWidth;
        var narrow = width < 800;
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
        var compactHero = width < 900;
        Place("TrackMetadata", 0, 0, compactHero ? 2 : 1, new(0, 0, compactHero ? 0 : 20, 0));
        Place("HeroLyricPanel", compactHero ? 0 : 1, compactHero ? 1 : 0, compactHero ? 2 : 1, new(0, compactHero ? 16 : 0, 0, 0));
        Find<Border>("HeroLyricPanel").BorderThickness = new(0);
        Find<Border>("HeroLyricPanel").Padding = compactHero ? new(0) : new(22, 4, 0, 4);
        Find<ColumnDefinition>("ArtworkColumn").Width = new(narrow ? 128 : 216);
        Find<Border>("ArtworkFrame").Width = Find<Border>("ArtworkFrame").Height = narrow ? 110 : 194;
        Find<TextBlock>("TrackText").FontSize = narrow ? 24 : 28;
    }
}
