using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using ShopList.Services;
using ShopList.Views;


namespace ShopList;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont(
                    "OpenSans-Regular.ttf",
                    "OpenSansRegular");

                fonts.AddFont(
                    "OpenSans-Semibold.ttf",
                    "OpenSansSemibold");
            });

        builder.Services.AddSingleton<ISecureStorage>(SecureStorage.Default);
        builder.Services.AddSingleton<SessionStorageService>();
        builder.Services.AddSingleton<SupabaseService>();
        builder.Services.AddSingleton<AuthService>();
        builder.Services.AddSingleton<ListService>();

        builder.Services.AddTransient<StartupPage>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<RegisterPage>();
        builder.Services.AddTransient<ListsPage>();
#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
