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
        try
        {
            // First, scan the device to get MAC and key
            var scanResult = await ScanDeviceAsync(operation.IpAddress, cancellationToken);
            if (!scanResult.IsSuccess || string.IsNullOrWhiteSpace(scanResult.CryptoKey))
            {
                return new DeviceStatusResult
                (
                    success: false,
                    message: scanResult.Message,
                    errorCode: scanResult.ErrorCode
                );
            }

            var command = new QueryStatusCommand(["host", "name"]);
            // Query device status using the cryptokey
            var result = await SendPackCommandAsync<QueryResponse, QueryStatusCommand>(operation.IpAddress, scanResult.MacAddress!, scanResult.CryptoKey, command, 0, cancellationToken);
            if (result.IsSuccess
                && result.ResponseData != null
                && result.ResponseData.ParameterValues.Count == command.ParameterNames.Count
                && command.ParameterNames.SequenceEqual(result.ResponseData.ParameterNames))
            {
                var hostName = result.ResponseData.ParameterValues[0];
                var deviceName = result.ResponseData.ParameterValues[1];

                return new DeviceStatusResult
                (
                    success: true,
                    message: string.Empty,
                    deviceName: deviceName,
                    remoteHost: hostName,
                    macAddress: scanResult.MacAddress
                );
            }
            return new DeviceStatusResult
            (
                success: false,
                message: result.Message,
                errorCode: result.ErrorCode
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying device status for IP {IpAddress}", operation.IpAddress);
            return new DeviceStatusResult
            (
                success: false,
                message: $"Failed to query device status: {ex.Message}",
                errorCode: "QUERY_ERROR"
            );

        }
    }

    public async Task<SimpleDeviceOperationResult> SetDeviceNameAsync(SetDeviceNameRequest operation, CancellationToken cancellationToken = default)
    {
        try
        {
            var scanResult = await ScanDeviceAsync(operation.IpAddress, cancellationToken);
            if (!scanResult.IsSuccess)
            {
                return new SimpleDeviceOperationResult
                (
                    success: false,
                    message: scanResult.Message,
                    errorCode: scanResult.ErrorCode
                );
            }

            var command = new ParameterCommand(["name"], [operation.DeviceName]);
            // Set device name using the cryptokey
            var result = await SendPackCommandAsync<ParameterResponse, ParameterCommand>(operation.IpAddress, scanResult.MacAddress!, scanResult.CryptoKey!, command, 0, cancellationToken);
            if (result.IsSuccess && result.ResponseData?.ResultCode == (int)HttpStatusCode.OK)
            {
                return new SimpleDeviceOperationResult
                (
                    success: true,
                    message: string.Empty
                );
            }
            return new SimpleDeviceOperationResult
            (
                success: false,
                message: result.Message,
                errorCode: result.ErrorCode
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting device name for IP {IpAddress}", operation.IpAddress);
            return new SimpleDeviceOperationResult
            (
                success: false,
                message: $"Failed to set device name: {ex.Message}",
                errorCode: "SET_NAME_ERROR"
            );
        }
    }

    public async Task<SimpleDeviceOperationResult> SetRemoteHostAsync(SetRemoteHostRequest operation, CancellationToken cancellationToken = default)
    {
        try
        {
            var scanResult = await ScanDeviceAsync(operation.IpAddress, cancellationToken);
            if (!scanResult.IsSuccess)
            {
                return new SimpleDeviceOperationResult
                (
                    success: false,
                    message: scanResult.Message,
                    errorCode: scanResult.ErrorCode
                );
            }

            // Set device name using the cryptokey
            var command = new ParameterCommand(["host"], [operation.RemoteHost]);
            var result = await SendPackCommandAsync<ParameterResponse, ParameterCommand>(operation.IpAddress, scanResult.MacAddress!, scanResult.CryptoKey!, command, 0, cancellationToken);
            if (result.IsSuccess && result.ResponseData?.ResultCode == (int)HttpStatusCode.OK)
            {
                return new SimpleDeviceOperationResult
                (
                    success: true,
                    message: string.Empty
                );
            }
            return new SimpleDeviceOperationResult
            (
                success: false,
                message: result.Message,
                errorCode: result.ErrorCode
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting remote host for IP {IpAddress}", operation.IpAddress);
            return new SimpleDeviceOperationResult
            (
                success: false,
                message: $"Failed to set remote host: {ex.Message}",
                errorCode: "SET_HOST_ERROR"
            );
        }
    }

    private async Task<ScanResult> ScanDeviceAsync(string ipAddress, CancellationToken cancellationToken)
    {
        try
        {
            var scanCommand = new ScanCommand();
            var response = await SendUdpCommandAsync<ScanCommand, PackResponse>(ipAddress, scanCommand, cancellationToken);

            if (response == null || string.IsNullOrEmpty(response.Data))
            {
                return new ScanResult
                (
                    success: false,
                    message: "Device did not respond to scan request",
                    errorCode: "NO_RESPONSE"
                );
            }

            // Decrypt the pack using default key (empty string)
            var decryptedPack = _cryptoService.Decrypt(response.Data, "");
            var packResponse = JsonSerializer.Deserialize<ScanResponse>(decryptedPack);

            if (packResponse == null
                || string.IsNullOrWhiteSpace(packResponse.Mac))
            {
                return new ScanResult
                (
                    success: false,
                    message: "MAC address not found in scan response",
                    errorCode: "MAC_NOT_FOUND"
                );
            }

            var bindCommand = new BindCommand
            {
                UId = 0,
                Mac = packResponse.Mac
            };

            var bindResponse = await SendPackCommandAsync<BindResponse, BindCommand>(ipAddress, packResponse.Mac, null, bindCommand, 1, cancellationToken);

            if (bindResponse?.ResponseData == null || bindResponse.ResponseData.ResponseType != "bindok")
            {
                return new ScanResult
                (
                    success: false,
                    message: "Device did not respond to bind request",
                    errorCode: "BIND_NO_RESPONSE"
                );
            }

            if (string.IsNullOrEmpty(bindResponse.ResponseData.CryptoKey))
            {
                return new ScanResult
                (
                    success: false,
                    message: "Crypto key not found in bind response",
                    errorCode: "KEY_NOT_FOUND"
                );
            }

            return new ScanResult
            (
                success: true,
                message: "Device scan successful",
                macAddress: packResponse.Mac,
                cryptoKey: bindResponse.ResponseData.CryptoKey
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning device at IP {IpAddress}", ipAddress);
            return new ScanResult
            (
                success: false,
                message: $"Scan failed: {ex.Message}",
                errorCode: "SCAN_EXCEPTION"
            );
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

            var response = await SendUdpCommandAsync<PackCommand, PackResponse>(ipAddress, packCommand, cancellationToken);

            if (response == null
                || string.IsNullOrEmpty(response.Data))
            {
                return new PackCommandResult<TResponse>
                (
                    success: false,
                    message: "No response from device",
                    errorCode: "NO_RESPONSE"
                );
            }

            var decryptedResponse = _cryptoService.Decrypt(response.Data, cryptoKey);
            var responseData = JsonSerializer.Deserialize<TResponse>(decryptedResponse);

            return new PackCommandResult<TResponse>
            (
                success: true,
                message: "Pack command executed successfully",
                responseData: responseData
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending pack command to device at IP {IpAddress}", ipAddress);
            return new PackCommandResult<TResponse>
            (
                success: false,
                message: $"Pack command failed: {ex.Message}",
                errorCode: "PACK_COMMAND_EXCEPTION"
            );
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

        return null;
    }
}