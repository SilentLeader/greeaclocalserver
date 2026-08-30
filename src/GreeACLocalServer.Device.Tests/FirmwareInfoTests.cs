using GreeACLocalServer.Device.Services;

namespace GreeACLocalServer.Device.Tests;

public class FirmwareInfoTests
{
    [Theory]
    [InlineData("362001065736+U-QCOM4004CV3.76.bin", "362001065736", "3.76")]
    [InlineData("100001234567+U-SOMEMODULEV1.2.3.bin", "100001234567", "1.2.3")]
    public void TryParse_ValidHid_ExtractsCodeAndVersion(string hid, string expectedCode, string expectedVersion)
    {
        Assert.True(FirmwareInfo.TryParse(hid, out var code, out var version));
        Assert.Equal(expectedCode, code);
        Assert.Equal(expectedVersion, version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("garbage")]
    [InlineData("no-version-here.bin")]
    [InlineData("362001065736+U-QCOM4004CV3.76.txt")]
    public void TryParse_InvalidHid_ReturnsFalseWithEmptyOutputs(string? hid)
    {
        Assert.False(FirmwareInfo.TryParse(hid, out var code, out var version));
        Assert.Equal(string.Empty, code);
        Assert.Equal(string.Empty, version);
    }

    [Theory]
    [InlineData("3.76", "3.77", -1)]
    [InlineData("3.77", "3.76", 1)]
    [InlineData("3.76", "3.76", 0)]
    [InlineData("3.7", "3.7.0", 0)]
    [InlineData("3.10", "3.9", 1)]
    [InlineData("v1.0", "1.0", 0)]
    [InlineData("", "1.0", -1)]
    public void CompareVersions_NumericComparison(string left, string right, int expectedSign)
    {
        Assert.Equal(expectedSign, Math.Sign(FirmwareInfo.CompareVersions(left, right)));
    }
}
