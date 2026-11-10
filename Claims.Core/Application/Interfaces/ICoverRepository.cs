using Claims.Models;

namespace Claims.Application;

public interface ICoverRepository
{
    Task<List<Cover>> ListAsync(CancellationToken cancellationToken);
    Task<Cover?> GetAsync(string id, CancellationToken cancellationToken);
    void Add(Cover cover);
    void Remove(Cover cover);
}