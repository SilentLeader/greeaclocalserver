namespace GreeACLocalServer.Shared.Interfaces;

public interface ILocalStorageService
{
    Task<TResult?> Get<TResult>(string itemName);
    Task Remove(string itemName);
    Task Set<TValue>(string itemName, TValue value);
}