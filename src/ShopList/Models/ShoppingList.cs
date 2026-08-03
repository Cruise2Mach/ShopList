using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace ShopList.Models;

[Table("shopping_lists")]
public sealed class ShoppingList : BaseModel
{
    [PrimaryKey("id")]
    public Guid Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("created_by")]
    public Guid CreatedBy { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    [Column("archived_at")]
    public DateTimeOffset? ArchivedAt { get; set; }
}
