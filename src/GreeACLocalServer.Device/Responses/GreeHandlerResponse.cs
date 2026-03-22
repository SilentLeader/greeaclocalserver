namespace GreeACLocalServer.Device.Responses;

public class GreeHandlerResponse
{
    public string Data { get; set; } = string.Empty;
    public bool KeepAlive { get; set; }
    public string? MacAddress { get; set; }
}