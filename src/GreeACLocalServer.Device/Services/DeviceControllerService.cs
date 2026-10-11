using GreeACLocalServer.Device.Commands;
using GreeACLocalServer.Device.Crypto;
using GreeACLocalServer.Device.Interfaces;
using GreeACLocalServer.Device.Requests;
using GreeACLocalServer.Device.Responses;
using GreeACLocalServer.Device.Results;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Serilog.Context;

namespace GreeACLocalServer.Device.Services;

internal class DeviceControllerService(
        ILogger<DeviceControllerService> logger,
        IEnumerable<IGreeCipher> ciphers) : IDeviceControllerService
{
    private const int CommandPort = 7000;
    private const int CommandTimeoutMs = 3000;
    private readonly ILogger<DeviceControllerService> _logger = logger;
    private readonly Dictionary<GreeProtocolGeneration, IGreeCipher> _ciphers = ciphers.ToDictionary(c => c.Generation);

    /// <summary>
    /// Per-IP cache of the <c>scan → bind</c> result (MAC + device crypto key) so
    /// the recurring runtime-state poll and the firmware probe do not repeat the
    /// full handshake every time. Entries expire after <see cref="BindCacheTtl"/>
    /// and are dropped immediately when the device rejects the cached key.
    /// </summary>
    private readonly ConcurrentDictionary<string, CachedBind> _bindCache = new();

    private static readonly TimeSpan BindCacheTtl = TimeSpan.FromMinutes(30);

    private sealed record CachedBind(string MacAddress, string CryptoKey, GreeProtocolGeneration Generation, DateTime CachedUtc);

    /// <summary>
    /// Per-IP protocol generation the last successful bind used. Unlike
    /// <see cref="_bindCache"/> it never expires, so a known GCM device is not
    /// probed with the ECB bind (and its timeout) again after the key expires.
    /// </summary>
    private readonly ConcurrentDictionary<string, GreeProtocolGeneration> _knownGeneration = new();

    private readonly record struct BindResolution(ScanResult Scan, bool FromCache);

    public async Task<DeviceStatusResult> GetDeviceStatusAsync(GetDeviceStatusRequest operation, CancellationToken cancellationToken = default)
    {
        var operationId = $"DEV-{Guid.NewGuid().ToString("N")[..8]}";

        using (LogContext.PushProperty("OperationId", operationId))
        {
            try
            {
                _logger.LogDebug("Querying device status for IP {IpAddress}", operation.IpAddress);

                var command = new QueryStatusCommand(["host", "name"]);
                var (scan, result) = await SendPackWithBindAsync<QueryResponse, QueryStatusCommand>(operation.IpAddress, command, cancellationToken);

                if (!scan.IsSuccess || string.IsNullOrWhiteSpace(scan.CryptoKey))
                {
                    _logger.LogWarning("Scan failed: {ErrorCode} - {Message}", scan.ErrorCode, scan.Message);
                    return new DeviceStatusResult(false, scan.Message, scan.ErrorCode);
                }

                if (result.IsSuccess
                    && result.ResponseData != null
                    && result.ResponseData.ParameterValues.Count == command.ParameterNames.Count
                    && command.ParameterNames.SequenceEqual(result.ResponseData.ParameterNames))
                {
                    var hostName = result.ResponseData.ValueAsText(0);
                    var deviceName = result.ResponseData.ValueAsText(1);

                    _logger.LogDebug("Device status retrieved: Name={DeviceName}, Host={HostName}", deviceName, hostName);

                    return new DeviceStatusResult(true, string.Empty, deviceName: deviceName, remoteHost: hostName, macAddress: scan.MacAddress);
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

    public async Task<DeviceFirmwareResult> GetDeviceFirmwareAsync(GetDeviceStatusRequest operation, CancellationToken cancellationToken = default)
    {
        var operationId = $"DEV-{Guid.NewGuid().ToString("N")[..8]}";

        using (LogContext.PushProperty("OperationId", operationId))
        {
            try
            {
                _logger.LogDebug("Querying device firmware for IP {IpAddress}", operation.IpAddress);

                var command = new QueryStatusCommand(["hid"]);
                var (scan, result) = await SendPackWithBindAsync<QueryResponse, QueryStatusCommand>(operation.IpAddress, command, cancellationToken);

                if (!scan.IsSuccess || string.IsNullOrWhiteSpace(scan.CryptoKey))
                {
                    // Best-effort: the opportunistic background refresh calls this on
                    // every device, so an unreachable device must not log at warning.
                    _logger.LogDebug("Firmware scan failed: {ErrorCode} - {Message}", scan.ErrorCode, scan.Message);
                    return new DeviceFirmwareResult(false, scan.Message, scan.ErrorCode);
                }

                var hid = result.IsSuccess ? result.ResponseData?.ValueAsText(0) : null;
                if (result.IsSuccess && !string.IsNullOrWhiteSpace(hid))
                {
                    FirmwareInfo.TryParse(hid, out var code, out var version);
                    _logger.LogDebug("Device firmware retrieved: Version={Version}, Code={Code}", version, code);
                    return new DeviceFirmwareResult(true, string.Empty, hid: hid, firmwareVersion: version, firmwareCode: code, macAddress: scan.MacAddress);
                }

                _logger.LogDebug("Firmware query failed: {ErrorCode} - {Message}", result.ErrorCode, result.Message);
                return new DeviceFirmwareResult(false, result.IsSuccess ? "Device did not report a firmware identifier" : result.Message, result.ErrorCode ?? "NO_HID");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error querying device firmware for IP {IpAddress}", operation.IpAddress);
                return new DeviceFirmwareResult(false, $"Failed to query device firmware: {ex.Message}", "QUERY_ERROR");
            }
        }
    }

    public async Task<DeviceRuntimeStateResult> GetDeviceRuntimeStateAsync(GetDeviceStatusRequest operation, CancellationToken cancellationToken = default)
    {
        var operationId = $"DEV-{Guid.NewGuid().ToString("N")[..8]}";

        using (LogContext.PushProperty("OperationId", operationId))
        {
            try
            {
                _logger.LogDebug("Querying device runtime state for IP {IpAddress}", operation.IpAddress);

                var command = new QueryStatusCommand(["Pow", "Mod", "SetTem", "TemUn", "TemSen"]);
                var (scan, result) = await SendPackWithBindAsync<QueryResponse, QueryStatusCommand>(operation.IpAddress, command, cancellationToken);

                if (!scan.IsSuccess || string.IsNullOrWhiteSpace(scan.CryptoKey))
                {
                    _logger.LogDebug("Runtime-state scan failed: {ErrorCode} - {Message}", scan.ErrorCode, scan.Message);
                    return new DeviceRuntimeStateResult(false, scan.Message, scan.ErrorCode);
                }

                if (!result.IsSuccess || result.ResponseData is null)
                {
                    _logger.LogDebug("Runtime-state query failed: {ErrorCode} - {Message}", result.ErrorCode, result.Message);
                    return new DeviceRuntimeStateResult(false, result.Message, result.ErrorCode ?? "QUERY_FAILED");
                }

                var values = ZipColumns(result.ResponseData.ParameterNames, result.ResponseData);

                var power = ParseBool(values, "Pow");
                var mode = ParseInt(values, "Mod");
                var setTemp = ParseInt(values, "SetTem");
                var tempUnit = ParseInt(values, "TemUn");
                var currentTempRaw = ParseInt(values, "TemSen");

                if (power is null || mode is null || setTemp is null)
                {
                    _logger.LogDebug("Runtime-state response missing expected columns (got {Cols})",
                        string.Join(",", result.ResponseData.ParameterNames));
                    return new DeviceRuntimeStateResult(false, "Device did not report the expected status columns", "INCOMPLETE_STATUS");
                }

                _logger.LogDebug("Runtime state retrieved: Pow={Power} Mod={Mode} SetTem={SetTem} TemUn={TemUn} TemSen={TemSen}",
                    power, mode, setTemp, tempUnit, currentTempRaw);

                return new DeviceRuntimeStateResult(true, string.Empty,
                    power: power, mode: mode, targetTemperature: setTemp, temperatureUnit: tempUnit,
                    currentTemperatureRaw: currentTempRaw, macAddress: scan.MacAddress);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error querying device runtime state for IP {IpAddress}", operation.IpAddress);
                return new DeviceRuntimeStateResult(false, $"Failed to query device runtime state: {ex.Message}", "QUERY_ERROR");
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

                var command = new ParameterCommand(["name"], [operation.DeviceName]);
                var (scan, result) = await SendPackWithBindAsync<ParameterResponse, ParameterCommand>(operation.IpAddress, command, cancellationToken);

                if (!scan.IsSuccess)
                {
                    return new SimpleDeviceOperationResult(false, scan.Message, scan.ErrorCode);
                }

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

                var command = new ParameterCommand(["host"], [operation.RemoteHost]);
                var (scan, result) = await SendPackWithBindAsync<ParameterResponse, ParameterCommand>(operation.IpAddress, command, cancellationToken);

                if (!scan.IsSuccess)
                {
                    return new SimpleDeviceOperationResult(false, scan.Message, scan.ErrorCode);
                }

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

    /// <summary>
    /// Resolves the device MAC + crypto key for <paramref name="ipAddress"/>,
    /// serving a cached <c>scan → bind</c> result when one is fresh. The returned
    /// <see cref="BindResolution.FromCache"/> flag lets the caller retry once with
    /// a forced re-bind if the cached key turns out to be stale.
    /// </summary>
    private async Task<BindResolution> ResolveBindAsync(string ipAddress, bool forceRefresh, CancellationToken cancellationToken)
    {
        if (!forceRefresh
            && _bindCache.TryGetValue(ipAddress, out var cached)
            && DateTime.UtcNow - cached.CachedUtc < BindCacheTtl)
        {
            _logger.LogDebug("Using cached bind key for IP {IpAddress} MAC {MacAddress}", ipAddress, cached.MacAddress);
            return new BindResolution(
                new ScanResult(true, "Using cached bind key", null, cached.MacAddress, cached.CryptoKey, cached.Generation),
                FromCache: true);
        }

        var scan = await ScanDeviceAsync(ipAddress, cancellationToken);
        if (scan.IsSuccess && !string.IsNullOrWhiteSpace(scan.MacAddress) && !string.IsNullOrWhiteSpace(scan.CryptoKey))
        {
            _bindCache[ipAddress] = new CachedBind(scan.MacAddress!, scan.CryptoKey!, scan.Generation, DateTime.UtcNow);
            _knownGeneration[ipAddress] = scan.Generation;
        }

        return new BindResolution(scan, FromCache: false);
    }

    private void InvalidateBind(string ipAddress) => _bindCache.TryRemove(ipAddress, out _);

    /// <summary>
    /// Sends a pack command using a (possibly cached) bind key. If the command
    /// fails and the key came from the cache, the entry is dropped and the command
    /// is retried once against a fresh <c>scan → bind</c> handshake.
    /// </summary>
    private async Task<(ScanResult Scan, PackCommandResult<TResponse> Result)> SendPackWithBindAsync<TResponse, TCommand>(
        string ipAddress, TCommand command, CancellationToken cancellationToken)
        where TCommand : class
    {
        var bind = await ResolveBindAsync(ipAddress, forceRefresh: false, cancellationToken);
        if (!bind.Scan.IsSuccess || string.IsNullOrWhiteSpace(bind.Scan.CryptoKey))
        {
            return (bind.Scan, new PackCommandResult<TResponse>(false, bind.Scan.Message, bind.Scan.ErrorCode));
        }

        var result = await SendPackCommandAsync<TResponse, TCommand>(
            ipAddress, bind.Scan.MacAddress!, _ciphers[bind.Scan.Generation], bind.Scan.CryptoKey, command, 0, cancellationToken);

        if (result.IsSuccess || !bind.FromCache)
        {
            return (bind.Scan, result);
        }

        _logger.LogDebug("Cached bind key for IP {IpAddress} was rejected; re-binding and retrying once", ipAddress);
        InvalidateBind(ipAddress);

        var fresh = await ResolveBindAsync(ipAddress, forceRefresh: true, cancellationToken);
        if (!fresh.Scan.IsSuccess || string.IsNullOrWhiteSpace(fresh.Scan.CryptoKey))
        {
            return (fresh.Scan, new PackCommandResult<TResponse>(false, fresh.Scan.Message, fresh.Scan.ErrorCode));
        }

        result = await SendPackCommandAsync<TResponse, TCommand>(
            ipAddress, fresh.Scan.MacAddress!, _ciphers[fresh.Scan.Generation], fresh.Scan.CryptoKey, command, 0, cancellationToken);
        return (fresh.Scan, result);
    }

    private static Dictionary<string, string?> ZipColumns(IReadOnlyList<string> names, QueryResponse response)
    {
        var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < names.Count; i++)
        {
            map[names[i]] = response.ValueAsText(i);
        }
        return map;
    }

    private static int? ParseInt(IReadOnlyDictionary<string, string?> values, string key)
        => values.TryGetValue(key, out var raw)
            && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static bool? ParseBool(IReadOnlyDictionary<string, string?> values, string key)
        => ParseInt(values, key) is { } value ? value != 0 : null;

    private async Task<ScanResult> ScanDeviceAsync(string ipAddress, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Scanning device at IP {IpAddress}", ipAddress);

            var scanCommand = new ScanCommand();
            var response = await SendUdpCommandAsync<ScanCommand, PackResponse>(ipAddress, scanCommand, cancellationToken);

            if (response == null || string.IsNullOrEmpty(response.Data))
            {
                _logger.LogDebug("No response from device at IP {IpAddress}", ipAddress);
                return new ScanResult(false, "Device did not respond to scan request", "NO_RESPONSE");
            }


            // The scan reply is encrypted with a generic key: GCM when the envelope
            // carries a tag, ECB otherwise. Many GCM units still answer the scan in
            // ECB, so this alone does not tell which generation the bind needs.
            var scanGeneration = string.IsNullOrEmpty(response.Tag) ? GreeProtocolGeneration.V1Ecb : GreeProtocolGeneration.V2Gcm;
            if (!_ciphers.TryGetValue(scanGeneration, out var scanCipher)
                || !scanCipher.TryDecrypt(response.Data, response.Tag, null, out var decryptedPack))
            {
                _logger.LogDebug("Could not decrypt the scan response from IP {IpAddress}", ipAddress);
                return new ScanResult(false, "Could not decrypt the scan response", "SCAN_DECRYPT_FAILED");
            }

            var packResponse = JsonSerializer.Deserialize<ScanResponse>(decryptedPack);

            if (packResponse == null || string.IsNullOrWhiteSpace(packResponse.Mac))
            {
                _logger.LogWarning("MAC address not found in scan response from IP {IpAddress}", ipAddress);
                return new ScanResult(false, "MAC address not found in scan response", "MAC_NOT_FOUND");
            }

            _logger.LogDebug("Device MAC discovered: {MacAddress}", packResponse.Mac);

            var candidates = GetBindCandidates(ipAddress, scanGeneration);
            var keyMissing = false;

            for (var i = 0; i < candidates.Count; i++)
            {
                var cipher = candidates[i];
                var isLastCandidate = i == candidates.Count - 1;

                // The GCM payloads mirror, field for field, what is confirmed to work on
                // a real GCM unit; the ECB ones stay as they always were.
                var bindCommand = new BindCommand
                {
                    UId = 0,
                    Mac = packResponse.Mac,
                    Cid = cipher.Generation == GreeProtocolGeneration.V2Gcm ? packResponse.Mac : null,
                };

                // A device silently drops a bind encrypted for the other generation, so
                // "no response" is expected while probing and only worth a warning once
                // every generation has been tried.
                var bindResponse = await SendPackCommandAsync<BindResponse, BindCommand>(
                    ipAddress, packResponse.Mac, cipher, null, bindCommand, 1, cancellationToken,
                    isLastCandidate ? LogLevel.Warning : LogLevel.Debug);

                if (bindResponse.ResponseData == null || bindResponse.ResponseData.ResponseType != "bindok")
                {
                    _logger.LogDebug("Bind failed for MAC {MacAddress} using {Generation}", packResponse.Mac, cipher.Generation);
                    continue;
                }

                if (string.IsNullOrEmpty(bindResponse.ResponseData.CryptoKey))
                {
                    _logger.LogWarning("Crypto key not found in bind response for MAC {MacAddress}", packResponse.Mac);
                    keyMissing = true;
                    continue;
                }

                _logger.LogDebug("Device scan completed successfully: MAC={MacAddress} Generation={Generation}", packResponse.Mac, cipher.Generation);
                return new ScanResult(true, "Device scan successful", null, macAddress: packResponse.Mac, cryptoKey: bindResponse.ResponseData.CryptoKey, generation: cipher.Generation);
            }

            return keyMissing
                ? new ScanResult(false, "Crypto key not found in bind response", "KEY_NOT_FOUND")
                : new ScanResult(false, "Device did not respond to bind request", "BIND_NO_RESPONSE");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error scanning device at IP {IpAddress}", ipAddress);
            return new ScanResult(false, $"Scan failed: {ex.Message}", "SCAN_EXCEPTION");
        }
    }

    /// <summary>
    /// Generations to try the bind with, most likely first: the one the scan reply
    /// proved (GCM tag), else the one this IP last bound with, else ECB then GCM.
    /// </summary>
    private List<IGreeCipher> GetBindCandidates(string ipAddress, GreeProtocolGeneration scanGeneration)
    {
        var preferred = scanGeneration == GreeProtocolGeneration.V2Gcm
            ? GreeProtocolGeneration.V2Gcm
            : _knownGeneration.GetValueOrDefault(ipAddress, GreeProtocolGeneration.V1Ecb);

        GreeProtocolGeneration[] order = preferred == GreeProtocolGeneration.V2Gcm
            ? [GreeProtocolGeneration.V2Gcm, GreeProtocolGeneration.V1Ecb]
            : [GreeProtocolGeneration.V1Ecb, GreeProtocolGeneration.V2Gcm];

        var candidates = new List<IGreeCipher>(order.Length);
        foreach (var generation in order)
        {
            if (generation == GreeProtocolGeneration.V2Gcm && !AesGcmGreeCipher.IsSupported)
            {
                _logger.LogDebug("AES-GCM is not supported on this platform; skipping the GCM bind");
                continue;
            }

            if (_ciphers.TryGetValue(generation, out var cipher))
            {
                candidates.Add(cipher);
            }
        }

        return candidates;
    }

    private async Task<PackCommandResult<TResponse>> SendPackCommandAsync<TResponse, TCommand>(string ipAddress, string macAddress, IGreeCipher cipher, string? cryptoKey, TCommand command, int? id, CancellationToken cancellationToken, LogLevel noResponseLogLevel = LogLevel.Warning)
    where TCommand : class
    {
        try
        {
            if (command is QueryStatusCommand statusCommand)
            {
                statusCommand.Mac = cipher.Generation == GreeProtocolGeneration.V2Gcm ? macAddress : 0;
            }

            var packData = JsonSerializer.Serialize(command);
            var encryptedCommand = cipher.Encrypt(packData, cryptoKey);
            var packCommand = new PackCommand(encryptedCommand.Pack, macAddress, id, encryptedCommand.Tag);

            _logger.LogDebug("Sending pack command to IP {IpAddress} MAC {MacAddress} Data: {packData}", ipAddress, macAddress, packData);

            var response = await SendUdpCommandAsync<PackCommand, PackResponse>(ipAddress, packCommand, cancellationToken);

            if (response == null || string.IsNullOrEmpty(response.Data))
            {
                _logger.Log(noResponseLogLevel, "No response from device at IP {IpAddress} MAC {MacAddress}", ipAddress, macAddress);
                return new PackCommandResult<TResponse>(false, "No response from device", "NO_RESPONSE", default!);
            }

            if (!cipher.TryDecrypt(response.Data, response.Tag, cryptoKey, out var decryptedResponse))
            {
                _logger.LogWarning("Could not decrypt the response from device at IP {IpAddress} MAC {MacAddress}", ipAddress, macAddress);
                return new PackCommandResult<TResponse>(false, "Could not decrypt the device response", "PACK_DECRYPT_FAILED", default!);
            }

            var responseData = JsonSerializer.Deserialize<TResponse>(decryptedResponse);
            _logger.LogDebug("Response data: {decryptedResponse}", decryptedResponse);

            return new PackCommandResult<TResponse>(true, "Pack command executed successfully", null, responseData);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error sending pack command to device at IP {IpAddress}", ipAddress);
            return new PackCommandResult<TResponse>(false, $"Pack command failed: {ex.Message}", "PACK_COMMAND_EXCEPTION");
        }
    }

    private async Task<TResult?> SendUdpCommandAsync<TCommand, TResult>(string ipAddress, TCommand command, CancellationToken cancellationToken)
    where TCommand : class where TResult : class
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(CommandTimeoutMs);

                using var udpClient = new UdpClient();
                udpClient.Connect(IPAddress.Parse(ipAddress), CommandPort);

                var rawCommand = JsonSerializer.Serialize(command);
                var sendBytes = Encoding.UTF8.GetBytes(rawCommand);
                await udpClient.SendAsync(sendBytes, cts.Token);

                var result = await udpClient.ReceiveAsync(cts.Token);
                var response = Encoding.UTF8.GetString(result.Buffer);

                return JsonSerializer.Deserialize<TResult>(response);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Caller-initiated cancellation is not retryable.
                throw;
            }
            catch (Exception ex) when (attempt < 2)
            {
                _logger.LogDebug(ex, "UDP command attempt {Attempt} failed for IP {IpAddress}, retrying...", attempt + 1, ipAddress);
                await Task.Delay(500, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "UDP command failed for IP {IpAddress} after {Attempts} attempts", ipAddress, attempt + 1);
            }
        }

        return null;
    }
}
