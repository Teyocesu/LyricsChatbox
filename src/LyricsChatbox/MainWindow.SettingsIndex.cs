using System.Windows;
using System.Windows.Controls;

namespace LyricsChatbox;

public partial class MainWindow
{
    private bool settingsIndexSyncing, settingsIndexJumping;

    private double SectionTop(ListBoxItem item) =>
        ((FrameworkElement)FindName((string)item.Tag)).TranslatePoint(new Point(), SettingsContent).Y;

    private void SettingsIndexChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ready || settingsIndexSyncing || SettingsIndex.SelectedItem is not ListBoxItem item) return;
        var offset = Math.Min(SectionTop(item), SettingsScroller.ScrollableHeight);
        if (Math.Abs(offset - SettingsScroller.VerticalOffset) < 1) return;
        // A last section may stop short of the top, so the chosen item outlives this scroll.
        settingsIndexJumping = true;
        SettingsScroller.ScrollToVerticalOffset(offset);
    }

    private void SettingsScrolled(object sender, ScrollChangedEventArgs e)
    {
        if (e.VerticalChange == 0) return;
        if (settingsIndexJumping) { settingsIndexJumping = false; return; }
        var items = SettingsIndex.Items.OfType<ListBoxItem>().ToArray();
        var current = e.VerticalOffset >= SettingsScroller.ScrollableHeight - 1 ? items[^1]
            : items.Last(item => SectionTop(item) <= e.VerticalOffset + 24);
        settingsIndexSyncing = true;
        SettingsIndex.SelectedItem = current;
        settingsIndexSyncing = false;
    }

    private void ApplyBehaviorSummary() => BehaviorSummaryText.Text = PresentationText.BehaviorSummary(
        StartWindowsBox.IsChecked == true, StartMinimizedBox.IsChecked == true, MinimizeTrayBox.IsChecked == true, CloseTrayBox.IsChecked == true);
}
