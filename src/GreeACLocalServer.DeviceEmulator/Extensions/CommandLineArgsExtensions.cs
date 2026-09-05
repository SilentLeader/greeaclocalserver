namespace GreeACLocalServer.DeviceEmulator.Extensions;

internal static class CommandLineArgsExtensions
{
    extension(string[] args)
    {
        public string GetOption(string flag, string defaultValue)
        {
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return defaultValue;
        }

        public bool HasFlag(string flag)
            => args.Any(a => string.Equals(a, flag, StringComparison.OrdinalIgnoreCase));
    }

    extension(string arg)
    {
        public bool TryParseOnOff(out bool value)
        {
            switch (arg.ToLowerInvariant())
            {
                case "on":
                    value = true;
                    return true;
                case "off":
                    value = false;
                    return true;
                default:
                    value = false;
                    return false;
            }
        }

        public bool TryParseMode(out int mode)
        {
            switch (arg.ToLowerInvariant())
            {
                case "auto": mode = 0; return true;
                case "cool": mode = 1; return true;
                case "dry": mode = 2; return true;
                case "fan": mode = 3; return true;
                case "heat": mode = 4; return true;
                default: mode = 0; return false;
            }
        }
    }
}
