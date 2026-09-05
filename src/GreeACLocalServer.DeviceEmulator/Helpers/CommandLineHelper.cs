using GreeACLocalServer.DeviceEmulator.Models;
using GreeACLocalServer.DeviceEmulator.Services;

namespace GreeACLocalServer.DeviceEmulator.Helpers;

internal static class CommandLineHelper
{
    public static void PrintHelp()
    {
        Console.WriteLine("""
                          connect / disconnect          - open/close the inbound connection (simulates power on/off)
                          pow on|off                    - set reported power state
                          mode auto|cool|dry|fan|heat   - set reported mode
                          settemp <n>                   - set reported target temperature
                          tempsen <celsius>|off         - set reported room temperature, or disable the sensor
                          tempunit c|f                  - set reported temperature unit
                          name <text>                   - set reported device name
                          host <text>                   - set reported remote host
                          status                        - show current state and connection status
                          help                          - show this help
                          quit / exit                   - stop the emulator
                          """);
    }

    public static void PrintUsage()
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
                            --room-temp <celsius>     Initial room temperature reading (default: 25)
                            --no-temp-sensor          Emulate a unit without a room-temperature sensor
                            --key <key>               Default crypto key, must match the server's DefaultCryptoKey
                            --device-key <key>        Per-device bind key, 16 chars (default: derived from --mac)
                            --heartbeat-seconds <n>   Heartbeat interval in seconds (default: 60)
                          """);
    }

    public static void PrintStatus(InboundClient inbound, EmulatedDeviceState state)
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
                           temSen      : {(state.TemSenSupported ? $"{state.TemSen - 40}°C" : "unsupported")}
                           """);
    }
}
