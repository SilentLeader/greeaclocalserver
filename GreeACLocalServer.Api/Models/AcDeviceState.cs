namespace GreeACLocalServer.Api.Models
{
    public class AcDeviceState
    {
        public string MacAddress { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public DateTime LastConnectionTime { get; set; }
    }
}
