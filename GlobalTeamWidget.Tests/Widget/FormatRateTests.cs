using GlobalTeamWidget.Widget;
using Xunit;

namespace GlobalTeamWidget.Tests.Widget;

/// <summary>
/// Currency display formatting changed twice during development.
/// These lock down the exact rules: 2dp when fractional, none when whole.
/// </summary>
public class FormatRateTests
{
    [Theory]
    [InlineData(34691.00,  "34,691")]   // whole large number → thousands separator, no decimals
    [InlineData(34691.50,  "34691.50")] // fractional large number → 2dp, no separator
    [InlineData(12.8686,   "12.87")]    // typical SEK rate → rounded to 2dp
    [InlineData(1.3247,    "1.32")]     // typical USD rate
    [InlineData(1.0000,    "1")]        // exact 1.00 → no decimals
    [InlineData(0.0087,    "0.01")]     // small rate → 2dp
    [InlineData(100.00,    "100")]      // exactly 100 → whole, no decimals
    [InlineData(100.50,    "100.50")]   // just over 100 → 2dp (fractional even above 100)
    public void FormatRate_ReturnsExpectedString(decimal input, string expected)
    {
        var result = AdaptiveCardBuilder.FormatRate(input);
        Assert.Equal(expected, result);
    }
}
