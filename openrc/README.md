# OpenRC Service Files

OpenRC equivalents of [`systemd/greeac-localserver.service`](../systemd/greeac-localserver.service)
for OpenRC-based distributions (Alpine Linux, Gentoo, Devuan).

| File | Installs to | Purpose |
|------|-------------|---------|
| `greeac-localserver` | `/etc/init.d/greeac-localserver` | Init script (process behaviour) |
| `greeac-localserver.conf.d` | `/etc/conf.d/greeac-localserver` | Environment variables (OpenRC sources it automatically for the matching init script) |

## How the systemd directives map

| systemd | OpenRC | Notes |
|---------|--------|-------|
| `After=/Wants=network-online.target` | `depend() { need net }` | OpenRC has no wait-online target |
| `WorkingDirectory=` | `directory="/opt/greeac-localserver"` | The .NET host loads `appsettings.json` from the content root, which defaults to the cwd — without this the service runs with cwd `/` and finds no `appsettings.json` |
| `Type=notify` | — | The app calls `UseSystemd()`, which is a no-op unless `NOTIFY_SOCKET` is set; `supervise-daemon` tracks the process directly |
| `User=/Group=greeac` | `command_user="greeac:greeac"` | 1:1 |
| `Restart=always` + `RestartSec=10` | `supervisor=supervise-daemon` + `respawn_max=0`, `respawn_delay=10` | Without `supervise-daemon` a plain `command=` script does **not** respawn on crash |
| `TimeoutStopSec=30` | `retry="TERM/30/KILL/5"` | Send TERM, wait up to 30s, then KILL |
| `SyslogIdentifier` + journal | `output_log` / `error_log` | OpenRC has no journal; logs go to `/var/log/greeac-localserver/stdout.log` and `stderr.log` (rotate them, see below) |
| `LimitNOFILE=65536` / `LimitNPROC=4096` | `ulimit -n 65536` / `ulimit -u 4096` in `start_pre()` | dash (Devuan) has no `-u`, so the script falls back to `ulimit -p` |
| `ReadWritePaths=` (log dirs) | `checkpath` in `start_pre()` | Creates the directories with the correct owner |
| `Environment=` | `/etc/conf.d/greeac-localserver` | Every variable needs an `export` prefix, see below |
| `WantedBy=multi-user.target` | `rc-update add greeac-localserver default` | 1:1 |
| `NoNewPrivileges=yes` | `no_new_privs=yes` | `supervise-daemon` >= OpenRC 0.45 |
| `KillMode=process` | — | `supervise-daemon` signals only the child process unless `stopgroup` is set |
| `PrivateTmp` / `ProtectSystem` / `ProtectHome` | — | No OpenRC equivalent; running as the unprivileged `greeac` user is the remaining isolation |

## Installation

1. **Get the binary.** The GitHub release tarball is `linux-x64`, which is a
   glibc build and does **not** run on Alpine (musl) out of the box — the
   apphost fails to exec with `No such file or directory` because the glibc
   loader is absent. On Alpine, publish for musl instead (verified path):

   ```bash
   # Alpine: install the .NET SDK, then build self-contained for musl
   sudo apk add dotnet10-sdk   # version per your Alpine release
   git clone https://github.com/SilentLeader/greeaclocalserver.git
   cd greeaclocalserver
   dotnet publish src/GreeACLocalServer.Api/GreeACLocalServer.Api.csproj \
     -c Release -r linux-musl-x64 --self-contained true \
     -o /tmp/greeac-build
   sudo mkdir -p /opt/greeac-localserver
   sudo cp -r /tmp/greeac-build/* /opt/greeac-localserver/
   sudo chmod +x /opt/greeac-localserver/GreeACLocalServer.Api
   ```

   On Gentoo/Devuan (glibc) you can use the release tarball directly:

   ```bash
   sudo mkdir -p /opt/greeac-localserver
   sudo tar -xzf greeac-localserver-linux-x64-v*.tar.gz -C /opt/greeac-localserver
   sudo chmod +x /opt/greeac-localserver/GreeACLocalServer.Api
   ```

   (Alpine also ships a `gcompat` glibc-compatibility package that lets some
   glibc binaries run; it is not tested with this app, so the musl build above
   is the recommended Alpine path.)

