#!/bin/bash

# Build and run the Docker container for GREE AC Local Server

set -e

echo "🚀 Building GREE AC Local Server Docker image..."
docker build -t gree-ac-local-server:latest .

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
