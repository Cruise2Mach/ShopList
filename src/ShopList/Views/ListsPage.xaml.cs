using System.Collections.ObjectModel;
using ShopList.Models;
using ShopList.Services;

namespace ShopList.Views;

public partial class ListsPage : ContentPage
{
    private readonly ListService _listService;
    private readonly AuthService _authService;
    private readonly ObservableCollection<ShoppingList> _lists = new();
    private bool _hasLoaded;
    private bool _hasSuccessfulLoad;
    private bool _isBusy;
    private bool _isNavigating;

    public ListsPage(
        ListService listService,
        AuthService authService)
    {
        InitializeComponent();
        _listService = listService;
        _authService = authService;
        ListsCollectionView.ItemsSource = _lists;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_hasLoaded)
        {
            return;
        }

        _hasLoaded = true;
        await LoadListsAsync();
    }

    private async Task LoadListsAsync()
    {
        if (_isBusy)
        {
            return;
        }

        SetBusy(true);
        HideMessage();

        try
        {
            await ReloadListsAsync();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnCreateListClicked(
        object? sender,
        EventArgs e)
    {
        await CreateListAsync();
    }

    private async void OnNewListNameCompleted(
        object? sender,
        EventArgs e)
    {
        await CreateListAsync();
    }

    private async Task CreateListAsync()
    {
        if (_isBusy)
        {
            return;
        }

        SetBusy(true);
        HideMessage();

        try
        {
            var result = await _listService.CreateListAsync(
                NewListNameEntry.Text ?? string.Empty);

            if (!result.Succeeded)
            {
                ShowMessage(
                    result.ErrorMessage ?? "Couldn't create the list.",
                    isError: true);

                return;
            }

            NewListNameEntry.Text = string.Empty;

            if (await ReloadListsAsync())
            {
                ShowMessage("List created.", isError: false);
            }
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task<bool> ReloadListsAsync()
    {
        var result = await _listService.LoadListsAsync();

        if (!result.Succeeded)
        {
            _hasSuccessfulLoad = false;
            ShowMessage(
                result.ErrorMessage ?? "Couldn't load your lists.",
                isError: true);

            return false;
        }

        _lists.Clear();

        foreach (var list in result.Lists)
        {
            _lists.Add(list);
        }

        _hasSuccessfulLoad = true;
        UpdateListVisibility();
        return true;
    }

    private async void OnListSelectionChanged(
        object? sender,
        SelectionChangedEventArgs e)
    {
        if (_isBusy || _isNavigating)
        {
            ListsCollectionView.SelectedItem = null;
            return;
        }

        if (e.CurrentSelection.FirstOrDefault() is not ShoppingList list)
        {
            return;
        }

        _isNavigating = true;

        try
        {
            var parameters = new ShellNavigationQueryParameters
            {
                ["listId"] = list.Id.ToString("D"),
                ["listName"] = list.Name
            };

            await Shell.Current.GoToAsync(
                "shopping-list",
                parameters);
        }
        finally
        {
            ListsCollectionView.SelectedItem = null;
            _isNavigating = false;
        }
    }

    private async void OnSignOutClicked(object? sender, EventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        SetBusy(true);
        HideMessage();

        try
        {
            await _authService.SignOutAsync();
            _lists.Clear();
            _hasLoaded = false;
            _hasSuccessfulLoad = false;

            await Shell.Current.GoToAsync("//login");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool isBusy)
    {
        _isBusy = isBusy;
        LoadingIndicator.IsVisible = isBusy;
        LoadingIndicator.IsRunning = isBusy;
        NewListNameEntry.IsEnabled = !isBusy;
        CreateListButton.IsEnabled = !isBusy;
        SignOutButton.IsEnabled = !isBusy;
        ListsCollectionView.IsEnabled = !isBusy;
        UpdateListVisibility();
    }

    private void UpdateListVisibility()
    {
        ListsCollectionView.IsVisible = _lists.Count > 0;
        EmptyStateLabel.IsVisible =
            !_isBusy &&
            _hasSuccessfulLoad &&
            _lists.Count == 0;
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
