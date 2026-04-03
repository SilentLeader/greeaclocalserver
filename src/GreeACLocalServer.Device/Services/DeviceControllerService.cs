using GreeACLocalServer.Device.Commands;
using GreeACLocalServer.Device.Interfaces;
using GreeACLocalServer.Device.Requests;
using GreeACLocalServer.Device.Responses;
using GreeACLocalServer.Device.Results;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Serilog.Context;

namespace GreeACLocalServer.Device.Services;

internal class DeviceControllerService(
        ILogger<DeviceControllerService> logger,
        ICryptoService cryptoService) : IDeviceControllerService
{
    private const int CommandPort = 7000;
    private const int CommandTimeoutMs = 3000;
    private readonly ILogger<DeviceControllerService> _logger = logger;
    private readonly ICryptoService _cryptoService = cryptoService;

    public async Task<DeviceStatusResult> GetDeviceStatusAsync(GetDeviceStatusRequest operation, CancellationToken cancellationToken = default)
    {
        var operationId = $"DEV-{Guid.NewGuid().ToString("N")[..8]}";

        using (LogContext.PushProperty("OperationId", operationId))
        {
            try
            {
                _logger.LogDebug("Querying device status for IP {IpAddress}", operation.IpAddress);

                var scanResult = await ScanDeviceAsync(operation.IpAddress, cancellationToken);
                if (!scanResult.IsSuccess || string.IsNullOrWhiteSpace(scanResult.CryptoKey))
                {
                    _logger.LogWarning("Scan failed: {ErrorCode} - {Message}", scanResult.ErrorCode, scanResult.Message);
                    return new DeviceStatusResult(false, scanResult.Message, scanResult.ErrorCode);
                }

                var command = new QueryStatusCommand(["host", "name"]);
                var result = await SendPackCommandAsync<QueryResponse, QueryStatusCommand>(operation.IpAddress, scanResult.MacAddress!, scanResult.CryptoKey, command, 0, cancellationToken);

                if (result.IsSuccess
                    && result.ResponseData != null
                    && result.ResponseData.ParameterValues.Count == command.ParameterNames.Count
                    && command.ParameterNames.SequenceEqual(result.ResponseData.ParameterNames))
                {
                    var hostName = result.ResponseData.ParameterValues[0];
                    var deviceName = result.ResponseData.ParameterValues[1];

                    _logger.LogDebug("Device status retrieved: Name={DeviceName}, Host={HostName}", deviceName, hostName);

                    return new DeviceStatusResult(true, string.Empty, deviceName: deviceName, remoteHost: hostName, macAddress: scanResult.MacAddress);
                }

                _logger.LogWarning("Query failed: {ErrorCode} - {Message}", result.ErrorCode, result.Message);
                return new DeviceStatusResult(false, result.Message, result.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying device status for IP {IpAddress}", operation.IpAddress);
                return new DeviceStatusResult(false, $"Failed to query device status: {ex.Message}", "QUERY_ERROR");
            }
        }
    }

    public async Task<SimpleDeviceOperationResult> SetDeviceNameAsync(SetDeviceNameRequest operation, CancellationToken cancellationToken = default)
    {
        var operationId = $"DEV-{Guid.NewGuid().ToString("N")[..8]}";

        using (LogContext.PushProperty("OperationId", operationId))
        {
            try
            {
                _logger.LogDebug("Setting device name for IP {IpAddress} to {DeviceName}", operation.IpAddress, operation.DeviceName);

                var scanResult = await ScanDeviceAsync(operation.IpAddress, cancellationToken);
                if (!scanResult.IsSuccess)
                {
                    return new SimpleDeviceOperationResult(false, scanResult.Message, scanResult.ErrorCode);
                }

                var command = new ParameterCommand(["name"], [operation.DeviceName]);
                var result = await SendPackCommandAsync<ParameterResponse, ParameterCommand>(operation.IpAddress, scanResult.MacAddress!, scanResult.CryptoKey!, command, 0, cancellationToken);

                if (result.IsSuccess && result.ResponseData?.ResultCode == (int)HttpStatusCode.OK)
                {
                    _logger.LogInformation("Device name set successfully");
                    return new SimpleDeviceOperationResult(true, string.Empty);
                }

                return new SimpleDeviceOperationResult(false, result.Message, result.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting device name for IP {IpAddress}", operation.IpAddress);
                return new SimpleDeviceOperationResult(false, $"Failed to set device name: {ex.Message}", "SET_NAME_ERROR");
            }
        }
    }

    public async Task<SimpleDeviceOperationResult> SetRemoteHostAsync(SetRemoteHostRequest operation, CancellationToken cancellationToken = default)
    {
        var operationId = $"DEV-{Guid.NewGuid().ToString("N")[..8]}";

        using (LogContext.PushProperty("OperationId", operationId))
        {
            try
            {
                _logger.LogDebug("Setting remote host for IP {IpAddress} to {RemoteHost}", operation.IpAddress, operation.RemoteHost);

                var scanResult = await ScanDeviceAsync(operation.IpAddress, cancellationToken);
                if (!scanResult.IsSuccess)
                {
                    return new SimpleDeviceOperationResult(false, scanResult.Message, scanResult.ErrorCode);
                }

                var command = new ParameterCommand(["host"], [operation.RemoteHost]);
                var result = await SendPackCommandAsync<ParameterResponse, ParameterCommand>(operation.IpAddress, scanResult.MacAddress!, scanResult.CryptoKey!, command, 0, cancellationToken);

                if (result.IsSuccess && result.ResponseData?.ResultCode == (int)HttpStatusCode.OK)
                {
                    _logger.LogInformation("Remote host set successfully");
                    return new SimpleDeviceOperationResult(true, string.Empty);
                }

                return new SimpleDeviceOperationResult(false, result.Message, result.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting remote host for IP {IpAddress}", operation.IpAddress);
                return new SimpleDeviceOperationResult(false, $"Failed to set remote host: {ex.Message}", "SET_HOST_ERROR");
            }
        }
    }

    private async Task<ScanResult> ScanDeviceAsync(string ipAddress, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Scanning device at IP {IpAddress}", ipAddress);

            var scanCommand = new ScanCommand();
            var response = await SendUdpCommandAsync<ScanCommand, PackResponse>(ipAddress, scanCommand, cancellationToken);

            if (response == null || string.IsNullOrEmpty(response.Data))
            {
                _logger.LogWarning("No response from device at IP {IpAddress}", ipAddress);
                return new ScanResult(false, "Device did not respond to scan request", "NO_RESPONSE");
            }


            var decryptedPack = _cryptoService.Decrypt(response.Data, "");
            var packResponse = JsonSerializer.Deserialize<ScanResponse>(decryptedPack);

            if (packResponse == null || string.IsNullOrWhiteSpace(packResponse.Mac))
            {
                _logger.LogWarning("MAC address not found in scan response from IP {IpAddress}", ipAddress);
                return new ScanResult(false, "MAC address not found in scan response", "MAC_NOT_FOUND");
            }

            _logger.LogDebug("Device MAC discovered: {MacAddress}", packResponse.Mac);

            var bindCommand = new BindCommand { UId = 0, Mac = packResponse.Mac };
            var bindResponse = await SendPackCommandAsync<BindResponse, BindCommand>(ipAddress, packResponse.Mac, null, bindCommand, 1, cancellationToken);

            if (bindResponse?.ResponseData == null || bindResponse.ResponseData.ResponseType != "bindok")
            {
                _logger.LogWarning("Bind failed for MAC {MacAddress}", packResponse.Mac);
                return new ScanResult(false, "Device did not respond to bind request", "BIND_NO_RESPONSE");
            }

            if (string.IsNullOrEmpty(bindResponse.ResponseData.CryptoKey))
            {
                _logger.LogWarning("Crypto key not found in bind response for MAC {MacAddress}", packResponse.Mac);
                return new ScanResult(false, "Crypto key not found in bind response", "KEY_NOT_FOUND");
            }

            _logger.LogDebug("Device scan completed successfully: MAC={MacAddress}", packResponse.Mac);
            return new ScanResult(true, "Device scan successfull", null, macAddress: packResponse.Mac, cryptoKey: bindResponse.ResponseData.CryptoKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning device at IP {IpAddress}", ipAddress);
            return new ScanResult(false, $"Scan failed: {ex.Message}", "SCAN_EXCEPTION");
        }
    }

    private async Task<PackCommandResult<TResponse>> SendPackCommandAsync<TResponse, TCommand>(string ipAddress, string macAddress, string? cryptoKey, TCommand command, int? id, CancellationToken cancellationToken)
    where TCommand : class
    {
        try
        {
            var packData = JsonSerializer.Serialize(command);
            var encryptedCommand = _cryptoService.Encrypt(packData, cryptoKey);
            var packCommand = new PackCommand(encryptedCommand, macAddress, id);

            _logger.LogDebug("Sending pack command to IP {IpAddress} MAC {MacAddress} Data: {packData}", ipAddress, macAddress, packData);

            var response = await SendUdpCommandAsync<PackCommand, PackResponse>(ipAddress, packCommand, cancellationToken);

            if (response == null || string.IsNullOrEmpty(response.Data))
            {
                _logger.LogWarning("No response from device at IP {IpAddress} MAC {MacAddress}", ipAddress, macAddress);
                return new PackCommandResult<TResponse>(false, "No response from device", "NO_RESPONSE", default!);
            }

            var decryptedResponse = _cryptoService.Decrypt(response.Data, cryptoKey);
            var responseData = JsonSerializer.Deserialize<TResponse>(decryptedResponse);
            _logger.LogDebug("Response data: {decryptedResponse}", decryptedResponse);

            return new PackCommandResult<TResponse>(true, "Pack command executed successfully", null, responseData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending pack command to device at IP {IpAddress}", ipAddress);
            return new PackCommandResult<TResponse>(false, $"Pack command failed: {ex.Message}", "PACK_COMMAND_EXCEPTION");
        }
    }

    private async Task<TResult?> SendUdpCommandAsync<TCommand, TResult>(string ipAddress, TCommand command, CancellationToken cancellationToken)
    where TCommand : class where TResult : class
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            UdpClient? udpClient = null;
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(CommandTimeoutMs);

                udpClient = new UdpClient();
                udpClient.Client.SendTimeout = CommandTimeoutMs;
                udpClient.Client.ReceiveTimeout = CommandTimeoutMs;

                udpClient.Connect(IPAddress.Parse(ipAddress), CommandPort);
                var rawCommand = JsonSerializer.Serialize(command);

                var sendBytes = Encoding.ASCII.GetBytes(rawCommand);
                udpClient.Send(sendBytes, sendBytes.Length);

                var remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                var receiveBytes = udpClient.Receive(ref remoteEndPoint);
                var response = Encoding.ASCII.GetString(receiveBytes);

                return response == null ? null : JsonSerializer.Deserialize<TResult>(response);
            }
            catch (Exception ex) when (attempt < 2)
            {
                _logger.LogDebug(ex, "UDP command attempt {Attempt} failed for IP {IpAddress}, retrying...", attempt + 1, ipAddress);
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

        return null;
    }
}