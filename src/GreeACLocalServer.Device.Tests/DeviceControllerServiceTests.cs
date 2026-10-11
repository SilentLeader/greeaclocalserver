using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using GreeACLocalServer.Device.Crypto;
using GreeACLocalServer.Device.Interfaces;
using GreeACLocalServer.Device.Requests;
using GreeACLocalServer.Device.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GreeACLocalServer.Device.Tests;

/// <summary>
/// Covers <see cref="DeviceControllerService"/> end to end over a loopback UDP stub that
/// speaks the scan → bind → pack handshake. Guards WP-06 findings: the UDP exchange must be
/// async/cancellable (F6), UTF-8 clean (F7) and time out cleanly when the device is silent.
///
/// The service hard-codes UDP port 7000, so these tests bind that fixed port and are marked
/// as integration tests (serialised via the collection) like <see cref="SocketHandlerServiceTests"/>.
/// </summary>
[Collection("DeviceControllerService")]
[Trait("Category", "Integration")]
public sealed class DeviceControllerServiceTests
{
    private const int CommandPort = 7000;
    private const string Mac = "aabbccddeeff";
    private const string CryptoKey = "testkey1234567890";

    /// <summary>Device key handed out by the stub in GCM mode; AES needs a real 16 byte key there.</summary>
    private const string GcmDeviceKey = "0123456789abcdef";

    /// <summary>Pass-through crypto so the stub can work with plain JSON payloads.</summary>
    private sealed class IdentityCryptoService : ICryptoService
    {
        public string Decrypt(string pack, string? key = null) => pack;
        public string Encrypt(string pack, string? key = null) => pack;
        public X509Certificate2 GetCertificate(string? hostName = null)
            => throw new NotSupportedException();
    }

    private static DeviceControllerService CreateService()
        => new(
            NullLogger<DeviceControllerService>.Instance,
            [new AesEcbGreeCipher(new IdentityCryptoService()), new AesGcmGreeCipher()]);

    /// <summary>
    /// Minimal loopback device: answers <c>scan</c> with a MAC, <c>pack</c>/<c>i:1</c> with a
    /// bind key and any other <c>pack</c> with a canned status query response. When
    /// <paramref name="reply"/> is <c>false</c> it consumes datagrams but never answers.
    /// With <see cref="GreeProtocolGeneration.V2Gcm"/> it acts like a newer Wi-Fi module:
    /// bind / pack must be real AES-GCM (a pack without a tag is silently dropped) while the
    /// scan is answered in "ECB" (plain JSON here) unless <paramref name="gcmScanReply"/> is set.
    /// </summary>
    private sealed class UdpDeviceStub : IDisposable
    {
        private readonly UdpClient _udp = new(new IPEndPoint(IPAddress.Loopback, CommandPort));
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _loop;

        private readonly string _statusHid;
        private int _scanCount;
        private int _statusFailuresRemaining;
        private int _droppedEcbPackCount;

        /// <summary>Decrypted bind / status payloads a GCM stub accepted, in arrival order.</summary>
        public System.Collections.Concurrent.ConcurrentQueue<string> GcmRequests { get; } = new();

        /// <summary>Number of <c>scan</c> datagrams answered so far (a fresh scan → bind handshake bumps this).</summary>
        public int ScanCount => Volatile.Read(ref _scanCount);

        /// <summary>Number of tag-less (ECB) <c>pack</c> datagrams a GCM stub ignored.</summary>
        public int DroppedEcbPackCount => Volatile.Read(ref _droppedEcbPackCount);

