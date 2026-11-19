using System.Text;
using MyBlazorPlayground.Domain.Graphics;
using MyBlazorPlayground.Domain.Security;

namespace MyBlazorPlayground.Domain.Tests;

public sealed class TotpTests
{
    private static readonly byte[] Rfc6238Secret = Encoding.ASCII.GetBytes("12345678901234567890");

    [Theory]
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(1111111111L, "050471")]
    [InlineData(1234567890L, "005924")]
    [InlineData(2000000000L, "279037")]
    [InlineData(20000000000L, "353130")]
    public void MatchesTheRfc6238Vectors(long seconds, string expected) =>
        Assert.Equal(expected, Totp.Code(Rfc6238Secret, seconds / Totp.PeriodSeconds));

    [Fact]
    public void AcceptsOneStepEitherSide()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        var step = Totp.StepAt(now);
        Assert.Equal(step - 1, Totp.Match(Rfc6238Secret, Totp.Code(Rfc6238Secret, step - 1), now, 0));
        Assert.Equal(step, Totp.Match(Rfc6238Secret, Totp.Code(Rfc6238Secret, step), now, 0));
        Assert.Equal(step + 1, Totp.Match(Rfc6238Secret, Totp.Code(Rfc6238Secret, step + 1), now, 0));
        Assert.Null(Totp.Match(Rfc6238Secret, Totp.Code(Rfc6238Secret, step + 2), now, 0));
        Assert.Null(Totp.Match(Rfc6238Secret, Totp.Code(Rfc6238Secret, step - 2), now, 0));
    }

    [Fact]
    public void RefusesStepsAlreadyUsed()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        var step = Totp.StepAt(now);
        var code = Totp.Code(Rfc6238Secret, step);
        Assert.Null(Totp.Match(Rfc6238Secret, code, now, step));
        Assert.Equal(step, Totp.Match(Rfc6238Secret, code, now, step - 1));
    }

    [Theory]
    [InlineData("123 456", "123456")]
    [InlineData(" 123-456 ", "123456")]
    [InlineData("12345", null)]
    [InlineData("1234567", null)]
    [InlineData("12a456", null)]
    [InlineData(null, null)]
    public void NormalizesCodes(string? input, string? expected) => Assert.Equal(expected, Totp.Normalize(input));

    [Fact]
    public void BuildsAnOtpauthUri()
    {
        var uri = Totp.Uri("my blazor playground", "kushal", Rfc6238Secret);
        Assert.Equal("otpauth://totp/my%20blazor%20playground:kushal?secret=GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ&issuer=my%20blazor%20playground&algorithm=SHA1&digits=6&period=30", uri);
    }

    [Fact]
    public void NewSecretsAreTwentyRandomBytes()
    {
        var first = Totp.NewSecret();
        Assert.Equal(Totp.SecretBytes, first.Length);
        Assert.NotEqual(Convert.ToHexString(first), Convert.ToHexString(Totp.NewSecret()));
    }
}

public sealed class Base32Tests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("f", "MY")]
    [InlineData("fo", "MZXQ")]
    [InlineData("foo", "MZXW6")]
    [InlineData("foob", "MZXW6YQ")]
    [InlineData("fooba", "MZXW6YTB")]
    [InlineData("foobar", "MZXW6YTBOI")]
    public void MatchesRfc4648(string plain, string encoded)
    {
        Assert.Equal(encoded, Base32.Encode(Encoding.ASCII.GetBytes(plain)));
        Assert.Equal(plain, Encoding.ASCII.GetString(Base32.Decode(encoded)!));
    }

    [Fact]
    public void DecodesGroupedLowercaseAndPadding() =>
        Assert.Equal("foobar", Encoding.ASCII.GetString(Base32.Decode("mzxw 6ytb-oi======")!));

    [Fact]
    public void RefusesOtherCharacters() => Assert.Null(Base32.Decode("MZXW1"));

    [Fact]
    public void GroupsInFours() => Assert.Equal("ABCD EFGH IJ", Base32.Group("ABCDEFGHIJ"));
}

public sealed class RecoveryCodeTests
{
    [Fact]
    public void MakesTenDistinctReadableCodes()
    {
        var codes = RecoveryCodes.Create();
        Assert.Equal(RecoveryCodes.Count, codes.Count);
        Assert.Equal(codes.Count, codes.Distinct().Count());
        Assert.All(codes, code => Assert.Matches("^[a-z2-9]{5}-[a-z2-9]{5}$", code));
        Assert.All(codes, code => Assert.NotNull(RecoveryCodes.Normalize(code)));
    }

    [Fact]
    public void HashesIgnoreCaseSpacesAndDashes()
    {
        var hash = RecoveryCodes.Hash("abcde-fghjk");
        Assert.NotNull(hash);
        Assert.Equal(64, hash.Length);
        Assert.Equal(hash, RecoveryCodes.Hash(" ABCDE FGHJK "));
        Assert.Equal(hash, RecoveryCodes.Hash("abcdefghjk"));
        Assert.NotEqual(hash, RecoveryCodes.Hash("abcde-fghjm"));
    }

    [Theory]
    [InlineData("abcde-fghj")]
    [InlineData("abcde-fghji")]
    [InlineData("123456")]
    [InlineData(null)]
    public void RejectsWhatIsNotARecoveryCode(string? input) => Assert.Null(RecoveryCodes.Hash(input));
}

public sealed class QrCodeTests
{
    [Theory]
    [InlineData(1, 14)]
    [InlineData(2, 26)]
    [InlineData(5, 84)]
    [InlineData(8, 152)]
    [InlineData(10, 213)]
    public void KnowsByteModeCapacityAtLevelM(int version, int bytes) => Assert.Equal(bytes, QrCode.Capacity(version));

    [Fact]
    public void PicksTheSmallestVersion()
    {
        Assert.Equal(1, QrCode.Encode(new string('a', 14))!.Version);
        Assert.Equal(2, QrCode.Encode(new string('a', 15))!.Version);
        Assert.Equal(21, QrCode.Encode("HELLO")!.Size);
    }

    [Fact]
    public void RefusesTextTooLongForVersionTen() => Assert.Null(QrCode.Encode(new string('a', 214)));

    [Fact]
    public void DrawsFinderPatternsAndTheDarkModule()
    {
        var code = QrCode.Encode(Totp.Uri("my blazor playground", "someone", new byte[20]))!;
        foreach (var (x, y) in new[] { (0, 0), (code.Size - 7, 0), (0, code.Size - 7) })
        {
            Assert.True(code[x, y]);
            Assert.True(code[x + 6, y + 6]);
            Assert.False(code[x + 1, y + 1]);
            Assert.True(code[x + 3, y + 3]);
        }

        Assert.True(code[8, code.Size - 8]);
    }

    [Fact]
    public void RendersAStandaloneSvg()
    {
        var svg = QrCode.Encode("HELLO")!.ToSvg("a <code>");
        Assert.StartsWith("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 29 29\"", svg);
        Assert.Contains("aria-label=\"a &lt;code&gt;\"", svg);
        Assert.EndsWith("</svg>", svg);
    }
}
