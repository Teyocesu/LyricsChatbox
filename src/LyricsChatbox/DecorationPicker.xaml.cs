using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
    private DecorationState state;
    private DecorationLibrary library;
    private string? editingId;
    private bool initialized;

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
        ModeList.SelectedIndex = 0;
        initialized = true;
        RefreshItems();
        Loaded += (_, _) => SearchBox.Focus();
    }

    private DecorationNavigation Mode => (ModeList.SelectedItem as DecorationNavigationOption)?.Mode ?? DecorationNavigation.Popular;

    private void RefreshItems(string? selectedId = null)
    {
        if (!initialized) return;
        selectedId ??= (DecorationList.SelectedItem as PickerItem)?.Entry.Id;
        var entries = DecorationPickerPolicy.Filter(library, Mode, SearchBox.Text);
        var favoriteIds = state.FavoriteIds!.ToHashSet(StringComparer.Ordinal);
        var items = entries.Select(entry => new PickerItem(entry, favoriteIds.Contains(entry.Id))).ToArray();
        DecorationList.ItemsSource = items;
        DecorationList.ItemsPanel = (ItemsPanelTemplate)FindResource(
            DecorationPickerPolicy.Presentation(Mode) == DecorationPresentation.Dense ? "DensePanel" : "RowPanel");
        DecorationList.ItemTemplate = (DataTemplate)FindResource(DecorationPickerPolicy.Presentation(Mode) switch
        {
            DecorationPresentation.Dense => "DenseTemplate",
            DecorationPresentation.Wide => "WideTemplate",
            DecorationPresentation.Art => "ArtTemplate",
            _ => "CompactTemplate"
        });
        DecorationList.SelectedItem = items.FirstOrDefault(item => item.Entry.Id == selectedId) ?? items.FirstOrDefault();
        EmptyText.Text = DecorationPickerPolicy.EmptyMessage(Mode, SearchBox.Text, catalog.IsAvailable);
        EmptyText.Visibility = items.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        AddItemButton.Visibility = Mode == DecorationNavigation.MyItems ? Visibility.Visible : Visibility.Collapsed;
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        if (DecorationList.SelectedItem is not PickerItem item)
        {
            SelectedText.Text = ""; PreviewStatus.Text = ""; InsertButton.IsEnabled = false; return;
        }
        var result = preview(item.Entry.Content);
        var lines = item.Entry.Content.Count(c => c == '\n') + 1;
        SelectedText.Text = item.Entry.Name + (lines > 1 ? $" · {lines} lines" : "");
        PreviewStatus.Text = !result.CanInsert ? "Not enough editor space for this item."
            : result.WouldTruncate ? "This composition will be truncated in VRChat."
            : $"{result.VisibleUnits} / {result.Limit} visible units";
        PreviewStatus.SetResourceReference(TextBlock.ForegroundProperty,
            !result.CanInsert || result.WouldTruncate ? "WarningBrush" : "MutedBrush");
        InsertButton.IsEnabled = result.CanInsert;
    }

    private void ModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!initialized) return;
        EditorPanel.Visibility = Visibility.Collapsed;
        RefreshItems();
    }
    private void SearchChanged(object sender, TextChangedEventArgs e) => RefreshItems();
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
        var result = preview(item.Entry.Content);
        if (!result.CanInsert) { MutationStatus.Text = "Not enough editor space for this item."; return; }
        SelectedContent = item.Entry.Content;
        DialogResult = true;
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
        var saved = saveState(state);
        MutationStatus.Text = saved ? success : "Change works this session but could not be saved.";
        RefreshItems(selectedId);
    }
}
