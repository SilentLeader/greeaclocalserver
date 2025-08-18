using System.ComponentModel.DataAnnotations;

namespace GreeACLocalServer.Shared.DTOs;

public class DeviceConfigRequest
{
    [Required]
    public string IpAddress { get; set; } = string.Empty;
}

public class DeviceStatusRequest : DeviceConfigRequest
{
}

public class SetDeviceNameRequest : DeviceConfigRequest
{
    [Required]
    public string DeviceName { get; set; } = string.Empty;
}

public class SetRemoteHostRequest : DeviceConfigRequest
{
    [Required]
    public string RemoteHost { get; set; } = string.Empty;
}
