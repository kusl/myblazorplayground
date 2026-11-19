namespace MyBlazorPlayground.Web.Nest;

internal sealed class NestOptions
{
    public const string SectionName = "Nest";

    public int Capacity { get; set; } = 100_000;

    public double PostBurst { get; set; } = 6;

    public double PostsPerSecond { get; set; } = 0.5;

    public bool IsValid() =>
        Capacity is >= 1 and <= 1_000_000
        && PostBurst >= 1
        && PostsPerSecond > 0;
}
