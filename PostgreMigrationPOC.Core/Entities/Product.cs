using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PostgreMigrationPOC.Core.Entities;

public class Product
{
    // Integer types
    public int Id { get; set; }
    public short SmallIntField { get; set; }
    public long BigIntField { get; set; }
    public byte TinyIntField { get; set; }

    // Decimal types
    [Column(TypeName = "decimal(18,2)")]
    public decimal DecimalPrice { get; set; }
    
    [Column(TypeName = "money")]
    public decimal MoneyField { get; set; }
    
    [Column(TypeName = "smallmoney")]
    public decimal SmallMoneyField { get; set; }
    
    [Column(TypeName = "float")]
    public double FloatField { get; set; }
    
    [Column(TypeName = "real")]
    public float RealField { get; set; }

    // String types
    [Column(TypeName = "varchar(100)")]
    public string VarcharField { get; set; } = string.Empty;
    
    [Column(TypeName = "nvarchar(200)")]
    public string NvarcharField { get; set; } = string.Empty;
    
    [Column(TypeName = "char(10)")]
    public string CharField { get; set; } = string.Empty;
    
    [Column(TypeName = "nchar(5)")]
    public string NcharField { get; set; } = string.Empty;
    
    [Column(TypeName = "text")]
    public string? TextField { get; set; }
    
    [Column(TypeName = "ntext")]
    public string? NtextField { get; set; }

    // Date/Time types
    [Column(TypeName = "datetime")]
    public DateTime DateTimeField { get; set; }
    
    [Column(TypeName = "datetime2")]
    public DateTime DateTime2Field { get; set; }
    
    [Column(TypeName = "date")]
    public DateOnly DateField { get; set; }
    
    [Column(TypeName = "time")]
    public TimeOnly TimeField { get; set; }
    
    [Column(TypeName = "datetimeoffset")]
    public DateTimeOffset DateTimeOffsetField { get; set; }
    
    [Column(TypeName = "smalldatetime")]
    public DateTime SmallDateTimeField { get; set; }

    // Binary types
    [Column(TypeName = "binary(16)")]
    public byte[]? BinaryField { get; set; }
    
    [Column(TypeName = "varbinary(max)")]
    public byte[]? VarbinaryField { get; set; }

    // Boolean type
    public bool BooleanField { get; set; }

    // GUID type
    public Guid GuidField { get; set; }

    // XML type
    [Column(TypeName = "xml")]
    public string? XmlField { get; set; }

    // SQL Server specific types
    [Column(TypeName = "hierarchyid")]
    public string? HierarchyIdField { get; set; }
    
    [Column(TypeName = "geography")]
    public string? GeographyField { get; set; }
    
    [Column(TypeName = "geometry")]
    public string? GeometryField { get; set; }

    // Rowversion/timestamp
    [Timestamp]
    public byte[]? RowVersion { get; set; }

    // Foreign keys
    public int CategoryId { get; set; }
    
    // Navigation properties
    public virtual Category Category { get; set; } = null!;
    public virtual ProductDetail? ProductDetail { get; set; }
    public virtual ICollection<ProductTag> ProductTags { get; set; } = new List<ProductTag>();
}