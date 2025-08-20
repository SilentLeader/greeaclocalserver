using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace GreeACLocalServer.Api.Services;

public class DeviceConfigService : IDeviceConfigService
{
    private readonly ILogger<DeviceConfigService> _logger;
    private readonly ICryptoService _cryptoService;

    public DeviceConfigService(ILogger<DeviceConfigService> logger, ICryptoService cryptoService)
    {
        _logger = logger;
        _cryptoService = cryptoService;
    }

    public async Task<DeviceStatusResponse> QueryDeviceStatusAsync(DeviceStatusRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            // First, scan the device to get MAC and key
            var scanResult = await ScanDeviceAsync(request.IpAddress, cancellationToken);
            if (!scanResult.Success)
            {
                return new DeviceStatusResponse
                {
                    Success = false,
                    Message = scanResult.Message,
                    ErrorCode = scanResult.ErrorCode
                };
            }

            // Query device status using the cryptokey
            var statusResult = await QueryStatusAsync(request.IpAddress, scanResult.MacAddress!, scanResult.CryptoKey!, cancellationToken);
            return statusResult;
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

    public async Task<DeviceOperationResponse> SetDeviceNameAsync(SetDeviceNameRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            // First, scan the device to get MAC and key
            var scanResult = await ScanDeviceAsync(request.IpAddress, cancellationToken);
            if (!scanResult.Success)
            {
                return new DeviceOperationResponse
                {
                    Success = false,
                    Message = scanResult.Message,
                    ErrorCode = scanResult.ErrorCode
                };
            }

            // Set device name using the cryptokey
            var result = await SetNameAsync(request.IpAddress, scanResult.MacAddress!, scanResult.CryptoKey!, request.DeviceName, cancellationToken);
            return result;
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

    public async Task<DeviceOperationResponse> SetRemoteHostAsync(SetRemoteHostRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            // First, scan the device to get MAC and key
            var scanResult = await ScanDeviceAsync(request.IpAddress, cancellationToken);
            if (!scanResult.Success)
            {
                return new DeviceOperationResponse
                {
                    Success = false,
                    Message = scanResult.Message,
                    ErrorCode = scanResult.ErrorCode
                };
            }

            // Set remote host using the cryptokey
            var result = await SetRemoteHostAsync(request.IpAddress, scanResult.MacAddress!, scanResult.CryptoKey!, request.RemoteHost, cancellationToken);
            return result;
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

    private async Task<ScanResult> ScanDeviceAsync(string ipAddress, CancellationToken cancellationToken)
    {
        try
        {
            var scanRequest = new { t = "scan" };
            var response = await SendUdpCommandAsync(ipAddress, JsonSerializer.Serialize(scanRequest), cancellationToken);
            
            if (string.IsNullOrEmpty(response))
            {
                return new ScanResult
                {
                    Success = false,
                    Message = "Device did not respond to scan request",
                    ErrorCode = "NO_RESPONSE"
                };
            }

            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;
            
            if (!root.TryGetProperty("pack", out var packProp))
            {
                return new ScanResult
                {
                    Success = false,
                    Message = "Invalid scan response format",
                    ErrorCode = "INVALID_RESPONSE"
                };
            }

            // Decrypt the pack using default key (empty string)
            var decryptedPack = _cryptoService.Decrypt(packProp.GetString()!, "");
            using var packDoc = JsonDocument.Parse(decryptedPack);
            var packRoot = packDoc.RootElement;

            if (!packRoot.TryGetProperty("mac", out var macProp))
            {
                return new ScanResult
                {
                    Success = false,
                    Message = "MAC address not found in scan response",
                    ErrorCode = "MAC_NOT_FOUND"
                };
            }

            var macAddress = macProp.GetString()!;

            // Now bind to get the key
            var bindRequest = new
            {
                mac = macAddress,
                t = "bind",
                uid = 0
            };

            var bindPack = JsonSerializer.Serialize(bindRequest);
            var bindRequestObj = new
            {
                cid = "app",
                i = 1,
                pack = _cryptoService.Encrypt(bindPack, ""),
                t = "pack",
                tcid = macAddress,
                uid = 22130
            };

            var bindResponse = await SendUdpCommandAsync(ipAddress, JsonSerializer.Serialize(bindRequestObj), cancellationToken);
            
            if (string.IsNullOrEmpty(bindResponse))
            {
                return new ScanResult
                {
                    Success = false,
                    Message = "Device did not respond to bind request",
                    ErrorCode = "BIND_NO_RESPONSE"
                };
            }

            using var bindDoc = JsonDocument.Parse(bindResponse);
            var bindRoot = bindDoc.RootElement;
            
            if (!bindRoot.TryGetProperty("pack", out var bindPackProp))
            {
                return new ScanResult
                {
                    Success = false,
                    Message = "Invalid bind response format",
                    ErrorCode = "BIND_INVALID_RESPONSE"
                };
            }

            var decryptedBindPack = _cryptoService.Decrypt(bindPackProp.GetString()!, "");
            using var bindPackDoc = JsonDocument.Parse(decryptedBindPack);
            var bindPackRoot = bindPackDoc.RootElement;

            if (!bindPackRoot.TryGetProperty("t", out var typeProp) || typeProp.GetString() != "bindok")
            {
                return new ScanResult
                {
                    Success = false,
                    Message = "Device binding failed",
                    ErrorCode = "BIND_FAILED"
                };
            }

            if (!bindPackRoot.TryGetProperty("key", out var keyProp))
            {
                return new ScanResult
                {
                    Success = false,
                    Message = "Crypto key not found in bind response",
                    ErrorCode = "KEY_NOT_FOUND"
                };
            }

            return new ScanResult
            {
                Success = true,
                MacAddress = macAddress,
                CryptoKey = keyProp.GetString()!
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning device at IP {IpAddress}", ipAddress);
            return new ScanResult
            {
                Success = false,
                Message = $"Scan failed: {ex.Message}",
                ErrorCode = "SCAN_EXCEPTION"
            };
        }
    }

    private async Task<DeviceStatusResponse> QueryStatusAsync(string ipAddress, string macAddress, string cryptoKey, CancellationToken cancellationToken)
    {
        try
        {
            var statusRequest = new
            {
                cols = new[] { "host", "name" },
                t = "status",
                mac = 0
            };

            var result = await SendPackCommandAsync(ipAddress, macAddress, cryptoKey, statusRequest, cancellationToken);
            
            if (!result.Success)
            {
                return new DeviceStatusResponse
                {
                    Success = false,
                    Message = result.Message,
                    ErrorCode = result.ErrorCode
                };
            }

            using var doc = JsonDocument.Parse(result.ResponseData!);
            var root = doc.RootElement;

            if (!root.TryGetProperty("dat", out var datProp) || datProp.ValueKind != JsonValueKind.Array)
            {
                return new DeviceStatusResponse
                {
                    Success = false,
                    Message = "Invalid status response format",
                    ErrorCode = "INVALID_STATUS_RESPONSE"
                };
            }

            var datArray = datProp.EnumerateArray().ToArray();
            if (datArray.Length < 2)
            {
                return new DeviceStatusResponse
                {
                    Success = false,
                    Message = "Insufficient data in status response",
                    ErrorCode = "INSUFFICIENT_DATA"
                };
            }

            return new DeviceStatusResponse
            {
                Success = true,
                Message = "Device status retrieved successfully",
                DeviceName = datArray[1].GetString() ?? "",
                RemoteHost = datArray[0].GetString() ?? "",
                MacAddress = macAddress
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying status for device at IP {IpAddress}", ipAddress);
            return new DeviceStatusResponse
            {
                Success = false,
                Message = $"Status query failed: {ex.Message}",
                ErrorCode = "STATUS_QUERY_EXCEPTION"
            };
        }
    }

    private async Task<DeviceOperationResponse> SetNameAsync(string ipAddress, string macAddress, string cryptoKey, string deviceName, CancellationToken cancellationToken)
    {
        try
        {
            var setNameRequest = new
            {
                opt = new[] { "name" },
                p = new[] { deviceName },
                t = "cmd"
            };

            var result = await SendPackCommandAsync(ipAddress, macAddress, cryptoKey, setNameRequest, cancellationToken);
            
            return new DeviceOperationResponse
            {
                Success = result.Success,
                Message = result.Success ? "Device name set successfully" : result.Message,
                ErrorCode = result.ErrorCode
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting name for device at IP {IpAddress}", ipAddress);
            return new DeviceOperationResponse
            {
                Success = false,
                Message = $"Set name failed: {ex.Message}",
                ErrorCode = "SET_NAME_EXCEPTION"
            };
        }
    }

    private async Task<DeviceOperationResponse> SetRemoteHostAsync(string ipAddress, string macAddress, string cryptoKey, string remoteHost, CancellationToken cancellationToken)
    {
        try
        {
            var setHostRequest = new
            {
                opt = new[] { "host" },
                p = new[] { remoteHost },
                t = "cmd"
            };

            var result = await SendPackCommandAsync(ipAddress, macAddress, cryptoKey, setHostRequest, cancellationToken);
            
            return new DeviceOperationResponse
            {
                Success = result.Success,
                Message = result.Success ? "Remote host set successfully" : result.Message,
                ErrorCode = result.ErrorCode
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting remote host for device at IP {IpAddress}", ipAddress);
            return new DeviceOperationResponse
            {
                Success = false,
                Message = $"Set remote host failed: {ex.Message}",
                ErrorCode = "SET_HOST_EXCEPTION"
            };
        }
    }

    private async Task<PackCommandResult> SendPackCommandAsync(string ipAddress, string macAddress, string cryptoKey, object requestData, CancellationToken cancellationToken)
    {
        try
        {
            var packData = JsonSerializer.Serialize(requestData);
            var requestObj = new
            {
                cid = "app",
                i = 0,
                pack = _cryptoService.Encrypt(packData, cryptoKey),
                t = "pack",
                tcid = macAddress,
                uid = 22130
            };

            var response = await SendUdpCommandAsync(ipAddress, JsonSerializer.Serialize(requestObj), cancellationToken);
            
            if (string.IsNullOrEmpty(response))
            {
                return new PackCommandResult
                {
                    Success = false,
                    Message = "No response from device",
                    ErrorCode = "NO_RESPONSE"
                };
            }

            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;
            
            if (!root.TryGetProperty("pack", out var packProp))
            {
                return new PackCommandResult
                {
                    Success = false,
                    Message = "Invalid response format",
                    ErrorCode = "INVALID_RESPONSE"
                };
            }

            var decryptedResponse = _cryptoService.Decrypt(packProp.GetString()!, cryptoKey);
            
            return new PackCommandResult
            {
                Success = true,
                ResponseData = decryptedResponse
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending pack command to device at IP {IpAddress}", ipAddress);
            return new PackCommandResult
            {
                Success = false,
                Message = $"Pack command failed: {ex.Message}",
                ErrorCode = "PACK_COMMAND_EXCEPTION"
            };
        }
    }

    private async Task<string> SendUdpCommandAsync(string ipAddress, string command, CancellationToken cancellationToken)
    {
        const int port = 7000;
        const int timeoutMs = 3000;
        
        for (int attempt = 0; attempt < 3; attempt++)
        {
            UdpClient? udpClient = null;
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(timeoutMs);

                udpClient = new UdpClient();
                udpClient.Client.SendTimeout = timeoutMs;
                udpClient.Client.ReceiveTimeout = timeoutMs;
                
                udpClient.Connect(IPAddress.Parse(ipAddress), port);

                var sendBytes = Encoding.ASCII.GetBytes(command);
                udpClient.Send(sendBytes, sendBytes.Length);

                var remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                var receiveBytes = udpClient.Receive(ref remoteEndPoint);
                var response = Encoding.ASCII.GetString(receiveBytes);
                
                return response;
            }
            catch (Exception ex) when (attempt < 2)
            {
                _logger.LogWarning(ex, "UDP command attempt {Attempt} failed for IP {IpAddress}, retrying...", attempt + 1, ipAddress);
                await Task.Delay(500, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UDP command failed for IP {IpAddress} after {Attempts} attempts", ipAddress, attempt + 1);
            }
            finally
            {
                udpClient?.Close();
                udpClient?.Dispose();
            }
        }

        return string.Empty;
    }

    private class ScanResult
    {
        public bool Success { get; set; }
        public string? MacAddress { get; set; }
        public string? CryptoKey { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? ErrorCode { get; set; }
    }

    private class PackCommandResult
    {
        public bool Success { get; set; }
        public string? ResponseData { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? ErrorCode { get; set; }
    }
}
