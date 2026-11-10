using System.ComponentModel.DataAnnotations;
using Claims.Models;

namespace Claims.Application;

public record CreateClaimRequest
{
    [Required, StringLength(36)]
    public required string CoverId { get; init; }
    public required DateOnly Created { get; init; }
    [Required, StringLength(200)]
    public required string Name { get; init; }
    [EnumDataType(typeof(ClaimType))]
    public required ClaimType Type { get; init; }
    [Range(typeof(decimal), "0", "100000")]
    public required decimal DamageCost { get; init; }
}