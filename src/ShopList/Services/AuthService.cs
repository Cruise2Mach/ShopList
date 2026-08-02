using Supabase.Gotrue;

namespace ShopList.Services;

public sealed record AuthOperationResult(
    bool Succeeded,
    string? ErrorMessage = null,
    bool RequiresEmailConfirmation = false);

public sealed class AuthService
{
    private readonly SupabaseService _supabaseService;

    public AuthService(SupabaseService supabaseService)
    {
        _supabaseService = supabaseService;
    }

    public bool IsSignedIn =>
        _supabaseService.Client.Auth.CurrentSession is not null;

    public async Task<AuthOperationResult> SignUpAsync(
        string displayName,
        string email,
        string password)
    {
        displayName = displayName.Trim();
        email = email.Trim().ToLowerInvariant();

        if (displayName.Length is < 1 or > 80)
        {
            return new(
                false,
                "Display name must contain between 1 and 80 characters.");
        }

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            return new(false, "Enter a valid email address.");
        }

        if (password.Length < 8)
        {
            return new(
                false,
                "Password must contain at least 8 characters.");
        }

        try
        {
            await _supabaseService.InitializeAsync();

            var options = new SignUpOptions
            {
                Data = new Dictionary<string, object>
                {
                    ["display_name"] = displayName
                }
            };

            var session = await _supabaseService.Client.Auth.SignUp(
                email,
                password,
                options);

            return new(
                true,
                RequiresEmailConfirmation: session is null);
        }
        catch (Exception exception)
        {
            return new(false, exception.Message);
        }
    }

    public async Task<AuthOperationResult> SignInAsync(
        string email,
        string password)
    {
        email = email.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
        {
            return new(false, "Email and password are required.");
        }

        try
        {
            await _supabaseService.InitializeAsync();

            var session = await _supabaseService.Client.Auth.SignIn(
                email,
                password);

            return session is null
                ? new(false, "The server did not return a session.")
                : new(true);
        }
        catch (Exception exception)
        {
            return new(false, exception.Message);
        }
    }

    public async Task SignOutAsync()
    {
        await _supabaseService.InitializeAsync();
        await _supabaseService.Client.Auth.SignOut();
    }
}