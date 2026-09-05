using GreeACLocalServer.Device.Interfaces;
using GreeACLocalServer.Device.Models;
using GreeACLocalServer.Device.Services;
using GreeACLocalServer.DeviceEmulator;
using Microsoft.Extensions.Logging.Abstractions;

if (HasFlag(args, "--help") || HasFlag(args, "-h"))
{
    PrintUsage();
    return;
}

var useTls = HasFlag(args, "--tls");
var host = GetOption(args, "--host", "localhost");
var port = int.Parse(GetOption(args, "--port", useTls ? "1813" : "5000"));
var allowLegacyTls = HasFlag(args, "--legacy-tls");
var mac = GetOption(args, "--mac", "aabbccddeeff");
var name = GetOption(args, "--name", "Emulated AC");
var hid = GetOption(args, "--hid", "100000000000+U-TESTV1.00.bin");
var cryptoKey = GetOption(args, "--key", "a3K8Bx%2r8Y7#xDh");
var heartbeatSeconds = int.Parse(GetOption(args, "--heartbeat-seconds", "60"));

// Derived from the MAC (not random) so restarting the emulator with the same
// --mac keeps working against the server's cached bind key (30 min TTL) instead
// of forcing a decrypt-failure/re-bind cycle on every restart.
var deviceCryptoKey = GetOption(args, "--device-key", mac.PadRight(16, '0')[..16]);
var state = new EmulatedDeviceState
{
    Mac = mac,
    CryptoKey = deviceCryptoKey,
    Name = name,
    Hid = hid,
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

await RunReplAsync(inbound, state, cts).ConfigureAwait(false);

cts.Cancel();
await Task.WhenAll(SwallowCancellation(inboundTask), SwallowCancellation(outboundTask)).ConfigureAwait(false);

return;

static async Task RunReplAsync(InboundClient inbound, EmulatedDeviceState state, CancellationTokenSource cts)
{
    while (!cts.IsCancellationRequested)
    {
        var line = await Console.In.ReadLineAsync(cts.Token).ConfigureAwait(false);
        if (line is null)
        {
            break;
        }

        var parts = line.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            continue;
        }

        var command = parts[0].ToLowerInvariant();
        var arg = parts.Length > 1 ? parts[1].Trim() : string.Empty;

        switch (command)
        {
            case "connect":
                inbound.RequestConnect();
                break;

            case "disconnect":
                inbound.RequestDisconnect();
                break;

            case "pow":
                if (TryParseOnOff(arg, out var pow))
                {
                    state.Pow = pow;
                    ConsoleLog.Info($"Pow -> {(pow ? "on" : "off")}");
                }
                else
                {
                    ConsoleLog.Warn("Usage: pow on|off");
                }

                break;

            case "mode":
                if (TryParseMode(arg, out var mode))
                {
                    state.Mode = mode;
                    ConsoleLog.Info($"Mode -> {arg} ({mode})");
                }
                else
                {
                    ConsoleLog.Warn("Usage: mode auto|cool|dry|fan|heat");
                }

                break;

            case "settemp":
                if (int.TryParse(arg, out var setTemp))
                {
                    state.SetTem = setTemp;
                    ConsoleLog.Info($"SetTem -> {setTemp}");
                }
                else
                {
                    ConsoleLog.Warn("Usage: settemp <number>");
                }

                break;

            case "tempunit":
                if (arg.Equals("c", StringComparison.OrdinalIgnoreCase))
                {
                    state.TemUn = 0;
                    ConsoleLog.Info("TemUn -> Celsius");
                }
                else if (arg.Equals("f", StringComparison.OrdinalIgnoreCase))
                {
                    state.TemUn = 1;
                    ConsoleLog.Info("TemUn -> Fahrenheit");
                }
                else
                {
                    ConsoleLog.Warn("Usage: tempunit c|f");
                }

                break;

            case "name":
                state.Name = arg;
                ConsoleLog.Info($"Name -> '{state.Name}'");
                break;

            case "host":
                state.Host = arg;
                ConsoleLog.Info($"Host -> '{state.Host}'");
                break;

            case "status":
                PrintStatus(inbound, state);
                break;

            case "help":
                PrintHelp();
                break;

            case "quit":
            case "exit":
                return;

            default:
                ConsoleLog.Warn($"Unknown command '{command}'. Type 'help' for the list of commands.");
                break;
        }
    }
}

static bool TryParseOnOff(string arg, out bool value)
{
    switch (arg.ToLowerInvariant())
    {
        case "on":
            value = true;
            return true;
        case "off":
            value = false;
            return true;
        default:
            value = false;
            return false;
    }
}

static bool TryParseMode(string arg, out int mode)
{
    switch (arg.ToLowerInvariant())
    {
        case "auto": mode = 0; return true;
        case "cool": mode = 1; return true;
        case "dry": mode = 2; return true;
        case "fan": mode = 3; return true;
        case "heat": mode = 4; return true;
        default: mode = 0; return false;
    }
}

static void PrintStatus(InboundClient inbound, EmulatedDeviceState state)
{
    Console.WriteLine($"""
        mac         : {state.Mac}
        name        : {state.Name}
        host        : {state.Host}
        hid         : {state.Hid}
        connected   : {inbound.IsConnected}
        pow         : {(state.Pow ? "on" : "off")}
        mode        : {state.Mode}
        setTem      : {state.SetTem}
        temUn       : {(state.TemUn == 0 ? "celsius" : "fahrenheit")}
        """);
}

static void PrintHelp()
{
    Console.WriteLine("""
        connect / disconnect          - open/close the inbound connection (simulates power on/off)
        pow on|off                    - set reported power state
        mode auto|cool|dry|fan|heat   - set reported mode
        settemp <n>                   - set reported target temperature
        tempunit c|f                  - set reported temperature unit
        name <text>                   - set reported device name
        host <text>                   - set reported remote host
        status                        - show current state and connection status
        help                          - show this help
        quit / exit                   - stop the emulator
        """);
}

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

static void PrintUsage()
{
    Console.WriteLine("""
        GreeACLocalServer.DeviceEmulator - emulates a GREE AC unit for local dev/testing.

        Usage: dotnet run --project src/GreeACLocalServer.DeviceEmulator -- [options]

          --host <host>            Server host to connect to (default: localhost)
          --port <port>            Server port (default: 5000, or 1813 with --tls)
          --tls                    Use TLS for the inbound connection
          --legacy-tls             Also allow SSL3/TLS1.0/1.1 (matches AllowLegacyTlsProtocols=true)
          --mac <mac>               Emulated device MAC, 12 hex chars (default: aabbccddeeff)
          --name <name>             Emulated device name (default: "Emulated AC")
          --hid <hid>               Emulated firmware identifier (default: a fake but parseable one)
          --key <key>               Default crypto key, must match the server's DefaultCryptoKey
          --device-key <key>        Per-device bind key, 16 chars (default: derived from --mac)
          --heartbeat-seconds <n>   Heartbeat interval in seconds (default: 60)
        """);
}

static string GetOption(string[] args, string flag, string defaultValue)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
        {
            return args[i + 1];
        }
    }

    return defaultValue;
}

static bool HasFlag(string[] args, string flag)
    => args.Any(a => string.Equals(a, flag, StringComparison.OrdinalIgnoreCase));
