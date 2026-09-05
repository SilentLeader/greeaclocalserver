using GreeACLocalServer.Device.Models;
using GreeACLocalServer.Device.Services;
using GreeACLocalServer.DeviceEmulator.Extensions;
using GreeACLocalServer.DeviceEmulator.Helpers;
using GreeACLocalServer.DeviceEmulator.Models;
using GreeACLocalServer.DeviceEmulator.Services;
using Microsoft.Extensions.Logging.Abstractions;

if (args.HasFlag("--help") || args.HasFlag("-h"))
{
    CommandLineHelper.PrintUsage();
    return;
}

var useTls = args.HasFlag("--tls");
var host = args.GetOption("--host", "localhost");
var port = int.Parse(args.GetOption("--port", useTls ? "1813" : "5000"));
var allowLegacyTls = args.HasFlag("--legacy-tls");
var mac = args.GetOption("--mac", "aabbccddeeff");
var name = args.GetOption("--name", "Emulated AC");
var hid = args.GetOption("--hid", "100000000000+U-TESTV1.00.bin");
var roomTemp = int.Parse(args.GetOption("--room-temp", "25"));
var temSenSupported = !args.HasFlag("--no-temp-sensor");
var cryptoKey = args.GetOption("--key", "a3K8Bx%2r8Y7#xDh");
var heartbeatSeconds = int.Parse(args.GetOption("--heartbeat-seconds", "60"));

// Derived from the MAC (not random) so restarting the emulator with the same
// --mac keeps working against the server's cached bind key (30 min TTL) instead
// of forcing a decrypt-failure/re-bind cycle on every restart.
var deviceCryptoKey = args.GetOption("--device-key", mac.PadRight(16, '0')[..16]);
var state = new EmulatedDeviceState
{
    Mac = mac,
    CryptoKey = deviceCryptoKey,
    Name = name,
    Hid = hid,
    TemSen = roomTemp + 40,
    TemSenSupported = temSenSupported,
};

var crypto = new CryptoService(
    new StaticOptionsMonitor<EncryptionOptions>(new EncryptionOptions { DefaultCryptoKey = cryptoKey }),
    NullLogger<CryptoService>.Instance);

var inbound = new InboundClient(state, crypto, new InboundClientOptions
{
    Host = host,
    Port = port,
    UseTls = useTls,
    AllowLegacyTls = allowLegacyTls,
    HeartbeatInterval = TimeSpan.FromSeconds(heartbeatSeconds),
});
var outbound = new OutboundResponder(state, crypto);

using var cts = new CancellationTokenSource();

ConsoleLog.Info($"GREE AC emulator starting: mac={state.Mac} target={host}:{port} tls={useTls} heartbeat={heartbeatSeconds}s");
ConsoleLog.Info("Type 'help' for the list of commands.");

var inboundTask = inbound.RunAsync(cts.Token);
var outboundTask = outbound.RunAsync(cts.Token);

var repl = new ConsoleRepl(inbound, state);
await repl.RunAsync(cts).ConfigureAwait(false);

cts.Cancel();
await Task.WhenAll(SwallowCancellation(inboundTask), SwallowCancellation(outboundTask)).ConfigureAwait(false);

return;

static async Task SwallowCancellation(Task task)
{
    try
    {
        await task.ConfigureAwait(false);
    }
    catch (OperationCanceledException)
    {
    }
}
