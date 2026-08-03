using ShopList.Configuration;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using AuthState = Supabase.Gotrue.Constants.AuthState;
using SupabaseClient = Supabase.Client;

namespace ShopList.Services;

public sealed class SupabaseService
{
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private readonly SemaphoreSlim _sessionPersistenceLock = new(1, 1);
    private readonly SessionStorageService _sessionStorageService;
    private bool _isInitialized;

    public SupabaseClient Client { get; }

    public SupabaseService(SessionStorageService sessionStorageService)
    {
        _sessionStorageService = sessionStorageService;

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

            try
            {
                var storedTokens =
                    await _sessionStorageService.LoadAsync();

                if (storedTokens is not null)
                {
                    var session = await Client.Auth.SetSession(
                        storedTokens.AccessToken,
                        storedTokens.RefreshToken,
                        forceAccessTokenRefresh: true);

                    if (session is null)
                    {
                        await ClearPersistedSessionAsync();
                    }
                    else
                    {
                        await PersistCurrentSessionAsync();
                    }
                }
            }
            catch
            {
                await ClearPersistedSessionAsync();
            }

            Client.Auth.AddStateChangedListener(OnAuthStateChanged);
            _isInitialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    public async Task PersistCurrentSessionAsync()
    {
        await _sessionPersistenceLock.WaitAsync();

        try
        {
            var session = Client.Auth.CurrentSession;

            if (session is null)
            {
                _sessionStorageService.Clear();
                return;
            }

            await _sessionStorageService.SaveAsync(session);
        }
        finally
        {
            _sessionPersistenceLock.Release();
        }
    }

    public async Task ClearPersistedSessionAsync()
    {
        await _sessionPersistenceLock.WaitAsync();

        try
        {
            _sessionStorageService.Clear();
        }
        finally
        {
            _sessionPersistenceLock.Release();
        }
    }

    private async void OnAuthStateChanged(
        IGotrueClient<User, Session> sender,
        AuthState state)
    {
        try
        {
            if (state == AuthState.SignedOut)
            {
                await ClearPersistedSessionAsync();
                return;
            }

            if (sender.CurrentSession is not null)
            {
                await PersistCurrentSessionAsync();
            }
        }
        catch
        {
            try
            {
                await ClearPersistedSessionAsync();
            }
            catch
            {
                // Auth-state callbacks cannot propagate storage failures.
            }
        }
    }
}
