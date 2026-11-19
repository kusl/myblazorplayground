namespace MyBlazorPlayground.Web.Chaos.Postcards;

internal sealed record StoredPostcard(Postcard Card, CleanImage[] Images)
{
    public bool Matches(string text, CleanImage[] images) =>
        string.Equals(Card.Text, text, StringComparison.Ordinal)
        && Images.Length == images.Length
        && Images.Zip(images).All(pair => pair.First.Bytes.AsSpan().SequenceEqual(pair.Second.Bytes));
}
