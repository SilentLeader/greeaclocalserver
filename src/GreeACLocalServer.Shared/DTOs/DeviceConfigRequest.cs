using System.ComponentModel.DataAnnotations;

namespace GreeACLocalServer.Shared.DTOs;

public class DeviceConfigRequest
{
    [Required]
    public string IpAddress { get; set; } = string.Empty;
}

public class QueryDeviceStatusRequest : DeviceConfigRequest
{
}

public class UpdateDeviceNameRequest : DeviceConfigRequest
{
    [Required]
    public string DeviceName { get; set; } = string.Empty;
}

public class UpdateRemoteHostRequest : DeviceConfigRequest
{
    [Required]
    public string RemoteHost { get; set; } = string.Empty;
}
