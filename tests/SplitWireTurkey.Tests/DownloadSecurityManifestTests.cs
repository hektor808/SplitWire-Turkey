using SplitWireTurkey.Services;
using Xunit;

namespace SplitWireTurkey.Tests;

public sealed class DownloadSecurityManifestTests
{
    [Theory]
    [InlineData("v2.2.30", "2.2.30")]
    [InlineData(" 1.0.9234 ", "1.0.9234")]
    [InlineData("V1.0.1090", "1.0.1090")]
    public void NormalizeVersionToken_ValidTokens_AreNormalized(string input, string expected)
    {
        var normalized = DownloadSecurityManifest.NormalizeVersionToken(input);

        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1.0")]
    [InlineData("abc")]
    [InlineData("1.0.0-beta")]
    public void NormalizeVersionToken_InvalidTokens_ReturnsEmpty(string input)
    {
        var normalized = DownloadSecurityManifest.NormalizeVersionToken(input);

        Assert.Equal(string.Empty, normalized);
    }
}
