using Claims.Application;
using Claims.Auditing;
using Claims.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Claims.Tests;

public class AuditingTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(AuditEntity.Claim, AuditOperation.Create)]
    [InlineData(AuditEntity.Claim, AuditOperation.Delete)]
    [InlineData(AuditEntity.Cover, AuditOperation.Create)]
    [InlineData(AuditEntity.Cover, AuditOperation.Delete)]
    public async Task Pending_audit_survives_context_disposal_and_is_processed_once(AuditEntity entity, AuditOperation operation)
    {
        using var database = new TestDatabase();
        Guid eventId;
        await using (var context = database.CreateContext())
        {
            context.EnqueueAudit(entity, "deleted-entity", operation);
            await context.SaveChangesAsync(Token);
            eventId = (await context.AuditOutbox.SingleAsync(Token)).Id;
        }

        await using (var context = database.CreateContext())
        {
            var processor = new AuditProcessor(context);
            Assert.True(await processor.ProcessNextAsync(Token));
            Assert.False(await processor.ProcessNextAsync(Token));
        }

        await using var verification = database.CreateContext();
        Assert.Empty(await verification.AuditOutbox.ToListAsync(Token));
        var expectedOperation = operation == AuditOperation.Create ? "POST" : "DELETE";
        if (entity == AuditEntity.Claim)
        {
            var audit = await verification.ClaimAudits.SingleAsync(Token);
            Assert.Equal(eventId, audit.EventId);
            Assert.Equal("deleted-entity", audit.ClaimId);
            Assert.Equal(expectedOperation, audit.HttpRequestType);
            Assert.Equal(new TestClock().GetUtcNow().UtcDateTime, audit.Created);
            Assert.Empty(await verification.CoverAudits.ToListAsync(Token));
        }
        else
        {
            var audit = await verification.CoverAudits.SingleAsync(Token);
            Assert.Equal(eventId, audit.EventId);
            Assert.Equal("deleted-entity", audit.CoverId);
            Assert.Equal(expectedOperation, audit.HttpRequestType);
            Assert.Empty(await verification.ClaimAudits.ToListAsync(Token));
        }
    }

    [Fact]
    public async Task Business_change_and_outbox_roll_back_together_on_constraint_failure()
    {
        using var database = new TestDatabase();
        await using (var context = database.CreateContext())
        {
            var cover = new Cover { StartDate = new(2028, 1, 1), EndDate = new(2028, 2, 1) };
            context.Covers.Add(cover);
            context.Claims.Add(new Claim { CoverId = "missing-cover", Name = "Invalid claim" });
            context.EnqueueAudit(AuditEntity.Cover, cover.Id, AuditOperation.Create);
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(Token));
        }
        await using var verification = database.CreateContext();
        Assert.Empty(await verification.Covers.ToListAsync(Token));
        Assert.Empty(await verification.Claims.ToListAsync(Token));
        Assert.Empty(await verification.AuditOutbox.ToListAsync(Token));
    }

    [Fact]
    public async Task Failed_audit_write_preserves_pending_message_for_retry()
    {
        using var database = new TestDatabase();
        await using (var context = database.CreateContext())
        {
            context.EnqueueAudit(AuditEntity.Claim, "claim", AuditOperation.Create);
            await context.SaveChangesAsync(Token);
        }
        await using (var failingContext = database.CreateContext(new FailAuditWrite()))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => new AuditProcessor(failingContext).ProcessNextAsync(Token));
        }
        await using (var verification = database.CreateContext())
        {
            Assert.Single(await verification.AuditOutbox.ToListAsync(Token));
            Assert.Empty(await verification.ClaimAudits.ToListAsync(Token));
            Assert.True(await new AuditProcessor(verification).ProcessNextAsync(Token));
        }
        await using var recovered = database.CreateContext();
        Assert.Empty(await recovered.AuditOutbox.ToListAsync(Token));
        Assert.Single(await recovered.ClaimAudits.ToListAsync(Token));
    }

    [Fact]
    public async Task Duplicate_event_ids_are_rejected_by_database()
    {
        using var database = new TestDatabase();
        await using var context = database.CreateContext();
        var eventId = Guid.NewGuid();
        context.ClaimAudits.Add(new ClaimAudit { EventId = eventId, ClaimId = "claim", Created = DateTime.UtcNow });
        await context.SaveChangesAsync(Token);
        context.ClaimAudits.Add(new ClaimAudit { EventId = eventId, ClaimId = "claim", Created = DateTime.UtcNow });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(Token));
    }

}