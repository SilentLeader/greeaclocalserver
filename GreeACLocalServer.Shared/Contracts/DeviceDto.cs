namespace GreeACLocalServer.Shared.Contracts;

public record DeviceDto(string MacAddress, string IpAddress, string DNSName, DateTime LastConnectionTimeUtc);
