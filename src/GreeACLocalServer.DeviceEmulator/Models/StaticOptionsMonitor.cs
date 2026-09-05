using Microsoft.Extensions.Options;

namespace GreeACLocalServer.DeviceEmulator.Models;

/// <summary>
/// Minimal <see cref="IOptionsMonitor{TOptions}"/> that always returns the same
/// fixed value. Lets the emulator construct the real
/// <c>GreeACLocalServer.Device.Services.CryptoService</c> directly, without
/// pulling in a full DI container just to satisfy its constructor.
/// </summary>
public sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    where T : class
{
    public T CurrentValue { get; } = value;

    public T Get(string? name) => CurrentValue;

    public IDisposable OnChange(Action<T, string> listener) => NoopDisposable.Instance;

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();
        public void Dispose()
        {
        }
    }
}
