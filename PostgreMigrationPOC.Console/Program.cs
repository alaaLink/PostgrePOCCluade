using Microsoft.EntityFrameworkCore;
using PostgreMigrationPOC.Infrastructure.SqlServer.Data;
using PostgreMigrationPOC.Infrastructure.Postgres.Data;
using PostgreMigrationPOC.Console.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PostgreMigrationPOC.Console;

class Program
{
    static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        
        using var scope = host.Services.CreateScope();
        var sqlServerContext = scope.ServiceProvider.GetRequiredService<SqlServerDbContext>();
        var postgresContext = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        logger.LogInformation("Starting PostgreSQL Migration POC");
        
        try
        {
            // Show menu options
            var option = GetUserOption();
            
            switch (option)
            {
                case "1":
                    await ExecuteDataSeedingOnly(sqlServerContext);
                    break;
                case "2":
                    await ExecuteMigrationOnly(sqlServerContext, postgresContext);
                    break;
                case "3":
                    await ExecuteCompleteMigration(sqlServerContext, postgresContext);
                    break;
                case "4":
                    await ExecuteBinaryDataVerification(sqlServerContext, postgresContext);
                    break;
                default:
                    System.Console.WriteLine("Invalid option selected.");
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred during execution");
            System.Console.WriteLine($"❌ Critical error: {ex.Message}");
        }

        System.Console.WriteLine();
        System.Console.WriteLine("Press any key to exit...");
        System.Console.ReadKey();
    }

    private static async Task ExecuteDataSeedingOnly(SqlServerDbContext sqlServerContext)
    {
        System.Console.WriteLine("=== Data Seeding Only ===");
        var seedingOrchestrator = new DataSeedingOrchestrator(sqlServerContext);
        
        int productCount = GetProductCountFromUser();
        var report = await seedingOrchestrator.ExecuteDataSeedingAsync(productCount);
        
        if (report.Success)
        {
            System.Console.WriteLine();
            System.Console.WriteLine("🎉 Data seeding completed successfully!");
            System.Console.WriteLine($"Total time: {report.TotalDuration.TotalMinutes:F2} minutes");
            System.Console.WriteLine($"Total records created: {report.SeedingResult?.TotalRecords:N0}");
        }
        else
        {
            System.Console.WriteLine();
            System.Console.WriteLine("❌ Data seeding failed!");
            if (!string.IsNullOrEmpty(report.ErrorMessage))
            {
                System.Console.WriteLine($"Error: {report.ErrorMessage}");
            }
        }
    }

    private static async Task ExecuteMigrationOnly(SqlServerDbContext sqlServerContext, PostgresDbContext postgresContext)
    {
        System.Console.WriteLine("=== Data Migration Only ===");
        var migrationOrchestrator = new MigrationOnlyOrchestrator(sqlServerContext, postgresContext);
        
        var report = await migrationOrchestrator.ExecuteMigrationAsync();
        
        if (report.Success)
        {
            System.Console.WriteLine();
            System.Console.WriteLine("🎉 Data migration completed successfully!");
            System.Console.WriteLine($"Total time: {report.TotalDuration.TotalMinutes:F2} minutes");
            System.Console.WriteLine($"Total records migrated: {report.MigrationResult?.TotalRecords:N0}");
        }
        else
        {
            System.Console.WriteLine();
            System.Console.WriteLine("❌ Data migration failed!");
            if (!string.IsNullOrEmpty(report.ErrorMessage))
            {
                System.Console.WriteLine($"Error: {report.ErrorMessage}");
            }
        }
    }

    private static async Task ExecuteCompleteMigration(SqlServerDbContext sqlServerContext, PostgresDbContext postgresContext)
    {
        System.Console.WriteLine("=== Complete Migration (Seeding + Migration) ===");
        var orchestrator = new MigrationOrchestrator(sqlServerContext, postgresContext);
        
        int productCount = GetProductCountFromUser();
        var report = await orchestrator.ExecuteCompleteMigrationAsync(productCount);
        
        if (report.Success)
        {
            System.Console.WriteLine();
            System.Console.WriteLine("🎉 Complete migration completed successfully!");
            System.Console.WriteLine($"Total time: {report.TotalDuration.TotalMinutes:F2} minutes");
            System.Console.WriteLine($"Total records processed: {report.SeedingResult?.TotalRecords:N0}");
        }
        else
        {
            System.Console.WriteLine();
            System.Console.WriteLine("❌ Complete migration failed!");
            if (!string.IsNullOrEmpty(report.ErrorMessage))
            {
                System.Console.WriteLine($"Error: {report.ErrorMessage}");
            }
        }
    }

    private static async Task ExecuteBinaryDataVerification(SqlServerDbContext sqlServerContext, PostgresDbContext postgresContext)
    {
        System.Console.WriteLine("=== Binary Data Verification ===");
        var validator = new BinaryDataValidator(sqlServerContext, postgresContext);
        
        var result = await validator.ValidateBinaryDataIntegrityAsync();
        
        if (result.Success)
        {
            System.Console.WriteLine();
            System.Console.WriteLine("🎉 Binary data verification completed successfully!");
            System.Console.WriteLine($"Verification time: {result.Duration.TotalSeconds:F2} seconds");
            System.Console.WriteLine($"Records verified: {result.RecordsChecked}");
        }
        else
        {
            System.Console.WriteLine();
            System.Console.WriteLine("❌ Binary data verification failed!");
            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                System.Console.WriteLine($"Error: {result.ErrorMessage}");
            }
            if (result.Errors.Count > 0)
            {
                System.Console.WriteLine($"Found {result.Errors.Count} validation errors.");
            }
        }
    }

    private static string GetUserOption()
    {
        System.Console.WriteLine();
        System.Console.WriteLine("=== PostgreSQL Migration POC ===");
        System.Console.WriteLine("Select an option:");
        System.Console.WriteLine("1. Data Seeding Only (Generate test data in SQL Server)");
        System.Console.WriteLine("2. Migration Only (Migrate existing data from SQL Server to PostgreSQL)");
        System.Console.WriteLine("3. Complete Process (Data Seeding + Migration)");
        System.Console.WriteLine("4. Binary Data Verification (Verify binary data integrity between databases)");
        System.Console.WriteLine();
        System.Console.Write("Enter your choice (1-4): ");
        
        return System.Console.ReadLine() ?? "4";
    }

    private static int GetProductCountFromUser()
    {
        System.Console.WriteLine("Enter the number of products to generate and migrate (default: 10000):");
        System.Console.Write("> ");
        
        var input = System.Console.ReadLine();
        
        if (int.TryParse(input, out int count) && count > 0)
        {
            return count;
        }
        
        return 10000; // Default value
    }

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Configure SQL Server connection
                var sqlServerConnectionString = "Data Source=.;Database=PostgreMigrationPOC;User ID=sa;Password=Dev@123456;Connect Timeout=30;Encrypt=True;Trust Server Certificate=True;Application Intent=ReadWrite;Multi Subnet Failover=False";
                services.AddDbContext<SqlServerDbContext>(options =>
                    options.UseSqlServer(sqlServerConnectionString));

                // Configure PostgreSQL connection
                var postgresConnectionString = "Host=localhost;Database=PostgreMigrationPOC_Postgres;Username=postgres;Password=Dev@123456;";
                services.AddDbContext<PostgresDbContext>(options =>
                    options.UseNpgsql(postgresConnectionString));
            });
}
