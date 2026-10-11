# /etc/conf.d/greeac-localserver - environment for the GreeAC Local Server.
# OpenRC sources this file for the init script of the same name. Variables
# must be prefixed with `export` so they reach the service process (plain
# assignments stay shell-local). This file is the OpenRC equivalent of
# systemd's Environment= lines in greeac-localserver.service.

export DOTNET_ENVIRONMENT=Production
export ASPNETCORE_ENVIRONMENT=Production

# Optional: configure the application via environment variables instead of
# editing appsettings.json (nesting uses `__`, see the Configuration section
# in the Readme):
#export GreeServer__ServerOptions__DomainName=gree.example.com
#export GreeServer__ServerOptions__ExternalIp=192.168.1.100
#export GreeServer__ServerOptions__TLSEnabled=true
#export Server__EnableUI=false
