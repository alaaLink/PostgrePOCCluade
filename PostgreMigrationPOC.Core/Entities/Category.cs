namespace PostgreMigrationPOC.Core.Entities;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
    
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}