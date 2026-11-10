using Claims.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Claims.Tests;

public class TestDatabase : IDisposable
{
    public SqliteConnection Connection { get; } = new("Data Source=:memory:;Foreign Keys=True");

    public TestDatabase()
    {
        Connection.Open();
        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public ClaimsDbContext CreateContext(params IInterceptor[] interceptors) =>
        new(new DbContextOptionsBuilder<ClaimsDbContext>().UseSqlite(Connection)
            .AddInterceptors(interceptors).Options, new TestClock());

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
            Connection.Dispose();
    }
}