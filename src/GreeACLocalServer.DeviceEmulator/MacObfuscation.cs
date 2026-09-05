namespace GreeACLocalServer.DeviceEmulator;

/// <summary>
/// Exact inverse of the server's <c>NormalizeMac</c> de-scrambling
/// (<c>GreeACLocalServer.Device.Services.MessageHandlerService.NormalizeMac</c>),
/// which recovers the 12-character real MAC from a 16-character scrambled one via
/// <c>normalized[0..11] = obscured[8,9,14,15,2,3,10,11,4,5,0,1]</c>. The four
/// unused obscured positions (6, 7, 12, 13) are filled with an arbitrary
/// character since the server never reads them.
/// </summary>
public static class MacObfuscation
{
    public static string Obscure(string normalizedMac)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedMac);
        if (normalizedMac.Length < 12)
        {
            throw new ArgumentException("MAC must be at least 12 characters.", nameof(normalizedMac));
        }

        var obscured = new char[16];
        obscured[8] = normalizedMac[0];
        obscured[9] = normalizedMac[1];
        obscured[14] = normalizedMac[2];
        obscured[15] = normalizedMac[3];
        obscured[2] = normalizedMac[4];
        obscured[3] = normalizedMac[5];
        obscured[10] = normalizedMac[6];
        obscured[11] = normalizedMac[7];
        obscured[4] = normalizedMac[8];
        obscured[5] = normalizedMac[9];
        obscured[0] = normalizedMac[10];
        obscured[1] = normalizedMac[11];

        // Unused by the server's NormalizeMac; any filler works.
        obscured[6] = '0';
        obscured[7] = '0';
        obscured[12] = '0';
        obscured[13] = '0';

        return new string(obscured);
    }
}
