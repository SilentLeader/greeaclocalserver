namespace GreeACLocalServer.Shared.Interfaces;

public interface IThemeService
{
    bool IsDarkMode { get; }
    bool IsAutoMode { get; }

    Task Init(object mudThemeProvider, Action themeChangedCallback);
    Task ToggleTheme();
}