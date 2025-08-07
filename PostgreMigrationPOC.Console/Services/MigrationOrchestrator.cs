using System.Diagnostics;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PostgreMigrationPOC.Infrastructure.SqlServer.Data;
using PostgreMigrationPOC.Infrastructure.SqlServer.Services;
using PostgreMigrationPOC.Infrastructure.Postgres.Data;
using PostgreMigrationPOC.Infrastructure.Postgres.Services;

namespace PostgreMigrationPOC.Console.Services;

public class MigrationOrchestrator
{
    private readonly SqlServerDbContext _sqlServerContext;
    private readonly PostgresDbContext _postgresContext;

    public MigrationOrchestrator(SqlServerDbContext sqlServerContext, PostgresDbContext postgresContext)
    {
        _sqlServerContext = sqlServerContext;
        _postgresContext = postgresContext;
    }

    public async Task<CompleteMigrationReport> ExecuteCompleteMigrationAsync(int productCount = 100000)
    {
        var report = new CompleteMigrationReport
        {
            StartTime = DateTime.UtcNow
        };

        var totalStopwatch = Stopwatch.StartNew();

        try
        {
            System.Console.WriteLine("=== PostgreSQL Migration POC ===");
            System.Console.WriteLine($"Starting complete migration process with {productCount:N0} products");
            System.Console.WriteLine($"Start Time: {report.StartTime:yyyy-MM-dd HH:mm:ss} UTC");
            System.Console.WriteLine();

            // Step 1: Setup and ensure databases
            System.Console.WriteLine("Step 1: Setting up databases...");
            var dbSetupStopwatch = Stopwatch.StartNew();
            
            await _sqlServerContext.Database.EnsureCreatedAsync();
            await _postgresContext.Database.EnsureCreatedAsync();
            
            dbSetupStopwatch.Stop();
            report.DatabaseSetupDuration = dbSetupStopwatch.Elapsed;
            System.Console.WriteLine($"Database setup completed in {dbSetupStopwatch.ElapsedMilliseconds:N0}ms");
            System.Console.WriteLine();

            // Step 2: Seed SQL Server data
            System.Console.WriteLine("Step 2: Seeding SQL Server data...");
            var seedingService = new DataSeedingService(_sqlServerContext, new SimpleLogger<DataSeedingService>());
            var seedingResult = await seedingService.SeedDataAsync(productCount);
            
            report.SeedingResult = seedingResult;
            System.Console.WriteLine($"Seeding completed in {seedingResult.TotalDuration.TotalMilliseconds:N0}ms");
            System.Console.WriteLine($"Records created: {seedingResult.TotalRecords:N0}");
            System.Console.WriteLine($"Throughput: {seedingResult.TotalRecords / seedingResult.TotalDuration.TotalSeconds:N0} records/second");
            System.Console.WriteLine();

            // Step 3: Validate SQL Server data
            System.Console.WriteLine("Step 3: Validating SQL Server data...");
            var sqlValidationStopwatch = Stopwatch.StartNew();
            var sqlValidation = await ValidateSqlServerDataAsync();
            sqlValidationStopwatch.Stop();
            
            report.SqlServerValidation = sqlValidation;
            report.SqlServerValidationDuration = sqlValidationStopwatch.Elapsed;
            System.Console.WriteLine($"SQL Server validation completed in {sqlValidationStopwatch.ElapsedMilliseconds:N0}ms");
            System.Console.WriteLine($"Validation passed: {sqlValidation.IsValid}");
            System.Console.WriteLine();

            // Step 4: Migrate data to PostgreSQL
            System.Console.WriteLine("Step 4: Migrating data to PostgreSQL...");
            var migrationService = new DataMigrationService(_sqlServerContext, _postgresContext, new SimpleLogger<DataMigrationService>());
            var migrationResult = await migrationService.MigrateDataAsync();
            
            report.MigrationResult = migrationResult;
            System.Console.WriteLine($"Migration completed in {migrationResult.TotalDuration.TotalMilliseconds:N0}ms");
            System.Console.WriteLine($"Records migrated: {migrationResult.TotalRecords:N0}");
            System.Console.WriteLine($"Migration throughput: {migrationResult.TotalRecords / migrationResult.TotalDuration.TotalSeconds:N0} records/second");
            System.Console.WriteLine();

            // Step 5: Validate PostgreSQL data
            System.Console.WriteLine("Step 5: Validating PostgreSQL data...");
            var pgValidationStopwatch = Stopwatch.StartNew();
            var pgValidation = await ValidatePostgresDataAsync();
            pgValidationStopwatch.Stop();
            
            report.PostgresValidation = pgValidation;
            report.PostgresValidationDuration = pgValidationStopwatch.Elapsed;
            System.Console.WriteLine($"PostgreSQL validation completed in {pgValidationStopwatch.ElapsedMilliseconds:N0}ms");
            System.Console.WriteLine($"Validation passed: {pgValidation.IsValid}");
            System.Console.WriteLine();

            // Step 6: Cross-validate data consistency
            System.Console.WriteLine("Step 6: Cross-validating data consistency...");
            var crossValidationStopwatch = Stopwatch.StartNew();
            var crossValidation = await CrossValidateDataAsync();
            crossValidationStopwatch.Stop();
            
            report.CrossValidation = crossValidation;
            report.CrossValidationDuration = crossValidationStopwatch.Elapsed;
            System.Console.WriteLine($"Cross-validation completed in {crossValidationStopwatch.ElapsedMilliseconds:N0}ms");
            System.Console.WriteLine($"Data consistency check passed: {crossValidation.IsConsistent}");
            System.Console.WriteLine();

            totalStopwatch.Stop();
            report.TotalDuration = totalStopwatch.Elapsed;
            report.EndTime = DateTime.UtcNow;
            report.Success = seedingResult.Success && migrationResult.Success && crossValidation.IsConsistent;

            // Generate final report
            GenerateFinalReport(report);

            return report;
        }
        catch (Exception ex)
        {
            totalStopwatch.Stop();
            report.TotalDuration = totalStopwatch.Elapsed;
            report.EndTime = DateTime.UtcNow;
            report.Success = false;
            report.ErrorMessage = ex.Message;
            
            System.Console.WriteLine($"Migration failed with error: {ex.Message}");
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

    private async Task<DataValidationResult> ValidatePostgresDataAsync()
    {
        var result = new DataValidationResult();
        
        result.CategoryCount = await _postgresContext.Categories.CountAsync();
        result.TagCount = await _postgresContext.Tags.CountAsync();
        result.ProductCount = await _postgresContext.Products.CountAsync();
        result.ProductDetailCount = await _postgresContext.ProductDetails.CountAsync();
        result.ProductTagCount = await _postgresContext.ProductTags.CountAsync();
        
        // Validate referential integrity
        var orphanedProducts = await _postgresContext.Products
            .Where(p => !_postgresContext.Categories.Any(c => c.Id == p.CategoryId))
            .CountAsync();
            
        var orphanedProductDetails = await _postgresContext.ProductDetails
            .Where(pd => !_postgresContext.Products.Any(p => p.Id == pd.ProductId))
            .CountAsync();

        result.IsValid = orphanedProducts == 0 && orphanedProductDetails == 0;
        result.ValidationErrors = new List<string>();
        
        if (orphanedProducts > 0)
            result.ValidationErrors.Add($"Found {orphanedProducts} orphaned products");
        if (orphanedProductDetails > 0)
            result.ValidationErrors.Add($"Found {orphanedProductDetails} orphaned product details");

        return result;
    }

    private async Task<CrossValidationResult> CrossValidateDataAsync()
    {
        var result = new CrossValidationResult();
        var errors = new List<string>();

        // Compare record counts
        var sqlCategories = await _sqlServerContext.Categories.CountAsync();
        var pgCategories = await _postgresContext.Categories.CountAsync();
        if (sqlCategories != pgCategories)
            errors.Add($"Category count mismatch: SQL Server={sqlCategories}, PostgreSQL={pgCategories}");

        var sqlTags = await _sqlServerContext.Tags.CountAsync();
        var pgTags = await _postgresContext.Tags.CountAsync();
        if (sqlTags != pgTags)
            errors.Add($"Tag count mismatch: SQL Server={sqlTags}, PostgreSQL={pgTags}");

        var sqlProducts = await _sqlServerContext.Products.CountAsync();
        var pgProducts = await _postgresContext.Products.CountAsync();
        if (sqlProducts != pgProducts)
            errors.Add($"Product count mismatch: SQL Server={sqlProducts}, PostgreSQL={pgProducts}");

        var sqlProductDetails = await _sqlServerContext.ProductDetails.CountAsync();
        var pgProductDetails = await _postgresContext.ProductDetails.CountAsync();
        if (sqlProductDetails != pgProductDetails)
            errors.Add($"Product Detail count mismatch: SQL Server={sqlProductDetails}, PostgreSQL={pgProductDetails}");

        var sqlProductTags = await _sqlServerContext.ProductTags.CountAsync();
        var pgProductTags = await _postgresContext.ProductTags.CountAsync();
        if (sqlProductTags != pgProductTags)
            errors.Add($"Product Tag count mismatch: SQL Server={sqlProductTags}, PostgreSQL={pgProductTags}");

        // Sample data consistency check (first 100 products)
        var sampleProducts = await _sqlServerContext.Products.Take(100).ToListAsync();
        foreach (var sqlProduct in sampleProducts)
        {
            var pgProduct = await _postgresContext.Products.FindAsync(sqlProduct.Id);
            if (pgProduct == null)
            {
                errors.Add($"Product ID {sqlProduct.Id} not found in PostgreSQL");
                continue;
            }

            // Check key fields for consistency
            if (sqlProduct.DecimalPrice != pgProduct.DecimalPrice)
                errors.Add($"Product {sqlProduct.Id}: Price mismatch");
            if (sqlProduct.CategoryId != pgProduct.CategoryId)
                errors.Add($"Product {sqlProduct.Id}: Category mismatch");
        }

        result.ValidationErrors = errors;
        result.IsConsistent = errors.Count == 0;

        return result;
    }

    private void GenerateFinalReport(CompleteMigrationReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=".PadRight(80, '='));
        sb.AppendLine(" COMPLETE MIGRATION REPORT");
        sb.AppendLine("=".PadRight(80, '='));
        sb.AppendLine();
        
        sb.AppendLine($"Migration Status: {(report.Success ? "SUCCESS" : "FAILED")}");
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
        
        if (report.MigrationResult != null)
        {
            sb.AppendLine($"  Data Migration: {report.MigrationResult.TotalDuration.TotalMilliseconds:N0}ms");
            sb.AppendLine($"  Migration Throughput: {report.MigrationResult.TotalRecords / report.MigrationResult.TotalDuration.TotalSeconds:N0} records/sec");
        }
        
        sb.AppendLine($"  SQL Server Validation: {report.SqlServerValidationDuration.TotalMilliseconds:N0}ms");
        sb.AppendLine($"  PostgreSQL Validation: {report.PostgresValidationDuration.TotalMilliseconds:N0}ms");
        sb.AppendLine($"  Cross Validation: {report.CrossValidationDuration.TotalMilliseconds:N0}ms");
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
        
        // Type Mapping Summary
        sb.AppendLine("SQL SERVER TO POSTGRESQL TYPE MAPPING:");
        sb.AppendLine("  int → integer");
        sb.AppendLine("  bigint → bigint");
        sb.AppendLine("  smallint → smallint");
        sb.AppendLine("  tinyint → smallint");
        sb.AppendLine("  decimal → decimal");
        sb.AppendLine("  money → money");
        sb.AppendLine("  smallmoney → money");
        sb.AppendLine("  float → double precision");
        sb.AppendLine("  real → real");
        sb.AppendLine("  varchar → varchar");
        sb.AppendLine("  nvarchar → text");
        sb.AppendLine("  char → char");
        sb.AppendLine("  nchar → char");
        sb.AppendLine("  text/ntext → text");
        sb.AppendLine("  datetime/datetime2 → timestamp without time zone");
        sb.AppendLine("  date → date");
        sb.AppendLine("  time → time without time zone");
        sb.AppendLine("  datetimeoffset → timestamp with time zone");
        sb.AppendLine("  binary/varbinary → bytea");
        sb.AppendLine("  bit → boolean");
        sb.AppendLine("  uniqueidentifier → uuid");
        sb.AppendLine("  xml → xml");
        sb.AppendLine("  hierarchyid → ltree (requires extension)");
        sb.AppendLine("  geography/geometry → PostGIS types (requires extension)");
        sb.AppendLine("  rowversion → bytea with custom logic");
        sb.AppendLine();
        
        // Validation Results
        sb.AppendLine("VALIDATION RESULTS:");
        sb.AppendLine($"  SQL Server Data Valid: {report.SqlServerValidation?.IsValid ?? false}");
        sb.AppendLine($"  PostgreSQL Data Valid: {report.PostgresValidation?.IsValid ?? false}");
        sb.AppendLine($"  Cross-Validation Passed: {report.CrossValidation?.IsConsistent ?? false}");
        
        if (report.CrossValidation?.ValidationErrors?.Any() == true)
        {
            sb.AppendLine("  Validation Errors:");
            foreach (var error in report.CrossValidation.ValidationErrors)
            {
                sb.AppendLine($"    - {error}");
            }
        }
        
        sb.AppendLine();
        sb.AppendLine("=".PadRight(80, '='));
        
        var reportContent = sb.ToString();
        System.Console.WriteLine(reportContent);
        
        // Save report to file
        var reportFileName = $"migration_report_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        File.WriteAllText(reportFileName, reportContent);
        System.Console.WriteLine($"Detailed report saved to: {reportFileName}");
    }
}

public class CompleteMigrationReport
{
    public bool Success { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public TimeSpan DatabaseSetupDuration { get; set; }
    public TimeSpan SqlServerValidationDuration { get; set; }
    public TimeSpan PostgresValidationDuration { get; set; }
    public TimeSpan CrossValidationDuration { get; set; }
    public SeedingResult? SeedingResult { get; set; }
    public MigrationResult? MigrationResult { get; set; }
    public DataValidationResult? SqlServerValidation { get; set; }
    public DataValidationResult? PostgresValidation { get; set; }
    public CrossValidationResult? CrossValidation { get; set; }
    public string? ErrorMessage { get; set; }
}

public class DataValidationResult
{
    public bool IsValid { get; set; }
    public int CategoryCount { get; set; }
    public int TagCount { get; set; }
    public int ProductCount { get; set; }
    public int ProductDetailCount { get; set; }
    public int ProductTagCount { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
}

public class CrossValidationResult
{
    public bool IsConsistent { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
}