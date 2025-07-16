using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace GreeACHeartBeatServer.Api.Responses
{
    public class TimeResponse : BaseResponse
    {   
        [JsonPropertyName("time")]
        public string Time { get; set; }
    }
}