using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PostgreMigrationPOC.Infrastructure.Postgres.Data;

public class PostgresDbContextFactory : IDesignTimeDbContextFactory<PostgresDbContext>
{
    public PostgresDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PostgresDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=PostgreMigrationPOC_Postgres;Username=postgres;Password=Dev@123456");
        
        return new PostgresDbContext(optionsBuilder.Options);
    }
}