        public UdpDeviceStub(
            string statusHost,
            string statusName,
            bool reply = true,
            string statusHid = "362001065736+U-QCOM4004CV3.76.bin",
            int statusPow = 1,
            int statusMod = 1,
            int statusSetTem = 24,
            int statusTemUn = 0,
            int statusTemSen = 65,
            int statusFailures = 0,
            GreeProtocolGeneration generation = GreeProtocolGeneration.V1Ecb,
            bool gcmScanReply = false)
        {
            var isGcm = generation == GreeProtocolGeneration.V2Gcm;
            var gcm = new AesGcmGreeCipher();
            var deviceKey = isGcm ? GcmDeviceKey : CryptoKey;
            _statusHid = statusHid;
            _statusFailuresRemaining = statusFailures;
            _loop = Task.Run(async () =>
            {
                while (!_cts.IsCancellationRequested)
                {
                    UdpReceiveResult received;
                    try
                    {
                        received = await _udp.ReceiveAsync(_cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }

                    if (!reply)
                    {
                        continue;
                    }

                    var request = Encoding.UTF8.GetString(received.Buffer);
                    using var doc = JsonDocument.Parse(request);
                    var type = doc.RootElement.GetProperty("t").GetString();

                    if (type == "scan")
                    {
                        Interlocked.Increment(ref _scanCount);
                    }

                    var isScan = type == "scan";
                    var isBind = !isScan && doc.RootElement.TryGetProperty("i", out var i) && i.GetInt32() == 1;
                    var requestInner = isScan ? string.Empty : doc.RootElement.GetProperty("pack").GetString()!;

                    if (isGcm && !isScan)
                    {
                        var requestTag = doc.RootElement.TryGetProperty("tag", out var t) ? t.GetString() : null;
                        if (requestTag is null)
                        {
                            Interlocked.Increment(ref _droppedEcbPackCount);
                            continue;
                        }

                        if (!gcm.TryDecrypt(requestInner, requestTag, isBind ? null : deviceKey, out requestInner))
                        {
                            continue;
                        }

                        GcmRequests.Enqueue(requestInner);
                    }

                    string inner = isScan
                        ? JsonSerializer.Serialize(new { mac = Mac })
                        : isBind
                            ? JsonSerializer.Serialize(new { t = "bindok", key = deviceKey })
                            : BuildStatusReply(requestInner);

                    string BuildStatusReply(string innerJson)
                    {
                        if (Interlocked.Decrement(ref _statusFailuresRemaining) >= 0)
                        {
                            // Simulate a stale-key / corrupt exchange: an undeserializable pack body.
                            return "}}} not json {{{";
                        }

                        using var innerDoc = JsonDocument.Parse(innerJson);
                        var cols = innerDoc.RootElement.GetProperty("cols").EnumerateArray().Select(c => c.GetString()).ToArray();
                        var dat = cols.Select(object (c) => c switch
                        {
                            "host" => statusHost,
                            "name" => statusName,
                            "hid" => _statusHid,
                            "Pow" => statusPow,
                            "Mod" => statusMod,
                            "SetTem" => statusSetTem,
                            "TemUn" => statusTemUn,
                            "TemSen" => statusTemSen,
                            _ => string.Empty,
                        }).ToArray();
                        return JsonSerializer.Serialize(new { t = "ok", r = 200, cols, dat });
                    }

                    string envelopeJson;
                    if (isGcm && (!isScan || gcmScanReply))
                    {
                        var encrypted = gcm.Encrypt(inner, isScan || isBind ? null : deviceKey);
                        envelopeJson = JsonSerializer.Serialize(new { t = "pack", pack = encrypted.Pack, tag = encrypted.Tag });
                    }
                    else
                    {
                        envelopeJson = JsonSerializer.Serialize(new { t = "pack", pack = inner });
                    }

                    var envelope = Encoding.UTF8.GetBytes(envelopeJson);
                    await _udp.SendAsync(envelope, received.RemoteEndPoint, _cts.Token);
                }
            });
        }

        public void Dispose()
        {
            _cts.Cancel();
            try { _loop.Wait(TimeSpan.FromSeconds(5)); } catch { /* best effort */ }
            _udp.Dispose();
            _cts.Dispose();
        }
    }

