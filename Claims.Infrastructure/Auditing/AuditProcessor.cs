using Claims.Application;
using Claims.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Claims.Auditing;

public class AuditProcessor(ClaimsDbContext context)
{
    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var message = await context.AuditOutbox.OrderBy(message => message.Created).ThenBy(message => message.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (message is null)
            return false;

        var operation = message.Operation switch
        {
            AuditOperation.Create => "POST",
            AuditOperation.Delete => "DELETE",
            _ => throw new InvalidOperationException("Unknown audit operation.")
        };
        switch (message.Entity)
        {
            case AuditEntity.Claim:
                context.ClaimAudits.Add(new ClaimAudit
                {
                    EventId = message.Id, ClaimId = message.EntityId,
                    Created = message.Created, HttpRequestType = operation
                });
                break;
            case AuditEntity.Cover:
                context.CoverAudits.Add(new CoverAudit
                {
                    EventId = message.Id, CoverId = message.EntityId,
                    Created = message.Created, HttpRequestType = operation
                });
                break;
            default:
                throw new InvalidOperationException("Unknown audit entity.");
        }
        context.AuditOutbox.Remove(message);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}