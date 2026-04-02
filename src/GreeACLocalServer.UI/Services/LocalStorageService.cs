using System.Text.Json;
using GreeACLocalServer.Shared.Interfaces;
using Microsoft.JSInterop;

namespace GreeACLocalServer.UI.Services;

public class LocalStorageService(IJSRuntime _jsRuntime) : ILocalStorageService
{
    private bool _isInitialized = false;
    private IJSObjectReference? _module;

    public async Task<TResult?> Get<TResult>(string itemName)
    {
        await Init();
        var rawValue = await _module!.InvokeAsync<string>("localStorageService.getItem", itemName);
        if (string.IsNullOrEmpty(rawValue))
        {
            return default;
        }

        var type = typeof(TResult);

        return type switch
        {
            _ when type == typeof(string) => (TResult)(object)rawValue,
            _ when type == typeof(int) => (TResult)(object)int.Parse(rawValue),
            _ when type == typeof(long) => (TResult)(object)long.Parse(rawValue),
            _ when type == typeof(short) => (TResult)(object)short.Parse(rawValue),
            _ when type == typeof(uint) => (TResult)(object)uint.Parse(rawValue),
            _ when type == typeof(ulong) => (TResult)(object)ulong.Parse(rawValue),
            _ when type == typeof(float) => (TResult)(object)float.Parse(rawValue),
            _ when type == typeof(double) => (TResult)(object)double.Parse(rawValue),
            _ when type == typeof(decimal) => (TResult)(object)decimal.Parse(rawValue),
            _ when type == typeof(bool) => (TResult)(object)bool.Parse(rawValue),
            _ when type == typeof(bool?) => (TResult)(object)bool.Parse(rawValue),
            _ when type == typeof(DateTime) => (TResult)(object)DateTime.Parse(rawValue),
            _ when IsComplexType(type) => JsonSerializer.Deserialize<TResult>(rawValue),
            _ => (TResult)Convert.ChangeType(rawValue, type)
        };
    }

    public async Task Set<TValue>(string itemName, TValue value)
    {
        await Init();
        var type = typeof(TValue);
        var rawValue = string.Empty;

        if (value != null)
        {
            if (IsComplexType(type))
            {
                rawValue = JsonSerializer.Serialize(value);
            }
            else
            {
                rawValue = value.ToString();
            }
        }

        await _module!.InvokeVoidAsync("localStorageService.setItem", itemName, rawValue);
    }

    public async Task Remove(string itemName)
    {
        await Init();
        await _module!.InvokeVoidAsync("localStorageService.removeItem", itemName);
    }

    private async Task Init()
    {
        if (_isInitialized)
        {
            return;
        }

        _module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./scripts/localstorage.module.js");
        _isInitialized = true;
    }

    private static bool IsComplexType(Type type)
    {
        return !type.IsPrimitive &&
               type != typeof(string) &&
               type != typeof(DateTime);
    }
}