using System;
using GreeACLocalServer.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GreeACLocalServer.UI.Layout;

public partial class MainLayout : LayoutComponentBase
{
    private bool _drawerOpen = true;
    
    [Inject] private IThemeService ThemeService { get; set; } = default!;
    
    private MudThemeProvider? _mudThemeProvider;
    
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && _mudThemeProvider != null)
        {
            await ThemeService.Init(_mudThemeProvider, StateHasChanged);
        }
    }

    private void ToggleDrawer()
    {
        _drawerOpen = !_drawerOpen;
    }

    private async Task ToggleTheme()
    {
        await ThemeService.ToggleTheme();
    }
}
