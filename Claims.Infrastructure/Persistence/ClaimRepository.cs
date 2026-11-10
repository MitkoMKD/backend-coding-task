using Claims.Application;
using Claims.Models;
using Microsoft.EntityFrameworkCore;

namespace Claims.Infrastructure;

public class ClaimRepository(ClaimsDbContext context) : IClaimRepository
{
    public Task<List<Claim>> ListAsync(CancellationToken cancellationToken) =>
        context.Claims.AsNoTracking().OrderBy(claim => claim.Id).ToListAsync(cancellationToken);
    public Task<Claim?> GetAsync(string id, CancellationToken cancellationToken) =>
        context.Claims.SingleOrDefaultAsync(claim => claim.Id == id, cancellationToken);
    public Task<bool> ExistsForCoverAsync(string coverId, CancellationToken cancellationToken) =>
        context.Claims.AnyAsync(claim => claim.CoverId == coverId, cancellationToken);
    public void Add(Claim claim) => context.Claims.Add(claim);
    public void Remove(Claim claim) => context.Claims.Remove(claim);
}