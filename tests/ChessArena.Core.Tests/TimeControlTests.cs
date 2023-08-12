using ChessArena.Core.Clocks;

namespace ChessArena.Core.Tests;

public class TimeControlTests
{
    [Theory]
    [InlineData("3+2", 180, 2)]
    [InlineData("10+5", 600, 5)]
    [InlineData(" 1 + 0 ", 60, 0)]
    [InlineData("0.5+0", 30, 0)]
    public void ParsesMinutesPlusSeconds(string text, int initialSeconds, int incrementSeconds)
    {
        var control = TimeControl.Parse(text);
        Assert.Equal(TimeSpan.FromSeconds(initialSeconds), control.Initial);
        Assert.Equal(TimeSpan.FromSeconds(incrementSeconds), control.Increment);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("3")]
    [InlineData("3+")]
    [InlineData("0+2")]
    [InlineData("-1+2")]
    [InlineData("3+-2")]
    [InlineData("500+0")]
    public void RejectsInvalidText(string? text) => Assert.False(TimeControl.TryParse(text, out _));

    [Theory]
    [InlineData("1+0", TimeControlCategory.Bullet)]
    [InlineData("2+1", TimeControlCategory.Bullet)]
    [InlineData("3+0", TimeControlCategory.Blitz)]
    [InlineData("3+2", TimeControlCategory.Blitz)]
    [InlineData("5+3", TimeControlCategory.Blitz)]
    [InlineData("10+5", TimeControlCategory.Rapid)]
    [InlineData("30+0", TimeControlCategory.Classical)]
    public void CategorisesByEstimatedDuration(string text, TimeControlCategory expected) =>
        Assert.Equal(expected, TimeControl.Parse(text).Category);

    [Fact]
    public void FormatsForPgnAndDisplay()
    {
        Assert.Equal("180+2", TimeControl.Blitz3Plus2.ToPgn());
        Assert.Equal("3+2", TimeControl.Blitz3Plus2.ToString());
        Assert.Equal(TimeControl.Blitz3Plus2, TimeControl.Parse(TimeControl.Blitz3Plus2.ToString()));
    }
}
