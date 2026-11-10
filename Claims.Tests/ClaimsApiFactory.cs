using System.Text.Json;
using System.Text.Json.Serialization;
using Claims.Auditing;
using Claims.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Claims.Tests;

public class ClaimsApiFactory : WebApplicationFactory<Program>
{
    private readonly TestDatabase database = new();
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ClaimsDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ClaimsDbContext>>();
            services.AddDbContext<ClaimsDbContext>(options => options.UseSqlite(database.Connection));
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new TestClock());
            var worker = services.SingleOrDefault(descriptor =>
                descriptor.ServiceType == typeof(IHostedService) && descriptor.ImplementationType == typeof(AuditWorker));
            if (worker is not null)
                services.Remove(worker);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            database.Dispose();
    }
}