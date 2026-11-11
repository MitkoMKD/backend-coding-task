using Claims.Models;

namespace Claims.Application;

public class ClaimService(IClaimRepository claims, ICoverRepository covers, IUnitOfWork unitOfWork) : IClaimService
{
    public Task<List<Claim>> ListAsync(CancellationToken cancellationToken) => claims.ListAsync(cancellationToken);

    public async Task<Claim> GetAsync(string id, CancellationToken cancellationToken) =>
        await claims.GetAsync(id, cancellationToken)
        ?? throw new BusinessException("id", "Claim not found.", BusinessError.NotFound);

    public async Task<Claim> CreateAsync(CreateClaimRequest request, CancellationToken cancellationToken)
    {
        if (request.DamageCost is < 0 or > 100000)
            throw new BusinessException("damageCost", "Damage cost must be between 0 and 100000.");
        if (decimal.Round(request.DamageCost, 2) != request.DamageCost)
            throw new BusinessException("damageCost", "Damage cost cannot have more than two decimal places.");
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 200)
            throw new BusinessException("name", "Name is required and cannot exceed 200 characters.");
        if (!Enum.IsDefined(request.Type))
            throw new BusinessException("type", "Unknown claim type.");
        if (string.IsNullOrWhiteSpace(request.CoverId))
            throw new BusinessException("coverId", "A related cover is required.");

        var cover = await covers.GetAsync(request.CoverId, cancellationToken)
            ?? throw new BusinessException("coverId", "Related cover does not exist.");
        if (request.Created < cover.StartDate || request.Created >= cover.EndDate)
            throw new BusinessException("created", "Claim date must fall within the cover period (end date excluded).");

        var claim = new Claim
        {
            CoverId = request.CoverId,
            Created = request.Created,
            Name = request.Name.Trim(),
            Type = request.Type,
            DamageCost = request.DamageCost
        };
        claims.Add(claim);
        unitOfWork.EnqueueAudit(AuditEntity.Claim, claim.Id, AuditOperation.Create);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return claim;
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var claim = await GetAsync(id, cancellationToken);
        claims.Remove(claim);
        unitOfWork.EnqueueAudit(AuditEntity.Claim, id, AuditOperation.Delete);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}