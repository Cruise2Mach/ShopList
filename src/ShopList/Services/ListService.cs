using ShopList.Models;
using Ordering = Supabase.Postgrest.Constants.Ordering;

namespace ShopList.Services;

public sealed record LoadListsResult(
    bool Succeeded,
    IReadOnlyList<ShoppingList> Lists,
    string? ErrorMessage = null);

public sealed record ListOperationResult(
    bool Succeeded,
    string? ErrorMessage = null);

public sealed class ListService
{
    private readonly SupabaseService _supabaseService;

    public ListService(SupabaseService supabaseService)
    {
        _supabaseService = supabaseService;
    }

    public async Task<LoadListsResult> LoadListsAsync()
    {
        try
        {
            await _supabaseService.InitializeAsync();

            if (_supabaseService.Client.Auth.CurrentSession is null)
            {
                return new(
                    false,
                    Array.Empty<ShoppingList>(),
                    "Your session has expired. Please sign in again.");
            }

            var response = await _supabaseService.Client
                .From<ShoppingList>()
                .Where(list => list.ArchivedAt == null)
                .Order(
                    list => list.UpdatedAt,
                    Ordering.Descending)
                .Get();

            return new(true, response.Models);
        }
        catch
        {
            return new(
                false,
                Array.Empty<ShoppingList>(),
                "Couldn't load your lists. Check your connection and try again.");
        }
    }

    public async Task<ListOperationResult> CreateListAsync(string name)
    {
        name = name.Trim();

        if (name.Length is < 1 or > 100)
        {
            return new(
                false,
                "List name must contain between 1 and 100 characters.");
        }

        try
        {
            await _supabaseService.InitializeAsync();

            if (_supabaseService.Client.Auth.CurrentSession is null)
            {
                return new(
                    false,
                    "Your session has expired. Please sign in again.");
            }

            await _supabaseService.Client.Rpc(
                "create_shopping_list",
                new Dictionary<string, object>
                {
                    ["list_name"] = name
                });

            return new(true);
        }
        catch
        {
            return new(
                false,
                "Couldn't create the list. Check your connection and try again.");
        }
    }
}
