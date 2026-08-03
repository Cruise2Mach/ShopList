using System.Collections.ObjectModel;
using ShopList.Models;
using ShopList.Services;

namespace ShopList.Views;

public partial class ShoppingListPage : ContentPage, IQueryAttributable
{
    private readonly ItemService _itemService;
    private readonly ObservableCollection<ShoppingItem> _items = new();
    private Guid _listId;
    private bool _hasLoaded;
    private bool _hasSuccessfulLoad;
    private bool _isBusy;

    public ShoppingListPage(ItemService itemService)
    {
        InitializeComponent();
        _itemService = itemService;
        ItemsCollectionView.ItemsSource = _items;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _listId = Guid.Empty;

        if (query.TryGetValue("listId", out var listIdValue))
        {
            Guid.TryParse(listIdValue?.ToString(), out _listId);
        }

        var listName = query.TryGetValue("listName", out var listNameValue)
            ? listNameValue as string
            : null;

        listName = string.IsNullOrWhiteSpace(listName)
            ? "Shopping list"
            : listName.Trim();

        Title = listName;
        ListNameLabel.Text = listName;
        _hasLoaded = false;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_hasLoaded)
        {
            return;
        }

        _hasLoaded = true;
        await LoadItemsAsync();
    }

    private async Task LoadItemsAsync()
    {
        if (_isBusy)
        {
            return;
        }

        SetBusy(true);
        HideMessage();

        try
        {
            await ReloadItemsAsync();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnAddItemClicked(object? sender, EventArgs e)
    {
        await AddItemAsync();
    }

    private async void OnNoteCompleted(object? sender, EventArgs e)
    {
        await AddItemAsync();
    }

    private async Task AddItemAsync()
    {
        if (_isBusy)
        {
            return;
        }

        SetBusy(true);
        HideMessage();

        try
        {
            var result = await _itemService.AddItemAsync(
                _listId,
                ItemNameEntry.Text ?? string.Empty,
                QuantityEntry.Text,
                NoteEntry.Text);

            if (!result.Succeeded)
            {
                ShowMessage(
                    result.ErrorMessage ?? "Couldn't add the item.",
                    isError: true);

                return;
            }

            ItemNameEntry.Text = string.Empty;
            QuantityEntry.Text = string.Empty;
            NoteEntry.Text = string.Empty;

            if (await ReloadItemsAsync())
            {
                ShowMessage("Item added.", isError: false);
            }
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnCompletedChanged(
        object? sender,
        CheckedChangedEventArgs e)
    {
        if (sender is not CheckBox checkBox ||
            checkBox.BindingContext is not ShoppingItem item ||
            e.Value == item.IsCompleted)
        {
            return;
        }

        if (_isBusy)
        {
            checkBox.IsChecked = item.IsCompleted;
            return;
        }

        SetBusy(true);
        HideMessage();

        try
        {
            var result = await _itemService.SetCompletedAsync(
                item,
                e.Value);

            if (!result.Succeeded)
            {
                checkBox.IsChecked = item.IsCompleted;
                ShowMessage(
                    result.ErrorMessage ?? "Couldn't update the item.",
                    isError: true);

                return;
            }

            if (await ReloadItemsAsync())
            {
                ShowMessage("Item updated.", isError: false);
            }
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnDeleteItemClicked(object? sender, EventArgs e)
    {
        if (_isBusy ||
            sender is not Button button ||
            button.CommandParameter is not ShoppingItem item)
        {
            return;
        }

        SetBusy(true);
        HideMessage();

        try
        {
            var result = await _itemService.SoftDeleteAsync(item);

            if (!result.Succeeded)
            {
                ShowMessage(
                    result.ErrorMessage ?? "Couldn't delete the item.",
                    isError: true);

                return;
            }

            if (await ReloadItemsAsync())
            {
                ShowMessage("Item deleted.", isError: false);
            }
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task<bool> ReloadItemsAsync()
    {
        var result = await _itemService.LoadItemsAsync(_listId);

        if (!result.Succeeded)
        {
            _hasSuccessfulLoad = false;
            ShowMessage(
                result.ErrorMessage ?? "Couldn't load the items.",
                isError: true);

            return false;
        }

        _items.Clear();

        foreach (var item in result.Items)
        {
            _items.Add(item);
        }

        _hasSuccessfulLoad = true;
        UpdateItemVisibility();
        return true;
    }

    private void SetBusy(bool isBusy)
    {
        _isBusy = isBusy;
        LoadingIndicator.IsVisible = isBusy;
        LoadingIndicator.IsRunning = isBusy;
        ItemNameEntry.IsEnabled = !isBusy;
        QuantityEntry.IsEnabled = !isBusy;
        NoteEntry.IsEnabled = !isBusy;
        AddItemButton.IsEnabled = !isBusy;
        ItemsCollectionView.IsEnabled = !isBusy;
        UpdateItemVisibility();
    }

    private void UpdateItemVisibility()
    {
        ItemsCollectionView.IsVisible = _items.Count > 0;
        EmptyStateLabel.IsVisible =
            !_isBusy &&
            _hasSuccessfulLoad &&
            _items.Count == 0;
    }

    private void ShowMessage(string message, bool isError)
    {
        MessageLabel.Text = message;
        MessageLabel.TextColor = isError
            ? Color.FromArgb("#D32F2F")
            : Color.FromArgb("#2E7D32");

        MessageLabel.IsVisible = true;
    }

    private void HideMessage()
    {
        MessageLabel.Text = string.Empty;
        MessageLabel.IsVisible = false;
    }
}
