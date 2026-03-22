namespace GreeACLocalServer.Device.Interfaces;

internal interface ICryptoService
{
    string Decrypt(string pack, string? key = null);
    string Encrypt(string pack, string? key = null);
}
