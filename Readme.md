# GREE AC Local Server

This project provides a **modern, feature-rich local replacement server for GREE air conditioners** that normally require internet connectivity to communicate with GREE's cloud servers. The solution allows GREE AC units to function completely offline by implementing a local server that mimics the original GREE server functionality.

**Based on the excellent foundation of [GreeAC-DummyServer](https://github.com/emtek-at/GreeAC-DummyServer)**, this project has been completely rewritten and modernized with .NET 10, featuring a comprehensive web UI, real-time device monitoring, advanced management capabilities, and TLS/HTTPS support.[^ai]

[^ai]: This project contains AI-assisted contributions — parts of the code, tests, and documentation were written or refined with the help of AI coding assistants, and reviewed before being merged.

## 🌟 **Features**

### **Core GREE Protocol Support**
- **Complete GREE AC Protocol Implementation** - Handles all essential commands:
  - Device discovery (`discover`)
  - Device authentication (`devLogin`) 
  - Data pack/unpack operations (`pack`)
  - Time synchronization (`time`)
  - Heartbeat monitoring (`heartbeat`)
- **AES Encryption/Decryption** using GREE's crypto format
- **TCP Server** listening on port 5000 (required by GREE devices)

### **Modern Web Interface**
- **Blazor Interactive WebAssembly** hybrid application
- **MudBlazor Material Design** components with automatic dark/light theme detection
- **Real-time Device Monitoring** via SignalR
- **Device Dashboard** showing MAC addresses, IP addresses, DNS names, and connection status
- **Built-in Device Configuration Tool** for managing AC settings without external tools
- **WiFi Configuration Tool** with cross-platform command generation (Linux, macOS, Windows)
- **Management Control** - Device configuration features can be disabled for security
- **Responsive Design** optimized for desktop and mobile

### **Advanced Device Management**
- **Automatic Device Discovery** when ACs connect to the network
- **Manual Device Removal** with confirmation dialogs for unwanted devices
- **DNS Resolution** of device IP addresses to FQDNs (with fallback to IP)
- **Connection Health Monitoring** with configurable device timeouts
- **Real-time Status Updates** via SignalR broadcasting
- **Device Control Interface** with remove buttons and management actions

### **Developer & Operations Features**
- **Structured Logging** with Serilog (per-connection correlation IDs)
- **Background Services** for TCP server hosting, with idle-timeout and a concurrent-connection cap
- **Fail-fast configuration** - required settings are validated at startup
- **Cross-platform Support** (Windows/Linux)
- **Docker Ready** with proper networking
- **System Service Integration** (systemd/Windows Service)
- **Unit + integration tests** covering the protocol core (crypto, message handler, socket/UDP handling)

## 🚀 **Installation**

### **Prerequisites**
- **.NET 10 Runtime** (for running) or SDK (for building)
- **DNS Server Configuration** - Add an entry pointing to your server's IP address
- **Network Access** - Server must be accessible on port 5000, 5100 (HTTP), and optionally 1813 (TLS)

### **Option 1: Docker (Recommended)**

#### **Quick Start with Docker Compose**
```bash
# Clone the repository
git clone https://github.com/SilentLeader/greeaclocalserver.git
cd greeaclocalserver

# Edit docker-compose.yml to set your domain and IP
# Update GreeServer__ServerOptions__DomainName and GreeServer__ServerOptions__ExternalIp values

# Start the server
./docker-run.sh
# or manually: docker-compose up -d
```

#### **Using Docker Run Command**
```bash
docker run -d \
  --restart=always \
  --name gree-ac-server \
  -e GreeServer__ServerOptions__DomainName=gree.example.com \
  -e GreeServer__ServerOptions__ExternalIp=192.168.1.100 \
  -e GreeServer__EnableUI=true \
  -p 5000:5000 \
  -p 1813:1813 \
  -p 5100:5100 \
  gree-ac-local-server:latest
```

#### **TLS Configuration (device listener on port 1813)**
`GreeServer__ServerOptions__TLSEnabled=true` starts an additional TLS listener on port **1813**
for AC firmware that talks TLS instead of plain TCP. Provide a certificate, or let the server
generate a self-signed one:

```bash
docker run -d \
  --restart=always \
  --name gree-ac-server-tls \
  -e GreeServer__ServerOptions__DomainName=gree.example.com \
  -e GreeServer__ServerOptions__ExternalIp=192.168.1.100 \
  -e GreeServer__EnableUI=true \
  -e GreeServer__ServerOptions__TLSEnabled=true \
  -e GreeServer__EncryptionOptions__TLSCertificatePath=/app/certs/server.pfx \
  -e GreeServer__EncryptionOptions__TLSCertificatePassword=your-cert-password \
  -p 5000:5000 \
  -p 1813:1813 \
  -p 5100:5100 \
  -v /path/to/certs:/app/certs:ro \
  gree-ac-local-server:latest
```

- The certificate file must be **PKCS#12** (`.pfx` / `.p12`) so it carries the private key.
  Other extensions are loaded as a public-key-only certificate and the TLS listener will not start.
- With `TLSCertificateAutoCreate=true` (default) and no `TLSCertificatePath`, a self-signed cert
  (with SAN for `DomainName`) is generated in memory on each start. If a path is also given, the
  generated cert is written there as a `.pfx`.
- `AllowLegacyTlsProtocols=false` restricts this listener to TLS 1.2 / 1.3 (default `true` keeps
  SSL3–TLS1.1 for old firmware).

This is independent of the web UI: to serve the UI itself over HTTPS, configure Kestrel
(`ASPNETCORE_URLS=...;https://+:5443`) with its own certificate.

#### **Building Docker Image Locally**
```bash
# Build the image
./docker-build.sh
# or manually: docker build -t gree-ac-local-server:latest .

# Run with docker-compose
docker-compose up -d
```

#### **Headless Mode (No Web UI)**
For deployments without a web interface, set `GreeServer__EnableUI=false`:
```bash
docker run -d \
  --restart=always \
  --name gree-ac-server \
  -e GreeServer__ServerOptions__DomainName=gree.example.com \
  -e GreeServer__ServerOptions__ExternalIp=192.168.1.100 \
  -e GreeServer__EnableUI=false \
  -p 5000:5000 \
  -p 1813:1813 \
  gree-ac-local-server:latest
```

In headless mode, only the TCP server runs on port 5000 for GREE device communication. The web UI is disabled to reduce resource usage and attack surface.

#### **Development Mode**
For development with hot reload, use `docker-compose.dev.yml`:
```bash
docker-compose -f docker-compose.dev.yml up -d
```

### **Option 2: Bare Metal**

1. **Download the latest release** from the releases page
2. **Extract** to your desired location
3. **Configure** `appsettings.json` (see Configuration section)
4. **Run** the application:
   ```bash
   dotnet GreeACLocalServer.Api.dll
   ```

### **Option 3: Build from Source**

```bash
git clone https://github.com/yourusername/GreeACLocalServer.git
cd GreeACLocalServer
dotnet build src/GreeACLocalServer.sln
dotnet run --project src/GreeACLocalServer.Api
```

### **Option 4: System Service Installation**

For production deployments, it's recommended to run the application as a system service.

#### **Linux (systemd)**

1. **Download and extract** the Linux release:
   ```bash
   sudo mkdir -p /opt/greeac-localserver
   sudo tar -xzf greeac-localserver-linux-x64-v*.tar.gz -C /opt/greeac-localserver
   sudo chmod +x /opt/greeac-localserver/GreeACLocalServer.Api
   ```

2. **Create dedicated user** for security:
   ```bash
   sudo useradd --system --no-create-home --shell /bin/false greeac
   sudo chown -R greeac:greeac /opt/greeac-localserver
   ```

3. **Create log directory**:
   ```bash
   sudo mkdir -p /var/log/greeac-localserver
   sudo chown greeac:greeac /var/log/greeac-localserver
   ```

4. **Install systemd service**:
   ```bash
   # Copy the service file (included in the repository)
   sudo cp systemd/greeac-localserver.service /etc/systemd/system/
   
   # Reload systemd and enable the service
   sudo systemctl daemon-reload
   sudo systemctl enable greeac-localserver.service
   ```

5. **Configure the application**:
   ```bash
   sudo nano /opt/greeac-localserver/appsettings.json
   ```
   Update the Server settings (DomainName, ExternalIp, etc.)

6. **Start the service**:
   ```bash
   sudo systemctl start greeac-localserver.service
   
   # Check status
   sudo systemctl status greeac-localserver.service
   
   # View logs
   sudo journalctl -u greeac-localserver.service -f
   ```

#### **Windows Service**

1. **Download and extract** the Windows release to `C:\Program Files\GreeACLocalServer\`

2. **Configure the application**:
   - Edit `C:\Program Files\GreeACLocalServer\appsettings.json`
   - Update Server settings (DomainName, ExternalIp, etc.)

3. **Install as Windows Service** using PowerShell (as Administrator):
   ```powershell
   # Navigate to the installation directory
   cd "C:\Program Files\GreeACLocalServer"
   
   # Create the Windows Service
   sc.exe create "GreeACLocalServer" `
     binPath= "C:\Program Files\GreeACLocalServer\GreeACLocalServer.Api.exe" `
     DisplayName= "GreeAC Local Server" `
     Description= "Local server for GREE air conditioners" `
     start= auto
   
   # Start the service
   Start-Service -Name "GreeACLocalServer"
   
   # Check service status
   Get-Service -Name "GreeACLocalServer"
   ```

4. **Alternative: Using .NET hosting bundle** (if available):
   ```powershell
   # If you have the .NET hosting bundle, you can also use:
   dotnet "C:\Program Files\GreeACLocalServer\GreeACLocalServer.Api.dll" `
     --install-service --service-name "GreeACLocalServer"
   ```

5. **Service Management**:
   ```powershell
   # Stop the service
   Stop-Service -Name "GreeACLocalServer"
   
   # Start the service
   Start-Service -Name "GreeACLocalServer"
   
   # Remove the service (if needed)
   sc.exe delete "GreeACLocalServer"
   ```

6. **View logs** in Windows Event Viewer:
   - Open Event Viewer
   - Navigate to **Windows Logs** → **Application**
   - Filter by source "GreeACLocalServer"

#### **Service Configuration Notes**

- **Automatic startup**: Both systemd and Windows services are configured to start automatically on boot
- **Process monitoring**: Services will automatically restart if the application crashes
- **Security**: Linux service runs as non-privileged user; Windows service runs as Local System
- **Logging**: 
  - Linux: Uses systemd journal (`journalctl -u greeac-localserver.service`)
  - Windows: Logs to Windows Event Log and application log files
- **Resource limits**: systemd service includes security and resource restrictions

#### **Port Configuration for Services**

When running as a service, ensure:
- **Port 5000** (TCP) - GREE device communication (required)
- **Port 1813** (TCP) - GREE device TLS communication (optional)
- **Port 5100** (HTTP) - Web interface (if EnableUI=true)
- **Port 5443** (TCP) - Web interface TLS (optional)

**Firewall configuration:**
```bash
# Linux (ufw)
sudo ufw allow 5000/tcp
sudo ufw allow 1813/tcp
sudo ufw allow 5100/tcp

# Linux (firewalld)
sudo firewall-cmd --permanent --add-port=5000/tcp
sudo firewall-cmd --permanent --add-port=1813/tcp
sudo firewall-cmd --permanent --add-port=5100/tcp
sudo firewall-cmd --reload
```

```powershell
# Windows PowerShell (as Administrator)
New-NetFirewallRule -DisplayName "GreeAC Server TCP" -Direction Inbound -Protocol TCP -LocalPort 5000
New-NetFirewallRule -DisplayName "GreeAC Server TLS" -Direction Inbound -Protocol TCP -LocalPort 1813
New-NetFirewallRule -DisplayName "GreeAC Server Web" -Direction Inbound -Protocol TCP -LocalPort 5100
```

## ⚙️ **Configuration**

The application is configured via `appsettings.json`. Here are the key settings:

### **Configuration sources & precedence**

All entry points (UI mode, headless mode, and the early startup bootstrap) load the
exact same sources, lowest to highest precedence — a later source overrides an
earlier one:

1. `appsettings.json`
2. `appsettings.{Environment}.json` (`{Environment}` = `ASPNETCORE_ENVIRONMENT` / `DOTNET_ENVIRONMENT`)
3. `/etc/greeac-localserver/appsettings.json` (Linux only)
4. `/etc/greeac-localserver/appsettings.{Environment}.json` (Linux only)
5. `appsettings.dev.json` (local developer convenience, git-ignored)
6. **Environment variables** — nesting uses `__`, e.g. `GreeServer__ServerOptions__DomainName`
7. **Command-line arguments** — these override everything else

> **Command-line syntax:** the `__` separator only works for environment variables.
> On the command line use `:` — `--GreeServer:ServerOptions:DomainName=gree.example.com`
> or `/GreeServer:ServerOptions:DomainName gree.example.com`.

On startup the server logs a banner line with the running build version and the
active environment, e.g. `GreeAC Local Server 1.6.1+42.Sha.abcd starting (environment: Production)`.
Use it to confirm which build a container is actually running. It also logs a
warning if `ExternalIp` is a loopback address, since GREE devices on the LAN
cannot reach the server at `127.0.0.1`.


### **Server Configuration**
```json
{
  "GreeServer": {
    "ServerOptions": {
      "DomainName": "gree.example.com",   // Domain name pointed to your server (required)
      "ExternalIp": "192.168.1.100",      // IP address of your server (required)
      "TLSEnabled": true,                  // Start the extra device TLS listener on port 1813
      "ListenIPAddresses": [],             // Specific IPs to bind to (empty = all)
      "IdleTimeoutSeconds": 180,           // Close an idle device connection after N seconds (<=0 disables)
      "MaxConcurrentConnections": 200,     // Cap on concurrent device connections (<=0 disables)
      "AllowLegacyTlsProtocols": true      // Accept SSL3/TLS1.0/1.1 for old firmware; false = TLS1.2+ only
    },
    "EncryptionOptions": {
      "DefaultCryptoKey": "a3K8Bx%2r8Y7#xDh",  // GREE encryption key (default works; required)
      "TLSCertificateAutoCreate": true,         // Generate a self-signed cert if no path is set
      "TLSCertificatePath": "",                 // Path to a PKCS#12 (.pfx/.p12) certificate
      "TLSCertificatePassword": ""              // Password for the .pfx (blank if none)
    },
    "FirmwareUpdateCheck": {
      "Enabled": false,                    // Cloud check: ask the GREE update server if newer firmware exists (see privacy note)
      "AutoQuery": true,                   // Local-only: read each device's firmware version over LAN UDP; false = never probe automatically
      "CacheHours": 24,                    // How long a cloud lookup result is reused
      "BaseUrl": "https://grih.gree.com/wifiModule/Lastversion"
    }
  },
  "Server": {
    "EnableUI": true,                      // Enable/disable web interface
    "EnableManagement": true               // Enable/disable device management features
  }
}
```

### **Required Settings**

#### **`GreeServer.ServerOptions.DomainName`**
- **Purpose**: The domain name that GREE devices will connect to
- **Setup**: Create a DNS entry pointing this domain to your server's IP
- **Example**: `"gree.example.com"` → Points to `192.168.1.100`
- **Important**: GREE devices are configured to connect to specific domains

#### **`GreeServer.ServerOptions.ExternalIp`** 
- **Purpose**: The IP address where your server is accessible
- **Usage**: Must match the IP address that the DNS entry points to
- **Example**: `"192.168.1.100"` (your server's LAN IP)
- **Note**: Use the actual IP address, not `localhost` or `127.0.0.1`

> `DomainName`, `ExternalIp` and `GreeServer.EncryptionOptions.DefaultCryptoKey` are validated
> at startup — if any is missing the server refuses to start with a clear error instead of
> failing on every device packet.

#### **`Server.EnableUI`**
- **Purpose**: Controls whether the web interface is available
- **Values**: 
  - `true` - Web UI available at `http://your-server:5100`
  - `false` - Disables web interface (TCP server still runs)
- **Use Cases**: 
  - Set to `false` for headless/embedded deployments
  - Set to `true` for monitoring and management

#### **`Server.EnableManagement`**
- **Purpose**: Controls whether device management features are available
- **Values**: 
  - `true` - Device configuration features enabled (default)
  - `false` - Device management operations disabled
- **Affects**:
  - **API Endpoints**: `/device-config/set-name` and `/device-config/set-remote-host` return an error when disabled. `/device-config/status` (read-only query) stays available regardless — you can always inspect a device's configuration
  - **Web UI**: the Set Device Name / Set Remote Host sections are hidden when disabled; the Query Status form stays available
- **Use Cases**: 
  - Set to `false` for read-only deployments or security-conscious environments
  - Set to `true` when device configuration changes are needed
- **Security**: Provides an additional layer of protection against unauthorized device configuration changes

#### **`GreeServer.FirmwareUpdateCheck`**
- **`AutoQuery`** (default `true`): the server reads each connected device's firmware
  version over the **local network only** (outbound UDP to the device on port 7000,
  the same scan→bind→status handshake used by the Device Config tool). No cloud
  traffic. The result is shown in the "Device Details" dialog. A failed probe is
  retried at most every 6 hours, a successful one every 7 days. Set to `false` on
  locked-down deployments that must emit no automatic device traffic — the manual
  **Refresh** button in the dialog still works.
- **`Enabled`** (default `false`): when `true`, the server additionally asks the
  **GREE update server** (`BaseUrl`) whether a newer firmware exists for a device's
  firmware code, and the dialog then shows an "update available" / "up to date"
  icon. Results are cached per firmware code for `CacheHours`.
  > **Privacy note:** with `Enabled: true` the server makes outbound HTTPS requests
  > to a GREE-operated host, sending the device firmware code as a query parameter.
  > It stays off by default; leave it off if the server must not contact GREE.

### **Additional Configuration**
```json
{
  "DeviceManager": {
    "DeviceTimeoutMinutes": 60        // Online/offline threshold shown in the UI (no automatic removal); exposed via /api/config/server
  },
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://*:5100"        // Web UI port
      }
    }
  },
  "Serilog": {
    "MinimumLevel": "Information",   // Logging level (Debug, Information, Warning, Error)
    "WriteTo": [
      {"Name": "Console"},           // Output to console
      {"Name": "File"}               // Output to file (Production only)
    ]
  }
}
```

**Note**: Device removal is now **manual only**. The `DeviceTimeoutMinutes` setting is used only to determine the online/offline status display and does not automatically remove devices from the system.

## 🔧 **DNS Server Setup**

You **must** configure your DNS server to point GREE devices to your local server:

### **Option 1: Router/Local DNS**
1. Access your router's admin interface
2. Add a custom DNS entry:
   - **Host**: `gree.example.com` (use your chosen domain)
   - **IP**: `192.168.1.100` (your server's IP)

### **Option 2: Pi-hole/AdGuard Home**
1. Open your Pi-hole admin interface
2. Go to **Local DNS** → **DNS Records**
3. Add entry: `gree.example.com` → `192.168.1.100`

### **Option 3: Dedicated DNS Server**
Configure your DNS server (BIND, Unbound, etc.) with appropriate zone files.

## 📱 **Device Configuration**

### **Built-in Device Configuration Tool**

The server now includes a **built-in web-based device configuration tool** accessible through the web interface at `/device-config`. This eliminates the need for external tools in most scenarios.

#### **Features**
- **Query Device Status** - Retrieve current device name and remote host settings
- **Set Device Name** - Change the friendly name of your AC device
- **Configure Remote Host** - Update the server address the device connects to
- **Autocomplete IP Selection** - Choose from known devices or enter IP manually
- **Automatic Device Discovery** - Scans and binds devices automatically
- **Real-time Feedback** - Immediate success/error notifications
- **Management Control** - Configuration features can be disabled server-wide for security

#### **How to Use**
1. **Access the tool** at `http://your-server:5100/device-config`
2. **Select or enter device IP** from the autocomplete dropdown
3. **Choose your operation**:
   - **Query Status** - View current device configuration
   - **Set Name** - Change device display name
   - **Set Remote Host** - Configure server connection settings
4. **Execute** - The tool automatically handles device scanning and encryption

#### **Requirements**
- **Network Access** - Device must be accessible on the same network
- **UDP Port 7000** - Used for device communication
- **Device State** - AC must be powered on and network-connected

### **Built-in WiFi Configuration Tool**

The server includes a dedicated **WiFi Configuration page** at `/wifi-config` to help users configure their air conditioner's WiFi settings without external tools.

#### **Features**
- **Cross-platform Support** - Generates appropriate commands for Linux, macOS, and Windows
- **Real-time Command Generation** - Command updates immediately as you type
- **Multiple Windows Options** - WSL, PowerShell (native), and Ncat alternatives
- **Injection-safe** - The payload is built with a JSON serializer and shell-quoted per target
  shell, so any SSID/password (including quotes, `$`, backticks, etc.) is passed literally and
  cannot break the command or run injected code. Control characters are rejected.
- **Copy to Clipboard** - One-click command copying
- **Step-by-step Instructions** - Clear guidance for the entire process
- **Installation Help** - Platform-specific installation instructions

#### **Supported Platforms**
- **Linux** - Standard netcat (`nc -u`)
- **macOS** - Standard netcat (`nc -u`)
- **Windows (WSL)** - Netcat in Windows Subsystem for Linux
- **Windows (PowerShell)** - Native .NET UDP socket approach (no additional software needed)
- **Windows (Ncat)** - Nmap suite's netcat alternative

#### **How to Use**
1. **Reset AC WiFi** - Press MODE + WIFI (or MODE + TURBO) on remote for 5 seconds
2. **Connect to AC hotspot** - Join the AC's WiFi network (8-character alphanumeric SSID)
3. **Access the tool** at `http://your-server:5100/wifi-config`
4. **Enter WiFi credentials** - Input your home WiFi SSID and password
5. **Select your OS** - Choose appropriate operating system
6. **Copy and run command** - Execute the generated command in your terminal

#### **Generated Commands**

**Linux/macOS/WSL:**
```bash
printf %s '{"psw":"password","ssid":"network","t":"wlan"}' | nc -u 192.168.1.1 7000
```

**Windows PowerShell (recommended for Windows users):**
```powershell
$bytes = [System.Text.Encoding]::UTF8.GetBytes('{"psw":"password","ssid":"network","t":"wlan"}'); $client = New-Object System.Net.Sockets.UdpClient; $client.Connect('192.168.1.1', 7000); $client.Send($bytes, $bytes.Length); $client.Close()
```

**Windows Ncat:**
```bash
printf %s '{"psw":"password","ssid":"network","t":"wlan"}' | ncat -u 192.168.1.1 7000
```

The SSID/password are single-quoted (POSIX) or `''`-doubled (PowerShell) in the real output,
so the examples above only show the shape of the command.

#### **Requirements**
- **AC in AP mode** - Device must be broadcasting its own WiFi network
- **Network connection** - Connected to the AC's WiFi hotspot (192.168.1.1)
- **Appropriate tools** - netcat, PowerShell, or Ncat depending on platform

### **External Configuration Tool (Alternative)**

For advanced use cases or initial setup, you can also use:

1. **Use the original configuration tool**: [GreeAC-ConfigTool](https://github.com/emtek-at/GreeAC-ConfigTool)
2. **Configure the device** to point to your domain name (e.g., `gree.example.com`)
3. **Restart the AC device** to apply new settings

**Note**: Device configuration is a one-time setup. Once configured, devices will automatically connect to your local server.

## 🌐 **Web Interface**

Access the web interface at: `http://your-server-ip:5100`

### **Dashboard Features**
- **Live Device Dashboard** - Real-time view of connected devices
- **Device Information** - MAC addresses, IP addresses, DNS names
- **Connection Status** - Last seen timestamps and health indicators
- **Manual Device Removal** - Remove unwanted devices with confirmation dialogs
- **Device Action Controls** - Remove buttons with intuitive icon-based interface
- **Dark/Light Theme** - Automatic detection based on browser preference
- **Responsive Design** - Works on desktop, tablet, and mobile

### **Device Configuration Tool** (`/device-config`)
- **Query Device Status** - View current device name and remote host settings
- **Set Device Name** - Change the friendly name displayed on your AC
- **Configure Remote Host** - Update which server the device connects to
- **Autocomplete Selection** - Easy selection from known connected devices
- **Real-time Operations** - Immediate feedback on configuration changes

### **WiFi Configuration Tool** (`/wifi-config`)
- **Cross-platform Command Generation** - Creates appropriate commands for Linux, macOS, and Windows
- **Real-time Updates** - Command generates immediately as you type
- **Multiple Windows Support** - WSL, PowerShell (native), and Ncat options
- **Security Features** - Password visibility toggle and JSON string escaping
- **Step-by-step Guidance** - Complete instructions for AC WiFi setup
- **Clipboard Integration** - One-click command copying

### **Dashboard Information**
- **MAC Address** - Device hardware identifier
- **IP Address** - Current network address of the device
- **DNS Name** - Resolved hostname (if available)
- **Last Seen** - Timestamp of last communication
- **Status** - Online/Offline indicator
- **Device Actions** - Details button for configuration and remove button for device management

### **Device Removal**
- **Manual Removal** - Click the red delete icon on any device card
- **Confirmation Dialog** - Prevents accidental device removal
- **Real-time Updates** - Removed devices disappear immediately from all connected clients
- **Error Handling** - Clear feedback if device removal fails

## � **API Endpoints**

The server exposes RESTful API endpoints for programmatic access:

### **Configuration API**
- **GET `/api/config/server`** - Retrieve server configuration settings
  ```json
  {
    "enableManagement": true,
    "enableUI": true,
    "deviceTimeoutMinutes": 60
  }
  ```
  `deviceTimeoutMinutes` mirrors `DeviceManager:DeviceTimeoutMinutes` and drives the
  online/offline threshold shown in the web UI.

### **Device Configuration API**
- **POST `/api/device-config/status`** - Query device status (always available)
- **POST `/api/device-config/set-name`** - Set device name (requires `EnableManagement: true`)
- **POST `/api/device-config/set-remote-host`** - Configure remote host (requires `EnableManagement: true`)

### **Device Management API**
- **GET `/api/devices`** - List all known devices
- **GET `/api/devices/{mac}`** - Get specific device by MAC address
- **DELETE `/api/devices/{mac}`** - Remove device from the system (manual operation)
  ```json
  // Success response (HTTP 200)
  {
    "success": true,
    "message": "Device AA:BB:CC:DD:EE:FF removed successfully"
  }
  
  // Not found response (HTTP 404)
  {
    "success": false,
    "message": "Device AA:BB:CC:DD:EE:FF not found"
  }
  ```

**Note**: The `set-name` / `set-remote-host` endpoints return HTTP 200 with an error body
(`errorCode: "MANAGEMENT_DISABLED"`) when `EnableManagement` is disabled. `status` is not gated.

## �🔧 **Troubleshooting**

### **Devices Not Connecting**
1. **Verify DNS Setup** - Ensure domain points to correct IP
2. **Check Port Access** - Port 5000 must be accessible
3. **Firewall Rules** - Allow inbound connections on port 5000
4. **Device Configuration** - Verify AC is configured for your domain

### **Device Configuration Issues**
1. **Device Not Found** - Ensure AC is powered on and network-connected
2. **Connection Timeout** - Verify UDP port 7000 is not blocked
3. **Encryption Errors** - Device may need to be reset to factory defaults
4. **IP Address Not Listed** - Only devices that have connected appear in autocomplete
5. **Name Change Not Applied** - Power cycle the AC unit after changing settings
6. **Remote Host Update Failed** - Verify the new server address is correct and accessible
7. **Management Features Disabled** - Check `EnableManagement` setting in server configuration
8. **"Device management is disabled" Error** - Server administrator has disabled management features via `EnableManagement: false`

### **Device Removal Issues**
1. **Remove Button Not Visible** - Check if device card is fully loaded
2. **Removal Confirmation Not Appearing** - Browser may be blocking dialog prompts
3. **Device Not Removed** - Check network connection and API availability
4. **Device Reappears After Removal** - Device may be actively reconnecting; configure device to point to different server first
5. **API Error During Removal** - Check server logs for specific error messages

### **WiFi Configuration Issues**
1. **AC Not in AP Mode** - Reset WiFi by pressing MODE + WIFI (or MODE + TURBO) for 5 seconds on remote
2. **Cannot Connect to AC Hotspot** - Look for 8-character alphanumeric SSID (e.g., "u34k5l166")
3. **Command Not Found (Windows)** - Use PowerShell option (no additional software needed) or install WSL/Ncat
4. **Connection Refused** - Ensure you're connected to AC's WiFi network and 192.168.1.1 is reachable
5. **Command Fails on Windows** - Try PowerShell version or install netcat via `choco install netcat`
6. **AC Doesn't Connect to Home WiFi** - Verify SSID and password are correct, check WiFi signal strength
7. **Special Characters in SSID/Password** - Handled automatically for every platform (JSON serialization + shell quoting). Only raw control characters (newline, tab, NUL) are rejected — remove them from the value

### **Web UI Not Loading**
1. **Check Port 5100** - Ensure it's not blocked by firewall
2. **Verify EnableUI Setting** - Must be `true` in configuration
3. **Check Logs** - Review application logs for errors

### **DNS Resolution Issues**
- **Fallback Behavior** - Application shows IP addresses when DNS fails
- **DNS Server** - Verify your DNS server is accessible
- **Network Configuration** - Check server's DNS settings

### **Service Issues**

#### **Linux (systemd)**
```bash
# Check service status
sudo systemctl status greeac-localserver.service

# View recent logs
sudo journalctl -u greeac-localserver.service -n 50

# Follow logs in real-time
sudo journalctl -u greeac-localserver.service -f

# Restart service
sudo systemctl restart greeac-localserver.service

# Check if service is enabled for auto-start
sudo systemctl is-enabled greeac-localserver.service

# Service configuration file location
sudo nano /etc/systemd/system/greeac-localserver.service
```

#### **Windows Service**
```powershell
# Check service status
Get-Service -Name "GreeACLocalServer"

# Start/Stop/Restart service
Start-Service -Name "GreeACLocalServer"
Stop-Service -Name "GreeACLocalServer"
Restart-Service -Name "GreeACLocalServer"

# View Event Logs
Get-EventLog -LogName Application -Source "GreeACLocalServer" -Newest 20

# Check service configuration
Get-WmiObject -Class Win32_Service -Filter "Name='GreeACLocalServer'"
```

### **Common Service Problems**

1. **Service fails to start**:
   - Check configuration file syntax (JSON)
   - Verify file permissions
   - Check if ports are already in use: `netstat -tulpn | grep :5000`
   - Review error logs

2. **Service stops unexpectedly**:
   - Check system resources (memory, disk space)
   - Review application logs for errors
   - Verify .NET runtime is installed and compatible

3. **Permission denied errors**:
   - Ensure service user has read access to application files
   - Check log directory permissions
   - Verify network interface binding permissions

### **Performance Tuning**

For high-traffic scenarios, consider:
- **Increase file descriptor limits** (Linux)
- **Adjust timeout values** in configuration
- **Monitor memory usage** and adjust limits
- **Use dedicated network interface** if available

## 🐳 **Docker Deployment**

### **Environment Variables**

The following environment variables can be used to configure the container:

| Variable | Default | Description |
|----------|---------|-------------|
| `ASPNETCORE_ENVIRONMENT` | `Production` | ASP.NET Core environment (Development/Production) |
| `ASPNETCORE_URLS` | `http://+:5100;https://+:5443` | Kestrel URLs for web UI (HTTP and TLS) |
| `GreeServer__ServerOptions__DomainName` | `gree.local.server` | Domain name for DNS configuration |
| `GreeServer__ServerOptions__ExternalIp` | `127.0.0.1` | Server IP address |
| `GreeServer__EnableUI` | `true` | Enable/disable web interface |
| `GreeServer__ServerOptions__TLSEnabled` | `false` | Start the device TLS listener on port 1813 |
| `GreeServer__ServerOptions__AllowLegacyTlsProtocols` | `true` | Accept SSL3/TLS1.0/1.1 on the 1813 listener; `false` = TLS1.2+ only |
| `GreeServer__ServerOptions__IdleTimeoutSeconds` | `180` | Drop a device connection after N seconds of silence |
| `GreeServer__ServerOptions__MaxConcurrentConnections` | `200` | Cap on concurrent device connections |
| `Server__EnableManagement` | `true` | Allow device-config **writes** (set name / set remote host); status query is always allowed |
| `GreeServer__FirmwareUpdateCheck__AutoQuery` | `true` | Read device firmware version over LAN UDP; `false` = no automatic device probing |
| `GreeServer__FirmwareUpdateCheck__Enabled` | `false` | Also check the GREE update server for newer firmware (outbound HTTPS to GREE) |
| `DeviceManager__DeviceTimeoutMinutes` | `60` | Device online/offline display threshold (minutes) |

### **Docker Compose**

For easy deployment, use the provided `docker-compose.yml`:

```bash
# Edit docker-compose.yml to configure your domain and IP
nano docker-compose.yml

# Start the server
docker-compose up -d

# View logs
docker-compose logs -f

# Stop the server
docker-compose down
```

> **Updating a running deployment:** `docker compose up` does **not** rebuild an
> existing image, and `docker compose restart` does **not** re-read the
> `environment:` block. To pick up new code or new settings:
> ```bash
> docker compose down
> ./docker-build.sh          # or: docker compose build --no-cache
> docker compose up -d
> ```
> `./docker-build.sh` derives the version from `git describe` and writes it to
> `.env`, which `docker compose build` reads automatically; it also tags the
> image `gree-ac-local-server:latest`, the same tag `docker-compose.yml` uses, so
> both paths build one image. A bare `docker compose build` with no `.env` and no
> exported `APP_INFORMATIONAL_VERSION` stamps the banner `0.0.0-compose`.
> After starting, confirm the `GreeAC Local Server … starting` banner in the logs
> shows the version you expect, and that the web UI loads (a stuck loading screen
> usually means `/_framework/blazor.web.js` 404s - rebuild with a single
> `dotnet publish`, which the current Dockerfile does).

### **Development Mode**

For development with hot reload, use `docker-compose.dev.yml`:

```bash
docker-compose -f docker-compose.dev.yml up -d
```

### **Device TLS Listener (port 1813)**

To accept AC firmware that connects over TLS:

1. Set `GreeServer__ServerOptions__TLSEnabled=true` in your environment variables or docker-compose.yml
2. Provide a **PKCS#12** certificate (or rely on the auto-generated self-signed one):
   - `GreeServer__EncryptionOptions__TLSCertificatePath`: Path to a `.pfx` / `.p12` file inside the container
   - `GreeServer__EncryptionOptions__TLSCertificatePassword`: Password for the `.pfx` (blank if none)
3. Mount your certificates volume: `-v /path/to/certs:/app/certs:ro`
4. Optionally set `GreeServer__ServerOptions__AllowLegacyTlsProtocols=false` to require TLS 1.2+

Serving the **web UI** over HTTPS is a separate concern — configure Kestrel via
`ASPNETCORE_URLS` (e.g. `https://+:5443`) with its own certificate.

## 🧪 **Development**

### **Project Structure**
- **`GreeACLocalServer.Device`** - GREE protocol core: AES crypto, TCP listener for devices, outbound UDP device control (no ASP.NET dependency)
- **`GreeACLocalServer.Api`** - Host process: minimal-API endpoints, SignalR hub, and the Blazor Server render host
- **`GreeACLocalServer.UI`** - Blazor WebAssembly UI components
- **`GreeACLocalServer.Shared`** - Shared contracts and interfaces
- **`GreeACLocalServer.Api.Tests`** - xUnit tests for the Api layer
- **`GreeACLocalServer.Device.Tests`** - xUnit tests for the protocol core (crypto, message handler, socket/UDP handling)

### **Building**
```bash
dotnet build src/GreeACLocalServer.sln
```

### **Testing**
```bash
dotnet test src/GreeACLocalServer.sln
```

Integration tests that bind real loopback sockets are tagged; skip them with
`dotnet test src/GreeACLocalServer.sln --filter "Category!=Integration"`.

### **Running in Development**
```bash
dotnet run --project src/GreeACLocalServer.Api
```

## 📄 **License**

This project is licensed under the **GNU General Public License v3.0** - see the [LICENSE](LICENSE) file for details.

## 🙏 **Acknowledgments**

- **[emtek-at/GreeAC-DummyServer](https://github.com/emtek-at/GreeAC-DummyServer)** - Original implementation that inspired this project
- **GREE Community** - For reverse engineering the AC protocol
- **Contributors** - All who have helped improve this project

## 📚 **Additional Resources**

- **Original Project**: [GreeAC-DummyServer](https://github.com/emtek-at/GreeAC-DummyServer)
- **Configuration Tool**: [GreeAC-ConfigTool](https://github.com/emtek-at/GreeAC-ConfigTool)
- **WiFi Configuration Reference**: [GREE HVAC MQTT Bridge](https://github.com/arthurkrupa/gree-hvac-mqtt-bridge) - Source for WiFi configuration method

---

**This project enables your GREE air conditioners to work completely offline while providing modern monitoring and management capabilities through a beautiful web interface.**