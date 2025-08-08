namespace GreeACLocalServer.Shared.Contracts;

public record DeviceDto(string MacAddress, string IpAddress, DateTime LastConnectionTimeUtc);
