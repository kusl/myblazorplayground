namespace MyBlazorPlayground.Web.Identity;

internal sealed class SignInThrottle(TimeProvider time)
{
    private readonly KeyedTokenBuckets _buckets = new(10, 0.1, time);

    public bool TryTake(string key, string purpose) => _buckets.TryTake(string.Concat(purpose, "|", key));
}
