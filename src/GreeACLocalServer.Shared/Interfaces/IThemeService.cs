using MudBlazor;

namespace GreeACLocalServer.Shared.Interfaces;

public interface IThemeService
{
    bool IsDarkMode { get; }
    bool IsAutoMode { get; }

    MudTheme Theme { get; }

    Task Init(MudThemeProvider mudThemeProvider, Action themeChangedCallback);
    Task ToggleTheme();
}