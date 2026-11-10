namespace Claims.Models;

public class Cover
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public CoverType Type { get; set; }

    public decimal Premium { get; set; }
}

