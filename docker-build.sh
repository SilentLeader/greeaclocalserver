#!/bin/bash

# Build and run the Docker container for GREE AC Local Server

set -e

echo "🚀 Building GREE AC Local Server Docker image..."

# Derive a version stamp from git so the running container reports which build it is
# (the startup banner logs this). Without it every image would report 0.0.0.
VERSION_RAW=$(git describe --tags --always --dirty 2>/dev/null || echo "0.0.0-local")
VERSION_NUM=$(printf '%s' "$VERSION_RAW" | sed -E 's/^v//; s/-.*$//')

# `docker compose build` reads .env from the project directory automatically and
# uses it to interpolate build.args, so write the same version there. .env is
# gitignored (*.env).
cat > .env <<EOF
APP_VERSION=${VERSION_NUM:-0.0.0}
APP_INFORMATIONAL_VERSION=${VERSION_RAW#v}
EOF

docker build \
  --build-arg APP_VERSION="${VERSION_NUM:-0.0.0}" \
  --build-arg APP_INFORMATIONAL_VERSION="${VERSION_RAW#v}" \
  -t gree-ac-local-server:latest .

echo "✅ Build completed successfully!"
echo ""
echo "To run the container, use one of the following commands:"
echo ""
echo "1. Using docker run:"
echo "   docker run -d --name gree-ac-server \\"
echo "     -p 5000:5000 -p 1813:1813 -p 5100:5100 \\"
echo "     -e GreeServer__ServerOptions__DomainName=gree.example.com \\"
echo "     -e GreeServer__ServerOptions__ExternalIp=192.168.1.100 \\"
echo "     gree-ac-local-server:latest"
echo ""
echo "2. Using docker-compose:"
echo "   docker-compose up -d"
echo ""
echo "3. To view logs:"
echo "   docker logs gree-ac-server"
echo ""
echo "4. To access the web interface:"
echo "   http://localhost:5100"
echo ""
echo "⚠️  Remember to:"
echo "   - Update GreeServer__ServerOptions__DomainName and GreeServer__ServerOptions__ExternalIp with your values"
echo "   - Configure your DNS server to point to your server IP"
echo "   - Configure your GREE AC devices to use your domain"
