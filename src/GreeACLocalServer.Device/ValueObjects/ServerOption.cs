
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

    /// <summary>
    /// Alternate plaintext port used by some GREE firmware revisions instead of <see cref="PORT"/>.
    /// </summary>
    public const int ALT_PORT = 1812;

    public const int ReceiveTimeout = 300000;

    /// <summary>
    /// Maximum accepted length (in characters) of a single protocol line. Longer
    /// lines are rejected instead of being buffered unbounded (memory DoS guard).
    /// </summary>
    public const int MaxLineLength = 16 * 1024;

    /// <summary>
    /// Maximum number of bytes buffered / saved from a single connection that does
    /// not speak the GREE JSON line protocol (e.g. a binary "fg" frame).
    /// </summary>
    public const int MaxUnknownFrameBytes = 64 * 1024;

    /// <summary>
    /// Maximum number of unknown-frame capture files written per process run, so a
    /// misbehaving device cannot fill the disk.
    /// </summary>
    public const int MaxUnknownFrameCaptureFiles = 200;

    /// <summary>
    /// Seconds to keep draining an unrecognized (non-JSON) connection while waiting
    /// for further bytes before giving up and closing it.
    /// </summary>
    public const int UnknownFrameDrainSeconds = 10;
}