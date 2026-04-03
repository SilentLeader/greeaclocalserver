using GreeACLocalServer.Shared.Interfaces;
using Microsoft.JSInterop;
using MudBlazor;

namespace GreeACLocalServer.UI.Services;

public class ThemeService(IJSRuntime _jsRuntime, ILocalStorageService _localStorageService, ILogger<ThemeService> _logger) : IThemeService
{
    private bool _isInitialized = false;
    private IJSObjectReference _module = null!;

    private MudThemeProvider _mudThemeProvider = null!;
    private bool _isDarkMode;
    private bool _isAutoMode = true;

    private const string DarkModeSettingsName = "IsDarkTheme";

    private Action? _themeChangedCallback;

    public bool IsDarkMode => _isDarkMode;
    public bool IsAutoMode => _isAutoMode;

    public async Task Init(object mudThemeProvider, Action themeChangedCallback)
    {
        if (_isInitialized)
        {
            return;
        }

        _module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./scripts/themeservice.module.js");
        _isInitialized = true;

        _mudThemeProvider = (MudThemeProvider)mudThemeProvider;
        _themeChangedCallback = themeChangedCallback;
        await InitTheme();
        await _module.InvokeVoidAsync("themeService.removeLoadingStyle");
    }


    public async Task ToggleTheme()
    {
        var isCurrentModeDark = await _mudThemeProvider.GetSystemDarkModeAsync();

        if (IsAutoMode)
        {
            _isAutoMode = false;
            _isDarkMode = !isCurrentModeDark;
            await _localStorageService.Set(DarkModeSettingsName, _isDarkMode);
        }
        else
        {
            if (_isDarkMode == isCurrentModeDark)
            {
                _isAutoMode = true;
                await _localStorageService.Remove(DarkModeSettingsName);
            }
            else
            {
                _isDarkMode = !_isDarkMode;
                await _localStorageService.Set(DarkModeSettingsName, _isDarkMode);
            }
        }
    }


    private async Task InitTheme()
    {
        try
        {
            var isSavedDarkTheme = await _localStorageService.Get<bool?>(DarkModeSettingsName);

            if (isSavedDarkTheme != null)
            {
                _isAutoMode = false;
                _isDarkMode = isSavedDarkTheme.Value;
            }
            else
            {
                // Get system preference
                _isDarkMode = await _mudThemeProvider.GetSystemDarkModeAsync();

                // Watch for system changes
                await _mudThemeProvider.WatchSystemDarkModeAsync(OnSystemThemeChanged);
            }
            _themeChangedCallback?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dark mode detection failed");
            // Fallback to light mode if detection fails
            _isDarkMode = false;
        }
    }


    private async Task OnSystemThemeChanged(bool isDark)
    {
        if (_isAutoMode)
        {
            _isDarkMode = isDark;
            _themeChangedCallback?.Invoke();
        }
    }
}