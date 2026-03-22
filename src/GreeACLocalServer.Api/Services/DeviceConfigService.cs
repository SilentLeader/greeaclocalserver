using GreeACLocalServer.Device.Interfaces;
using GreeACLocalServer.Device.Requests;

namespace GreeACLocalServer.Api.Services;

public class DeviceConfigService : IDeviceConfigService
{
    private readonly ILogger<DeviceConfigService> _logger;
    private readonly IDeviceControllerService _deviceManagementService;
    private readonly IOptionsMonitor<ServerOptions> _serverOptions;

    public DeviceConfigService(ILogger<DeviceConfigService> logger, IDeviceControllerService deviceManagementService, IOptionsMonitor<ServerOptions> serverOptions)
    {
        _logger = logger;
        _deviceManagementService = deviceManagementService;
        _serverOptions = serverOptions;
    }

    public async Task<DeviceStatusResponse> QueryDeviceStatusAsync(QueryDeviceStatusRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
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
            // Check if management is enabled
            if (!_serverOptions.CurrentValue.EnableManagement)
            {
                _logger.LogWarning("Device management is disabled. Set device name operation rejected for IP {IpAddress}", request.IpAddress);
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
            // Check if management is enabled
            if (!_serverOptions.CurrentValue.EnableManagement)
            {
                _logger.LogWarning("Device management is disabled. Set remote host operation rejected for IP {IpAddress}", request.IpAddress);
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
