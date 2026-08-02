using ShopList.Services;

namespace ShopList.Views;

public partial class ListsPage : ContentPage
{
    private readonly AuthService _authService;
    private bool _isBusy;

    public ListsPage(AuthService authService)
    {
        InitializeComponent();
        _authService = authService;
    }

    private async void OnSignOutClicked(object? sender, EventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        _isBusy = true;
        SignOutButton.IsEnabled = false;

        try
        {
            await _authService.SignOutAsync();
            await Shell.Current.GoToAsync("//login");
        }
        finally
        {
            _isBusy = false;
            SignOutButton.IsEnabled = true;
        }
    }
}