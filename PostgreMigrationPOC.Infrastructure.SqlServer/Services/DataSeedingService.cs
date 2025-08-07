using Bogus;
using Microsoft.EntityFrameworkCore;
using PostgreMigrationPOC.Core.Entities;
using PostgreMigrationPOC.Infrastructure.SqlServer.Data;
using System.Diagnostics;

namespace PostgreMigrationPOC.Infrastructure.SqlServer.Services;

public class DataSeedingService
{
    private readonly SqlServerDbContext _context;
    private readonly SimpleLogger<DataSeedingService>? _logger;

    public DataSeedingService(SqlServerDbContext context, SimpleLogger<DataSeedingService>? logger = null)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<SeedingResult> SeedDataAsync(int productCount = 100000)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new SeedingResult();

        try
        {
            _logger?.LogInformation("Starting data seeding for {ProductCount} products", productCount);

            // Disable change tracking for better performance during seeding
            _context.ChangeTracker.AutoDetectChangesEnabled = false;

            // Clear existing data
            await ClearExistingDataAsync();

            // Seed categories first
            var categories = await SeedCategoriesAsync();
            result.CategoriesCreated = categories.Count;

            // Seed tags
            var tags = await SeedTagsAsync();
            result.TagsCreated = tags.Count;

            // Seed products in batches
            result.ProductsCreated = await SeedProductsAsync(productCount, categories);

            // Seed product details
            result.ProductDetailsCreated = await SeedProductDetailsAsync();

            // Seed product tags (many-to-many relationships)
            result.ProductTagsCreated = await SeedProductTagsAsync(tags);

            stopwatch.Stop();
            result.TotalDuration = stopwatch.Elapsed;
            result.Success = true;

            _logger?.LogInformation("Data seeding completed successfully in {Duration}ms", 
                stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.TotalDuration = stopwatch.Elapsed;
            
            _logger?.LogError(ex, "Data seeding failed after {Duration}ms", 
                stopwatch.ElapsedMilliseconds);
            
            throw;
        }
        finally
        {
            _context.ChangeTracker.AutoDetectChangesEnabled = true;
        }
    }

    private async Task ClearExistingDataAsync()
    {
        _logger?.LogInformation("Clearing existing data");
        
        await _context.Database.ExecuteSqlRawAsync("DELETE FROM ProductTags");
        await _context.Database.ExecuteSqlRawAsync("DELETE FROM ProductDetails");
        await _context.Database.ExecuteSqlRawAsync("DELETE FROM Products");
        await _context.Database.ExecuteSqlRawAsync("DELETE FROM Tags");
        await _context.Database.ExecuteSqlRawAsync("DELETE FROM Categories");
        
        // Reset identity seeds
        await _context.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT('Categories', RESEED, 0)");
        await _context.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT('Tags', RESEED, 0)");
        await _context.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT('Products', RESEED, 0)");
        await _context.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT('ProductDetails', RESEED, 0)");
    }

    private async Task<List<Category>> SeedCategoriesAsync()
    {
        _logger?.LogInformation("Seeding categories");

        var categoryFaker = new Faker<Category>()
            .RuleFor(c => c.Name, f => f.Commerce.Categories(1).First())
            .RuleFor(c => c.Description, f => f.Lorem.Sentence(10))
            .RuleFor(c => c.CreatedAt, f => f.Date.Past(2))
            .RuleFor(c => c.IsActive, f => f.Random.Bool(0.8f));

        var categories = categoryFaker.Generate(50);
        await _context.Categories.AddRangeAsync(categories);
        await _context.SaveChangesAsync();
        
        return categories;
    }

    private async Task<List<Tag>> SeedTagsAsync()
    {
        _logger?.LogInformation("Seeding tags");

        var tagNames = new[]
        {
            "Popular", "New", "Sale", "Premium", "Eco-Friendly", "Limited Edition",
            "Best Seller", "Trending", "Seasonal", "Clearance", "Featured",
            "High Quality", "Budget", "Luxury", "Vintage", "Modern"
        };

        var tagFaker = new Faker<Tag>()
            .RuleFor(t => t.Name, f => f.PickRandom(tagNames))
            .RuleFor(t => t.Description, f => f.Lorem.Sentence(5))
            .RuleFor(t => t.CreatedAt, f => f.Date.Past(1));

        var tags = tagFaker.Generate(tagNames.Length);
        
        // Ensure unique names
        for (int i = 0; i < tags.Count; i++)
        {
            tags[i].Name = tagNames[i];
        }

        await _context.Tags.AddRangeAsync(tags);
        await _context.SaveChangesAsync();
        
        return tags;
    }

