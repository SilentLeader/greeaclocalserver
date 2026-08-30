using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
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

    /// <summary>Pass-through crypto so the stub can work with plain JSON payloads.</summary>
    private sealed class IdentityCryptoService : ICryptoService
    {
        public string Decrypt(string pack, string? key = null) => pack;
        public string Encrypt(string pack, string? key = null) => pack;
        public X509Certificate2 GetCertificate(string? hostName = null)
            => throw new NotSupportedException();
    }

    private static DeviceControllerService CreateService()
        => new(NullLogger<DeviceControllerService>.Instance, new IdentityCryptoService());

    /// <summary>
    /// Minimal loopback device: answers <c>scan</c> with a MAC, <c>pack</c>/<c>i:1</c> with a
    /// bind key and any other <c>pack</c> with a canned status query response. When
    /// <paramref name="reply"/> is <c>false</c> it consumes datagrams but never answers.
    /// </summary>
    private sealed class UdpDeviceStub : IDisposable
    {
        private readonly UdpClient _udp = new(new IPEndPoint(IPAddress.Loopback, CommandPort));
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _loop;

        public UdpDeviceStub(string statusHost, string statusName, bool reply = true)
        {
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

                    string inner = type switch
                    {
                        "scan" => JsonSerializer.Serialize(new { mac = Mac }),
                        "pack" when doc.RootElement.TryGetProperty("i", out var i) && i.GetInt32() == 1
                            => JsonSerializer.Serialize(new { t = "bindok", key = CryptoKey }),
                        _ => JsonSerializer.Serialize(new
                        {
                            t = "ok",
                            r = 200,
                            cols = new[] { "host", "name" },
                            dat = new[] { statusHost, statusName },
                        }),
                    };

                    var envelope = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { t = "pack", pack = inner }));
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
}
