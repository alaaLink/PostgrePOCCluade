using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using PostgreMigrationPOC.Infrastructure.SqlServer.Data;
using PostgreMigrationPOC.Infrastructure.SqlServer.Services;

namespace PostgreMigrationPOC.Console.Services;

public class DataSeedingOrchestrator
{
    private readonly SqlServerDbContext _sqlServerContext;

    public DataSeedingOrchestrator(SqlServerDbContext sqlServerContext)
    {
        _sqlServerContext = sqlServerContext;
    }

    public async Task<SeedingReport> ExecuteDataSeedingAsync(int productCount = 10000)
    {
        var report = new SeedingReport
        {
            StartTime = DateTime.UtcNow
        };

        var totalStopwatch = Stopwatch.StartNew();

        try
        {
            System.Console.WriteLine("=== Data Seeding Process ===");
            System.Console.WriteLine($"Starting data seeding with {productCount:N0} products");
            System.Console.WriteLine($"Start Time: {report.StartTime:yyyy-MM-dd HH:mm:ss} UTC");
            System.Console.WriteLine();

            // Step 1: Setup SQL Server database
            System.Console.WriteLine("Step 1: Setting up SQL Server database...");
            var dbSetupStopwatch = Stopwatch.StartNew();
            
            await _sqlServerContext.Database.EnsureCreatedAsync();
            
            dbSetupStopwatch.Stop();
            report.DatabaseSetupDuration = dbSetupStopwatch.Elapsed;
            System.Console.WriteLine($"Database setup completed in {dbSetupStopwatch.ElapsedMilliseconds:N0}ms");
            System.Console.WriteLine();

            // Step 2: Check if data already exists
            var existingProductCount = await _sqlServerContext.Products.CountAsync();
            if (existingProductCount > 0)
            {
                System.Console.WriteLine($"Found {existingProductCount:N0} existing products in database.");
                System.Console.Write("Do you want to clear existing data and reseed? (y/n): ");
                var response = System.Console.ReadLine()?.ToLower();
                
                if (response != "y" && response != "yes")
                {
                    report.Success = true;
                    report.SeedingResult = new SeedingResult { Success = true, ProductsCreated = existingProductCount };
                    report.EndTime = DateTime.UtcNow;
                    totalStopwatch.Stop();
                    report.TotalDuration = totalStopwatch.Elapsed;
                    
                    System.Console.WriteLine("Seeding cancelled. Using existing data.");
                    return report;
                }
            }

            // Step 3: Seed SQL Server data
            System.Console.WriteLine("Step 2: Seeding SQL Server data...");
            var seedingService = new DataSeedingService(_sqlServerContext, new SimpleLogger<DataSeedingService>());
            var seedingResult = await seedingService.SeedDataAsync(productCount);
            
            report.SeedingResult = seedingResult;
            System.Console.WriteLine($"Seeding completed in {seedingResult.TotalDuration.TotalMilliseconds:N0}ms");
            System.Console.WriteLine($"Records created: {seedingResult.TotalRecords:N0}");
            System.Console.WriteLine($"Throughput: {seedingResult.TotalRecords / seedingResult.TotalDuration.TotalSeconds:N0} records/second");
            System.Console.WriteLine();

            // Step 4: Validate seeded data
            System.Console.WriteLine("Step 3: Validating seeded data...");
            var validationStopwatch = Stopwatch.StartNew();
            var validation = await ValidateSqlServerDataAsync();
            validationStopwatch.Stop();
            
            report.Validation = validation;
            report.ValidationDuration = validationStopwatch.Elapsed;
            System.Console.WriteLine($"Validation completed in {validationStopwatch.ElapsedMilliseconds:N0}ms");
            System.Console.WriteLine($"Validation passed: {validation.IsValid}");
            
            if (!validation.IsValid && validation.ValidationErrors.Any())
            {
                System.Console.WriteLine("Validation errors:");
                foreach (var error in validation.ValidationErrors)
                {
                    System.Console.WriteLine($"  - {error}");
                }
            }

            totalStopwatch.Stop();
            report.TotalDuration = totalStopwatch.Elapsed;
            report.EndTime = DateTime.UtcNow;
            report.Success = seedingResult.Success && validation.IsValid;

            // Generate seeding report
            GenerateSeedingReport(report);

            return report;
        }
        catch (Exception ex)
        {
            totalStopwatch.Stop();
            report.TotalDuration = totalStopwatch.Elapsed;
            report.EndTime = DateTime.UtcNow;
            report.Success = false;
            report.ErrorMessage = ex.Message;
            
            System.Console.WriteLine($"Seeding failed with error: {ex.Message}");
            throw;
        }
    }

