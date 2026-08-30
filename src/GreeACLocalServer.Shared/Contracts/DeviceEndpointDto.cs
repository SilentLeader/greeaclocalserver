namespace GreeACLocalServer.Shared.Contracts;

/// <summary>
/// A distinct connection endpoint a device has been observed using: the local TCP
/// port it connected to and whether that connection was TLS-encrypted. A device
/// may use several endpoints (different firmware revisions connect on different
/// ports, plaintext or TLS), so each observed pair is tracked separately.
/// </summary>
public record DeviceEndpointDto(int Port, bool IsTls, DateTime LastSeenUtc);
