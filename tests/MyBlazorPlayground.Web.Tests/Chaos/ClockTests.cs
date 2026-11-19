using MyBlazorPlayground.Web.Infrastructure;
using MyBlazorPlayground.Web.Chaos.Messages;

namespace MyBlazorPlayground.Web.Tests.Chaos;

public sealed class ServerTimeTests
{
    [Theory]
    [InlineData(0, "UTC")]
    [InlineData(330, "UTC+05:30")]
    [InlineData(-240, "UTC-04:00")]
    public void ZonesAreLabelled(int minutes, string expected) => Assert.Equal(expected, ServerTime.Zone(minutes));

    [Fact]
    public void LabelsUseTheServerOffset()
    {
        var at = new DateTimeOffset(2026, 10, 16, 8, 10, 39, 500, TimeSpan.Zero).ToUnixTimeMilliseconds();
        Assert.Equal("2026-10-16 08:10:39 UTC", new ServerTime(at, 0).Label);
        Assert.Equal("2026-10-16 13:40:39 UTC+05:30", new ServerTime(at, 330).Label);
        Assert.Equal("2026-10-16T13:40:39+05:30", new ServerTime(at, 330).Iso);
    }
}

public sealed class Uuid7OrderTests
{
    [Fact]
    public void AfterIsAlwaysLater()
    {
        var at = new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);
        var previous = Guid.Empty;
        for (var i = 0; i < 1000; i++)
        {
            var next = Uuid7.After(previous, at);
            Assert.True(Uuid7.IsValid(next));
            Assert.True(next.CompareTo(previous) > 0);
            Assert.True(string.CompareOrdinal(next.ToString(), previous.ToString()) > 0);
            previous = next;
        }
    }

    [Fact]
    public void AfterSurvivesTheClockGoingBack()
    {
        var at = new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);
        var later = Guid.CreateVersion7(at.AddMinutes(5));
        var next = Uuid7.After(later, at);
        Assert.True(next.CompareTo(later) > 0);
        Assert.Equal(Uuid7.Timestamp(later), Uuid7.Timestamp(next));
    }
}

public sealed class LatencyOptionsTests
{
    [Fact]
    public void LatencyLimitsAreChecked()
    {
        Assert.False(new ChaosOptions { LatencyCapacity = 0 }.IsValid());
        Assert.False(new ChaosOptions { LatencyGapSeconds = -1 }.IsValid());
        Assert.False(new ChaosOptions { LatencyBudgetSeconds = 1 }.IsValid());
        Assert.False(new ChaosOptions { LatencyRankCapacity = 0 }.IsValid());
        Assert.False(new ChaosOptions { LatencyJackpotCapacity = 1_001 }.IsValid());
        Assert.True(new ChaosOptions { LatencyGapSeconds = 0 }.IsValid());
    }
}
