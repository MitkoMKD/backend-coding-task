using Claims.Application;
using Claims.Auditing;
using Claims.ExceptionHandling;
using Claims.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options => options.SuppressAsyncSuffixInActionNames = false)
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<ClaimsDbContext>(options => options.UseSqlServer(
    builder.Configuration.GetConnectionString("MainConnection")
    ?? throw new InvalidOperationException("Configure ConnectionStrings:MainConnection with a SQL Server connection string.")));
builder.Services.AddScoped<IClaimRepository, ClaimRepository>();
builder.Services.AddScoped<ICoverRepository, CoverRepository>();
builder.Services.AddScoped<IUnitOfWork>(services => services.GetRequiredService<ClaimsDbContext>());
builder.Services.AddScoped<ClaimService>();
builder.Services.AddScoped<CoverService>();
builder.Services.AddScoped<AuditProcessor>();
if (builder.Configuration.GetValue("Auditing:Enabled", true))
    builder.Services.AddHostedService<AuditWorker>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers();

if (app.Configuration.GetValue("Database:ApplyMigrations", false))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<ClaimsDbContext>().Database.MigrateAsync();
}

await app.RunAsync();
