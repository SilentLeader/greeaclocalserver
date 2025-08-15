# Use the official .NET 9.0 runtime image for the final stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app

# Expose ports for TCP server and web UI
EXPOSE 5000
EXPOSE 5100

# Use the .NET 9.0 SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution file
COPY src/GreeACLocalServer.sln ./

# Copy project files (excluding tests for production build)
COPY src/GreeACLocalServer.Api/GreeACLocalServer.Api.csproj ./GreeACLocalServer.Api/
COPY src/GreeACLocalServer.UI/GreeACLocalServer.UI.csproj ./GreeACLocalServer.UI/
COPY src/GreeACLocalServer.Shared/GreeACLocalServer.Shared.csproj ./GreeACLocalServer.Shared/

# Restore dependencies for production projects only
RUN dotnet restore GreeACLocalServer.Api/GreeACLocalServer.Api.csproj

# Copy the entire source code (excluding tests via .dockerignore)
COPY src/ ./

# Build the application
RUN dotnet build GreeACLocalServer.Api/GreeACLocalServer.Api.csproj -c Release --no-restore

# Publish the application
FROM build AS publish
RUN dotnet publish GreeACLocalServer.Api/GreeACLocalServer.Api.csproj -c Release --no-build -o /app/publish

# Final stage
FROM base AS final
WORKDIR /app

# Copy published application
COPY --from=publish /app/publish .

# Set environment variables with defaults
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5100
ENV Server__Port=5000
ENV Server__DomainName=gree.local.server
ENV Server__ExternalIp=127.0.0.1
ENV Server__EnableUI=true

# Create a non-root user for security
RUN groupadd -r appuser && useradd -r -g appuser appuser

# Create directories that might be volume mounted and set permissions
RUN mkdir -p /app/logs && chown -R appuser:appuser /app

# Set user before changing ownership to handle volume mounts
USER appuser

# Health check
#HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
#  CMD curl -f http://localhost:5100/api/devices || exit 1

# Start the application
ENTRYPOINT ["dotnet", "GreeACLocalServer.Api.dll"]
