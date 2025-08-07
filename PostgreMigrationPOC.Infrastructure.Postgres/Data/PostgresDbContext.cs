using Microsoft.EntityFrameworkCore;
using PostgreMigrationPOC.Core.Entities;

namespace PostgreMigrationPOC.Infrastructure.Postgres.Data;

public class PostgresDbContext : DbContext
{
    public PostgresDbContext(DbContextOptions<PostgresDbContext> options) : base(options)
    {
    }

    public DbSet<Product> Products { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public DbSet<ProductTag> ProductTags { get; set; }
    public DbSet<ProductDetail> ProductDetails { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Category configuration
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasColumnType("timestamp without time zone").HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // Product configuration with PostgreSQL type mappings
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            // Map SQL Server specific types to PostgreSQL equivalents
            entity.Property(p => p.DecimalPrice).HasColumnType("decimal(18,2)");
            entity.Property(p => p.MoneyField).HasColumnType("money"); // PostgreSQL has money type
            entity.Property(p => p.SmallMoneyField).HasColumnType("money");
            entity.Property(p => p.FloatField).HasColumnType("double precision");
            entity.Property(p => p.RealField).HasColumnType("real");
            
            // String mappings
            entity.Property(p => p.VarcharField).HasColumnType("varchar(100)");
            entity.Property(p => p.NvarcharField).HasColumnType("text"); // PostgreSQL uses text for Unicode
            entity.Property(p => p.CharField).HasColumnType("char(10)");
            entity.Property(p => p.NcharField).HasColumnType("char(5)");
            entity.Property(p => p.TextField).HasColumnType("text");
            entity.Property(p => p.NtextField).HasColumnType("text");
            
            // Date/Time mappings - using timestamp without time zone for better compatibility
            entity.Property(p => p.DateTimeField).HasColumnType("timestamp without time zone");
            entity.Property(p => p.DateTime2Field).HasColumnType("timestamp without time zone");
            entity.Property(p => p.DateField).HasColumnType("date");
            entity.Property(p => p.TimeField).HasColumnType("time without time zone");
            entity.Property(p => p.DateTimeOffsetField).HasColumnType("timestamp with time zone");
            entity.Property(p => p.SmallDateTimeField).HasColumnType("timestamp without time zone");
            
            // Binary mappings
            entity.Property(p => p.BinaryField).HasColumnType("bytea");
            entity.Property(p => p.VarbinaryField).HasColumnType("bytea");
            
            // GUID mapping
            entity.Property(p => p.GuidField).HasColumnType("uuid");
            
            // XML mapping
            entity.Property(p => p.XmlField).HasColumnType("xml");
            
            // Special SQL Server types mapped to PostgreSQL equivalents
            entity.Property(p => p.HierarchyIdField).HasColumnType("text"); // Store as text instead of ltree
            entity.Property(p => p.GeographyField).HasColumnType("text"); // Store as text instead of geography
            entity.Property(p => p.GeometryField).HasColumnType("text"); // Store as text instead of geometry
            
            // Rowversion equivalent - PostgreSQL uses xmin system column or custom bytea with trigger
            entity.Property(p => p.RowVersion).HasColumnType("bytea").IsRowVersion();
            
            // Relationships
            entity.HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ProductDetail configuration
        modelBuilder.Entity<ProductDetail>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ProductId).IsUnique();
            
            // DateTime mappings for consistency
            entity.Property(pd => pd.CreatedAt).HasColumnType("timestamp without time zone");
            entity.Property(pd => pd.UpdatedAt).HasColumnType("timestamp without time zone");
            
            entity.HasOne(pd => pd.Product)
                .WithOne(p => p.ProductDetail)
                .HasForeignKey<ProductDetail>(pd => pd.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Tag configuration
        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(200);
            entity.Property(e => e.CreatedAt).HasColumnType("timestamp without time zone");
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // ProductTag configuration (many-to-many)
        modelBuilder.Entity<ProductTag>(entity =>
        {
            entity.HasKey(pt => new { pt.ProductId, pt.TagId });
            
            entity.HasOne(pt => pt.Product)
                .WithMany(p => p.ProductTags)
                .HasForeignKey(pt => pt.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(pt => pt.Tag)
                .WithMany(t => t.ProductTags)
                .HasForeignKey(pt => pt.TagId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.Property(pt => pt.AssignedAt).HasColumnType("timestamp without time zone").HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // This will be overridden by dependency injection, but useful for migrations
            optionsBuilder.UseNpgsql("Host=localhost;Database=PostgreMigrationPOC;Username=postgres;Password=Dev@123456");
        }
    }
}