using GreeACLocalServer.Shared.Interfaces;
using Microsoft.JSInterop;
using MudBlazor;

namespace GreeACLocalServer.UI.Services;

public class ThemeService(
    IJSRuntime _jsRuntime,
    ILocalStorageService _localStorageService,
    ILogger<ThemeService> _logger) : IThemeService
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

    public MudTheme Theme { get; } = new()
    {
        PaletteLight = new PaletteLight()
        {
            Primary = "#0D9488",
            Success = "#0D9488",
            Background = "#EEF7F7",
            Surface = "#FFFFFF",
            AppbarBackground = "#FFFFFF",
            AppbarText = "#0F2733",
            DrawerBackground = "#FFFFFF",
            LinesDefault = "rgba(13,148,136,0.28)",
        },
        PaletteDark = new PaletteDark()
        {
            Primary = "#2DD4BF",
            Success = "#34E0B0",
            Background = "#0A1420",
            BackgroundGray = "#0D1B2A",
            Surface = "#101F2E",
            AppbarBackground = "#0D1B2A",
            AppbarText = "#E7F6F4",
            DrawerBackground = "#0A1420",
            TextPrimary = "#E7F6F4",
            TextSecondary = "#7FA3AB",
            LinesDefault = "rgba(45,212,191,0.28)",
            Divider = "rgba(255,255,255,0.08)",
        },
        LayoutProperties = new LayoutProperties()
        {
            DefaultBorderRadius = "16px"
        }
    };

    public async Task Init(MudThemeProvider mudThemeProvider, Action themeChangedCallback)
    {
        if (_isInitialized)
        {
            return;
        }

        _module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./scripts/themeservice.module.js");
        _isInitialized = true;

        _mudThemeProvider = mudThemeProvider;
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
            }

            // Always watch for system changes: ToggleTheme can switch back to auto
            // mode during the session. OnSystemThemeChanged is a no-op unless we are
            // in auto mode, so this has no effect while a preference is set.
            await _mudThemeProvider.WatchSystemDarkModeAsync(OnSystemThemeChanged);

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

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The circuit is already gone; nothing to clean up on the JS side.
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to dispose the theme service JS module");
            }
        }
    }
}