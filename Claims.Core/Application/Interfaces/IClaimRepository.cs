using Claims.Models;

namespace Claims.Application;

public interface IClaimRepository
{
    Task<List<Claim>> ListAsync(CancellationToken cancellationToken);
    Task<Claim?> GetAsync(string id, CancellationToken cancellationToken);
    Task<bool> ExistsForCoverAsync(string coverId, CancellationToken cancellationToken);
    void Add(Claim claim);
    void Remove(Claim claim);
}