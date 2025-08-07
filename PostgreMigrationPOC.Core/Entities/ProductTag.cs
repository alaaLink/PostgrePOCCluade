namespace PostgreMigrationPOC.Core.Entities;

public class ProductTag
{
    public int ProductId { get; set; }
    public int TagId { get; set; }
    public DateTime AssignedAt { get; set; }
    
    public virtual Product Product { get; set; } = null!;
    public virtual Tag Tag { get; set; } = null!;
}