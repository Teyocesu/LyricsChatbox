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
        Find<Border>("Sidebar").Padding = shortWindow ? new(12, 12, 12, 12) : new(16, 28, 16, 20);
        Find<StackPanel>("BrandPanel").Margin = new(0, 0, 0, shortWindow ? 12 : 32);
        Find<TextBlock>("VersionCaption").Visibility = shortWindow ? Visibility.Collapsed : Visibility.Visible;
        foreach (var item in Find<StackPanel>("NavigationPanel").Children.OfType<RadioButton>())
            item.Padding = shortWindow ? new(12, 6, 12, 6) : new(16, 12, 16, 12);
        Find<Grid>("ContentPanel").Margin = shortWindow ? new(16, 10, 16, 10) : new(28, 24, 28, 20);
        Find<StackPanel>("HeadingPanel").Margin = new(0, 0, 0, shortWindow ? 8 : 20);
        Find<Border>("PreviewCard").Padding = shortWindow ? new(10, 6, 10, 6) : new(16);
        Find<Border>("PreviewCard").Margin = new(0, shortWindow ? 8 : 16, 0, 0);
        Find<DockPanel>("PreviewHeading").Visibility = shortWindow ? Visibility.Collapsed : Visibility.Visible;
        Find<ScrollViewer>("PreviewScroller").MaxHeight = shortWindow ? 24 : 72;
    }
}
