using System.Diagnostics;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PostgreMigrationPOC.Infrastructure.SqlServer.Data;
using PostgreMigrationPOC.Infrastructure.Postgres.Data;
using PostgreMigrationPOC.Infrastructure.Postgres.Services;
using PostgreMigrationPOC.Infrastructure.SqlServer.Services;

namespace PostgreMigrationPOC.Console.Services;

public class MigrationOnlyOrchestrator
{
    private readonly SqlServerDbContext _sqlServerContext;
    private readonly PostgresDbContext _postgresContext;

    public MigrationOnlyOrchestrator(SqlServerDbContext sqlServerContext, PostgresDbContext postgresContext)
    {
        _sqlServerContext = sqlServerContext;
        _postgresContext = postgresContext;
    }

    public async Task<MigrationOnlyReport> ExecuteMigrationAsync()
    {
        var report = new MigrationOnlyReport
        {
            StartTime = DateTime.UtcNow
        };

        var totalStopwatch = Stopwatch.StartNew();

        try
        {
            System.Console.WriteLine("=== Data Migration Process ===");
            System.Console.WriteLine($"Starting data migration from SQL Server to PostgreSQL");
            System.Console.WriteLine($"Start Time: {report.StartTime:yyyy-MM-dd HH:mm:ss} UTC");
            System.Console.WriteLine();

            // Step 1: Validate SQL Server data exists
            System.Console.WriteLine("Step 1: Checking SQL Server data...");
            var sqlValidationStopwatch = Stopwatch.StartNew();
            var sqlValidation = await ValidateSqlServerDataAsync();
            sqlValidationStopwatch.Stop();
            
            report.SqlServerValidation = sqlValidation;
            report.SqlServerValidationDuration = sqlValidationStopwatch.Elapsed;
            System.Console.WriteLine($"SQL Server validation completed in {sqlValidationStopwatch.ElapsedMilliseconds:N0}ms");
            System.Console.WriteLine($"Total records found: {sqlValidation.CategoryCount + sqlValidation.TagCount + sqlValidation.ProductCount + sqlValidation.ProductDetailCount + sqlValidation.ProductTagCount:N0}");
            
            if (!sqlValidation.IsValid)
            {
                throw new InvalidOperationException("SQL Server data validation failed. Cannot proceed with migration.");
            }

            if (sqlValidation.ProductCount == 0)
            {
                throw new InvalidOperationException("No data found in SQL Server. Please run data seeding first.");
            }
            System.Console.WriteLine();

            // Step 2: Setup PostgreSQL database
            System.Console.WriteLine("Step 2: Setting up PostgreSQL database...");
            var dbSetupStopwatch = Stopwatch.StartNew();
            
            await _postgresContext.Database.EnsureCreatedAsync();
            
            dbSetupStopwatch.Stop();
            report.DatabaseSetupDuration = dbSetupStopwatch.Elapsed;
            System.Console.WriteLine($"PostgreSQL database setup completed in {dbSetupStopwatch.ElapsedMilliseconds:N0}ms");
            System.Console.WriteLine();

            // Step 3: Migrate data to PostgreSQL
            System.Console.WriteLine("Step 3: Migrating data to PostgreSQL...");
            var migrationService = new DataMigrationService(_sqlServerContext, _postgresContext, new SimpleLogger<DataMigrationService>());
            var migrationResult = await migrationService.MigrateDataAsync();
            
            report.MigrationResult = migrationResult;
            System.Console.WriteLine($"Migration completed in {migrationResult.TotalDuration.TotalMilliseconds:N0}ms");
            System.Console.WriteLine($"Records migrated: {migrationResult.TotalRecords:N0}");
            System.Console.WriteLine($"Migration throughput: {migrationResult.TotalRecords / migrationResult.TotalDuration.TotalSeconds:N0} records/second");
            System.Console.WriteLine();

            // Step 4: Validate PostgreSQL data
            System.Console.WriteLine("Step 4: Validating PostgreSQL data...");
            var pgValidationStopwatch = Stopwatch.StartNew();
            var pgValidation = await ValidatePostgresDataAsync();
            pgValidationStopwatch.Stop();
            
            report.PostgresValidation = pgValidation;
            report.PostgresValidationDuration = pgValidationStopwatch.Elapsed;
            System.Console.WriteLine($"PostgreSQL validation completed in {pgValidationStopwatch.ElapsedMilliseconds:N0}ms");
            System.Console.WriteLine($"Validation passed: {pgValidation.IsValid}");
            System.Console.WriteLine();

            // Step 5: Cross-validate data consistency
            System.Console.WriteLine("Step 5: Cross-validating data consistency...");
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
            report.Success = migrationResult.Success && crossValidation.IsConsistent;

            // Generate final report
            GenerateMigrationReport(report);

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

        result.ValidationErrors = errors;
        result.IsConsistent = errors.Count == 0;

        return result;
    }

    private void GenerateMigrationReport(MigrationOnlyReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=".PadRight(70, '='));
        sb.AppendLine(" DATA MIGRATION REPORT");
        sb.AppendLine("=".PadRight(70, '='));
        sb.AppendLine();
        
        sb.AppendLine($"Migration Status: {(report.Success ? "SUCCESS" : "FAILED")}");
        sb.AppendLine($"Start Time: {report.StartTime:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"End Time: {report.EndTime:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"Total Duration: {report.TotalDuration.TotalMinutes:F2} minutes");
        sb.AppendLine();
        
        // Performance Metrics
        sb.AppendLine("PERFORMANCE METRICS:");
        sb.AppendLine($"  Database Setup: {report.DatabaseSetupDuration.TotalMilliseconds:N0}ms");
        sb.AppendLine($"  SQL Server Validation: {report.SqlServerValidationDuration.TotalMilliseconds:N0}ms");
        
        if (report.MigrationResult != null)
        {
            sb.AppendLine($"  Data Migration: {report.MigrationResult.TotalDuration.TotalMilliseconds:N0}ms");
            sb.AppendLine($"  Migration Throughput: {report.MigrationResult.TotalRecords / report.MigrationResult.TotalDuration.TotalSeconds:N0} records/sec");
        }
        
        sb.AppendLine($"  PostgreSQL Validation: {report.PostgresValidationDuration.TotalMilliseconds:N0}ms");
        sb.AppendLine($"  Cross Validation: {report.CrossValidationDuration.TotalMilliseconds:N0}ms");
        sb.AppendLine();
        
        // Migration Summary
        sb.AppendLine("MIGRATION SUMMARY:");
        if (report.MigrationResult != null)
        {
            sb.AppendLine($"  Categories: {report.MigrationResult.CategoriesMigrated:N0}");
            sb.AppendLine($"  Tags: {report.MigrationResult.TagsMigrated:N0}");
            sb.AppendLine($"  Products: {report.MigrationResult.ProductsMigrated:N0}");
            sb.AppendLine($"  Product Details: {report.MigrationResult.ProductDetailsMigrated:N0}");
            sb.AppendLine($"  Product Tags: {report.MigrationResult.ProductTagsMigrated:N0}");
            sb.AppendLine($"  Total Records: {report.MigrationResult.TotalRecords:N0}");
        }
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
        sb.AppendLine("=".PadRight(70, '='));
        
        var reportContent = sb.ToString();
        System.Console.WriteLine(reportContent);
        
        // Save report to file
        var reportFileName = $"migration_only_report_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        File.WriteAllText(reportFileName, reportContent);
        System.Console.WriteLine($"Detailed report saved to: {reportFileName}");
    }
}

public class MigrationOnlyReport
{
    public bool Success { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public TimeSpan DatabaseSetupDuration { get; set; }
    public TimeSpan SqlServerValidationDuration { get; set; }
    public TimeSpan PostgresValidationDuration { get; set; }
    public TimeSpan CrossValidationDuration { get; set; }
    public MigrationResult? MigrationResult { get; set; }
    public DataValidationResult? SqlServerValidation { get; set; }
    public DataValidationResult? PostgresValidation { get; set; }
    public CrossValidationResult? CrossValidation { get; set; }
    public string? ErrorMessage { get; set; }
}

