# Docker Deployment Guide

This guide explains how to deploy the GREE AC Local Server using Docker.

## 📋 Prerequisites

- Docker Engine 20.10+ and Docker Compose v2.0+
- At least 512MB RAM available for the container
- Network access to ports 5000 (TCP server) and 5100 (Web UI)

## 🚀 Quick Start

### 1. Clone and Configure

```bash
git clone https://github.com/SilentLeader/greeaclocalserver.git
cd greeaclocalserver
```

### 2. Edit Configuration

Edit `docker-compose.yml` and update these values:

```yaml
environment:
  - Server__DomainName=your-domain.com     # Your DNS entry
  - Server__ExternalIp=192.168.1.100       # Your server's IP
```

### 3. Start the Server

```bash
# Using the convenience script
./docker-run.sh

# Or manually
docker-compose up -d
```

## 🔧 Configuration Options

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `Server__Port` | `5000` | TCP port for GREE devices (must be 5000) |
| `Server__DomainName` | `gree.example.com` | Domain name for DNS configuration |
| `Server__ExternalIp` | `192.168.1.100` | Server IP address |
| `Server__EnableUI` | `true` | Enable/disable web interface |
| `Server__CryptoKey` | `a3K8Bx%2r8Y7#xDh` | GREE encryption key |
| `DeviceManager__DeviceTimeoutMinutes` | `60` | Device timeout in minutes |
| `ASPNETCORE_ENVIRONMENT` | `Production` | ASP.NET Core environment |

### Volume Mounts

The docker-compose.yml includes optional volume mounts:

```yaml
volumes:
  # Configuration override
  - ./appsettings.Production.json:/app/appsettings.Production.json:ro
  
  # Logs directory
  - ./logs:/app/logs
```

## 🏗️ Building Custom Images

### Build Locally

```bash
# Using the build script
./docker-build.sh

# Or manually
docker build -t gree-ac-local-server:latest .
```

### Multi-Architecture Build

```bash
# Build for multiple architectures (requires buildx)
docker buildx build \
  --platform linux/amd64,linux/arm64 \
  -t gree-ac-local-server:latest \
  --push .
```

## 🛠️ Development Setup

For development with hot reload:

```bash
# Start development environment
docker-compose -f docker-compose.dev.yml up -d

# View logs
docker-compose -f docker-compose.dev.yml logs -f
```

## 📊 Monitoring and Maintenance

### View Logs

```bash
# View all logs
docker-compose logs -f

# View last 100 lines
docker-compose logs --tail=100 -f

# View specific service logs
docker-compose logs -f gree-ac-server
```

### Health Checks

The container includes a health check that verifies the API is responding:

```bash
# Check container health
docker-compose ps

# Manual health check
docker exec gree-ac-local-server curl -f http://localhost:5100/api/devices
```

### Container Management

```bash
# Stop services
docker-compose down

# Restart services
docker-compose restart

# Update and restart
docker-compose pull && docker-compose up -d

# View resource usage
docker stats gree-ac-local-server
```

## 🔒 Security Considerations

### Network Security

- The container runs as non-root user for security
- Only necessary ports are exposed
- Use environment variables for sensitive configuration

### Firewall Configuration

Ensure these ports are accessible:

```bash
# TCP port for GREE devices (required)
sudo ufw allow 5000/tcp

# Web UI port (optional, can be restricted to local network)
sudo ufw allow from 192.168.0.0/16 to any port 5100
```

## 🚨 Troubleshooting

### Common Issues

#### Container Won't Start
```bash
# Check logs for errors
docker-compose logs gree-ac-server

# Verify configuration
docker-compose config
```

#### Port Conflicts
```bash
# Check what's using the ports
sudo netstat -tulpn | grep -E ':5000|:5100'

# Change ports in docker-compose.yml if needed
ports:
  - "15000:5000"  # Changed external port
  - "15100:5100"
```

#### DNS Resolution Issues
```bash
# Test DNS from container
docker exec gree-ac-local-server nslookup your-domain.com

# Check container network
docker network inspect greeacheartbeatserver_gree-network
```

### Debug Mode

Enable debug logging:

```yaml
environment:
  - ASPNETCORE_ENVIRONMENT=Development
  - Serilog__MinimumLevel=Debug
```

## 📈 Performance Tuning

### Resource Limits

Add resource limits to docker-compose.yml:

```yaml
services:
  gree-ac-server:
    deploy:
      resources:
        limits:
          memory: 512M
          cpus: '0.5'
        reservations:
          memory: 256M
          cpus: '0.25'
```

### Production Optimizations

```yaml
environment:
  - ASPNETCORE_ENVIRONMENT=Production
  - DOTNET_EnableDiagnostics=0
  - DOTNET_USE_POLLING_FILE_WATCHER=false
```

## 🔄 Updates and Backup

### Updating

```bash
# Pull latest changes
git pull origin main

# Rebuild and restart
docker-compose down
docker-compose build --no-cache
docker-compose up -d
```

### Backup Configuration

```bash
# Backup configuration and logs
tar -czf gree-backup-$(date +%Y%m%d).tar.gz \
  docker-compose.yml \
  appsettings.Production.json \
  logs/
```

This completes the Docker setup for your GREE AC Local Server!
