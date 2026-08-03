using ShopList.Services;

namespace ShopList.Views;

public partial class StartupPage : ContentPage
{
    private readonly AuthService _authService;
    private bool _hasStarted;

    public StartupPage(AuthService authService)
    {
        InitializeComponent();
        _authService = authService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_hasStarted)
        {
            return;
        }

        _hasStarted = true;

        await Task.Delay(100);

        var isSignedIn = await _authService.RestoreSessionAsync();
        var destination = isSignedIn ? "//lists" : "//login";

        await Shell.Current.GoToAsync(destination, animate: false);
    }
}
