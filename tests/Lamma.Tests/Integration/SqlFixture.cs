using Lamma.Api.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lamma.Tests.Integration;

[CollectionDefinition("SqlServer")]
public sealed class SqlCollection : ICollectionFixture<SqlFixture>;

public sealed class SqlFixture : IAsyncLifetime
{
    private readonly string databaseName = $"LammaTests_{Guid.NewGuid():N}";
    public string ConnectionString { get; private set; } = "";

    public AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options);

    public async Task InitializeAsync()
    {
        var cs = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("LAMMA_TEST_SQL")
            ?? "Server=.\\SQLEXPRESS;Integrated Security=True;Encrypt=False;TrustServerCertificate=True")
            { InitialCatalog = databaseName };
        ConnectionString = cs.ConnectionString;
        await using var db = CreateDb();
        await db.Database.MigrateAsync();
        await SeedData.ApplyAsync(db);
    }

    public async Task DisposeAsync()
    {
        // Drop only the unique database this fixture created, never the configured database.
        if (!databaseName.StartsWith("LammaTests_", StringComparison.Ordinal) ||
            new SqlConnectionStringBuilder(ConnectionString).InitialCatalog != databaseName)
            throw new InvalidOperationException("Unexpected test database name.");
        await using var db = CreateDb();
        await db.Database.EnsureDeletedAsync();
    }
}