    private async Task<int> SeedProductsAsync(int productCount, List<Category> categories)
    {
        _logger?.LogInformation("Seeding {ProductCount} products", productCount);

        const int batchSize = 1000;
        int totalCreated = 0;

        var productFaker = new Faker<Product>()
            .RuleFor(p => p.SmallIntField, f => (short)f.Random.Int(1, 32000))
            .RuleFor(p => p.BigIntField, f => f.Random.Long(1, 1000000))
            .RuleFor(p => p.TinyIntField, f => (byte)f.Random.Int(1, 255))
            .RuleFor(p => p.DecimalPrice, f => f.Random.Decimal(1, 10000))
            .RuleFor(p => p.MoneyField, f => f.Random.Decimal(1, 10000))
            .RuleFor(p => p.SmallMoneyField, f => f.Random.Decimal(1, 1000))
            .RuleFor(p => p.FloatField, f => f.Random.Double(1, 10000))
            .RuleFor(p => p.RealField, f => (float)f.Random.Double(1, 1000))
            .RuleFor(p => p.VarcharField, f => {
                var productName = f.Commerce.ProductName();
                return productName.Length > 100 ? productName.Substring(0, 100) : productName;
            })
            .RuleFor(p => p.NvarcharField, f => {
                var productDescription = f.Commerce.ProductDescription();
                return productDescription.Length > 200 ? productDescription.Substring(0, 200) : productDescription;
            })
            .RuleFor(p => p.CharField, f => f.Random.String2(10))
            .RuleFor(p => p.NcharField, f => f.Random.String2(5))
            .RuleFor(p => p.TextField, f => f.Lorem.Paragraphs(3))
            .RuleFor(p => p.NtextField, f => f.Lorem.Paragraphs(2))
            .RuleFor(p => p.DateTimeField, f => f.Date.Past(5))
            .RuleFor(p => p.DateTime2Field, f => f.Date.Past(3))
            .RuleFor(p => p.DateField, f => DateOnly.FromDateTime(f.Date.Past(2)))
            .RuleFor(p => p.TimeField, f => TimeOnly.FromDateTime(f.Date.Recent()))
            .RuleFor(p => p.DateTimeOffsetField, f => f.Date.PastOffset(1))
            .RuleFor(p => p.SmallDateTimeField, f => f.Date.Past(1))
            .RuleFor(p => p.BinaryField, f => f.Random.Bytes(16))
            .RuleFor(p => p.VarbinaryField, f => f.Random.Bytes(f.Random.Int(10, 100)))
            .RuleFor(p => p.BooleanField, f => f.Random.Bool())
            .RuleFor(p => p.GuidField, f => f.Random.Guid())
            .RuleFor(p => p.XmlField, f => $"<product><name>{f.Commerce.ProductName()}</name><price>{f.Random.Decimal(1, 1000)}</price></product>")
            .RuleFor(p => p.HierarchyIdField, f => "/1/2/3/")
            .RuleFor(p => p.GeographyField, f => "POINT(-122.34900 47.65100)")
            .RuleFor(p => p.GeometryField, f => "POINT(10.0 20.0)")
            .RuleFor(p => p.CategoryId, f => f.PickRandom(categories).Id);

        for (int i = 0; i < productCount; i += batchSize)
        {
            int currentBatchSize = Math.Min(batchSize, productCount - i);
            var products = productFaker.Generate(currentBatchSize);

            await _context.Products.AddRangeAsync(products);
            await _context.SaveChangesAsync();
            
            totalCreated += currentBatchSize;
            
            if (totalCreated % 10000 == 0)
            {
                _logger?.LogInformation("Created {TotalCreated} products so far", totalCreated);
            }
            
            _context.ChangeTracker.Clear();
        }

        return totalCreated;
    }

