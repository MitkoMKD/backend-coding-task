namespace Claims.Tests;

public class TestClock : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(2028, 1, 1, 12, 0, 0, TimeSpan.Zero);
}