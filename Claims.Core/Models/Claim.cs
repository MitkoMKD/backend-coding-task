namespace Claims.Models
{
    public class Claim
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public required string CoverId { get; set; }

        public DateOnly Created { get; set; }

        public required string Name { get; set; }

        public ClaimType Type { get; set; }

        public decimal DamageCost { get; set; }
    }

}