    private async Task<int> SeedProductDetailsAsync()
    {
        _logger?.LogInformation("Seeding product details");

        const int batchSize = 1000;
        int totalCreated = 0;

        var productIds = await _context.Products
            .Select(p => p.Id)
            .ToListAsync();

        var detailFaker = new Faker<ProductDetail>()
            .RuleFor(pd => pd.ProductId, f => f.PickRandom(productIds))
            .RuleFor(pd => pd.DetailedDescription, f => f.Lorem.Paragraphs(3))
            .RuleFor(pd => pd.Specifications, f => f.Lorem.Paragraphs(2))
            .RuleFor(pd => pd.ManufacturerInfo, f => f.Company.CompanyName())
            .RuleFor(pd => pd.CreatedAt, f => f.Date.Past(1))
            .RuleFor(pd => pd.UpdatedAt, f => f.Date.Recent());

        // Create details for 80% of products
        var detailCount = (int)(productIds.Count * 0.8);
        var selectedProductIds = productIds.Take(detailCount).ToList();

        for (int i = 0; i < selectedProductIds.Count; i += batchSize)
        {
            int currentBatchSize = Math.Min(batchSize, selectedProductIds.Count - i);
            var batchProductIds = selectedProductIds.Skip(i).Take(currentBatchSize);
            
            var details = batchProductIds.Select(id => new ProductDetail
            {
                ProductId = id,
                DetailedDescription = detailFaker.Generate().DetailedDescription,
                Specifications = detailFaker.Generate().Specifications,
                ManufacturerInfo = detailFaker.Generate().ManufacturerInfo,
                CreatedAt = DateTime.Now.AddDays(-new Random().Next(1, 365)),
                UpdatedAt = DateTime.Now.AddDays(-new Random().Next(1, 30))
            }).ToList();

            await _context.ProductDetails.AddRangeAsync(details);
            await _context.SaveChangesAsync();
            
            totalCreated += currentBatchSize;
            _context.ChangeTracker.Clear();
        }

        return totalCreated;
    }

    private async Task<int> SeedProductTagsAsync(List<Tag> tags)
    {
        _logger?.LogInformation("Seeding product-tag relationships");

        const int batchSize = 500;
        int totalCreated = 0;

        var productIds = await _context.Products
            .Select(p => p.Id)
            .ToListAsync();

        var random = new Random();
        var productTags = new List<ProductTag>();

        foreach (var productId in productIds)
        {
            // Each product gets 1-5 random tags
            int tagCount = random.Next(1, 6);
            var selectedTags = tags.OrderBy(x => random.Next()).Take(tagCount);

            foreach (var tag in selectedTags)
            {
                // Check if this combination already exists in the current batch to avoid duplicates
                var existingProductTag = productTags.FirstOrDefault(pt => pt.ProductId == productId && pt.TagId == tag.Id);
                if (existingProductTag == null)
                {
                    productTags.Add(new ProductTag
                    {
                        ProductId = productId,
                        TagId = tag.Id,
                        AssignedAt = DateTime.Now.AddDays(-random.Next(1, 365))
                    });
                }

                if (productTags.Count >= batchSize)
                {
                    // Create a new context scope to avoid tracking issues
                    using var scope = _context.Database.BeginTransaction();
                    try
                    {
                        // Clear any existing tracking
                        _context.ChangeTracker.Clear();
                        
                        // Add the ProductTags directly without navigation properties
                        await _context.ProductTags.AddRangeAsync(productTags);
                        await _context.SaveChangesAsync();
                        
                        await scope.CommitAsync();
                        
                        totalCreated += productTags.Count;
                        productTags.Clear();
                    }
                    catch
                    {
                        await scope.RollbackAsync();
                        throw;
                    }
                }
            }
        }

        // Add remaining items
        if (productTags.Count > 0)
        {
            using var scope = _context.Database.BeginTransaction();
            try
            {
                _context.ChangeTracker.Clear();
                await _context.ProductTags.AddRangeAsync(productTags);
                await _context.SaveChangesAsync();
                await scope.CommitAsync();
                
                totalCreated += productTags.Count;
            }
            catch
            {
                await scope.RollbackAsync();
                throw;
            }
        }

        return totalCreated;
    }
}

public class SeedingResult
{
    public bool Success { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public int CategoriesCreated { get; set; }
    public int TagsCreated { get; set; }
    public int ProductsCreated { get; set; }
    public int ProductDetailsCreated { get; set; }
    public int ProductTagsCreated { get; set; }
    public string? ErrorMessage { get; set; }
    
    public int TotalRecords => CategoriesCreated + TagsCreated + ProductsCreated + ProductDetailsCreated + ProductTagsCreated;
}

// Simple logger implementation for compatibility
public class SimpleLogger<T>
{
    public void LogInformation(string message, params object[] args)
    {
        try
        {
            var formattedMessage = args?.Length > 0 ? string.Format(message, args) : message;
            Console.WriteLine($"[INFO] {formattedMessage}");
        }
        catch (FormatException)
        {
            // If formatting fails, just print the message as-is
            Console.WriteLine($"[INFO] {message}");
        }
    }

    public void LogError(Exception ex, string message, params object[] args)
    {
        try
        {
            var formattedMessage = args?.Length > 0 ? string.Format(message, args) : message;
            Console.WriteLine($"[ERROR] {formattedMessage} - {ex.Message}");
        }
        catch (FormatException)
        {
            // If formatting fails, just print the message as-is
            Console.WriteLine($"[ERROR] {message} - {ex.Message}");
        }
    }
}