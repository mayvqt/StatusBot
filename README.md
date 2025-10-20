# StatusBot

StatusBot is a production-ready .NET 8 service that monitors multiple endpoints (HTTP/TCP/ICMP) and posts real-time status updates to Discord channels. It includes persistent uptime tracking, rate limiting, and a REST API for programmatic access.

## Features

### Core Monitoring
- **Multi-Protocol Support**: Monitor HTTP endpoints, TCP ports, and ICMP (ping) targets
- **Real-Time Discord Updates**: Posts and updates embed messages with current status, uptime metrics, and timestamps
- **Persistent Uptime Tracking**: Calculates accurate uptime percentages across service restarts
- **Intelligent Rate Limiting**: Prevents Discord API abuse with configurable per-message cooldowns

### Data Persistence
- **Atomic State Management**: Thread-safe JSON persistence with atomic writes and corruption recovery
- **Message Metadata Tracking**: Remembers Discord message IDs for seamless updates
- **Uptime History**: Tracks cumulative uptime seconds and monitoring start times
- **Configuration Hot-Reload**: Automatically reloads config changes without restart

### API & Integration
- **REST API**: Expose current status via `/api/status` and `/api/status/{service}` endpoints
- **Structured Logging**: Uses Serilog with configurable output and error handling
- **Cross-Platform**: Runs on Windows, Linux, and macOS with identical behavior
- **Production Hardening**: Handles file locks, network timeouts, and transient failures gracefully

### Developer Experience
- **Universal Build Scripts**: Cross-platform `build.bat` and `build.sh` with extensive options
- **Comprehensive Documentation**: XML docs on all public APIs and thorough README
- **Unit Testing**: Validates uptime calculations and core business logic
- **Configuration Validation**: Clear error messages for missing or invalid settings

## Project Structure

```
StatusBot/
├── src/                          # Application source code
│   ├── Program.cs               # Entry point and DI configuration
│   ├── StatusBot.csproj         # Project file with dependencies
│   ├── Models/                  # Data models and configuration
│   │   ├── Config.cs           # Application configuration model
│   │   ├── ServiceDefinition.cs # Service endpoint definitions
│   │   ├── ServiceStatus.cs    # Runtime status and uptime data
│   │   └── State.cs            # Persistent application state
│   └── Services/               # Core business logic
│       ├── ApiHost.cs          # HTTP API server
│       ├── ConfigManager.cs    # Configuration loading and watching
│       ├── DiscordUpdater.cs   # Discord message management
│       ├── ErrorHelper.cs      # Centralized error logging
│       ├── Persistence.cs      # State file management
│       ├── RateLimiter.cs      # Discord API rate limiting
│       ├── SetupHelper.cs      # First-run initialization
│       ├── StatusMonitor.cs    # Service polling and uptime tracking
│       ├── StatusStore.cs      # In-memory status cache
│       └── UptimeCalculator.cs # Uptime percentage calculations
├── build.bat                   # Windows build script
├── build.sh                    # Unix build script
├── BUILDING.md                 # Build system documentation
└── README.md                   # This file
```

## Quick Start

