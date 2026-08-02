using ShopList.Configuration;
using SupabaseClient = Supabase.Client;

namespace ShopList.Services;

public sealed class SupabaseService
{
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _isInitialized;

    public SupabaseClient Client { get; }

    public SupabaseService()
    {
        var options = new Supabase.SupabaseOptions
        {
            AutoRefreshToken = true,
            AutoConnectRealtime = false
        };

        Client = new SupabaseClient(
            SupabaseSettings.Url,
            SupabaseSettings.PublishableKey,
            options);
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        await _initializationLock.WaitAsync();

        try
        {
            if (_isInitialized)
            {
                return;
            }

            await Client.InitializeAsync();
            _isInitialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }
}