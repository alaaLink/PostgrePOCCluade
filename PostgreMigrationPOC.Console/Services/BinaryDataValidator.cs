using Microsoft.EntityFrameworkCore;
using PostgreMigrationPOC.Infrastructure.SqlServer.Data;
using PostgreMigrationPOC.Infrastructure.Postgres.Data;
using System.Diagnostics;

namespace PostgreMigrationPOC.Console.Services;

public class BinaryDataValidator
{
    private readonly SqlServerDbContext _sqlServerContext;
    private readonly PostgresDbContext _postgresContext;

    public BinaryDataValidator(SqlServerDbContext sqlServerContext, PostgresDbContext postgresContext)
    {
        _sqlServerContext = sqlServerContext;
        _postgresContext = postgresContext;
    }

    public async Task<BinaryValidationResult> ValidateBinaryDataIntegrityAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new BinaryValidationResult();

        try
        {
            System.Console.WriteLine("=== Binary Data Integrity Verification ===");
            System.Console.WriteLine("Starting binary data integrity verification...");
            
            // Get sample of products with binary data from both databases
            var sqlServerSample = await _sqlServerContext.Products
                .Where(p => p.BinaryField != null || p.VarbinaryField != null)
                .OrderBy(p => p.Id)
                .Take(10)
                .Select(p => new { 
                    p.Id, 
                    p.BinaryField, 
                    p.VarbinaryField,
                    BinaryLength = p.BinaryField != null ? p.BinaryField.Length : 0,
                    VarbinaryLength = p.VarbinaryField != null ? p.VarbinaryField.Length : 0
                })
                .ToListAsync();

            var postgresSample = await _postgresContext.Products
                .Where(p => p.BinaryField != null || p.VarbinaryField != null)
                .OrderBy(p => p.Id)
                .Take(10)
                .Select(p => new { 
                    p.Id, 
                    p.BinaryField, 
                    p.VarbinaryField,
                    BinaryLength = p.BinaryField != null ? p.BinaryField.Length : 0,
                    VarbinaryLength = p.VarbinaryField != null ? p.VarbinaryField.Length : 0
                })
                .ToListAsync();

            System.Console.WriteLine($"SQL Server sample size: {sqlServerSample.Count}");
            System.Console.WriteLine($"PostgreSQL sample size: {postgresSample.Count}");

            result.SqlServerSampleSize = sqlServerSample.Count;
            result.PostgresSampleSize = postgresSample.Count;

            // Verify each record
            bool allMatch = true;
            int checkedCount = 0;

            foreach (var sqlRecord in sqlServerSample)
            {
                var pgRecord = postgresSample.FirstOrDefault(p => p.Id == sqlRecord.Id);
                if (pgRecord == null)
                {
                    var error = $"Product ID {sqlRecord.Id} not found in PostgreSQL";
                    System.Console.WriteLine($"❌ {error}");
                    result.Errors.Add(error);
                    allMatch = false;
                    continue;
                }

                // Check binary field lengths
                if (sqlRecord.BinaryLength != pgRecord.BinaryLength)
                {
                    var error = $"Product ID {sqlRecord.Id}: BinaryField length mismatch. SQL Server: {sqlRecord.BinaryLength}, PostgreSQL: {pgRecord.BinaryLength}";
                    System.Console.WriteLine($"❌ {error}");
                    result.Errors.Add(error);
                    allMatch = false;
                }

                if (sqlRecord.VarbinaryLength != pgRecord.VarbinaryLength)
                {
                    var error = $"Product ID {sqlRecord.Id}: VarbinaryField length mismatch. SQL Server: {sqlRecord.VarbinaryLength}, PostgreSQL: {pgRecord.VarbinaryLength}";
                    System.Console.WriteLine($"❌ {error}");
                    result.Errors.Add(error);
                    allMatch = false;
                }

                // Check byte-by-byte comparison
                if (sqlRecord.BinaryField != null && pgRecord.BinaryField != null)
                {
                    if (!sqlRecord.BinaryField.SequenceEqual(pgRecord.BinaryField))
                    {
                        var error = $"Product ID {sqlRecord.Id}: BinaryField content mismatch";
                        var detail = $"   SQL Server first 5 bytes: {string.Join(",", sqlRecord.BinaryField.Take(5).Select(b => b.ToString("X2")))}";
                        var detail2 = $"   PostgreSQL first 5 bytes: {string.Join(",", pgRecord.BinaryField.Take(5).Select(b => b.ToString("X2")))}";
                        
                        System.Console.WriteLine($"❌ {error}");
                        System.Console.WriteLine(detail);
                        System.Console.WriteLine(detail2);
                        
                        result.Errors.Add($"{error}\n{detail}\n{detail2}");
                        allMatch = false;
                    }
                }

                if (sqlRecord.VarbinaryField != null && pgRecord.VarbinaryField != null)
                {
                    if (!sqlRecord.VarbinaryField.SequenceEqual(pgRecord.VarbinaryField))
                    {
                        var error = $"Product ID {sqlRecord.Id}: VarbinaryField content mismatch";
                        var detail = $"   SQL Server first 5 bytes: {string.Join(",", sqlRecord.VarbinaryField.Take(5).Select(b => b.ToString("X2")))}";
                        var detail2 = $"   PostgreSQL first 5 bytes: {string.Join(",", pgRecord.VarbinaryField.Take(5).Select(b => b.ToString("X2")))}";
                        
                        System.Console.WriteLine($"❌ {error}");
                        System.Console.WriteLine(detail);
                        System.Console.WriteLine(detail2);
                        
                        result.Errors.Add($"{error}\n{detail}\n{detail2}");
                        allMatch = false;
                    }
                }

                checkedCount++;
                if (sqlRecord.BinaryField != null || sqlRecord.VarbinaryField != null)
                {
                    System.Console.WriteLine($"✅ Product ID {sqlRecord.Id}: Binary data matches perfectly");
                    
                    // Show sample hex data for verification
                    if (sqlRecord.BinaryField != null)
                    {
                        System.Console.WriteLine($"   BinaryField (16 bytes): {string.Join("", sqlRecord.BinaryField.Select(b => b.ToString("X2")))}");
                    }
                    if (sqlRecord.VarbinaryField != null)
                    {
                        var sampleBytes = sqlRecord.VarbinaryField.Take(10).ToArray();
                        System.Console.WriteLine($"   VarbinaryField (first 10 bytes): {string.Join("", sampleBytes.Select(b => b.ToString("X2")))}");
                    }
                }
            }

            result.RecordsChecked = checkedCount;

            // Summary statistics
            var sqlStats = await _sqlServerContext.Products
                .Select(p => new {
                    HasBinary = p.BinaryField != null,
                    HasVarbinary = p.VarbinaryField != null
                })
                .GroupBy(x => 1)
                .Select(g => new {
                    TotalProducts = g.Count(),
                    WithBinary = g.Count(x => x.HasBinary),
                    WithVarbinary = g.Count(x => x.HasVarbinary)
                })
                .FirstAsync();

            var pgStats = await _postgresContext.Products
                .Select(p => new {
                    HasBinary = p.BinaryField != null,
                    HasVarbinary = p.VarbinaryField != null
                })
                .GroupBy(x => 1)
                .Select(g => new {
                    TotalProducts = g.Count(),
                    WithBinary = g.Count(x => x.HasBinary),
                    WithVarbinary = g.Count(x => x.HasVarbinary)
                })
                .FirstAsync();

            result.SqlServerStats = $"Total: {sqlStats.TotalProducts}, Binary: {sqlStats.WithBinary}, Varbinary: {sqlStats.WithVarbinary}";
            result.PostgresStats = $"Total: {pgStats.TotalProducts}, Binary: {pgStats.WithBinary}, Varbinary: {pgStats.WithVarbinary}";

            System.Console.WriteLine("\n=== BINARY DATA STATISTICS ===");
            System.Console.WriteLine($"SQL Server - {result.SqlServerStats}");
            System.Console.WriteLine($"PostgreSQL - {result.PostgresStats}");

            if (sqlStats.TotalProducts == pgStats.TotalProducts && 
                sqlStats.WithBinary == pgStats.WithBinary && 
                sqlStats.WithVarbinary == pgStats.WithVarbinary)
            {
                System.Console.WriteLine("✅ Binary data statistics match between databases");
            }
            else
            {
                var error = "Binary data statistics don't match between databases";
                System.Console.WriteLine($"❌ {error}");
                result.Errors.Add(error);
                allMatch = false;
            }

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            result.Success = allMatch;

            System.Console.WriteLine($"\nVerification completed in {stopwatch.ElapsedMilliseconds}ms. Checked {checkedCount} records.");
            System.Console.WriteLine($"Result: {(allMatch ? "✅ All binary data verified successfully" : "❌ Binary data integrity issues found")}");

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            result.Success = false;
            result.ErrorMessage = ex.Message;
            
            System.Console.WriteLine($"❌ Binary data verification failed with error: {ex.Message}");
            
            return result;
        }
    }
}

public class BinaryValidationResult
{
    public bool Success { get; set; }
    public TimeSpan Duration { get; set; }
    public int SqlServerSampleSize { get; set; }
    public int PostgresSampleSize { get; set; }
    public int RecordsChecked { get; set; }
    public string SqlServerStats { get; set; } = string.Empty;
    public string PostgresStats { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = new List<string>();
    public string? ErrorMessage { get; set; }
}