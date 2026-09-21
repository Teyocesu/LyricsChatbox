using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace LyricsChatbox;

public partial class DecorationPicker : Window
{
    private sealed record PickerItem(DecorationEntry Entry, bool IsFavorite)
    {
        public bool IsUserItem => Entry.Id.StartsWith("user-", StringComparison.Ordinal);
        public string FavoriteGlyph => IsFavorite ? "♥" : "♡";
        public string FavoriteName => (IsFavorite ? "Remove " : "Add ") + Entry.Name + (IsFavorite ? " from favorites" : " to favorites");
    }

    private readonly DecorationCatalog catalog;
    private readonly Func<DecorationState, bool> saveState;
    private readonly Func<string, DecorationInsertionPreview> preview;
    private readonly Dictionary<string, DecorationInsertionPreview> previewCache = new(StringComparer.Ordinal);
    private DecorationState state;
    private DecorationLibrary library;
    private DecorationNavigation mode = DecorationPickerPolicy.DefaultMode;
    private string? editingId;
    private bool initialized;
    private bool refreshingGroups;

    public string? SelectedContent { get; private set; }

    public DecorationPicker(DecorationCatalog catalog, DecorationState state, DecorationLibrary library,
        Func<DecorationState, bool> saveState, Func<string, DecorationInsertionPreview> preview)
    {
        this.catalog = catalog;
        this.state = state;
        this.library = library;
        this.saveState = saveState;
        this.preview = preview;
        InitializeComponent();
        ModeList.ItemsSource = DecorationPickerPolicy.Options;
        ItemKindBox.ItemsSource = DecorationPickerPolicy.KindOptions;
        ModeList.SelectedItem = DecorationPickerPolicy.Options.Single(option => option.Mode == DecorationPickerPolicy.DefaultMode);
        initialized = true;
        RefreshGroups();
        RefreshItems();
        Loaded += (_, _) => SearchBox.Focus();
    }

    private void RefreshItems(string? selectedId = null, bool resetScroll = false)
    {
        if (!initialized) return;
        selectedId ??= (DecorationList.SelectedItem as PickerItem)?.Entry.Id;
        var group = GroupList.SelectedItem as string;
        if (group == "All") group = null;
        var fitsOnly = FitsBox.IsChecked == true;
        var entries = DecorationPickerPolicy.Filter(library, mode, SearchBox.Text, group,
            fitsOnly ? entry => PreviewFor(entry) is { CanInsert: true, WouldTruncate: false } : null);
        var favoriteIds = state.FavoriteIds!.ToHashSet(StringComparer.Ordinal);
        var items = entries.Select(entry => new PickerItem(entry, favoriteIds.Contains(entry.Id))).ToArray();
        DecorationList.ItemsSource = items;
        DecorationList.ItemsPanel = (ItemsPanelTemplate)FindResource(
            DecorationPickerPolicy.Presentation(mode) == DecorationPresentation.Dense ? "DensePanel" : "RowPanel");
        DecorationList.ItemTemplate = (DataTemplate)FindResource(DecorationPickerPolicy.Presentation(mode) switch
        {
            DecorationPresentation.Dense => "DenseTemplate",
            DecorationPresentation.Wide => "WideTemplate",
            DecorationPresentation.Art => "ArtTemplate",
            _ => "CompactTemplate"
        });
        DecorationList.SelectedItem = items.FirstOrDefault(item => item.Entry.Id == selectedId) ?? items.FirstOrDefault();
        EmptyText.Text = DecorationPickerPolicy.EmptyMessage(mode, SearchBox.Text, catalog.IsAvailable);
        EmptyText.Visibility = items.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        AddItemButton.Visibility = mode == DecorationNavigation.MyItems ? Visibility.Visible : Visibility.Collapsed;
        UpdateSelection();
        if (resetScroll) ResetScroll();
    }

    private void RefreshGroups()
    {
        var groups = DecorationPickerPolicy.Groups(library, mode);
        refreshingGroups = true;
        GroupList.ItemsSource = new[] { "All" }.Concat(groups).ToArray();
        GroupList.SelectedIndex = 0;
        GroupList.Visibility = groups.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        refreshingGroups = false;
    }

    private DecorationInsertionPreview PreviewFor(DecorationEntry entry)
    {
        if (!previewCache.TryGetValue(entry.Id, out var result))
            previewCache[entry.Id] = result = preview(entry.Content);
        return result;
    }

    private void UpdateSelection()
    {
        if (DecorationList.SelectedItem is not PickerItem item)
        {
            SelectedText.Text = ""; PreviewStatus.Text = ""; InsertButton.IsEnabled = false; return;
        }
        var result = PreviewFor(item.Entry);
        var lines = item.Entry.Content.Count(c => c == '\n') + 1;
        SelectedText.Text = item.Entry.Name + (lines > 1 ? $" · {lines} lines" : "");
        PreviewStatus.Text = DecorationPickerPolicy.PreviewText(result);
        PreviewStatus.SetResourceReference(TextBlock.ForegroundProperty,
            !result.CanInsert || result.WouldTruncate ? "WarningBrush" : "MutedBrush");
        InsertButton.IsEnabled = result.CanInsert;
    }

