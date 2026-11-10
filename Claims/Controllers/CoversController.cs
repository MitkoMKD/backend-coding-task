using Claims.Application;
using Claims.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Claims.Controllers;

[ApiController]
[Route("[controller]")]
public class CoversController(CoverService service) : ControllerBase
{
    [HttpPost("compute")]
    public ActionResult<decimal> ComputePremium(
        [FromQuery, BindRequired] DateOnly startDate,
        [FromQuery, BindRequired] DateOnly endDate,
        [FromQuery, BindRequired] CoverType coverType) =>
        Ok(service.ComputePremium(startDate, endDate, coverType));

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Cover>>> GetAsync(CancellationToken cancellationToken) =>
        Ok(await service.ListAsync(cancellationToken));

    [HttpGet("{id}")]
    public async Task<ActionResult<Cover>> GetAsync(string id, CancellationToken cancellationToken) =>
        Ok(await service.GetAsync(id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<Cover>> CreateAsync(CreateCoverRequest request, CancellationToken cancellationToken)
    {
        var cover = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetAsync), new { id = cover.Id }, cover);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