2. **Create the dedicated user** for security:
   ```bash
   # Alpine
   sudo adduser -D -H -s /sbin/nologin greeac
   # Gentoo/Devuan
   sudo useradd --system --no-create-home --shell /usr/sbin/nologin greeac
   sudo chown -R greeac:greeac /opt/greeac-localserver
   ```

3. **Create the log directory**:
   ```bash
   sudo mkdir -p /var/log/greeac-localserver
   sudo chown greeac:greeac /var/log/greeac-localserver
   ```

4. **Install the service files**:
   ```bash
   sudo cp openrc/greeac-localserver /etc/init.d/greeac-localserver
   sudo cp openrc/greeac-localserver.conf.d /etc/conf.d/greeac-localserver
   sudo chmod +x /etc/init.d/greeac-localserver
   sudo rc-update add greeac-localserver default
   ```

5. **Configure** the application by editing `/opt/greeac-localserver/appsettings.json`
   (see the Configuration section of the main Readme) or by setting environment
   variables in `/etc/conf.d/greeac-localserver`.

6. **Start**:
   ```bash
   sudo rc-service greeac-localserver start
   sudo rc-service greeac-localserver status
   ```

## Configuration via /etc/conf.d

Two things worth knowing:

- **`export` is required for env-based config.** OpenRC sources
  `/etc/conf.d/greeac-localserver` into the init script's shell; plain
  assignments stay shell-local and never reach the service process. This only
  matters when you override settings via environment variables — the shipped
  `appsettings.json` already provides defaults (including `DomainName`), so a
  missing `export` shows up as *your override being ignored*, not as a startup
  failure.
- **The `DomainName is required` startup error is a working-directory issue,
  not an env issue.** The .NET host loads `appsettings.json` from the content
  root, which defaults to the current working directory. If the service runs
  with cwd `/` (which happens if `directory` is not set in the init script),
  no `appsettings.json` is found, the defaults never load, and the required
  validation fails with `DomainName is required; ExternalIp is required` even
  though the file clearly exists under `/opt/greeac-localserver`. The init
  script sets `directory="/opt/greeac-localserver"` to fix this.
- **`EnableUI` lives in the `Server` section.** The correct variable is
  `Server__EnableUI=false` (Program.cs reads `GetSection("Server")`); see the
  `Server.EnableUI` subsection under Configuration in the main Readme.

Example with environment-based configuration:

```sh
export DOTNET_ENVIRONMENT=Production
export ASPNETCORE_ENVIRONMENT=Production
export GreeServer__ServerOptions__DomainName=gree.example.com
export GreeServer__ServerOptions__ExternalIp=192.168.1.100
export GreeServer__ServerOptions__TLSEnabled=true
export Server__EnableUI=false
```

## Log rotation

`supervise-daemon` keeps the log file descriptors open, so the files grow
unbounded until rotated. Because the descriptors stay open, rotation needs
`copytruncate`. Drop this into `/etc/logrotate.d/greeac-localserver`:

```
/var/log/greeac-localserver/*.log {
    weekly
    rotate 4
    compress
    missingok
    notifempty
    copytruncate
}
```

(Alternatively, set `output_logger` / `error_logger` in the init script to pipe
through a rotating logger such as `logger` or `rotatelogs`.)

## Operation

```bash
sudo rc-service greeac-localserver start|stop|restart|status

# Follow logs (no journal under OpenRC)
sudo tail -f /var/log/greeac-localserver/stdout.log
sudo tail -f /var/log/greeac-localserver/stderr.log

# Enabled at boot?
rc-status default | grep greeac-localserver
```

## Troubleshooting

- **Server starts then exits immediately, respawn loops**: check
  `/var/log/greeac-localserver/stderr.log` — usually a missing required setting
  (`DomainName`, `ExternalIp`, `DefaultCryptoKey`). If the log says
  `DomainName is required` even though `appsettings.json` exists, the working
  directory is wrong — confirm `directory=` is set in the init script.
- **No crash restart**: confirm the init script is the version with
  `supervisor=supervise-daemon`; without it OpenRC starts the process but never
  restarts it.
- **Log directory missing**: `start_pre()` creates it via `checkpath`, but only if
  `/var/log` itself is writable by root — verify the service was started via
  `rc-service`, not by running the binary directly as the unprivileged user.
