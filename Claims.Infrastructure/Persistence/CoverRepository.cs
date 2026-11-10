using Claims.Application;
using Claims.Models;
using Microsoft.EntityFrameworkCore;

namespace Claims.Infrastructure;

public class CoverRepository(ClaimsDbContext context) : ICoverRepository
{
    public Task<List<Cover>> ListAsync(CancellationToken cancellationToken) =>
        context.Covers.AsNoTracking().OrderBy(cover => cover.Id).ToListAsync(cancellationToken);
    public Task<Cover?> GetAsync(string id, CancellationToken cancellationToken) =>
        context.Covers.SingleOrDefaultAsync(cover => cover.Id == id, cancellationToken);
    public void Add(Cover cover) => context.Covers.Add(cover);
    public void Remove(Cover cover) => context.Covers.Remove(cover);
}