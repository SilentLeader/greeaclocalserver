using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using GreeACLocalServer.Device.Interfaces;
using GreeACLocalServer.Device.Models;
using GreeACLocalServer.Device.Responses;
using GreeACLocalServer.Device.Services;
using GreeACLocalServer.Device.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GreeACLocalServer.Device.Tests;

/// <summary>
/// Guards WP-04: idle timeout, concurrent-connection cap and clean restart of the
/// TCP listener (findings F5 and F11 restart part). Uses a real loopback socket on
/// <see cref="ServerOption.PORT"/>.
/// </summary>
[Collection("SocketHandlerService")]
[Trait("Category", "Integration")]
public class SocketHandlerServiceTests
{
    private static SocketHandlerService CreateService(ServerOptions options, IMessageHandlerService handler)
    {
        return new SocketHandlerService(
            handler,
            Options.Create(options),
            new NoopEventPublisher(),
            new StubCryptoService(),
            NullLogger<SocketHandlerService>.Instance);
    }

    private sealed class NoopEventPublisher : IDeviceEventPublisher
    {
        public void DeviceConnected(DeviceConnectedMessage message) { }
    }

    private sealed class CapturingEventPublisher : IDeviceEventPublisher
    {
        public ConcurrentQueue<DeviceConnectedMessage> Messages { get; } = new();
        public void DeviceConnected(DeviceConnectedMessage message) => Messages.Enqueue(message);
    }

    private sealed class MacReturningHandler : IMessageHandlerService
    {
        public GreeHandlerResponse GetResponse(string input, bool isTLS = false)
            => new() { Data = "ok", KeepAlive = false, MacAddress = "abcdef123456" };
    }

    private sealed class StubCryptoService : ICryptoService
    {
        public string Decrypt(string pack, string? key = null) => pack;
        public string Encrypt(string pack, string? key = null) => pack;
        public System.Security.Cryptography.X509Certificates.X509Certificate2 GetCertificate(string? hostName = null)
            => throw new NotSupportedException("TLS is disabled in these tests");
    }

    private static IMessageHandlerService KeepAliveHandler(string data = "ok")
        => new StubMessageHandler(data);

    private sealed class StubMessageHandler(string data) : IMessageHandlerService
    {
        public GreeHandlerResponse GetResponse(string input, bool isTLS = false)
            => new() { Data = data, KeepAlive = true };
    }

    private static ServerOptions LoopbackOptions() => new()
    {
        DomainName = "test.local",
        ExternalIp = "127.0.0.1",
        TLSEnabled = false,
        ListenIPAddresses = { "127.0.0.1" },
    };

    private static async Task<TcpClient> ConnectAsync()
    {
        var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", ServerOption.PORT);
        return client;
    }

    [Fact]
    public async Task Start_Stop_Start_ServesClientsAfterRestart()
    {
        var options = LoopbackOptions();
        var service = CreateService(options, KeepAliveHandler("pong"));

        service.Start();
        service.Stop();
        service.Start();
        try
        {
            using var client = await ConnectAsync();
            using var stream = client.GetStream();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
            using var reader = new StreamReader(stream, new UTF8Encoding(false));

            await writer.WriteLineAsync("{\"t\":\"heartbeat\"}");

            var response = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal("pong", response);
        }
        finally
        {
            service.Stop();
        }
    }

    [Fact]
    public async Task DeviceConnectedMessage_CarriesPlainListenerPortAndProtocol()
    {
        var options = LoopbackOptions();
        var publisher = new CapturingEventPublisher();
        var service = new SocketHandlerService(
            new MacReturningHandler(),
            Options.Create(options),
            publisher,
            new StubCryptoService(),
            NullLogger<SocketHandlerService>.Instance);

        service.Start();
        try
        {
            using var client = await ConnectAsync();
            using var stream = client.GetStream();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
            await writer.WriteLineAsync("{\"t\":\"pack\"}");

            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (publisher.Messages.IsEmpty && DateTime.UtcNow < deadline)
            {
                await Task.Delay(20);
            }

            Assert.True(publisher.Messages.TryDequeue(out var message));
            Assert.Equal(ServerOption.PORT, message!.Port);
            Assert.False(message.IsTls);
        }
        finally
        {
            service.Stop();
        }
    }

    [Fact]
    public async Task IdleConnection_IsClosedByServer_AfterIdleTimeout()
    {
        var options = LoopbackOptions();
        options.IdleTimeoutSeconds = 1;
        var service = CreateService(options, KeepAliveHandler());

        service.Start();
        try
        {
            using var client = await ConnectAsync();
            using var stream = client.GetStream();

            // Never send anything; the server must close the connection on its side.
            var read = stream.ReadAsync(new byte[1], 0, 1);
            var completed = await Task.WhenAny(read, Task.Delay(TimeSpan.FromSeconds(5)));

            Assert.Same(read, completed);
            Assert.Equal(0, await read); // 0 == remote closed
        }
        finally
        {
            service.Stop();
        }
    }

    [Fact]
    public async Task ConcurrentConnections_AreCappedAtConfiguredLimit()
    {
        var options = LoopbackOptions();
        options.MaxConcurrentConnections = 2;
        options.IdleTimeoutSeconds = 30;
        var service = CreateService(options, KeepAliveHandler());

        service.Start();
        var held = new System.Collections.Generic.List<TcpClient>();
        try
        {
            // Fill both slots and keep them busy with a live request each.
            for (var i = 0; i < 2; i++)
            {
                var c = await ConnectAsync();
                held.Add(c);
                var s = c.GetStream();
                var w = new StreamWriter(s, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
                await w.WriteLineAsync("hello");
                var buf = new byte[16];
                var n = await s.ReadAsync(buf).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
                Assert.True(n > 0);
            }

            // Give the accept loop a moment to settle on the two held handlers.
            await Task.Delay(200);

            using var extra = await ConnectAsync();
            using var extraStream = extra.GetStream();
            var read = extraStream.ReadAsync(new byte[1], 0, 1);
            var completed = await Task.WhenAny(read, Task.Delay(TimeSpan.FromSeconds(5)));

            Assert.Same(read, completed);
            Assert.Equal(0, await read); // dropped immediately -> EOF
        }
        finally
        {
            foreach (var c in held) c.Dispose();
            service.Stop();
        }
    }
}
