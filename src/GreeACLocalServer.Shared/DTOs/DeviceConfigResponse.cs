namespace GreeACLocalServer.Shared.DTOs;

public class DeviceConfigResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
}

public class DeviceStatusResponse : DeviceConfigResponse
{
    public string DeviceName { get; set; } = string.Empty;
    public string RemoteHost { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
}

public class DeviceOperationResponse : DeviceConfigResponse
{
    // For set name and set remote host operations
}
