using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace ShopList.Models;

[Table("shopping_items")]
public sealed class ShoppingItem : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [Column("list_id")]
    public Guid ListId { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("quantity")]
    public string? Quantity { get; set; }

    [Column("unit", ignoreOnInsert: true)]
    public string? Unit { get; set; }

    [Column("note")]
    public string? Note { get; set; }

    [Column("is_completed")]
    public bool IsCompleted { get; set; }

    [Column("completed_by", ignoreOnInsert: true)]
    public Guid? CompletedBy { get; set; }

    [Column("completed_at", ignoreOnInsert: true)]
    public DateTimeOffset? CompletedAt { get; set; }

    [Column("created_by", ignoreOnInsert: true)]
    public Guid CreatedBy { get; set; }

    [Column("created_at", ignoreOnInsert: true)]
    public DateTimeOffset CreatedAt { get; set; }

    [Column("updated_at", ignoreOnInsert: true)]
    public DateTimeOffset UpdatedAt { get; set; }

    [Column("deleted_at", ignoreOnInsert: true)]
    public DateTimeOffset? DeletedAt { get; set; }

    [JsonIgnore]
    public bool HasQuantity => !string.IsNullOrWhiteSpace(Quantity);

    [JsonIgnore]
    public bool HasNote => !string.IsNullOrWhiteSpace(Note);
}
