using GreeACLocalServer.DeviceEmulator;

namespace GreeACLocalServer.DeviceEmulator.Tests;

public class MacObfuscationTests
{
    [Theory]
    [InlineData("aabbccddeeff")]
    [InlineData("0123456789ab")]
    [InlineData("112233445566")]
    public void Obscure_RoundTripsThroughServerNormalizeMac(string realMac)
    {
        var obscured = MacObfuscation.Obscure(realMac);

        Assert.Equal(16, obscured.Length);
        Assert.Equal(realMac, NormalizeMacLikeServer(obscured));
    }

    [Fact]
    public void Obscure_RejectsTooShortMac()
    {
        Assert.Throws<ArgumentException>(() => MacObfuscation.Obscure("short"));
    }

    /// <summary>
    /// Reproduces
    /// <c>GreeACLocalServer.Device.Services.MessageHandlerService.NormalizeMac</c>
    /// (internal, so not directly callable) to verify <see cref="MacObfuscation.Obscure"/>
    /// is its exact inverse.
    /// </summary>
    private static string NormalizeMacLikeServer(string obscuredMac)
    {
        var obscuredArr = obscuredMac.ToCharArray();
        var normalizedArr = new char[12];

        normalizedArr[0] = obscuredArr[8];
        normalizedArr[1] = obscuredArr[9];
        normalizedArr[2] = obscuredArr[14];
        normalizedArr[3] = obscuredArr[15];
        normalizedArr[4] = obscuredArr[2];
        normalizedArr[5] = obscuredArr[3];
        normalizedArr[6] = obscuredArr[10];
        normalizedArr[7] = obscuredArr[11];
        normalizedArr[8] = obscuredArr[4];
        normalizedArr[9] = obscuredArr[5];
        normalizedArr[10] = obscuredArr[0];
        normalizedArr[11] = obscuredArr[1];

        return new string(normalizedArr);
    }
}
