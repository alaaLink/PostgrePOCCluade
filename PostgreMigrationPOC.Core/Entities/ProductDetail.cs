namespace PostgreMigrationPOC.Core.Entities;

public class ProductDetail
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string? DetailedDescription { get; set; }
    public string? Specifications { get; set; }
    public string? ManufacturerInfo { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public virtual Product Product { get; set; } = null!;
}