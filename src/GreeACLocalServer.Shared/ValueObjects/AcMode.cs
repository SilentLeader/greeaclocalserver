namespace GreeACLocalServer.Shared.ValueObjects;

/// <summary>
/// GREE operating mode (status column <c>Mod</c>). <see cref="Unknown"/> covers a
/// value the device reported that is outside the documented range.
/// </summary>
public enum AcMode
{
    Unknown = -1,
    Auto = 0,
    Cool = 1,
    Dry = 2,
    Fan = 3,
    Heat = 4
}
