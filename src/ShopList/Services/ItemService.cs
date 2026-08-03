using System.Diagnostics;
using ShopList.Models;
using Ordering = Supabase.Postgrest.Constants.Ordering;

namespace ShopList.Services;

public sealed record LoadItemsResult(
    bool Succeeded,
    IReadOnlyList<ShoppingItem> Items,
    string? ErrorMessage = null);

public sealed record ItemOperationResult(
    bool Succeeded,
    string? ErrorMessage = null);

public sealed class ItemService
{
    private readonly SupabaseService _supabaseService;

    public ItemService(SupabaseService supabaseService)
    {
        _supabaseService = supabaseService;
    }

    public async Task<LoadItemsResult> LoadItemsAsync(Guid listId)
    {
        if (listId == Guid.Empty)
        {
            return LoadFailure("The selected shopping list is invalid.");
        }

        try
        {
            await _supabaseService.InitializeAsync();

            if (_supabaseService.Client.Auth.CurrentSession is null)
            {
                return LoadFailure(
                    "Your session has expired. Please sign in again.");
            }

            var response = await _supabaseService.Client
                .From<ShoppingItem>()
                .Where(item => item.ListId == listId)
                .Where(item => item.DeletedAt == null)
                .Order(
                    item => item.IsCompleted,
                    Ordering.Ascending)
                .Order(
                    item => item.UpdatedAt,
                    Ordering.Descending)
                .Get();

            return new(true, response.Models);
        }
        catch
        {
            return LoadFailure(
                "Couldn't load the items. Check your connection and try again.");
        }
    }

    public async Task<ItemOperationResult> AddItemAsync(
        Guid listId,
        string name,
        string? quantity,
        string? note)
    {
        name = name.Trim();
        quantity = NormalizeOptionalText(quantity);
        note = NormalizeOptionalText(note);

        if (listId == Guid.Empty)
        {
            return new(false, "The selected shopping list is invalid.");
        }

        if (name.Length is < 1 or > 120)
        {
            return new(
                false,
                "Item name must contain between 1 and 120 characters.");
        }

        if (quantity?.Length > 60)
        {
            return new(
                false,
                "Quantity must contain at most 60 characters.");
        }

        if (note?.Length > 500)
        {
            return new(
                false,
                "Note must contain at most 500 characters.");
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

            await _supabaseService.Client
                .From<ShoppingItem>()
                .Insert(new ShoppingItem
                {
                    ListId = listId,
                    Name = name,
                    Quantity = quantity,
                    Note = note,
                    IsCompleted = false
                });

            return new(true);
        }
        catch
        {
            return new(
                false,
                "Couldn't add the item. Check your connection and try again.");
        }
    }

    public async Task<ItemOperationResult> SetCompletedAsync(
        ShoppingItem item,
        bool isCompleted)
    {
        if (item.Id == Guid.Empty || item.ListId == Guid.Empty)
        {
            return new(false, "The selected item is invalid.");
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

            await _supabaseService.Client
                .From<ShoppingItem>()
                .Where(candidate => candidate.Id == item.Id)
                .Where(candidate => candidate.ListId == item.ListId)
                .Where(candidate => candidate.DeletedAt == null)
                .Set(candidate => candidate.IsCompleted, isCompleted)
                .Update();

            return new(true);
        }
        catch (Exception exception)
        {
#if DEBUG
            Debug.WriteLine("ItemService.SetCompletedAsync failed:");
            Debug.WriteLine(exception.ToString());
#endif
            return new(
                false,
                "Couldn't update the item. Check your connection and try again.");
        }
    }

    public async Task<ItemOperationResult> SoftDeleteAsync(
        ShoppingItem item)
    {
        if (item.Id == Guid.Empty || item.ListId == Guid.Empty)
        {
            return new(false, "The selected item is invalid.");
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

            await _supabaseService.Client
                .From<ShoppingItem>()
                .Where(candidate => candidate.Id == item.Id)
                .Where(candidate => candidate.ListId == item.ListId)
                .Where(candidate => candidate.DeletedAt == null)
                .Set(
                    candidate => candidate.DeletedAt!,
                    DateTimeOffset.UtcNow)
                .Update();

            return new(true);
        }
        catch (Exception exception)
        {
#if DEBUG
            Debug.WriteLine("ItemService.SoftDeleteAsync failed:");
            Debug.WriteLine(exception.ToString());
#endif
            return new(
                false,
                "Couldn't delete the item. Check your connection and try again.");
        }
    }

    private static string? NormalizeOptionalText(string? value)
    {
        value = value?.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static LoadItemsResult LoadFailure(string message)
    {
        return new(false, Array.Empty<ShoppingItem>(), message);
    }
}
