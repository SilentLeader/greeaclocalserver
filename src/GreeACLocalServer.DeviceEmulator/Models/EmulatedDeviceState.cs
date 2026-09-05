namespace GreeACLocalServer.DeviceEmulator.Models;

/// <summary>
/// Mutable in-memory state of the emulated air conditioner, shared between the
/// inbound TCP/TLS client and the outbound UDP responder, and driven live by
/// the interactive console commands.
/// </summary>
public sealed class EmulatedDeviceState
{
    private readonly object _gate = new();

    public required string Mac { get; init; }

    public required string CryptoKey { get; init; }

    private string _name = "Emulated AC";
    private string _host = string.Empty;
    private string _hid = "100000000000+U-TESTV1.00.bin";
    private bool _pow = true;
    private int _mode = 1; // 0=auto,1=cool,2=dry,3=fan,4=heat
    private int _setTem = 24;
    private int _temUn; // 0=Celsius, 1=Fahrenheit
    private int _temSen; // raw sensor reading (+40 offset); 0 = "no sensor"

    public string Name { get => Read(() => _name); set => Write(() => _name = value); }

    public string Host { get => Read(() => _host); set => Write(() => _host = value); }

    public string Hid { get => Read(() => _hid); set => Write(() => _hid = value); }

    public bool Pow { get => Read(() => _pow); set => Write(() => _pow = value); }

    public int Mode { get => Read(() => _mode); set => Write(() => _mode = value); }

    public int SetTem { get => Read(() => _setTem); set => Write(() => _setTem = value); }

    public int TemUn { get => Read(() => _temUn); set => Write(() => _temUn = value); }

    public int TemSen { get => Read(() => _temSen); set => Write(() => _temSen = value); }

    /// <summary>
    /// The value for a <c>status</c> query column, boxed as the type a real
    /// device sends: a JSON string for text columns, a JSON number for the
    /// operating-state ones (mirrors <c>QueryResponse</c>'s doc comment).
    /// </summary>
    public object ValueForColumn(string column) => column switch
    {
        "host" => Host,
        "name" => Name,
        "hid" => Hid,
        "Pow" => Pow ? 1 : 0,
        "Mod" => Mode,
        "SetTem" => SetTem,
        "TemUn" => TemUn,
        "TemSen" => TemSen,
        _ => string.Empty,
    };

    private T Read<T>(Func<T> read)
    {
        lock (_gate)
        {
            return read();
        }
    }

    private void Write(Action write)
    {
        lock (_gate)
        {
            write();
        }
    }
}
