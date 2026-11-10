using Claims.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Claims.Tests;

internal class FailAuditWrite : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context!.ChangeTracker.Entries<ClaimAudit>().Any(entry => entry.State == EntityState.Added))
            throw new InvalidOperationException("Simulated audit storage failure.");
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}