    private void ModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!initialized || ModeList.SelectedItem is not DecorationNavigationOption option) return;
        mode = option.Mode;
        EditorPanel.Visibility = Visibility.Collapsed;
        RefreshGroups();
        RefreshItems(resetScroll: true);
    }
    private void SearchChanged(object sender, TextChangedEventArgs e) => RefreshItems(resetScroll: true);
    private void GroupChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!refreshingGroups) RefreshItems(resetScroll: true);
    }
    private void FitsChanged(object sender, RoutedEventArgs e) => RefreshItems(resetScroll: true);
    private void DecorationSelected(object sender, SelectionChangedEventArgs e) => UpdateSelection();
    private void SearchKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) InsertSelected(sender, e); }
    private void DecorationKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) InsertSelected(sender, e); }
    private void WindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) { SearchBox.Focus(); SearchBox.SelectAll(); e.Handled = true; }
        else if (e.Key == Key.Escape) { DialogResult = false; e.Handled = true; }
    }

    private void InsertSelected(object sender, RoutedEventArgs e)
    {
        if (DecorationList.SelectedItem is not PickerItem item) return;
        var result = PreviewFor(item.Entry);
        if (!result.CanInsert) { MutationStatus.Text = "Not enough editor space for this item."; return; }
        SelectedContent = item.Entry.Content;
        DialogResult = true;
    }

    private void OpenMyItems(object sender, RoutedEventArgs e)
    {
        mode = DecorationNavigation.MyItems;
        ModeList.SelectedItem = null;
        EditorPanel.Visibility = Visibility.Collapsed;
        RefreshGroups();
        RefreshItems(resetScroll: true);
    }

    private void ResetScroll() => Dispatcher.BeginInvoke(DispatcherPriority.Loaded,
        new Action(() => FindDescendant<ScrollViewer>(DecorationList)?.ScrollToTop()));

    private void ItemDoubleClicked(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && FindAncestor<Button>(source) is not null) return;
        if (sender is ListBoxItem { DataContext: PickerItem item }) DecorationList.SelectedItem = item;
        InsertSelected(sender, e); e.Handled = true;
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match) return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private static T? FindDescendant<T>(DependencyObject current) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(current); i++)
        {
            var child = VisualTreeHelper.GetChild(current, i);
            if (child is T match) return match;
            if (FindDescendant<T>(child) is { } descendant) return descendant;
        }
        return null;
    }

    private void ToggleFavorite(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: PickerItem item }) return;
        var next = item.IsFavorite ? state.Unfavorite(item.Entry.Id) : state.Favorite(item.Entry.Id);
        if (next is null) { MutationStatus.Text = "Favorites are full."; return; }
        ApplyState(next, item.Entry.Id, "Favorite updated.");
        e.Handled = true;
    }

    private void AddMyItem(object sender, RoutedEventArgs e)
    {
        editingId = null; EditorTitle.Text = "Add My item"; ItemNameBox.Clear(); ItemContentBox.Clear();
        ItemKindBox.SelectedValue = "Symbol"; EditorError.Text = ""; EditorPanel.Visibility = Visibility.Visible; ItemNameBox.Focus();
    }

    private void EditMyItem(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: PickerItem { IsUserItem: true } item }) return;
        editingId = item.Entry.Id; EditorTitle.Text = "Edit My item"; ItemNameBox.Text = item.Entry.Name;
        ItemKindBox.SelectedValue = item.Entry.Kind; ItemContentBox.Text = item.Entry.Content;
        EditorError.Text = ""; EditorPanel.Visibility = Visibility.Visible; ItemNameBox.Focus(); ItemNameBox.SelectAll(); e.Handled = true;
    }

    private void SaveMyItem(object sender, RoutedEventArgs e)
    {
        var name = ItemNameBox.Text.Trim();
        var kind = ItemKindBox.SelectedValue as string ?? "";
        DecorationState? next;
        string? selectedId;
        if (editingId is null)
        {
            var item = UserDecoration.Create(name, kind, ItemContentBox.Text);
            next = item is null ? null : state.AddMyItem(item);
            selectedId = item?.Id;
        }
        else
        {
            selectedId = editingId;
            next = state.UpdateMyItem(editingId, name, kind, ItemContentBox.Text);
        }
        if (next is null)
        {
            EditorError.Text = "Use a name, type and content within 40/512 characters and 9 lines. My items can hold 64 entries.";
            return;
        }
        EditorPanel.Visibility = Visibility.Collapsed;
        ApplyState(next, selectedId, "My item saved.");
    }

    private void DeleteMyItem(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: PickerItem { IsUserItem: true } item }) return;
        if (MessageBox.Show(this, $"Delete “{item.Entry.Name}”?", "Delete My item", MessageBoxButton.YesNo,
                MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        if (state.DeleteMyItem(item.Entry.Id) is { } next) ApplyState(next, null, "My item deleted.");
        e.Handled = true;
    }

    private void CancelMyItemEditor(object sender, RoutedEventArgs e) => EditorPanel.Visibility = Visibility.Collapsed;

    private void ApplyState(DecorationState next, string? selectedId, string success)
    {
        state = next;
        library = DecorationLibrary.Create(catalog, state);
        previewCache.Clear();
        var saved = saveState(state);
        MutationStatus.Text = saved ? success : "Change works this session but could not be saved.";
        RefreshItems(selectedId);
    }
}
