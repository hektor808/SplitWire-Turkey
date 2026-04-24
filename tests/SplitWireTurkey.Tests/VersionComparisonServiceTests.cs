using SplitWireTurkey.Services;
using Xunit;

namespace SplitWireTurkey.Tests;

public sealed class VersionComparisonServiceTests
{
    [Theory]
    [InlineData("1.0.0", "1.0.1", true)]
    [InlineData("1.0.1", "1.0.0", false)]
    [InlineData("1.5.0", "1.5.0", false)]
    [InlineData("invalid", "1.5.0", false)]
    [InlineData("1.5.0", "invalid", false)]
    public void IsNewerVersion_ReturnsExpectedResult(string current, string latest, bool expected)
    {
        var actual = VersionComparisonService.IsNewerVersion(current, latest);

        Assert.Equal(expected, actual);
    }
}
