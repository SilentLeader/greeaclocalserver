namespace GreeACLocalServer.Api.Options;

/// <summary>
/// Controls the optional firmware update check against the GREE update server.
/// Bound from <c>GreeServer:FirmwareUpdateCheck</c>. Disabled by default so the
/// server never contacts GREE unless the operator opts in.
/// </summary>
public class FirmwareUpdateOptions
{
    /// <summary>When false (default) no request is ever made to <see cref="BaseUrl"/>.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>How long a per-firmware-code lookup result is reused before refetching.</summary>
    public int CacheHours { get; set; } = 24;

    /// <summary>GREE "latest version" lookup endpoint. The <c>firmwareCode</c> query string is appended.</summary>
    public string BaseUrl { get; set; } = "http://grih.gree.com/wifiModule/Lastversion";
}