    private async Task<DataValidationResult> ValidateSqlServerDataAsync()
    {
        var result = new DataValidationResult();
        
        result.CategoryCount = await _sqlServerContext.Categories.CountAsync();
        result.TagCount = await _sqlServerContext.Tags.CountAsync();
        result.ProductCount = await _sqlServerContext.Products.CountAsync();
        result.ProductDetailCount = await _sqlServerContext.ProductDetails.CountAsync();
        result.ProductTagCount = await _sqlServerContext.ProductTags.CountAsync();
        
        // Validate referential integrity
        var orphanedProducts = await _sqlServerContext.Products
            .Where(p => !_sqlServerContext.Categories.Any(c => c.Id == p.CategoryId))
            .CountAsync();
            
        var orphanedProductDetails = await _sqlServerContext.ProductDetails
            .Where(pd => !_sqlServerContext.Products.Any(p => p.Id == pd.ProductId))
            .CountAsync();

        result.IsValid = orphanedProducts == 0 && orphanedProductDetails == 0;
        result.ValidationErrors = new List<string>();
        
        if (orphanedProducts > 0)
            result.ValidationErrors.Add($"Found {orphanedProducts} orphaned products");
        if (orphanedProductDetails > 0)
            result.ValidationErrors.Add($"Found {orphanedProductDetails} orphaned product details");

        return result;
    }

    private void GenerateSeedingReport(SeedingReport report)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=".PadRight(60, '='));
        sb.AppendLine(" DATA SEEDING REPORT");
        sb.AppendLine("=".PadRight(60, '='));
        sb.AppendLine();
        
        sb.AppendLine($"Seeding Status: {(report.Success ? "SUCCESS" : "FAILED")}");
        sb.AppendLine($"Start Time: {report.StartTime:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"End Time: {report.EndTime:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"Total Duration: {report.TotalDuration.TotalMinutes:F2} minutes");
        sb.AppendLine();
        
        // Performance Metrics
        sb.AppendLine("PERFORMANCE METRICS:");
        sb.AppendLine($"  Database Setup: {report.DatabaseSetupDuration.TotalMilliseconds:N0}ms");
        
        if (report.SeedingResult != null)
        {
            sb.AppendLine($"  Data Seeding: {report.SeedingResult.TotalDuration.TotalMilliseconds:N0}ms");
            sb.AppendLine($"  Seeding Throughput: {report.SeedingResult.TotalRecords / report.SeedingResult.TotalDuration.TotalSeconds:N0} records/sec");
        }
        
        sb.AppendLine($"  Data Validation: {report.ValidationDuration.TotalMilliseconds:N0}ms");
        sb.AppendLine();
        
        // Data Summary
        sb.AppendLine("DATA SUMMARY:");
        if (report.SeedingResult != null)
        {
            sb.AppendLine($"  Categories: {report.SeedingResult.CategoriesCreated:N0}");
            sb.AppendLine($"  Tags: {report.SeedingResult.TagsCreated:N0}");
            sb.AppendLine($"  Products: {report.SeedingResult.ProductsCreated:N0}");
            sb.AppendLine($"  Product Details: {report.SeedingResult.ProductDetailsCreated:N0}");
            sb.AppendLine($"  Product Tags: {report.SeedingResult.ProductTagsCreated:N0}");
            sb.AppendLine($"  Total Records: {report.SeedingResult.TotalRecords:N0}");
        }
        sb.AppendLine();
        
        // Validation Results
        sb.AppendLine("VALIDATION RESULTS:");
        sb.AppendLine($"  Data Valid: {report.Validation?.IsValid ?? false}");
        
        if (report.Validation?.ValidationErrors?.Any() == true)
        {
            sb.AppendLine("  Validation Errors:");
            foreach (var error in report.Validation.ValidationErrors)
            {
                sb.AppendLine($"    - {error}");
            }
        }
        
        sb.AppendLine();
        sb.AppendLine("=".PadRight(60, '='));
        
        var reportContent = sb.ToString();
        System.Console.WriteLine(reportContent);
        
        // Save report to file
        var reportFileName = $"seeding_report_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        File.WriteAllText(reportFileName, reportContent);
        System.Console.WriteLine($"Detailed report saved to: {reportFileName}");
    }
}

public class SeedingReport
{
    public bool Success { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public TimeSpan DatabaseSetupDuration { get; set; }
    public TimeSpan ValidationDuration { get; set; }
    public SeedingResult? SeedingResult { get; set; }
    public DataValidationResult? Validation { get; set; }
    public string? ErrorMessage { get; set; }
}

