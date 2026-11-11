using Claims.Models;

namespace Claims.Application;

public class CoverService(
    ICoverRepository covers, IClaimRepository claims, IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICoverService
{
    public Task<List<Cover>> ListAsync(CancellationToken cancellationToken) => covers.ListAsync(cancellationToken);

    public async Task<Cover> GetAsync(string id, CancellationToken cancellationToken) =>
        await covers.GetAsync(id, cancellationToken)
        ?? throw new BusinessException("id", "Cover not found.", BusinessError.NotFound);

    public decimal ComputePremium(DateOnly startDate, DateOnly endDate, CoverType type)
    {
        if (startDate < DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime))
            throw new BusinessException("startDate", "Start date cannot be in the past (UTC).");
        var latestEndDate = startDate.Year == 9999 ? DateOnly.MaxValue : startDate.AddYears(1);
        if (endDate <= startDate || endDate > latestEndDate)
            throw new BusinessException("endDate", "Cover must have a positive duration of at most one calendar year.");
        if (!Enum.IsDefined(type))
            throw new BusinessException("type", "Unknown cover type.");
        return PremiumCalculator.Calculate(startDate, endDate, type);
    }

    public async Task<Cover> CreateAsync(CreateCoverRequest request, CancellationToken cancellationToken)
    {
        var premium = ComputePremium(request.StartDate, request.EndDate, request.Type);
        var cover = new Cover
        {
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Type = request.Type,
            Premium = premium
        };
        covers.Add(cover);
        unitOfWork.EnqueueAudit(AuditEntity.Cover, cover.Id, AuditOperation.Create);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return cover;
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var cover = await GetAsync(id, cancellationToken);
        if (await claims.ExistsForCoverAsync(id, cancellationToken))
            throw new BusinessException("id", "Cannot delete a cover with existing claims.", BusinessError.Conflict);
        covers.Remove(cover);
        unitOfWork.EnqueueAudit(AuditEntity.Cover, id, AuditOperation.Delete);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}