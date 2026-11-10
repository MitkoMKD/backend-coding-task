using Claims.Application;
using Claims.Models;
using Microsoft.AspNetCore.Mvc;

namespace Claims.Controllers;

[ApiController]
[Route("[controller]")]
public class ClaimsController(ClaimService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Claim>>> GetAsync(CancellationToken cancellationToken) =>
        Ok(await service.ListAsync(cancellationToken));

    [HttpGet("{id}")]
    public async Task<ActionResult<Claim>> GetAsync(string id, CancellationToken cancellationToken) =>
        Ok(await service.GetAsync(id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<Claim>> CreateAsync(CreateClaimRequest request, CancellationToken cancellationToken)
    {
        var claim = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetAsync), new { id = claim.Id }, claim);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
