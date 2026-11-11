using Claims.Application;
using Claims.Auditing;
using Claims.Infrastructure;
using Claims.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Claims.Tests;

public class SqlServerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void Sql_server_migration_matches_model_and_generates_required_schema()
    {
        using var context = new ClaimsDbContext(new DbContextOptionsBuilder<ClaimsDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=ClaimsSchemaCheck;Trusted_Connection=True").Options, new TestClock());
        Assert.False(context.Database.HasPendingModelChanges());
        var script = context.GetService<IMigrator>().GenerateScript();
        Assert.Contains("CREATE TABLE [Claims]", script);
        Assert.Contains("CREATE TABLE [Covers]", script);
        Assert.Contains("CREATE TABLE [AuditOutbox]", script);
        Assert.Contains("FOREIGN KEY ([CoverId])", script);
        Assert.Contains("decimal(18,2)", script);
        Assert.Contains("CREATE UNIQUE INDEX [IX_ClaimAudits_EventId]", script);
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public async Task Real_sql_server_applies_migrations_persists_business_data_and_processes_audits()
    {
        var configuredConnection = Environment.GetEnvironmentVariable("CLAIMS_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(configuredConnection))
            Assert.Skip("Set CLAIMS_TEST_SQLSERVER to run the real SQL Server test (requires create-database permission).");

        var connection = new SqlConnectionStringBuilder(configuredConnection)
        {
            InitialCatalog = "ClaimsTests_" + Guid.NewGuid().ToString("N")
        };
        var options = new DbContextOptionsBuilder<ClaimsDbContext>().UseSqlServer(connection.ConnectionString).Options;
        await using var context = new ClaimsDbContext(options, new TestClock());
        try
        {
            await context.Database.MigrateAsync(Token);
            var claimRepository = new ClaimRepository(context);
            var coverRepository = new CoverRepository(context);
            var covers = new CoverService(coverRepository, claimRepository, context, new TestClock());
            var claims = new ClaimService(claimRepository, coverRepository, context);
            var cover = await covers.CreateAsync(new CreateCoverRequest
            {
                StartDate = new(2028, 1, 1), EndDate = new(2028, 2, 1), Type = CoverType.Yacht
            }, Token);
            var claim = await claims.CreateAsync(new CreateClaimRequest
            {
                CoverId = cover.Id, Created = cover.StartDate, Name = "Collision", Type = ClaimType.Collision, DamageCost = 100000
            }, Token);
            Assert.Equal(42556.25m, cover.Premium);
            Assert.Equal(100000m, (await claims.GetAsync(claim.Id, Token)).DamageCost);
            Assert.Equal(2, await context.AuditOutbox.CountAsync(Token));
            var processor = new AuditProcessor(context);
            Assert.True(await processor.ProcessNextAsync(Token));
            Assert.True(await processor.ProcessNextAsync(Token));
            Assert.False(await processor.ProcessNextAsync(Token));
            Assert.Equal(1, await context.ClaimAudits.CountAsync(Token));
            Assert.Equal(1, await context.CoverAudits.CountAsync(Token));

            context.ChangeTracker.Clear();
            context.Covers.Remove(await context.Covers.SingleAsync(Token));
            var unitOfWork = (IUnitOfWork)context;
            var conflict = await Assert.ThrowsAsync<BusinessException>(() => unitOfWork.SaveChangesAsync(Token));
            Assert.Equal(BusinessError.Conflict, conflict.Error);
        }
        finally
        {
            await context.Database.EnsureDeletedAsync(CancellationToken.None);
        }
    }
}