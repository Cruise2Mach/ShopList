using ShopList.Services;


namespace ShopList.Views;

public partial class RegisterPage : ContentPage
{
    private readonly AuthService _authService;
    private bool _isBusy;

    public RegisterPage(AuthService authService)
    {
        InitializeComponent();
        _authService = authService;
    }

    private async void OnRegisterClicked(object? sender, EventArgs e)
    {
        await RegisterAsync();
    }

    private async void OnConfirmPasswordCompleted(
        object? sender,
        EventArgs e)
    {
        await RegisterAsync();
    }

    private async Task RegisterAsync()
    {
        if (_isBusy)
        {
            return;
        }

        HideMessage();

        if (PasswordEntry.Text != ConfirmPasswordEntry.Text)
        {
            ShowMessage("Passwords do not match.", isError: true);
            return;
        }

        SetBusy(true);

        try
        {
            var result = await _authService.SignUpAsync(
                DisplayNameEntry.Text ?? string.Empty,
                EmailEntry.Text ?? string.Empty,
                PasswordEntry.Text ?? string.Empty);

            if (!result.Succeeded)
            {
                ShowMessage(
                    result.ErrorMessage ?? "Registration failed.",
                    isError: true);

                return;
            }

            PasswordEntry.Text = string.Empty;
            ConfirmPasswordEntry.Text = string.Empty;

            if (result.RequiresEmailConfirmation)
            {
                ShowMessage(
                    "Account created. Check your email to confirm it.",
                    isError: false);

                return;
            }

            await Shell.Current.GoToAsync("//lists");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnBackToLoginClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private void SetBusy(bool isBusy)
    {
        _isBusy = isBusy;
        BusyIndicator.IsVisible = isBusy;
        BusyIndicator.IsRunning = isBusy;
        RegisterButton.IsEnabled = !isBusy;
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