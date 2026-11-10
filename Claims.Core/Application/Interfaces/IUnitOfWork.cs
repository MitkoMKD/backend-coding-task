namespace Claims.Application;

public interface IUnitOfWork
{
    void EnqueueAudit(AuditEntity entity, string entityId, AuditOperation operation);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}