using MyBlazorPlayground.Domain.Security;

namespace MyBlazorPlayground.Domain.Tests;

public sealed class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new(PasswordHasher.MinimumIterations);

    [Fact]
    public void VerifiesWhatItHashed()
    {
        var hash = _hasher.Hash("correct horse battery staple");
        Assert.StartsWith("pbkdf2-sha512$100000$", hash);
        Assert.Equal(PasswordCheck.Succeeded, _hasher.Verify(hash, "correct horse battery staple"));
        Assert.Equal(PasswordCheck.Failed, _hasher.Verify(hash, "correct horse battery stapler"));
    }

    [Fact]
    public void SaltsEveryHash()
    {
        Assert.NotEqual(_hasher.Hash("same"), _hasher.Hash("same"));
    }

    [Fact]
    public void AsksForARehashWhenTheWorkFactorRose()
    {
        var hash = _hasher.Hash("correct horse battery staple");
        Assert.Equal(PasswordCheck.SucceededNeedsRehash, new PasswordHasher(PasswordHasher.MinimumIterations + 1).Verify(hash, "correct horse battery staple"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("plain")]
    [InlineData("pbkdf2-sha512$1000$AAAA$AAAA")]
    [InlineData("pbkdf2-sha512$100000$!!!$AAAA")]
    [InlineData("bcrypt$100000$AAAA$AAAA")]
    public void RejectsMalformedHashes(string? stored)
    {
        Assert.Equal(PasswordCheck.Failed, _hasher.Verify(stored, "anything"));
    }

    [Fact]
    public void RefusesTooFewIterations()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PasswordHasher(1000));
    }
}

public sealed class SetupCodeTests
{
    [Fact]
    public void MatchesIgnoringCaseAndSpaces()
    {
        var code = SetupCode.Create();
        Assert.Matches("^[A-Z2-9]{4}(-[A-Z2-9]{4}){3}$", code.Value);
        Assert.True(code.Matches(code.Value));
        Assert.True(code.Matches(" " + code.Value.ToLowerInvariant() + " "));
        Assert.False(code.Matches(null));
        Assert.False(code.Matches(SetupCode.Create().Value));
    }
}
