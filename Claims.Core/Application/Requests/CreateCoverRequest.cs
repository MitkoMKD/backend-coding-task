using System.ComponentModel.DataAnnotations;
using Claims.Models;

namespace Claims.Application;

public record CreateCoverRequest
{
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    [EnumDataType(typeof(CoverType))]
    public required CoverType Type { get; init; }
}