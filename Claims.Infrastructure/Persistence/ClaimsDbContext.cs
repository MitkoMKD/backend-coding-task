using Claims.Application;
using Claims.Auditing;
using Claims.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Claims.Infrastructure;

public class ClaimsDbContext(DbContextOptions<ClaimsDbContext> options, TimeProvider timeProvider)
    : DbContext(options), IUnitOfWork
{
    public DbSet<Claim> Claims => Set<Claim>();
    public DbSet<Cover> Covers => Set<Cover>();
    public DbSet<AuditMessage> AuditOutbox => Set<AuditMessage>();
    public DbSet<ClaimAudit> ClaimAudits => Set<ClaimAudit>();
    public DbSet<CoverAudit> CoverAudits => Set<CoverAudit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Claim>(entity =>
        {
            entity.Property(claim => claim.Id).HasMaxLength(36);
            entity.Property(claim => claim.CoverId).HasMaxLength(36);
            entity.Property(claim => claim.Name).HasMaxLength(200);
            entity.Property(claim => claim.DamageCost).HasPrecision(18, 2);
            entity.HasOne<Cover>().WithMany().HasForeignKey(claim => claim.CoverId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable("Claims", table => table.HasCheckConstraint("CK_Claims_DamageCost", "CAST([DamageCost] AS decimal(18, 2)) BETWEEN 0 AND 100000"));
        });
        modelBuilder.Entity<Cover>(entity =>
        {
            entity.Property(cover => cover.Id).HasMaxLength(36);
            entity.Property(cover => cover.Premium).HasPrecision(18, 2);
            entity.ToTable("Covers", table => table.HasCheckConstraint("CK_Covers_Dates", "[EndDate] > [StartDate]"));
        });
        modelBuilder.Entity<AuditMessage>(entity =>
        {
            entity.Property(message => message.EntityId).HasMaxLength(36);
            entity.HasIndex(message => message.Created);
        });
        modelBuilder.Entity<ClaimAudit>().HasIndex(audit => audit.EventId).IsUnique();
        modelBuilder.Entity<CoverAudit>().HasIndex(audit => audit.EventId).IsUnique();
        modelBuilder.Entity<ClaimAudit>().Property(audit => audit.ClaimId).HasMaxLength(36);
        modelBuilder.Entity<CoverAudit>().Property(audit => audit.CoverId).HasMaxLength(36);
        modelBuilder.Entity<ClaimAudit>().Property(audit => audit.HttpRequestType).HasMaxLength(6);
        modelBuilder.Entity<CoverAudit>().Property(audit => audit.HttpRequestType).HasMaxLength(6);
    }

    public void EnqueueAudit(AuditEntity entity, string entityId, AuditOperation operation) =>
        AuditOutbox.Add(new AuditMessage
        {
            Entity = entity,
            EntityId = entityId,
            Operation = operation,
            Created = timeProvider.GetUtcNow().UtcDateTime
        });

    async Task IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessException("resource", "The resource changed. Refresh and retry.", BusinessError.Conflict);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 547 })
        {
            throw new BusinessException("resource", "The operation conflicts with related data or a database constraint.", BusinessError.Conflict);
        }
    }
}