### Prerequisites
- .NET 8.0 SDK or later
- Discord bot token and channel ID (see [Discord Setup](#discord-setup))

### Development Setup
1. Clone and build the project:
   ```bash
   git clone <repository-url>
   cd StatusBot
   dotnet build src
   ```

2. Configure your Discord bot (first run creates `config/config.json`):
   ```bash
   dotnet run --project src
   # Edit config/config.json with your Discord token and channel ID
   # Press Ctrl+C and restart
   dotnet run --project src
   ```

3. The application will:
   - Create default configuration files in `config/`
   - Start monitoring configured services
   - Post status updates to your Discord channel
   - Expose API endpoints at `http://localhost:8080/api/status`

### Discord Setup
1. Create a Discord Application at https://discord.com/developers/applications
2. Go to "Bot" section and create a bot
3. Copy the bot token
4. Invite the bot to your server with "Send Messages" and "Embed Links" permissions
5. Get your channel ID (Enable Developer Mode → Right-click channel → Copy ID)

## Configuration

StatusBot uses `config/config.json` for all settings:

```json
{
  "Token": "YOUR_DISCORD_BOT_TOKEN",
  "ChannelId": 123456789012345678,
  "PollIntervalSeconds": 60,
  "Services": [
    {
      "Name": "MainSite",
      "Type": "HTTP",
      "Url": "https://example.com"
    },
    {
      "Name": "API",
      "Type": "TCP", 
      "Host": "api.example.com",
      "Port": 443
    },
    {
      "Name": "DNS",
      "Type": "ICMP",
      "Host": "8.8.8.8"
    }
  ]
}
```

### Configuration Options

| Field | Type | Description | Required |
|-------|------|-------------|----------|
| `Token` | string | Discord bot token | ✓ |
| `ChannelId` | ulong | Discord channel ID for status messages | ✓ |
| `PollIntervalSeconds` | int | Seconds between service checks (default: 60) | |
| `Services` | array | List of services to monitor | ✓ |

### Service Definition Options

| Field | Type | Description | Required |
|-------|------|-------------|----------|
| `Name` | string | Unique service identifier | ✓ |
| `Type` | string | Monitor type: "HTTP", "TCP", or "ICMP" | ✓ |
| `Url` | string | Full URL for HTTP monitoring | HTTP only |
| `Host` | string | Hostname or IP address | TCP/ICMP |
| `Port` | int | Port number for TCP connections | TCP only |

### Environment Variables

You can override configuration values using environment variables:

```bash
export StatusBot__Token="your_bot_token"
export StatusBot__ChannelId="123456789012345678" 
export StatusBot__PollIntervalSeconds="30"
```

### Hot Reload

StatusBot automatically reloads `config/config.json` when changed. No restart required for:
- Adding/removing services
- Changing poll intervals
- Updating service URLs/hosts

**Note**: Discord token and channel changes require a restart.

## Building & Deployment

### Build Scripts

StatusBot includes universal build scripts supporting multiple platforms and deployment scenarios:

#### Windows
```cmd
build.bat [options]
```

#### Linux/macOS
```bash
./build.sh [options]
```

#### Build Options

| Option | Description |
|--------|-------------|
| `--project <path>` | Project path (default: `./src`) |
| `--config <Debug\|Release>` | Build configuration (default: Release) |
| `--rids <rid1,rid2>` | Target platforms (default: win-x64,linux-x64,linux-arm64,osx-x64,osx-arm64) |
| `--self-contained` | Include .NET runtime (larger but portable) |
| `--single-file` | Create single executable (test compatibility first) |
| `--trim` | Reduce size by removing unused code (test thoroughly) |
| `--clean` | Remove previous build output |
| `--zip` | Create zip archives of published output |
| `--dry-run` | Show commands without executing |
| `--parallel` | Build multiple RIDs simultaneously (Linux/macOS only) |
| `--ci` | Disable interactive prompts for CI environments |

#### Examples

**Framework-dependent (recommended)**:
```bash
# Windows
build.bat --clean

# Linux
./build.sh --clean
```

**Self-contained for production**:
```bash
./build.sh --self-contained --clean --zip
```

**Single RID for testing**:
```bash
build.bat --rids win-x64 --config Debug --dry-run
```

**CI Pipeline**:
```bash
./build.sh --self-contained --clean --zip --ci --parallel
```

### Deployment

#### Framework-Dependent
1. Ensure .NET 8.0 Runtime is installed on target system
2. Copy published files to target directory
3. Run `StatusBot.exe` (Windows) or `./StatusBot` (Linux/macOS)

#### Self-Contained
1. Copy published files to target directory (includes runtime)
2. Run executable directly - no .NET installation required
3. Larger deployment but fully portable

#### Docker Deployment
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY published-files/ .
EXPOSE 8080
ENTRYPOINT ["./StatusBot"]
```

#### Systemd Service (Linux)
```ini
[Unit]
Description=StatusBot Service Monitor
After=network.target

[Service]
Type=notify
ExecStart=/opt/statusbot/StatusBot
WorkingDirectory=/opt/statusbot
User=statusbot
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
```

### Production Considerations

#### Security
- **Never commit Discord tokens**: Use environment variables or key vault
- **Run as dedicated user**: Don't run as root/administrator
- **Network access**: Only requires outbound HTTPS to Discord API
- **File permissions**: Needs write access to `config/` directory

#### Performance
- **Memory usage**: ~50MB baseline, scales with number of services
- **CPU usage**: Minimal during normal operation, spikes during polls
- **Disk I/O**: Atomic writes to `state.json` after each service update
- **Network**: HTTP client reuse minimizes connection overhead

#### Monitoring
- **Logs**: Structured JSON logging via Serilog
- **Health checks**: Monitor `/api/status` endpoint
- **File corruption**: Automatic backup and recovery of `state.json`
- **Rate limits**: Built-in Discord API protection

#### Scaling
- **Multiple instances**: Each needs unique Discord channel
- **Large service counts**: Consider splitting across multiple bots
- **High frequency**: Minimum 30-second poll intervals recommended

## API Reference

StatusBot exposes a REST API for programmatic status access:

### Endpoints

#### Get All Services Status
```http
GET /api/status
```

**Response**:
```json
{
  "MainSite": {
    "Online": true,
    "LastChecked": "2025-10-20T15:30:00Z",
    "LastChange": "2025-10-20T10:00:00Z", 
    "MonitoringSince": "2025-10-15T09:00:00Z",
    "UptimePercent": 99.2,
    "CumulativeUpSeconds": 432000.5,
    "TotalChecks": 1440
  }
}
```

#### Get Individual Service Status  
```http
GET /api/status/{serviceName}
```

**Response**: Single service object (same structure as above)

### Service Status Fields

| Field | Type | Description |
|-------|------|-------------|
| `Online` | bool | Current online status |
| `LastChecked` | DateTime | When service was last polled |
| `LastChange` | DateTime | When status last changed (up↔down) |
| `MonitoringSince` | DateTime | When monitoring began (survives restarts) |
| `UptimePercent` | double | Calculated uptime percentage |
| `CumulativeUpSeconds` | double | Total seconds observed online |
| `TotalChecks` | int | Number of polls performed |

## State Management

### Persistence
StatusBot maintains state in `config/state.json` with atomic writes and corruption protection:

```json
{
  "Version": 1,
  "MessageMetadata": {
    "MainSite": {
      "Id": 1234567890123456789,
      "LastUpdatedUtc": "2025-10-20T15:30:00Z"
    }
  },
  "Statuses": {
    "MainSite": {
      "Online": true,
      "LastChecked": "2025-10-20T15:30:00Z",
      "MonitoringSince": "2025-10-15T09:00:00Z",
      "CumulativeUpSeconds": 432000.5,
      "TotalChecks": 1440,
      "UptimePercent": 99.2
    }
  }
}
```

### State File Features
- **Atomic Updates**: Uses temp files with atomic replacement
- **Corruption Recovery**: Automatically backs up and recovers from corrupt files  
- **Absolute Paths**: Uses application base directory to avoid working directory issues
- **Thread Safety**: Synchronized access with retry logic for file locks
- **JSON Formatting**: Pretty-printed for manual inspection

### Inspecting State
**PowerShell**:
```powershell
Get-Content config/state.json | ConvertFrom-Json | ConvertTo-Json -Depth 5
```

**bash/zsh**:
```bash
cat config/state.json | jq '.'
```

## Troubleshooting

### Common Issues

#### Discord Bot Not Posting
1. **Check token**: Verify bot token is correct in config
2. **Check permissions**: Bot needs "Send Messages" and "Embed Links"
3. **Check channel**: Verify channel ID and bot has access
4. **Check logs**: Look for Discord API errors in console output

#### High Memory Usage
1. **Service count**: Each service uses ~2-5MB baseline memory
2. **Poll frequency**: Very frequent polls (< 30s) increase overhead  
3. **Log retention**: Serilog may retain logs in memory briefly
4. **Consider splitting**: Use multiple bot instances for 50+ services

#### State File Issues
- **File locks**: Antivirus may temporarily lock `state.json`
- **Permissions**: Ensure write access to `config/` directory
- **Corruption**: Automatic backup creates `.corrupt.*.bak` files
- **Missing file**: Recreated automatically from config on startup

#### Build/Publish Problems
- **Trimming failures**: Remove `--trim` flag for reflection-heavy builds
- **Single-file issues**: Test thoroughly, some libraries don't support it
- **Missing RID**: Add target platform to `--rids` parameter
- **Permission errors**: Run build script as administrator if needed

### Performance Tuning

#### Polling Optimization
```json
{
  "PollIntervalSeconds": 60,  // Recommended minimum: 30
  "Services": [
    // Prioritize critical services first in the array
    // Group by geographic region if monitoring global services
  ]
}
```

#### Resource Limits
- **Max services**: 100 per instance (practical limit)  
- **Min poll interval**: 30 seconds (Discord rate limiting)
- **Memory per service**: ~2-5MB depending on response size
- **Disk usage**: `state.json` grows ~1KB per service

### Logging

StatusBot uses structured logging with multiple levels:

```bash
# Enable verbose logging (development)
export StatusBot__Logging__Level="Debug"

# Production logging (errors and warnings only)  
export StatusBot__Logging__Level="Warning"
```

**Log locations**:
- Console output (default)
- Add Serilog sinks for files, databases, or external systems

### Debug Mode

Run with detailed logging to troubleshoot issues:

```bash
# Windows
set ASPNETCORE_ENVIRONMENT=Development
dotnet run --project src

# Linux/macOS  
export ASPNETCORE_ENVIRONMENT=Development
dotnet run --project src
```

## Advanced Usage

### Multiple Bot Instances
Deploy separate instances for different service groups:

```bash
# Production services
StatusBot --config /etc/statusbot/prod-config.json

# Development services  
StatusBot --config /etc/statusbot/dev-config.json
```

### Custom Service Types
Extend monitoring by modifying `StatusMonitor.cs`:

1. Add new service type to `ServiceDefinition.Type`
2. Implement check logic in `CheckServiceAsync`
3. Update configuration validation

### Integration Examples

#### Health Check Endpoint
```csharp
// Monitor StatusBot itself
services.AddHealthChecks()
    .AddCheck<StatusBotHealthCheck>("statusbot");
```

#### Prometheus Metrics
```csharp  
// Export uptime metrics
services.AddSingleton<IMetrics, PrometheusMetrics>();
```

#### Webhook Notifications
```csharp
// Send alerts on status changes
services.AddSingleton<INotificationService, WebhookNotifier>();
```

For detailed build system documentation, see `BUILDING.md`.

## Contributing

### Development Setup
1. Fork and clone the repository
2. Ensure .NET 8.0 SDK is installed  
3. Run tests: `dotnet test`
4. Make changes and test locally
5. Submit pull request with clear description

### Code Style
- Follow Microsoft C# coding conventions
- Add XML documentation for public APIs
- Include unit tests for business logic
- Use structured logging with Serilog

### Testing
```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test class
dotnet test --filter "UptimeCalculatorTests"
```

### Project Guidelines
- **Backward compatibility**: Maintain config file format compatibility
- **Error handling**: Graceful degradation and clear error messages
- **Documentation**: Update README for new features
- **Performance**: Profile changes with realistic service counts

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Support

- **Issues**: Report bugs and feature requests via GitHub Issues
- **Documentation**: See `BUILDING.md` for build system details
- **Security**: Report security vulnerabilities privately via GitHub Security

## Changelog

### Recent Improvements
- ✅ **Persistent uptime tracking** - Accurate calculations across restarts
- ✅ **Enhanced Discord embeds** - Server-local timestamps and better formatting  
- ✅ **Robust state management** - Atomic writes with corruption recovery
- ✅ **Rate limiting** - Discord API protection and per-message cooldowns
- ✅ **Structured logging** - Serilog integration with error handling
- ✅ **Universal build scripts** - Cross-platform publishing with extensive options
- ✅ **Comprehensive documentation** - XML docs and operational guides
- ✅ **Production hardening** - HTTP client reuse, file handling improvements

### Roadmap
- 🔄 **Health checks** - Built-in endpoint monitoring
- 🔄 **Metrics export** - Prometheus/OpenTelemetry integration  
- 🔄 **Container support** - Official Docker images
- 🔄 **Web dashboard** - Optional web UI for status visualization
- 🔄 **Alert channels** - Multiple Discord channels and webhook support