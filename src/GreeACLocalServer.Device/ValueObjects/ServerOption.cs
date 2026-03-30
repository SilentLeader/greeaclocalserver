
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
}