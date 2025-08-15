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

```bash
docker run -d \
  --restart=always \
  --name gree-ac-server \
  -e Server__DomainName=gree.example.com \
  -e Server__ExternalIp=192.168.1.100 \
  -e Server__EnableUI=true \
  -p 5000:5000 \
  -p 5100:5100 \
  your-registry/gree-ac-local-server:latest
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
    "EnableUI": true                  // Enable/disable web interface
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

To configure your GREE AC devices to use the local server:

1. **Use the original configuration tool**: [GreeAC-ConfigTool](https://github.com/emtek-at/GreeAC-ConfigTool)
2. **Configure the device** to point to your domain name (e.g., `gree.example.com`)
3. **Restart the AC device** to apply new settings

**Note**: Device configuration is a one-time setup. Once configured, devices will automatically connect to your local server.

## 🌐 **Web Interface**

Access the web interface at: `http://your-server-ip:5100`

### **Features**
- **Live Device Dashboard** - Real-time view of connected devices
- **Device Information** - MAC addresses, IP addresses, DNS names
- **Connection Status** - Last seen timestamps and health indicators
- **Dark/Light Theme** - Automatic detection based on browser preference
- **Responsive Design** - Works on desktop, tablet, and mobile

### **Dashboard Information**
- **MAC Address** - Device hardware identifier
- **IP Address** - Current network address of the device
- **DNS Name** - Resolved hostname (if available)
- **Last Seen** - Timestamp of last communication
- **Status** - Online/Offline indicator

## 🔧 **Troubleshooting**

### **Devices Not Connecting**
1. **Verify DNS Setup** - Ensure domain points to correct IP
2. **Check Port Access** - Port 5000 must be accessible
3. **Firewall Rules** - Allow inbound connections on port 5000
4. **Device Configuration** - Verify AC is configured for your domain

### **Web UI Not Loading**
1. **Check Port 5100** - Ensure it's not blocked by firewall
2. **Verify EnableUI Setting** - Must be `true` in configuration
3. **Check Logs** - Review application logs for errors

### **DNS Resolution Issues**
- **Fallback Behavior** - Application shows IP addresses when DNS fails
- **DNS Server** - Verify your DNS server is accessible
- **Network Configuration** - Check server's DNS settings

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
- **GREE Protocol Documentation**: Included in the original repository

---

**This project enables your GREE air conditioners to work completely offline while providing modern monitoring and management capabilities through a beautiful web interface.**