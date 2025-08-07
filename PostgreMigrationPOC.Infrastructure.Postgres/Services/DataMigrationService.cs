using Microsoft.EntityFrameworkCore;
using PostgreMigrationPOC.Core.Entities;
using PostgreMigrationPOC.Infrastructure.SqlServer.Data;
using PostgreMigrationPOC.Infrastructure.SqlServer.Services;
using PostgreMigrationPOC.Infrastructure.Postgres.Data;
using System.Diagnostics;

namespace PostgreMigrationPOC.Infrastructure.Postgres.Services;

public class DataMigrationService
{
    private readonly SqlServerDbContext _sqlServerContext;
    private readonly PostgresDbContext _postgresContext;
    private readonly SimpleLogger<DataMigrationService> _logger;

    public DataMigrationService(
        SqlServerDbContext sqlServerContext, 
        PostgresDbContext postgresContext,
        SimpleLogger<DataMigrationService> logger)
    {
        _sqlServerContext = sqlServerContext;
        _postgresContext = postgresContext;
        _logger = logger;
    }

    public async Task<MigrationResult> MigrateDataAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new MigrationResult();

        try
        {
            _logger.LogInformation("Starting data migration from SQL Server to PostgreSQL");

            // Disable change tracking for better performance
            _postgresContext.ChangeTracker.AutoDetectChangesEnabled = false;

            // Clear existing PostgreSQL data
            await ClearPostgresDataAsync();

            // Migrate categories first (no dependencies)
            result.CategoriesMigrated = await MigrateCategoriesAsync();

            // Migrate tags (no dependencies)
            result.TagsMigrated = await MigrateTagsAsync();

            // Migrate products (depends on categories)
            result.ProductsMigrated = await MigrateProductsAsync();

            // Migrate product details (depends on products)
            result.ProductDetailsMigrated = await MigrateProductDetailsAsync();

            // Migrate product tags (depends on products and tags)
            result.ProductTagsMigrated = await MigrateProductTagsAsync();

            stopwatch.Stop();
            result.TotalDuration = stopwatch.Elapsed;
            result.Success = true;

            _logger.LogInformation("Data migration completed successfully in {Duration}ms", 
                stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.TotalDuration = stopwatch.Elapsed;
            
            _logger.LogError(ex, "Data migration failed after {Duration}ms", 
                stopwatch.ElapsedMilliseconds);
            
            throw;
        }
        finally
        {
            _postgresContext.ChangeTracker.AutoDetectChangesEnabled = true;
        }
    }

