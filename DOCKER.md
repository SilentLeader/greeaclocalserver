# Docker Deployment Guide

This guide explains how to deploy the GREE AC Local Server using Docker.

## 📋 Prerequisites

- Docker Engine 20.10+ and Docker Compose v2.0+
- At least 512MB RAM available for the container
- Network access to ports 5000 (TCP server), 5100 (Web UI HTTP), and optionally 1813 (Web UI TLS/HTTPS)

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
  - GreeServer__ServerOptions__DomainName=your-domain.com     # Your DNS entry
  - GreeServer__ServerOptions__ExternalIp=192.168.1.100       # Your server's IP
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
| `ASPNETCORE_ENVIRONMENT` | `Production` | ASP.NET Core environment (Development/Production) |
| `ASPNETCORE_URLS` | `http://+:5100;https://+:5443` | Kestrel URLs for web UI (HTTP and TLS) |
| `GreeServer__ServerOptions__DomainName` | `gree.local.server` | Domain name for DNS configuration |
| `GreeServer__ServerOptions__ExternalIp` | `127.0.0.1` | Server IP address |
| `GreeServer__EnableUI` | `true` | Enable/disable web interface |
| `GreeServer__ServerOptions__TLSEnabled` | `false` | Enable TLS/HTTPS for web UI |
| `DeviceManager__DeviceTimeoutMinutes` | `60` | Device timeout in minutes |

### Volume Mounts

The docker-compose.yml includes optional volume mounts:

```yaml
volumes:
  # Logs directory
  - ./logs:/app/logs
  
  # TLS certificates (for HTTPS)
  - /path/to/certs:/app/certs:ro
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

## 🔒 TLS/HTTPS Configuration

To enable TLS/HTTPS for the web interface, configure the following environment variables:

1. **Enable TLS**: Set `GreeServer__ServerOptions__TLSEnabled=true`
2. **Certificate Path**: Set `GreeServer__EncryptionOptions__TLSCertificatePath=/app/certs/server.crt`
3. **Certificate Password**: Set `GreeServer__EncryptionOptions__TLSCertificatePassword=your-cert-password`

Example docker-compose.yml with TLS:

```yaml
services:
  gree-ac-server:
    environment:
      - GreeServer__ServerOptions__TLSEnabled=true
      - GreeServer__EncryptionOptions__TLSCertificatePath=/app/certs/server.crt
      - GreeServer__EncryptionOptions__TLSCertificatePassword=your-cert-password
    volumes:
      - /path/to/certs:/app/certs:ro
```

Access the web interface via HTTPS at `https://your-server-ip:5443`.

## 🚨 Troubleshooting

### Container Won't Start

```bash
# Check logs for errors
docker-compose logs gree-ac-server

# Verify configuration
docker-compose config
```

### Port Conflicts

```bash
# Check what's using the ports
sudo netstat -tulpn | grep -E ':5000|:1813|:5100|:5443'

# Change ports in docker-compose.yml if needed
ports:
  - "15000:5000"  # Changed external port
  - "11813:1813"
  - "15100:5100"
```

### DNS Resolution Issues

```bash
# Test DNS from container
docker exec gree-ac-local-server nslookup your-domain.com

# Check container network
docker network inspect greeacheartbeatserver_gree-network
```

### Health Check Failures

```bash
# Check health status
docker inspect --format='{{.State.Health.Status}}' gree-ac-local-server

# View health check logs
docker logs gree-ac-local-server | grep -i "health"
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

# TCP port for GREE devices TLS
sudo ufw allow 1813/tcp

# Web UI port (optional, can be restricted to local network)
sudo ufw allow from 192.168.0.0/16 to any port 5100
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

# TCP port for GREE devices TLS
sudo ufw allow 1813/tcp

# Web UI HTTP port
sudo ufw allow 5100/tcp

# Web UI TLS/HTTPS port (optional)
sudo ufw allow 5443/tcp
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
sudo netstat -tulpn | grep -E ':5000|:5100|:5443'

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
