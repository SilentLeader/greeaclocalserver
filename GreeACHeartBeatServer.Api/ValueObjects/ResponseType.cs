using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GreeACHeartBeatServer.Api.ValueObjects;

public static class ResponseType
{
    public const string LoginResponse = "loginRes";
    public const string HeartBeatOk = "hbok";
    public const string Time = "tm";
    public const string Pack = "pack";

    public const string Server = "svr";
}