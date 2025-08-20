# GitHub Copilot Instructions

## Project Overview
GREE AC Local Server is a modern .NET 9 application that replaces GREE air conditioner's cloud connectivity with a local server. It implements the complete GREE protocol (discover, devLogin, pack, time, heartbeat) with AES encryption and provides a Blazor WebAssembly UI with real-time device monitoring.

## Architecture & Key Components

### Dual-Mode Hosting Pattern
The application supports two hosting modes controlled by `Server.EnableUI`:
- **Web Application Mode**: Full Blazor UI with SignalR real-time updates
- **Headless Mode**: TCP server only using Generic Host for resource-constrained deployments

Both modes share common services via `ConfigureCommonServices()` in `Program.cs`.

### Core Services Architecture
- **MessageHandlerService**: Handles GREE protocol commands (discover, pack, time, heartbeat)
- **CryptoService**: AES encryption/decryption for GREE data packets
- **DeviceManagerService**: Device state management with SignalR broadcasting (UI mode)
- **HeadlessDeviceManagerService**: Lightweight device tracking (headless mode)
- **SocketHandlerBackgroundService**: TCP server listening on port 5000
- **DnsResolverService**: Resolves device IPs to FQDNs for friendly names

### GREE Protocol Implementation
All GREE communication flows through `MessageHandlerService.GetResponse()`:
1. JSON deserialization to `DefaultRequest`
2. Command routing via switch expression on `CommandType`
3. Response encryption using `CryptoService`
4. Device registration via `IDeviceManagerService`

Example command handlers: `HandleDiscover()`, `HandlePack()`, `HandleTime()`, `HandleHeartbeat()`

### WiFi Configuration Feature
The UI includes a dedicated page (`WifiConfig.razor`) for configuring AC WiFi:
- **Command Generation**: Creates netcat commands based on user inputs (SSID, password, OS)
- **Cross-platform Support**: Generates appropriate commands for Linux, macOS, and Windows
- **Security Features**: Password visibility toggle, JSON string escaping
- **User Experience**: Clipboard integration, form validation, step-by-step instructions

## Development Workflows

### Building & Running
```bash
# Build solution
dotnet build src/GreeACLocalServer.sln

# Run with UI (default)
dotnet run --project src/GreeACLocalServer.Api

# Run headless mode (set Server.EnableUI=false in appsettings)
```

### Docker Development
```bash
# Build image
./docker-build.sh

# Run with docker-compose (recommended)
./docker-run.sh
```

### Testing
```bash
# Run all tests
dotnet test src/GreeACLocalServer.sln

# Run specific test class
dotnet test --filter "MessageHandlerServiceTests"
```

## Code Patterns & Conventions

### Configuration Pattern
All services use strongly-typed options classes bound from `appsettings.json`:
```csharp
services.Configure<ServerOptions>(configuration.GetSection("Server"));
services.Configure<DeviceManagerOptions>(configuration.GetSection("DeviceManager"));
```

### Service Registration Pattern
Core services are singletons, request-scoped services use `AddScoped`:
```csharp
services.AddSingleton<ICryptoService, CryptoService>();
services.AddScoped<IDeviceConfigService, DeviceConfigService>();
```

### Error Handling Pattern
Use structured logging with Serilog throughout. Log warnings for malformed requests, debug for protocol messages:
```csharp
_logger.LogWarning(e, "Invalid message format. Input bytes: {InputBytes}", inputBytes);
_logger.LogDebug("Request: {Input}", input.Replace("\n", string.Empty));
```

### JSON Serialization Pattern
Use consistent naming policy for GREE protocol compatibility:
```csharp
var options = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.KebabCaseLower,
    WriteIndented = false
};
```

## Testing Patterns

### Service Testing
Use builder pattern for service creation with mocked dependencies:
```csharp
private MessageHandlerService CreateService(
    ICryptoService? cryptoService = null,
    ServerOptions? serverOptions = null)
{
    serverOptions ??= new ServerOptions { /* defaults */ };
    var options = Mock.Of<IOptions<ServerOptions>>(o => o.Value == serverOptions);
    // ... return service
}
```

### Integration Testing
Test files follow naming convention: `{ServiceName}Tests.cs` in `GreeACLocalServer.Api.Tests`

## Project Structure Insights

### Multi-Project Solution
- **Api**: Main application with TCP server, controllers, and services  
- **UI**: Blazor WebAssembly components and pages with MudBlazor Material Design
- **Shared**: DTOs, contracts, and value objects shared between projects
- **Api.Tests**: Unit tests using xUnit and Moq

### Blazor UI Architecture
The UI project uses **Blazor WebAssembly** with **MudBlazor** for Material Design components:

#### MudBlazor Component Patterns
- **Theme Provider**: Automatic dark/light mode detection with system preference
- **Layout Structure**: `MainLayout.razor` with MudAppBar, MudDrawer, and MudMainContent
- **Component Usage**: MudCard, MudGrid, MudText, MudIcon for device dashboard
- **Service Integration**: MudDialogProvider, MudSnackbarProvider for user interactions
- **Form Components**: MudTextField, MudSelect, MudButton for WiFi configuration
- **Interactive Elements**: Password visibility toggles, clipboard operations via JSRuntime

#### Real-time UI Updates
SignalR client integration for live device status:
```csharp
// HttpDeviceManagerService calls API endpoints
var devices = await _http.GetFromJsonAsync<List<DeviceDto>>("api/devices");

// SignalR hub connection for real-time updates
await hubConnection.StartAsync();
```

#### UI Service Pattern
HTTP-based services proxy API calls from WebAssembly client:
- `HttpDeviceManagerService`: Device state management
- `HttpDeviceConfigService`: Device configuration operations  
- `HttpConfigService`: Server configuration access

Example service implementation pattern:
```csharp
public class HttpDeviceManagerService(HttpClient httpClient) : IDeviceManagerService
{
    public async Task<IEnumerable<DeviceDto>> GetAllDeviceStatesAsync()
    {
        return await _http.GetFromJsonAsync<List<DeviceDto>>("api/devices") ?? [];
    }
}
```

### Configuration Hierarchy
1. `appsettings.json` (base config)
2. `appsettings.dev.json` (development overrides)  
3. `appsettings.Production.json` (production overrides)
4. Environment variables (Docker/deployment)

## Critical Integration Points

### SignalR Real-time Updates
Device state changes broadcast to UI via `DeviceHub`:
```csharp
await _hubContext.Clients.All.SendAsync(DeviceHubMethods.DeviceConnected, deviceState);
```

### Cross-Platform Service Hosting
Automatic OS detection for proper service integration:
```csharp
if (OperatingSystem.IsLinux()) hostBuilder.UseSystemd();
if (OperatingSystem.IsWindows()) hostBuilder.UseWindowsService();
```

### Network Configuration Requirements
- **Port 5000**: GREE device communication (TCP, required)
- **Port 5100**: Web UI (HTTP, optional based on EnableUI)
- **DNS Setup**: `Server.DomainName` must resolve to `Server.ExternalIp`

When making changes that affect device communication or protocol handling, always test both UI and headless modes. Use the existing test patterns for new services and maintain the consistent configuration binding approach.
