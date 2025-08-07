namespace PostgreMigrationPOC.Core.Entities;

public class Tag
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    
    public virtual ICollection<ProductTag> ProductTags { get; set; } = new List<ProductTag>();
}