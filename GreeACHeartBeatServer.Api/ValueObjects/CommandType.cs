using System;

namespace GreeACHeartBeatServer.Api.ValueObjects
{
    public static class CommandType
    {
        /// <summary>
        /// Look in Pack Object
        /// </summary>
        public const string Pack = "pack";
        
        public const string DevLogin = "devLogin";
        
        /// <summary>
        /// Heartbeat
        /// </summary>
        public const string HeartBeat = "hb";

        /// <summary>
        /// Discover
        /// </summary>
        public const string Discover = "dis";
        
        /// <summary>
        /// Get Time
        /// </summary>
        public const string Time = "tm";
    }
}
