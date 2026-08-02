using ShopList.Services;

namespace ShopList.Views;

public partial class LoginPage : ContentPage
{
    private readonly AuthService _authService;
    private bool _isBusy;

    public LoginPage(AuthService authService)
    {
        InitializeComponent();
        _authService = authService;
    }

    private async void OnSignInClicked(object? sender, EventArgs e)
    {
        await SignInAsync();
    }

    private async void OnPasswordCompleted(object? sender, EventArgs e)
    {
        await SignInAsync();
    }

    private async Task SignInAsync()
    {
        if (_isBusy)
        {
            return;
        }

        SetBusy(true);
        HideError();

        try
        {
            var result = await _authService.SignInAsync(
                EmailEntry.Text ?? string.Empty,
                PasswordEntry.Text ?? string.Empty);

            if (!result.Succeeded)
            {
                ShowError(result.ErrorMessage ?? "Sign in failed.");
                return;
            }

            PasswordEntry.Text = string.Empty;

            await Shell.Current.GoToAsync("//lists");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnRegisterClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("register");
    }

    private void SetBusy(bool isBusy)
    {
        _isBusy = isBusy;
        BusyIndicator.IsVisible = isBusy;
        BusyIndicator.IsRunning = isBusy;
        SignInButton.IsEnabled = !isBusy;
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }

    private void HideError()
    {
        ErrorLabel.Text = string.Empty;
        ErrorLabel.IsVisible = false;
    }
}