    [Fact]
    public async Task GetDeviceStatus_HappyPath_ParsesResponseAndKeepsUtf8Intact()
    {
        const string accentedName = "Nappali légkondi – Előszoba";
        using var stub = new UdpDeviceStub("host.example.com", accentedName);

        var result = await CreateService().GetDeviceStatusAsync(
            new GetDeviceStatusRequest("127.0.0.1"), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(accentedName, result.DeviceName);
        Assert.Equal("host.example.com", result.RemoteHost);
        Assert.Equal(Mac, result.MacAddress);
    }

    [Fact]
    public async Task GetDeviceFirmware_HappyPath_ParsesHid()
    {
        using var stub = new UdpDeviceStub("h", "n", statusHid: "362001065736+U-QCOM4004CV3.76.bin");

        var result = await CreateService().GetDeviceFirmwareAsync(
            new GetDeviceStatusRequest("127.0.0.1"), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal("362001065736+U-QCOM4004CV3.76.bin", result.Hid);
        Assert.Equal("362001065736", result.FirmwareCode);
        Assert.Equal("3.76", result.FirmwareVersion);
        Assert.Equal(Mac, result.MacAddress);
    }

    [Fact]
    public async Task GetDeviceFirmware_DeviceReportsNoHid_Fails()
    {
        using var stub = new UdpDeviceStub("h", "n", statusHid: "");

        var result = await CreateService().GetDeviceFirmwareAsync(
            new GetDeviceStatusRequest("127.0.0.1"), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task GetDeviceStatus_DeviceSilent_ReturnsNoResponseAfterTimeout()
    {
        using var stub = new UdpDeviceStub("h", "n", reply: false);

        var stopwatch = Stopwatch.StartNew();
        var result = await CreateService().GetDeviceStatusAsync(
            new GetDeviceStatusRequest("127.0.0.1"), CancellationToken.None);
        stopwatch.Stop();

        Assert.False(result.IsSuccess);
        Assert.Equal("NO_RESPONSE", result.ErrorCode);
        // 3 attempts * 3s command timeout (+ retry backoff); must not hang far beyond that.
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30), $"took {stopwatch.Elapsed}");
    }

    [Fact]
    public async Task GetDeviceStatus_CallerCancels_ReturnsPromptlyWithoutRetrying()
    {
        using var stub = new UdpDeviceStub("h", "n", reply: false);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var stopwatch = Stopwatch.StartNew();
        var result = await CreateService().GetDeviceStatusAsync(
            new GetDeviceStatusRequest("127.0.0.1"), cancelled.Token);
        stopwatch.Stop();

        Assert.False(result.IsSuccess);
        // Caller cancellation is not retryable: it must not sit through the 3x3s timeout loop.
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(3), $"took {stopwatch.Elapsed}");
    }

    [Fact]
    public async Task GetDeviceRuntimeState_HappyPath_ParsesAllColumns()
    {
        using var stub = new UdpDeviceStub("h", "n", statusPow: 1, statusMod: 4, statusSetTem: 22, statusTemUn: 1, statusTemSen: 66);

        var result = await CreateService().GetDeviceRuntimeStateAsync(
            new GetDeviceStatusRequest("127.0.0.1"), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(true, result.Power);
        Assert.Equal(4, result.Mode);
        Assert.Equal(22, result.TargetTemperature);
        Assert.Equal(1, result.TemperatureUnit);
        Assert.Equal(66, result.CurrentTemperatureRaw);
        Assert.Equal(Mac, result.MacAddress);
        Assert.Equal(1, stub.ScanCount);
    }

    [Fact]
    public async Task GetDeviceRuntimeState_SecondCall_ReusesCachedBind()
    {
        using var stub = new UdpDeviceStub("h", "n", statusPow: 0, statusMod: 1);
        var service = CreateService();

        var first = await service.GetDeviceRuntimeStateAsync(new GetDeviceStatusRequest("127.0.0.1"), CancellationToken.None);
        var second = await service.GetDeviceRuntimeStateAsync(new GetDeviceStatusRequest("127.0.0.1"), CancellationToken.None);

        Assert.True(first.IsSuccess, first.Message);
        Assert.True(second.IsSuccess, second.Message);
        Assert.Equal(false, second.Power);
        // Only the first call performed scan → bind; the second reused the cached key.
        Assert.Equal(1, stub.ScanCount);
    }

    [Fact]
    public async Task GetDeviceRuntimeState_CachedBindRejected_RebindsAndRetries()
    {
        // First call: fresh scan+bind, then a corrupt status reply -> fails (not from cache, no retry).
        // Second call: cached key, corrupt status reply -> invalidate, re-bind, valid reply -> succeeds.
        using var stub = new UdpDeviceStub("h", "n", statusFailures: 2);
        var service = CreateService();

        var first = await service.GetDeviceRuntimeStateAsync(new GetDeviceStatusRequest("127.0.0.1"), CancellationToken.None);
        var second = await service.GetDeviceRuntimeStateAsync(new GetDeviceStatusRequest("127.0.0.1"), CancellationToken.None);

        Assert.False(first.IsSuccess);
        Assert.True(second.IsSuccess, second.Message);
        Assert.Equal(2, stub.ScanCount);
    }

    [Fact]
    public async Task GetDeviceRuntimeState_DeviceSilent_Fails()
    {
        using var stub = new UdpDeviceStub("h", "n", reply: false);

        var result = await CreateService().GetDeviceRuntimeStateAsync(
            new GetDeviceStatusRequest("127.0.0.1"), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task GetDeviceStatus_GcmDevice_FallsBackFromEcbBindAndSucceeds()
    {
        const string accentedName = "Nappali légkondi – Előszoba";
        using var stub = new UdpDeviceStub("host.example.com", accentedName, generation: GreeProtocolGeneration.V2Gcm);

        var result = await CreateService().GetDeviceStatusAsync(
            new GetDeviceStatusRequest("127.0.0.1"), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(accentedName, result.DeviceName);
        Assert.Equal("host.example.com", result.RemoteHost);
        Assert.Equal(Mac, result.MacAddress);
        // The ECB bind was tried first (and ignored by the device) before the GCM one.
        Assert.True(stub.DroppedEcbPackCount > 0);

        // GCM payloads carry the MAC the way the known working clients send it.
        var requests = stub.GcmRequests.Select(r => JsonDocument.Parse(r).RootElement).ToArray();
        Assert.Equal(2, requests.Length);
        Assert.Equal("bind", requests[0].GetProperty("t").GetString());
        Assert.Equal(Mac, requests[0].GetProperty("cid").GetString());
        Assert.Equal(Mac, requests[0].GetProperty("mac").GetString());
        Assert.Equal("status", requests[1].GetProperty("t").GetString());
        Assert.Equal(Mac, requests[1].GetProperty("mac").GetString());
    }

    [Fact]
    public async Task GetDeviceRuntimeState_GcmDeviceWithGcmScanReply_BindsWithGcmFirst()
    {
        using var stub = new UdpDeviceStub("h", "n", statusPow: 1, statusMod: 4, statusSetTem: 22,
            generation: GreeProtocolGeneration.V2Gcm, gcmScanReply: true);

        var result = await CreateService().GetDeviceRuntimeStateAsync(
            new GetDeviceStatusRequest("127.0.0.1"), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(true, result.Power);
        Assert.Equal(4, result.Mode);
        Assert.Equal(22, result.TargetTemperature);
        // A tagged scan reply proves the generation, so no ECB bind probe is sent at all.
        Assert.Equal(0, stub.DroppedEcbPackCount);
    }

    [Fact]
    public async Task GetDeviceRuntimeState_GcmDevice_RemembersGenerationAcrossRebind()
    {
        // First call: ECB bind probe is ignored, GCM bind works, then a corrupt status reply -> fails.
        // Second call: cached key, corrupt status reply -> invalidate, re-bind, valid reply -> succeeds.
        using var stub = new UdpDeviceStub("h", "n", statusFailures: 2, generation: GreeProtocolGeneration.V2Gcm);
        var service = CreateService();

        var first = await service.GetDeviceRuntimeStateAsync(new GetDeviceStatusRequest("127.0.0.1"), CancellationToken.None);
        var ecbProbesAfterFirstBind = stub.DroppedEcbPackCount;
        var second = await service.GetDeviceRuntimeStateAsync(new GetDeviceStatusRequest("127.0.0.1"), CancellationToken.None);

        Assert.False(first.IsSuccess);
        Assert.True(second.IsSuccess, second.Message);
        Assert.Equal(2, stub.ScanCount);
        Assert.True(ecbProbesAfterFirstBind > 0);
        // The re-bind went straight to GCM: the device's generation is remembered per IP.
        Assert.Equal(ecbProbesAfterFirstBind, stub.DroppedEcbPackCount);
    }
}
