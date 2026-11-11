using Claims.Models;

namespace Claims.Application;

public interface ICoverService
{
    decimal ComputePremium(DateOnly startDate, DateOnly endDate, CoverType type);
    Task<List<Cover>> ListAsync(CancellationToken cancellationToken);
    Task<Cover> GetAsync(string id, CancellationToken cancellationToken);
    Task<Cover> CreateAsync(CreateCoverRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(string id, CancellationToken cancellationToken);
}