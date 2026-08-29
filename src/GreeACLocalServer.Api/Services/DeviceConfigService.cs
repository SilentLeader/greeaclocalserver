using GreeACLocalServer.Device.Interfaces;
using GreeACLocalServer.Device.Requests;

namespace GreeACLocalServer.Api.Services;

public class DeviceConfigService(ILogger<DeviceConfigService> logger, IDeviceControllerService deviceManagementService, IOptionsMonitor<ServerOptions> serverOptions) : IDeviceConfigService
{
    private readonly ILogger<DeviceConfigService> _logger = logger;
    private readonly IDeviceControllerService _deviceManagementService = deviceManagementService;
    private readonly IOptionsMonitor<ServerOptions> _serverOptions = serverOptions;

    /// <summary>
    /// Returns <c>true</c> and logs a warning when device management is disabled. All
    /// device-config operations (query included) poke the device over UDP, so they are
    /// gated the same way.
    /// </summary>
    private bool IsManagementDisabled(string ipAddress, string operation)
    {
        if (_serverOptions.CurrentValue.EnableManagement)
        {
            return false;
        }

        _logger.LogWarning("Device management is disabled. {Operation} operation rejected for IP {IpAddress}", operation, ipAddress);
        return true;
    }

    public async Task<DeviceStatusResponse> QueryDeviceStatusAsync(QueryDeviceStatusRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (IsManagementDisabled(request.IpAddress, "Query device status"))
            {
                return new DeviceStatusResponse
                {
                    Success = false,
                    Message = "Device management is disabled",
                    ErrorCode = "MANAGEMENT_DISABLED"
                };
            }

            var result = await _deviceManagementService.GetDeviceStatusAsync(new GetDeviceStatusRequest(request.IpAddress), cancellationToken);

            if (!result.IsSuccess)
            {
                return new DeviceStatusResponse
                {
                    Success = false,
                    Message = result.Message,
                    ErrorCode = result.ErrorCode
                };
            }

            return new DeviceStatusResponse
            {
                Success = true,
                DeviceName = result.DeviceName ?? string.Empty,
                MacAddress = result.MacAddress ?? string.Empty,
                RemoteHost = result.RemoteHost ?? string.Empty
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying device status for IP {IpAddress}", request.IpAddress);
            return new DeviceStatusResponse
            {
                Success = false,
                Message = $"Failed to query device status: {ex.Message}",
                ErrorCode = "QUERY_ERROR"
            };
        }
    }

    public async Task<DeviceOperationResponse> SetDeviceNameAsync(UpdateDeviceNameRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (IsManagementDisabled(request.IpAddress, "Set device name"))
            {
                return new DeviceOperationResponse
                {
                    Success = false,
                    Message = "Device management is disabled",
                    ErrorCode = "MANAGEMENT_DISABLED"
                };
            }

            var result = await _deviceManagementService.SetDeviceNameAsync(
                new SetDeviceNameRequest(request.IpAddress, request.DeviceName),
                cancellationToken);

            if (!result.IsSuccess)
            {
                return new DeviceOperationResponse
                {
                    Success = false,
                    Message = result.Message,
                    ErrorCode = result.ErrorCode
                };
            }

            return new DeviceOperationResponse
            {
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting device name for IP {IpAddress}", request.IpAddress);
            return new DeviceOperationResponse
            {
                Success = false,
                Message = $"Failed to set device name: {ex.Message}",
                ErrorCode = "SET_NAME_ERROR"
            };
        }
    }

    public async Task<DeviceOperationResponse> SetRemoteHostAsync(UpdateRemoteHostRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (IsManagementDisabled(request.IpAddress, "Set remote host"))
            {
                return new DeviceOperationResponse
                {
                    Success = false,
                    Message = "Device management is disabled",
                    ErrorCode = "MANAGEMENT_DISABLED"
                };
            }

            var result = await _deviceManagementService.SetRemoteHostAsync(
                new SetRemoteHostRequest(request.IpAddress, request.RemoteHost),
                cancellationToken);

            if (!result.IsSuccess)
            {
                return new DeviceOperationResponse
                {
                    Success = false,
                    Message = result.Message,
                    ErrorCode = result.ErrorCode
                };
            }

            return new DeviceOperationResponse
            {
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting remote host for IP {IpAddress}", request.IpAddress);
            return new DeviceOperationResponse
            {
                Success = false,
                Message = $"Failed to set remote host: {ex.Message}",
                ErrorCode = "SET_HOST_ERROR"
            };
        }
    }
}
