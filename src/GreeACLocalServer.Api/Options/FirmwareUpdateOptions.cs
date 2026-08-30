namespace GreeACLocalServer.Api.Options;

/// <summary>
/// Controls the optional firmware update check against the GREE update server.
/// Bound from <c>GreeServer:FirmwareUpdateCheck</c>. Disabled by default so the
/// server never contacts GREE unless the operator opts in.
/// </summary>
public class FirmwareUpdateOptions
{
    /// <summary>
    /// Cloud update check. When false (default) no request is ever made to
    /// <see cref="BaseUrl"/> (i.e. the server never contacts GREE).
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// When true (default) the server opportunistically queries each device's
    /// firmware identifier over the local network (outbound UDP to port 7000,
    /// scan -&gt; bind -&gt; status). Set to false on locked-down deployments that
    /// must not emit any automatic device traffic; the manual "Refresh" button in
    /// the UI still works. Independent of <see cref="Enabled"/> (the cloud update
    /// check) — this is local-only, no cloud.
    /// </summary>
    public bool AutoQuery { get; set; } = true;

    /// <summary>How long a per-firmware-code lookup result is reused before refetching.</summary>
    public int CacheHours { get; set; } = 24;

    /// <summary>GREE "latest version" lookup endpoint. The <c>firmwareCode</c> query string is appended.</summary>
    public string BaseUrl { get; set; } = "https://grih.gree.com/wifiModule/Lastversion";
}
