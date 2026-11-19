namespace MyBlazorPlayground.Web.Infrastructure;

internal interface IJournal<T>
{
    void Append(IReadOnlyList<T> items);
}

internal sealed class NoJournal<T> : IJournal<T>
{
    public static NoJournal<T> Instance { get; } = new();

    public void Append(IReadOnlyList<T> items)
    {
    }
}
