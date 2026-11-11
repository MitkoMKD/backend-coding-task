using Claims.Models;

namespace Claims.Application;

public interface IClaimService
{
    Task<List<Claim>> ListAsync(CancellationToken cancellationToken);
    Task<Claim> GetAsync(string id, CancellationToken cancellationToken);
    Task<Claim> CreateAsync(CreateClaimRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(string id, CancellationToken cancellationToken);
}