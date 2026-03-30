# ==============================================================================
# GREE AC Local Server - Docker Build Configuration
# ==============================================================================
# Multi-stage build optimized for production deployment
# Stage 0: Base runtime image (final stage)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

# Expose ports for TCP server, web UI, and TLS
EXPOSE 5000
EXPOSE 5100
EXPOSE 1813

# ==============================================================================
# Stage 1: Build stage - compile the application
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution file first for better layer caching
COPY src/GreeACLocalServer.sln ./

# Copy project files (excluding tests for production build)
COPY src/GreeACLocalServer.Api/GreeACLocalServer.Api.csproj ./GreeACLocalServer.Api/
COPY src/GreeACLocalServer.UI/GreeACLocalServer.UI.csproj ./GreeACLocalServer.UI/
COPY src/GreeACLocalServer.Shared/GreeACLocalServer.Shared.csproj ./GreeACLocalServer.Shared/
COPY src/GreeACLocalServer.Device/GreeACLocalServer.Device.csproj ./GreeACLocalServer.Device/

# Restore dependencies for production projects only
RUN dotnet restore GreeACLocalServer.Api/GreeACLocalServer.Api.csproj

# Copy the entire source code (excluding tests via .dockerignore)
COPY src/ ./

# Build the application
RUN dotnet build GreeACLocalServer.Api/GreeACLocalServer.Api.csproj -c Release --no-restore

# ==============================================================================
# Stage 2: Publish stage - prepare deployment artifacts
FROM build AS publish
RUN dotnet publish GreeACLocalServer.Api/GreeACLocalServer.Api.csproj -c Release --no-build -o /app/publish

# ==============================================================================
# Stage 3: Final runtime image - minimal and secure
FROM base AS final
WORKDIR /app

# Copy published application from build stage
COPY --from=publish /app/publish .

# Set environment variables with defaults
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5100
ENV GreeServer__ServerOptions__DomainName=gree.local.server
ENV GreeServer__ServerOptions__ExternalIp=127.0.0.1
ENV GreeServer__EnableUI=true
ENV GreeServer__ServerOptions__TLSEnabled=true

# Create a non-root user for security
RUN groupadd -r appuser && useradd -r -g appuser appuser

# Create directories that might be volume mounted and set permissions
RUN mkdir -p /app/logs && chown -R appuser:appuser /app

# Set user before changing ownership to handle volume mounts
USER appuser

# Start the application
ENTRYPOINT ["dotnet", "GreeACLocalServer.Api.dll"]
