# StatusBot

[![CI](https://github.com/mayvqt/StatusBot/actions/workflows/ci.yml/badge.svg)](https://github.com/mayvqt/StatusBot/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/mayvqt/StatusBot?style=flat-square)](https://github.com/mayvqt/StatusBot/releases/latest)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat&logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

## Overview

StatusBot is a lightweight .NET 8 service that monitors HTTP, TCP, and ICMP endpoints and posts a single consolidated status embed to Discord. Designed for 24/7 reliability with minimal resource usage, it provides persistent uptime tracking, automatic state recovery, and a simple REST API for programmatic access.

The service maintains a single Discord message that updates in real-time as service statuses change, preventing channel spam while providing clear visibility into your infrastructure health.

## Features

### Core Functionality

**Multi-Protocol Monitoring**
- HTTP/HTTPS endpoint checks (validates 2xx responses)
- TCP connection tests (port availability)
- ICMP ping tests (network reachability)
- Configurable polling intervals per service
- Parallel status checks for efficiency

**Discord Integration**
- Single consolidated status embed (no duplicate messages)
- Real-time updates when service states change
- Customizable bot presence text
- Color-coded status indicators (green/red)
- Automatic message recovery on bot restart
- Rich embed formatting with uptime statistics

**REST API**
- `GET /api/status` - List all monitored services and their current status
- `GET /api/status/{service}` - Query individual service status
- JSON responses for easy integration
- Configurable bind address and CORS support
- Default endpoint: `http://0.0.0.0:4130`

### Reliability & Resilience

**Persistent State Management**
- Uptime tracking persists across restarts
- Atomic file writes prevent state corruption
- Automatic `.corrupt.*.bak` backups for recovery
- Schema versioning for safe upgrades
- State file location: `config/state.json`

**Robust Error Handling**
- Service check failures don't crash the bot
- Automatic retry logic with exponential backoff
- Rate limiting to prevent API spam
- Graceful degradation when Discord is unavailable
- Comprehensive structured logging

**Background Service Architecture**
- Separate workers for monitoring, Discord updates, and API hosting
- Non-blocking async operations throughout
- Graceful shutdown handling
- Automatic recovery from transient failures

### Configuration & Deployment

**Flexible Configuration**
- JSON-based configuration file (`config/config.json`)
- Environment variable overrides for Docker/container deployments
- Auto-creation of config directory on first run
- Hot-reload support (restart required for config changes)
- Clear validation with detailed error messages

**Operational Features**
- Minimal resource footprint (typically <50MB RAM)
- Cross-platform support (Windows, Linux, macOS)
- Single binary deployment option (self-contained)
- Framework-dependent builds for smaller size
- systemd/Windows Service integration examples

## Installation

### Download Pre-built Binary

Download the appropriate binary for your platform from the [latest release](https://github.com/mayvqt/StatusBot/releases/latest):

**Framework-dependent** (requires .NET 8 installed):
- `statusbot-windows-x64.zip` - Windows (x64)
- `statusbot-windows-arm64.zip` - Windows (ARM)
- `statusbot-linux-x64.tar.gz` - Linux (x64)
- `statusbot-linux-arm64.tar.gz` - Linux (ARM)
- `statusbot-osx-x64.tar.gz` - macOS (Intel)
- `statusbot-osx-arm64.tar.gz` - macOS (Apple Silicon)

All releases include SHA256 checksums in `checksums.txt` for verification.

Example download and verification (Linux):

```bash
curl -LO https://github.com/mayvqt/StatusBot/releases/latest/download/statusbot-linux-x64.tar.gz
curl -LO https://github.com/mayvqt/StatusBot/releases/latest/download/checksums.txt
sha256sum -c checksums.txt 2>&1 | grep statusbot-linux-x64
tar -xzf statusbot-linux-x64.tar.gz
cd statusbot-linux-x64-*/
chmod +x StatusBot
```

### Build from Source

Requirements: .NET 8 SDK

```bash
git clone https://github.com/mayvqt/StatusBot.git
cd StatusBot
dotnet build src -c Release
dotnet run --project src
```

For production builds:

```bash
# Framework-dependent (smaller size, requires .NET 8 on target)
dotnet publish src -c Release -o ./publish

# Self-contained Windows x64 (includes .NET runtime)
dotnet publish src -c Release -r win-x64 --self-contained true -o ./publish/win-x64

# Self-contained Linux x64
dotnet publish src -c Release -r linux-x64 --self-contained true -o ./publish/linux-x64
```

**Note**: Avoid using `-p:PublishTrimmed=true` as Discord.Net and Newtonsoft.Json rely on reflection.

## Configuration

StatusBot uses a JSON configuration file located at `config/config.json`. The directory and file are auto-created on first run with default values.

### Configuration File

Create or edit `config/config.json`:

```json
{
  "Token": "YOUR_DISCORD_BOT_TOKEN",
  "ChannelId": 123456789012345678,
  "PollIntervalSeconds": 60,
  "PresenceText": "Monitoring Services",
  "Services": [
    {
      "Name": "MainSite",
      "Type": "HTTP",
      "Url": "https://example.com"
    },
    {
      "Name": "API",
      "Type": "HTTP",
      "Url": "https://api.example.com/health"
    },
    {
      "Name": "Database",
      "Type": "TCP",
      "Host": "db.example.com",
      "Port": 5432
    },
    {
      "Name": "Gateway",
      "Type": "ICMP",
      "Host": "192.168.1.1"
    }
  ]
}
```

### Configuration Options

| Field | Type | Description | Required |
|-------|------|-------------|----------|
| `Token` | string | Discord bot authentication token | Yes |
| `ChannelId` | number | Discord channel ID for status messages | Yes |
| `PollIntervalSeconds` | number | How often to check services (seconds) | Yes |
| `PresenceText` | string | Custom bot presence (empty = auto-detect from first HTTP service) | No |
| `Services` | array | List of services to monitor | Yes |

### Service Definition

Each service in the `Services` array requires:

| Field | Type | Description | Used By |
|-------|------|-------------|---------|
| `Name` | string | Unique service identifier | All |
| `Type` | string | Check type: `HTTP`, `TCP`, or `ICMP` | All |
| `Url` | string | Full HTTP/HTTPS URL to check | HTTP only |
| `Host` | string | Hostname or IP address | TCP, ICMP |
| `Port` | number | TCP port number | TCP only |

### Environment Variables

Override configuration via environment variables (useful for containers):

```bash
# PowerShell
$env:StatusBot__Token = "your_bot_token"
$env:StatusBot__ChannelId = "123456789012345678"
$env:StatusBot__PollIntervalSeconds = "30"
$env:ASPNETCORE_URLS = "http://0.0.0.0:4130"

# Bash
export StatusBot__Token="your_bot_token"
export StatusBot__ChannelId="123456789012345678"
export StatusBot__PollIntervalSeconds="30"
export ASPNETCORE_URLS="http://0.0.0.0:4130"
```

## Usage

### Discord Bot Setup

1. Create a Discord application at https://discord.com/developers/applications
2. Navigate to the "Bot" section and create a bot
3. Copy the bot token (this is your `Token` in config)
4. Enable the following Privileged Gateway Intents:
   - Presence Intent
   - Server Members Intent (if using multiple servers)
5. Generate an invite URL:
   - Go to OAuth2 → URL Generator
   - Select scopes: `bot`
   - Select permissions: `Send Messages`, `Embed Links`, `Read Message History`
6. Use the generated URL to invite the bot to your server
7. Right-click your target channel → Copy ID (enable Developer Mode in Discord settings)
8. Use this as your `ChannelId` in config

### Running the Service

Start StatusBot after configuring your `config/config.json`:

```bash
# From source
dotnet run --project src

# Published binary (framework-dependent)
./StatusBot

# Published binary (Windows)
StatusBot.exe
```

On startup, you should see output similar to:

```
[INF] Config directory exists: config
[INF] Configuration loaded from config/config.json
[INF] StatusBot services initialized
[INF] StatusMonitor starting...
[INF] DiscordUpdater starting...
[INF] ApiHost starting on http://0.0.0.0:4130
[INF] Discord client connected
[INF] Bot presence set: Monitoring Services
```

### API Endpoints

#### GET /api/status

Returns the current status of all monitored services.

**Response:**
```json
[
  {
    "name": "MainSite",
    "type": "HTTP",
    "url": "https://example.com",
    "isUp": true,
    "lastChecked": "2025-11-02T16:45:30Z",
    "uptimePercent": 99.95
  },
  {
    "name": "Database",
    "type": "TCP",
    "host": "db.example.com",
    "port": 5432,
    "isUp": true,
    "lastChecked": "2025-11-02T16:45:30Z",
    "uptimePercent": 100.0
  }
]
```

#### GET /api/status/{service}

Returns the status of a specific service by name.

**Example:**
```bash
curl http://localhost:4130/api/status/MainSite
```

**Response:**
```json
{
  "name": "MainSite",
  "type": "HTTP",
  "url": "https://example.com",
  "isUp": true,
  "lastChecked": "2025-11-02T16:45:30Z",
  "uptimePercent": 99.95
}
```

### Customizing API Bind Address

By default, the API listens on `http://0.0.0.0:4130`. Change this with the `ASPNETCORE_URLS` environment variable:

```bash
# Listen on specific port
export ASPNETCORE_URLS="http://localhost:8080"

# Listen on all interfaces with custom port
export ASPNETCORE_URLS="http://0.0.0.0:8080"

# HTTPS (requires certificate configuration)
export ASPNETCORE_URLS="https://0.0.0.0:4130"
```

## Running as a Service

For production deployments, run StatusBot as a system service to ensure automatic startup and restart on failure.

### systemd (Linux)

Create `/etc/systemd/system/statusbot.service`:

```ini
[Unit]
Description=StatusBot Discord Monitor
After=network.target

[Service]
Type=simple
User=statusbot
Group=statusbot
WorkingDirectory=/opt/statusbot
ExecStart=/opt/statusbot/StatusBot
Restart=always
RestartSec=10
StartLimitBurst=5
StartLimitIntervalSec=120

# Optional environment variables
Environment=ASPNETCORE_URLS=http://0.0.0.0:4130
Environment=DOTNET_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
```

Create a dedicated user and install:

```bash
# Create user and directory
sudo useradd -r -s /bin/false statusbot
sudo mkdir -p /opt/statusbot
sudo cp -r ./publish/* /opt/statusbot/
sudo chown -R statusbot:statusbot /opt/statusbot

# Install and start service
sudo systemctl daemon-reload
sudo systemctl enable statusbot
sudo systemctl start statusbot
sudo systemctl status statusbot
```

View logs:

```bash
sudo journalctl -u statusbot -f
```

### Windows Service (NSSM)

Download NSSM from https://nssm.cc/ and install the service:

```powershell
# Install NSSM service
nssm install StatusBot "C:\StatusBot\StatusBot.exe"
nssm set StatusBot AppDirectory "C:\StatusBot"
nssm set StatusBot DisplayName "StatusBot Discord Monitor"
nssm set StatusBot Description "Monitors HTTP/TCP/ICMP endpoints and posts status to Discord"
nssm set StatusBot Start SERVICE_AUTO_START

# Configure logging
nssm set StatusBot AppStdout "C:\StatusBot\logs\stdout.log"
nssm set StatusBot AppStderr "C:\StatusBot\logs\stderr.log"
nssm set StatusBot AppStdoutCreationDisposition 4
nssm set StatusBot AppStderrCreationDisposition 4

# Optional environment variables
nssm set StatusBot AppEnvironmentExtra ASPNETCORE_URLS=http://0.0.0.0:4130

# Start service
nssm start StatusBot
```

View service status:

```powershell
nssm status StatusBot
Get-Service StatusBot
Get-Content C:\StatusBot\logs\stdout.log -Tail 50
```

### Docker

Example Dockerfile:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app
COPY src/ ./
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
VOLUME /app/config
EXPOSE 4130
ENTRYPOINT ["./StatusBot"]
```

Example docker-compose.yml:

```yaml
version: '3.8'

services:
  statusbot:
    build: .
    container_name: statusbot
    restart: unless-stopped
    ports:
      - "4130:4130"
    volumes:
      - ./config:/app/config
    environment:
      ASPNETCORE_URLS: "http://0.0.0.0:4130"
      DOTNET_ENVIRONMENT: "Production"
```

Build and run:

```bash
docker-compose up -d
docker-compose logs -f statusbot
```

## State Management

### State File Structure

StatusBot maintains persistent state in `config/state.json`:

```json
{
  "Version": 2,
  "StatusMessageId": 123456789012345678,
  "Services": {
    "MainSite": {
      "TotalChecks": 1440,
      "SuccessfulChecks": 1438,
      "FirstSeenUtc": "2025-11-01T00:00:00Z"
    },
    "Database": {
      "TotalChecks": 1440,
      "SuccessfulChecks": 1440,
      "FirstSeenUtc": "2025-11-01T00:00:00Z"
    }
  }
}
```

### State Recovery

- **Automatic backups**: Corrupt state files are saved as `state.corrupt.{timestamp}.bak`
- **Safe writes**: Atomic file operations prevent partial writes
- **Graceful degradation**: Missing state file creates a new one
- **Message recovery**: On restart, bot finds existing status message in channel

### Upgrading from Older Versions

If upgrading from a version that tracked per-service message IDs:

1. Stop StatusBot
2. Optional: Delete `config/state.json` (uptime stats will reset)
3. Optional: Manually delete old status messages in Discord
4. Start StatusBot - it will create/find a single consolidated message

Uptime data will be preserved if you keep the state file, but old messages will not be automatically cleaned up.

## Troubleshooting

### Bot Not Connecting to Discord

**Symptoms**: No Discord connection, bot doesn't appear online

**Solutions**:
1. Verify `Token` in `config/config.json` is correct
2. Check bot token hasn't been regenerated in Discord Developer Portal
3. Ensure bot has been invited to the server
4. Check firewall/network allows Discord API access (discordapp.com)
5. Review logs for authentication errors

### No Status Messages Posted

**Symptoms**: Bot is online but doesn't post status embed

**Solutions**:
1. Verify `ChannelId` is correct (right-click channel → Copy ID with Developer Mode enabled)
2. Check bot permissions: `Send Messages`, `Embed Links`, `Read Message History`
3. Ensure bot can see the target channel (check channel permissions)
4. Try deleting any old status messages and restarting
5. Check logs for Discord API errors

### Services Showing as Down

**Symptoms**: Services appear offline when they should be online

**Solutions**:
1. Verify service configuration (URL, Host, Port)
2. Test connectivity manually:
   ```bash
   # HTTP
   curl -I https://example.com
   
   # TCP
   telnet db.example.com 5432
   # or: nc -zv db.example.com 5432
   
   # ICMP
   ping 192.168.1.1
   ```
3. Check network/firewall rules from StatusBot host
4. Review logs for specific error messages
5. Verify HTTP services return 2xx status codes

### Duplicate Status Messages

**Symptoms**: Multiple status embeds appear in the channel

**Solutions**:
1. Delete all status messages in the Discord channel
2. Delete `config/state.json`
3. Restart StatusBot
4. Bot will create a single new status message

### API Not Accessible

**Symptoms**: Cannot reach `http://localhost:4130/api/status`

**Solutions**:
1. Check `ASPNETCORE_URLS` environment variable
2. Verify firewall allows the configured port
3. Try accessing from localhost first: `curl http://localhost:4130/api/status`
4. Check logs for API hosting errors
5. For remote access, ensure binding to `0.0.0.0` not `localhost`

### High Memory Usage

**Symptoms**: StatusBot using excessive memory (>200MB)

**Solutions**:
1. Check `PollIntervalSeconds` - very short intervals may cause issues
2. Review number of services - hundreds of services may need optimization
3. Check for memory leaks in logs
4. Restart service to clear any accumulated state
5. Consider framework-dependent build instead of self-contained

### State File Corruption

**Symptoms**: Errors loading state, corrupt backup files created

**Solutions**:
1. Check disk space and filesystem health
2. Review `.corrupt.*.bak` files in config directory
3. Manually restore from backup if needed
4. Delete state file to start fresh (loses uptime history)
5. Ensure proper shutdown (avoid force-kill)

## Development

### Project Structure

```
StatusBot/
├── src/
│   ├── Program.cs                      # Entry point, DI container setup
│   ├── StatusBot.csproj                # Project file (.NET 8)
│   ├── Models/
│   │   ├── Config.cs                   # Configuration model
│   │   ├── ServiceDefinition.cs        # Service check definition
│   │   ├── ServiceStatus.cs            # Runtime status tracking
│   │   └── State.cs                    # Persistent state model
│   └── Services/
│       ├── ApiHost.cs                  # REST API hosting (ASP.NET Core minimal)
│       ├── ConfigManager.cs            # Config loading and validation
│       ├── DiscordUpdater.cs           # Discord embed updates
│       ├── ErrorHelper.cs              # Centralized error logging
│       ├── Persistence.cs              # State save/load with atomic writes
│       ├── RateLimiter.cs              # API rate limiting
│       ├── SetupHelper.cs              # Initial setup and directory creation
│       ├── StatusMonitor.cs            # Background service checks
│       ├── StatusStore.cs              # In-memory status cache
│       └── UptimeCalculator.cs         # Uptime percentage calculations
├── tests/
│   └── StatusBot.Tests/
│       ├── StatusBot.Tests.csproj      # Test project
│       └── UptimeCalculatorTests.cs    # Unit tests
├── .github/
│   └── workflows/
│       ├── ci.yml                      # CI workflow (build, test, coverage)
│       └── release.yml                 # Release workflow (multi-platform builds)
├── README.md
└── StatusBot.sln                       # Solution file
```

### Running Tests

```bash
cd tests/StatusBot.Tests
dotnet test --verbosity normal

# With coverage
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage
```

### CI/CD

The project uses GitHub Actions for continuous integration and releases.

**CI Workflow** (runs on all pushes and PRs):
- Multi-OS matrix build (Windows, Linux, macOS)
- Unit test execution with coverage
- Platform-specific publish artifacts
- Archive creation (.zip for Windows, .tar.gz for Linux/macOS)

**Release Workflow** (runs on version tags or manual trigger):
- Automated version tagging with semver bump
- Cross-platform builds (x64 + ARM64)
- Zipped release archives per platform
- SHA256 checksum generation
- Build provenance attestation
- GitHub Release creation with assets

To create a new release:

```bash
git tag -a v1.0.0 -m "Release v1.0.0"
git push origin v1.0.0
```

Or trigger manually via GitHub Actions UI with optional tag input.

### Contributing

Contributions are welcome! Please follow these guidelines:

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/your-feature-name`
3. Write tests for new functionality
4. Ensure all tests pass: `dotnet test`
5. Follow existing code style and patterns
6. Commit with clear, descriptive messages
7. Push to your fork and open a pull request

Pull requests will automatically run CI checks. Please ensure:
- All tests pass on Windows, Linux, and macOS
- Code follows C# conventions and best practices
- No new compiler warnings introduced
- Documentation updated for new features

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

## Support

- **Issues**: Report bugs or request features via [GitHub Issues](https://github.com/mayvqt/StatusBot/issues)
- **Discussions**: Ask questions or share ideas in [GitHub Discussions](https://github.com/mayvqt/StatusBot/discussions)

## Acknowledgments

Built with Discord.Net for monitoring infrastructure and services with minimal overhead.
