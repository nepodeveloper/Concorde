namespace Concorde.IntegrationTests;

using Concorde.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Provides a real SQLite database (in-memory, kept alive by an open connection)
/// so tests exercise actual relational constraints and SQL translation.
/// </summary>
public sealed class SqliteDatabaseFixture : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ConcordeDbContext> _options;

    public SqliteDatabaseFixture()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<ConcordeDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public ConcordeDbContext CreateContext() => new(_options);

    public void Dispose() => _connection.Dispose();
}
