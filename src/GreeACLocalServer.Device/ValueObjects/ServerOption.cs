
namespace GreeACLocalServer.Device.ValueObjects;

internal static class ServerOption
{
    /// <summary>
    /// GREE default port
    /// </summary>
    public const int PORT = 5000;

    /// <summary>
    /// GREE TLS support
    /// </summary>
    public const int TLS_PORT = 1813;

    public const int ReceiveTimeout = 300000;

    /// <summary>
    /// Maximum accepted length (in characters) of a single protocol line. Longer
    /// lines are rejected instead of being buffered unbounded (memory DoS guard).
    /// </summary>
    public const int MaxLineLength = 16 * 1024;
}