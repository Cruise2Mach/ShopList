using Microsoft.Maui.Storage;
using Supabase.Gotrue;

namespace ShopList.Services;

public sealed record StoredSessionTokens(
    string AccessToken,
    string RefreshToken);

public sealed class SessionStorageService
{
    private const string AccessTokenKey = "supabase_access_token";
    private const string RefreshTokenKey = "supabase_refresh_token";

    private readonly ISecureStorage _secureStorage;

    public SessionStorageService(ISecureStorage secureStorage)
    {
        _secureStorage = secureStorage;
    }

    public async Task SaveAsync(Session session)
    {
        if (string.IsNullOrWhiteSpace(session.AccessToken) ||
            string.IsNullOrWhiteSpace(session.RefreshToken))
        {
            Clear();
            return;
        }

        try
        {
            await _secureStorage.SetAsync(
                AccessTokenKey,
                session.AccessToken);

            await _secureStorage.SetAsync(
                RefreshTokenKey,
                session.RefreshToken);
        }
        catch
        {
            Clear();
            throw;
        }
    }

    public async Task<StoredSessionTokens?> LoadAsync()
    {
        var accessToken = await _secureStorage.GetAsync(AccessTokenKey);
        var refreshToken = await _secureStorage.GetAsync(RefreshTokenKey);

        if (string.IsNullOrWhiteSpace(accessToken) ||
            string.IsNullOrWhiteSpace(refreshToken))
        {
            Clear();
            return null;
        }

        return new StoredSessionTokens(accessToken, refreshToken);
    }

    public void Clear()
    {
        _secureStorage.Remove(AccessTokenKey);
        _secureStorage.Remove(RefreshTokenKey);
    }
}
