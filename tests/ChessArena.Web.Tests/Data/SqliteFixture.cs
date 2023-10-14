using ChessArena.Web.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ChessArena.Web.Tests.Data;

/// <summary>A private in-memory SQLite database that lives as long as the test class instance.</summary>
public abstract class SqliteTestBase : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    protected SqliteTestBase()
    {
        _connection.Open();
        using var db = NewContext();
        db.Database.EnsureCreated();
    }

    protected ArenaDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ArenaDbContext>().UseSqlite(_connection).Options);

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
