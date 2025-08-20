# GREE AC Local Server

This project provides a **modern, feature-rich local replacement server for GREE air conditioners** that normally require internet connectivity to communicate with GREE's cloud servers. The solution allows GREE AC units to function completely offline by implementing a local server that mimics the original GREE server functionality.

**Based on the excellent foundation of [GreeAC-DummyServer](https://github.com/emtek-at/GreeAC-DummyServer)**, this project has been completely rewritten and modernized with .NET 9, featuring a comprehensive web UI, real-time device monitoring, and advanced management capabilities.

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
- **DNS Resolution** of device IP addresses to FQDNs (with fallback to IP)
- **Connection Health Monitoring** with automatic cleanup of stale devices
- **Real-time Status Updates** via SignalR broadcasting
- **Configurable Device Timeouts**

### **Developer & Operations Features**
- **Structured Logging** with Serilog
- **Background Services** for TCP server hosting
- **Cross-platform Support** (Windows/Linux)
- **Docker Ready** with proper networking
- **System Service Integration** (systemd/Windows Service)
- **Comprehensive Unit Tests**

## 🚀 **Installation**

### **Prerequisites**
- **.NET 9 Runtime** (for running) or SDK (for building)
- **DNS Server Configuration** - Add an entry pointing to your server's IP address
- **Network Access** - Server must be accessible on port 5000 and 5100

### **Option 1: Docker (Recommended)**

#### **Quick Start with Docker Compose**
```bash
# Clone the repository
git clone https://github.com/SilentLeader/greeaclocalserver.git
cd greeaclocalserver

# Edit docker-compose.yml to set your domain and IP
# Update Server__DomainName and Server__ExternalIp values

# Start the server
./docker-run.sh
# or manually: docker-compose up -d
```

#### **Using Docker Run Command**
```bash
docker run -d \
  --restart=always \
  --name gree-ac-server \
  -e Server__DomainName=gree.example.com \
  -e Server__ExternalIp=192.168.1.100 \
  -e Server__EnableUI=true \
  -e Server__EnableManagement=true \
  -p 5000:5000 \
  -p 5100:5100 \
  gree-ac-local-server:latest
```

#### **Building Docker Image Locally**
```bash
# Build the image
./docker-build.sh
# or manually: docker build -t gree-ac-local-server:latest .

# Run with docker-compose
docker-compose up -d
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
- **Port 5100** (HTTP) - Web interface (if EnableUI=true)

**Firewall configuration:**
```bash
# Linux (ufw)
sudo ufw allow 5000/tcp
sudo ufw allow 5100/tcp

# Linux (firewalld)
sudo firewall-cmd --permanent --add-port=5000/tcp
sudo firewall-cmd --permanent --add-port=5100/tcp
sudo firewall-cmd --reload
```

```powershell
# Windows PowerShell (as Administrator)
New-NetFirewallRule -DisplayName "GreeAC Server TCP" -Direction Inbound -Protocol TCP -LocalPort 5000
New-NetFirewallRule -DisplayName "GreeAC Server Web" -Direction Inbound -Protocol TCP -LocalPort 5100
```

## ⚙️ **Configuration**

The application is configured via `appsettings.json`. Here are the key settings:

### **Server Configuration**
```json
{
  "Server": {
    "Port": 5000,                    // TCP port for GREE devices (must be 5000)
    "DomainName": "gree.example.com", // Domain name pointed to your server
    "ExternalIp": "192.168.1.100",   // IP address of your server
    "ListenIPAddresses": [],          // Specific IPs to bind to (empty = all)
    "CryptoKey": "a3K8Bx%2r8Y7#xDh", // GREE encryption key (default works)
    "EnableUI": true,                 // Enable/disable web interface
    "EnableManagement": true          // Enable/disable device management features
  }
}
```

### **Required Settings**

#### **`DomainName`**
- **Purpose**: The domain name that GREE devices will connect to
- **Setup**: Create a DNS entry pointing this domain to your server's IP
- **Example**: `"gree.example.com"` → Points to `192.168.1.100`
- **Important**: GREE devices are configured to connect to specific domains

#### **`ExternalIp`** 
- **Purpose**: The IP address where your server is accessible
- **Usage**: Must match the IP address that the DNS entry points to
- **Example**: `"192.168.1.100"` (your server's LAN IP)
- **Note**: Use the actual IP address, not `localhost` or `127.0.0.1`

#### **`EnableUI`**
- **Purpose**: Controls whether the web interface is available
- **Values**: 
  - `true` - Web UI available at `http://your-server:5100`
  - `false` - Disables web interface (TCP server still runs)
- **Use Cases**: 
  - Set to `false` for headless/embedded deployments
  - Set to `true` for monitoring and management

#### **`EnableManagement`**
- **Purpose**: Controls whether device management features are available
- **Values**: 
  - `true` - Device configuration features enabled (default)
  - `false` - Device management operations disabled
- **Affects**:
  - **API Endpoints**: `/device-config/set-name` and `/device-config/set-remote-host` return errors when disabled
  - **Web UI**: Management sections (Set Device Name, Set Remote Host) are hidden when disabled
  - **Query Operations**: Device status queries (`/device-config/status`) remain available regardless of this setting
- **Use Cases**: 
  - Set to `false` for read-only deployments or security-conscious environments
  - Set to `true` when device configuration changes are needed
- **Security**: Provides an additional layer of protection against unauthorized device configuration changes

### **Additional Configuration**
```json
{
  "DeviceManager": {
    "DeviceTimeoutMinutes": 60        // Minutes before removing stale devices
  },
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://*:5100"        // Web UI port
      }
    }
  }
}
```

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
- **Password Security** - Visibility toggle and proper JSON string escaping
- **Copy to Clipboard** - One-click command copying
- **Step-by-step Instructions** - Clear guidance for the entire process
- **Installation Help** - Platform-specific installation instructions

#### **Supported Platforms**
- **Linux** - Standard netcat (`nc -cu`)
- **macOS** - Standard netcat (`nc -cu`)
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
echo -n "{\"psw\": \"password\",\"ssid\": \"network\",\"t\": \"wlan\"}" | nc -cu 192.168.1.1 7000
```

**Windows PowerShell (recommended for Windows users):**
```powershell
$bytes = [System.Text.Encoding]::UTF8.GetBytes('{"psw": "password","ssid": "network","t": "wlan"}'); $client = New-Object System.Net.Sockets.UdpClient; $client.Connect('192.168.1.1', 7000); $client.Send($bytes, $bytes.Length); $client.Close()
```

**Windows Ncat:**
```cmd
echo {"psw": "password","ssid": "network","t": "wlan"} | ncat -u 192.168.1.1 7000
```

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

## � **API Endpoints**

The server exposes RESTful API endpoints for programmatic access:

### **Configuration API**
- **GET `/api/config/server`** - Retrieve server configuration settings
  ```json
  {
    "enableManagement": true,
    "enableUI": true
  }
  ```

### **Device Configuration API**
- **POST `/api/device-config/status`** - Query device status (always available)
- **POST `/api/device-config/set-name`** - Set device name (requires `EnableManagement: true`)
- **POST `/api/device-config/set-remote-host`** - Configure remote host (requires `EnableManagement: true`)

### **Device Management API**
- **GET `/api/devices`** - List all known devices
- **GET `/api/devices/{mac}`** - Get specific device by MAC address

**Note**: Management endpoints return HTTP 200 with error response when `EnableManagement` is disabled.

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

### **WiFi Configuration Issues**
1. **AC Not in AP Mode** - Reset WiFi by pressing MODE + WIFI (or MODE + TURBO) for 5 seconds on remote
2. **Cannot Connect to AC Hotspot** - Look for 8-character alphanumeric SSID (e.g., "u34k5l166")
3. **Command Not Found (Windows)** - Use PowerShell option (no additional software needed) or install WSL/Ncat
4. **Connection Refused** - Ensure you're connected to AC's WiFi network and 192.168.1.1 is reachable
5. **Command Fails on Windows** - Try PowerShell version or install netcat via `choco install netcat`
6. **AC Doesn't Connect to Home WiFi** - Verify SSID and password are correct, check WiFi signal strength
7. **Special Characters in Password** - Use PowerShell option as it handles JSON escaping automatically

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

## 🧪 **Development**

### **Project Structure**
- **`GreeACLocalServer.Api`** - Main API project with TCP server and Blazor UI
- **`GreeACLocalServer.UI`** - Blazor WebAssembly UI components
- **`GreeACLocalServer.Shared`** - Shared contracts and interfaces
- **`GreeACLocalServer.Api.Tests`** - Unit tests

### **Building**
```bash
dotnet build src/GreeACLocalServer.sln
```

### **Testing**
```bash
dotnet test src/GreeACLocalServer.Api.Tests
```

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