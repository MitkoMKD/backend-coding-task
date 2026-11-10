using Claims.Application;

namespace Claims.Auditing;

public class AuditMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public AuditEntity Entity { get; set; }
    public required string EntityId { get; set; }
    public AuditOperation Operation { get; set; }
    public DateTime Created { get; set; }
}