    private async Task ClearPostgresDataAsync()
    {
        _logger.LogInformation("Clearing existing PostgreSQL data");
        
        // Clear in dependency order
        await _postgresContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"ProductTags\" CASCADE");
        await _postgresContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"ProductDetails\" CASCADE");
        await _postgresContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"Products\" CASCADE");
        await _postgresContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"Tags\" CASCADE");
        await _postgresContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"Categories\" CASCADE");
        
        // Reset sequences
        await _postgresContext.Database.ExecuteSqlRawAsync("ALTER SEQUENCE \"Categories_Id_seq\" RESTART WITH 1");
        await _postgresContext.Database.ExecuteSqlRawAsync("ALTER SEQUENCE \"Tags_Id_seq\" RESTART WITH 1");
        await _postgresContext.Database.ExecuteSqlRawAsync("ALTER SEQUENCE \"Products_Id_seq\" RESTART WITH 1");
        await _postgresContext.Database.ExecuteSqlRawAsync("ALTER SEQUENCE \"ProductDetails_Id_seq\" RESTART WITH 1");
    }

    private async Task<int> MigrateCategoriesAsync()
    {
        _logger.LogInformation("Migrating categories");
        
        const int batchSize = 1000;
        int totalMigrated = 0;
        int skip = 0;

        while (true)
        {
            var categories = await _sqlServerContext.Categories
                .Skip(skip)
                .Take(batchSize)
                .AsNoTracking()
                .ToListAsync();

            if (categories.Count == 0)
                break;

            // Transform data if needed for PostgreSQL compatibility
            var postgresCategories = categories.Select(c => new Category
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                CreatedAt = EnsureUnspecifiedKind(c.CreatedAt),
                IsActive = c.IsActive
            }).ToList();

            await _postgresContext.Categories.AddRangeAsync(postgresCategories);
            await _postgresContext.SaveChangesAsync();
            
            totalMigrated += categories.Count;
            skip += batchSize;
            
            _postgresContext.ChangeTracker.Clear();
            
            _logger.LogInformation("Migrated {Count} categories so far", totalMigrated);
        }

        return totalMigrated;
    }

    private async Task<int> MigrateTagsAsync()
    {
        _logger.LogInformation("Migrating tags");
        
        const int batchSize = 1000;
        int totalMigrated = 0;
        int skip = 0;

        while (true)
        {
            var tags = await _sqlServerContext.Tags
                .Skip(skip)
                .Take(batchSize)
                .AsNoTracking()
                .ToListAsync();

            if (tags.Count == 0)
                break;

            var postgresTags = tags.Select(t => new Tag
            {
                Id = t.Id,
                Name = t.Name,
                Description = t.Description,
                CreatedAt = EnsureUnspecifiedKind(t.CreatedAt)
            }).ToList();

            await _postgresContext.Tags.AddRangeAsync(postgresTags);
            await _postgresContext.SaveChangesAsync();
            
            totalMigrated += tags.Count;
            skip += batchSize;
            
            _postgresContext.ChangeTracker.Clear();
        }

        return totalMigrated;
    }

    private async Task<int> MigrateProductsAsync()
    {
        _logger.LogInformation("Migrating products");
        
        const int batchSize = 1000;
        int totalMigrated = 0;
        int skip = 0;

        while (true)
        {
            // Use raw SQL to properly handle SQL Server specific data types
            var sql = @"
                SELECT 
                    Id, SmallIntField, BigIntField, TinyIntField, DecimalPrice, MoneyField, SmallMoneyField,
                    FloatField, RealField, VarcharField, NvarcharField, CharField, NcharField, 
                    TextField, NtextField, DateTimeField, DateTime2Field, DateField, TimeField, 
                    DateTimeOffsetField, SmallDateTimeField, BinaryField, VarbinaryField, BooleanField, 
                    GuidField, XmlField, 
                    CONVERT(NVARCHAR(MAX), HierarchyIdField) AS HierarchyIdField,
                    CONVERT(NVARCHAR(MAX), GeographyField) AS GeographyField,
                    CONVERT(NVARCHAR(MAX), GeometryField) AS GeometryField,
                    RowVersion, CategoryId
                FROM Products 
                ORDER BY Id 
                OFFSET {0} ROWS FETCH NEXT {1} ROWS ONLY";

            var products = await _sqlServerContext.Products
                .FromSqlRaw(string.Format(sql, skip, batchSize))
                .AsNoTracking()
                .ToListAsync();

            if (products.Count == 0)
                break;

            var postgresProducts = products.Select(p => new Product
            {
                Id = p.Id,
                SmallIntField = p.SmallIntField,
                BigIntField = p.BigIntField,
                TinyIntField = p.TinyIntField,
                DecimalPrice = p.DecimalPrice,
                MoneyField = p.MoneyField,
                SmallMoneyField = p.SmallMoneyField,
                FloatField = p.FloatField,
                RealField = p.RealField,
                VarcharField = p.VarcharField,
                NvarcharField = p.NvarcharField,
                CharField = p.CharField,
                NcharField = p.NcharField,
                TextField = p.TextField,
                NtextField = p.NtextField,
                // Ensure DateTime fields are Unspecified for PostgreSQL compatibility
                DateTimeField = EnsureUnspecifiedKind(p.DateTimeField),
                DateTime2Field = EnsureUnspecifiedKind(p.DateTime2Field),
                DateField = p.DateField,
                TimeField = p.TimeField,
                DateTimeOffsetField = EnsureUtcOffset(p.DateTimeOffsetField), // Convert to UTC for PostgreSQL compatibility
                SmallDateTimeField = EnsureUnspecifiedKind(p.SmallDateTimeField),
                BinaryField = p.BinaryField,
                VarbinaryField = p.VarbinaryField,
                BooleanField = p.BooleanField,
                GuidField = p.GuidField,
                XmlField = p.XmlField,
                // Transform SQL Server specific types to PostgreSQL compatible formats
                HierarchyIdField = TransformHierarchyId(p.HierarchyIdField),
                GeographyField = TransformGeography(p.GeographyField),
                GeometryField = TransformGeometry(p.GeometryField),
                RowVersion = p.RowVersion,
                CategoryId = p.CategoryId
            }).ToList();

            await _postgresContext.Products.AddRangeAsync(postgresProducts);
            await _postgresContext.SaveChangesAsync();
            
            totalMigrated += products.Count;
            skip += batchSize;
            
            _postgresContext.ChangeTracker.Clear();
            
            if (totalMigrated % 10000 == 0)
            {
                _logger.LogInformation("Migrated {Count} products so far", totalMigrated);
            }
        }

        return totalMigrated;
    }

    private async Task<int> MigrateProductDetailsAsync()
    {
        _logger.LogInformation("Migrating product details");
        
        const int batchSize = 1000;
        int totalMigrated = 0;
        int skip = 0;

        while (true)
        {
            var productDetails = await _sqlServerContext.ProductDetails
                .Skip(skip)
                .Take(batchSize)
                .AsNoTracking()
                .ToListAsync();

            if (productDetails.Count == 0)
                break;

            var postgresProductDetails = productDetails.Select(pd => new ProductDetail
            {
                Id = pd.Id,
                ProductId = pd.ProductId,
                DetailedDescription = pd.DetailedDescription,
                Specifications = pd.Specifications,
                ManufacturerInfo = pd.ManufacturerInfo,
                CreatedAt = EnsureUnspecifiedKind(pd.CreatedAt),
                UpdatedAt = EnsureUnspecifiedKind(pd.UpdatedAt)
            }).ToList();

            await _postgresContext.ProductDetails.AddRangeAsync(postgresProductDetails);
            await _postgresContext.SaveChangesAsync();
            
            totalMigrated += productDetails.Count;
            skip += batchSize;
            
            _postgresContext.ChangeTracker.Clear();
        }

        return totalMigrated;
    }

    private async Task<int> MigrateProductTagsAsync()
    {
        _logger.LogInformation("Migrating product tags");
        
        const int batchSize = 1000;
        int totalMigrated = 0;
        int skip = 0;

        while (true)
        {
            var productTags = await _sqlServerContext.ProductTags
                .Skip(skip)
                .Take(batchSize)
                .AsNoTracking()
                .ToListAsync();

            if (productTags.Count == 0)
                break;

            var postgresProductTags = productTags.Select(pt => new ProductTag
            {
                ProductId = pt.ProductId,
                TagId = pt.TagId,
                AssignedAt = EnsureUnspecifiedKind(pt.AssignedAt)
            }).ToList();

            await _postgresContext.ProductTags.AddRangeAsync(postgresProductTags);
            await _postgresContext.SaveChangesAsync();
            
            totalMigrated += productTags.Count;
            skip += batchSize;
            
            _postgresContext.ChangeTracker.Clear();
        }

        return totalMigrated;
    }

    // Transform SQL Server hierarchyid to PostgreSQL ltree format
    private string? TransformHierarchyId(string? hierarchyId)
    {
        if (string.IsNullOrEmpty(hierarchyId))
            return null;
            
        // Convert SQL Server hierarchyid format "/1/2/3/" to PostgreSQL ltree format "1.2.3"
        return hierarchyId.Trim('/').Replace('/', '.');
    }

    // Transform SQL Server geography to PostGIS format
    private string? TransformGeography(string? geography)
    {
        if (string.IsNullOrEmpty(geography))
            return null;
            
        // In a real scenario, you might need more sophisticated transformation
        // For now, assuming WKT format is compatible
        return geography;
    }

    // Transform SQL Server geometry to PostGIS format
    private string? TransformGeometry(string? geometry)
    {
        if (string.IsNullOrEmpty(geometry))
            return null;
            
        // In a real scenario, you might need more sophisticated transformation
        // For now, assuming WKT format is compatible
        return geometry;
    }

    // Ensure DateTime is Unspecified for PostgreSQL "timestamp without time zone" compatibility
    private DateTime EnsureUnspecifiedKind(DateTime dateTime)
    {
        // For "timestamp without time zone" columns, we need DateTimeKind.Unspecified
        // Convert any UTC or Local times to a consistent Unspecified representation
        return DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified);
    }

    // Ensure DateTimeOffset is UTC for PostgreSQL "timestamp with time zone" compatibility
    private DateTimeOffset EnsureUtcOffset(DateTimeOffset dateTimeOffset)
    {
        // Convert any offset to UTC (offset 0) for PostgreSQL compatibility
        return dateTimeOffset.ToUniversalTime();
    }
}

public class MigrationResult
{
    public bool Success { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public int CategoriesMigrated { get; set; }
    public int TagsMigrated { get; set; }
    public int ProductsMigrated { get; set; }
    public int ProductDetailsMigrated { get; set; }
    public int ProductTagsMigrated { get; set; }
    public string? ErrorMessage { get; set; }
    
    public int TotalRecords => CategoriesMigrated + TagsMigrated + ProductsMigrated + ProductDetailsMigrated + ProductTagsMigrated;
}

