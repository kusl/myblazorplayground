namespace MyBlazorPlayground.Web.Nest;

internal interface INestJournal
{
    static readonly INestJournal None = new NoJournal();

    void Append(IReadOnlyList<Entry> entries);

    private sealed class NoJournal : INestJournal
    {
        public void Append(IReadOnlyList<Entry> entries)
        {
        }
    }
}
