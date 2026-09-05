namespace GreeACLocalServer.DeviceEmulator.Services;

/// <summary>Tiny timestamped console logger for this dev tool - no logging framework needed.</summary>
public static class ConsoleLog
{
    private static readonly object Gate = new();

    public static void Info(string message) => Write(message, ConsoleColor.Gray);

    public static void Warn(string message) => Write(message, ConsoleColor.Yellow);

    public static void Error(string message) => Write(message, ConsoleColor.Red);

    private static void Write(string message, ConsoleColor color)
    {
        lock (Gate)
        {
            var previous = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
            Console.ForegroundColor = previous;
        }
    }
}
