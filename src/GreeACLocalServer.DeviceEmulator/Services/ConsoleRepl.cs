using GreeACLocalServer.DeviceEmulator.Extensions;
using GreeACLocalServer.DeviceEmulator.Helpers;
using GreeACLocalServer.DeviceEmulator.Models;

namespace GreeACLocalServer.DeviceEmulator.Services;

/// <summary>
/// Interactive stdin command loop that drives the emulator live: connect/disconnect
/// the <see cref="InboundClient"/> and edit the shared <see cref="EmulatedDeviceState"/>
/// (pow, mode, set temperature, name, host, ...) without restarting the process.
/// </summary>
public sealed class ConsoleRepl(InboundClient inbound, EmulatedDeviceState state)
{
    public async Task RunAsync(CancellationTokenSource cts)
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
                    if (arg.TryParseOnOff(out var pow))
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
                    if (arg.TryParseMode(out var mode))
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

                case "tempsen":
                    if (arg.Equals("off", StringComparison.OrdinalIgnoreCase))
                    {
                        state.TemSenSupported = false;
                        ConsoleLog.Info("TemSen -> unsupported (no room-temperature sensor)");
                    }
                    else if (int.TryParse(arg, out var roomTemp))
                    {
                        state.TemSen = roomTemp + 40;
                        state.TemSenSupported = true;
                        ConsoleLog.Info($"TemSen -> {roomTemp}°C");
                    }
                    else
                    {
                        ConsoleLog.Warn("Usage: tempsen <celsius>|off");
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
                    CommandLineHelper.PrintStatus(inbound, state);
                    break;

                case "help":
                    CommandLineHelper.PrintHelp();
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
}
