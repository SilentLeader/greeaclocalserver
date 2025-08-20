namespace GreeACLocalServer.Api.Interfaces;

public interface ICryptoService
{
    string Decrypt(string pack, string? key = null);
    string Encrypt(string pack, string? key = null);
}
