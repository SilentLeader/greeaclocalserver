#!/bin/bash

# Quick start script for GREE AC Local Server using Docker Compose

set -e

# Check if docker-compose.yml exists
if [ ! -f "docker-compose.yml" ]; then
    echo "❌ docker-compose.yml not found!"
    echo "Make sure you're running this script from the project root directory."
    exit 1
fi

echo "🚀 Starting GREE AC Local Server..."

# Check if we need to build the image first
if ! docker images | grep -q "gree-ac-local-server"; then
    echo "📦 Building Docker image (first run)..."
    docker-compose build
fi

# Start the services
docker-compose up -d

echo "✅ GREE AC Local Server is now running!"
echo ""
echo "📊 Service Status:"
docker-compose ps

echo ""
echo "🌐 Access Points:"
echo "   Web Interface: http://localhost:5100"
echo "   TCP Server:    localhost:5000 (for GREE devices)"
echo ""
echo "📋 Useful Commands:"
echo "   View logs:     docker-compose logs -f"
echo "   Stop server:   docker-compose down"
echo "   Restart:       docker-compose restart"
echo "   View status:   docker-compose ps"
echo ""
echo "⚠️  Configuration:"
echo "   Edit docker-compose.yml to update:"
echo "   - Server__DomainName (currently: gree.example.com)"
echo "   - Server__ExternalIp (currently: 192.168.1.100)"
