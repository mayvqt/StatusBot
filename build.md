
# Build Plan: Discord Service Status Bot (.NET 8 LTS, API Only)

A cross-platform .NET 8 console app that monitors service availability, posts live embeds to Discord, and exposes a JSON API for external access.

---

## Core Goals

- Monitor a dynamic list of services (HTTP, TCP, ICMP).
- Post one embed per service in Discord (auto-create, update, skip if unchanged).
- Expose REST API returning live status JSON.
- Update every 60 seconds (configurable).
- Configurable via `config.json`.
- State persistence (`state.json`) for message IDs and uptime tracking.
- Cross-platform: Windows, Linux, macOS.

---

## Technology Stack

- **.NET 8 LTS Console App**
- **Discord.Net** (stable client library)
- **ASP.NET Core Minimal API** (`/api/status`)
- **Newtonsoft.Json** (config/state persistence)
- **System.Net.Http**, **System.Net.NetworkInformation**, **System.Net.Sockets**
- **Microsoft.Extensions.Hosting** (service orchestration)

---

## Recommended Folder Structure

```
ServiceStatusBot/
├── src/
│   ├── Program.cs
│   ├── Models/
│   │   ├── Config.cs
│   │   ├── ServiceDefinition.cs
│   │   ├── ServiceStatus.cs
│   │   ├── State.cs
│   ├── Services/
│   │   ├── StatusMonitor.cs
│   │   ├── DiscordUpdater.cs
│   │   ├── ApiHost.cs
│   │   ├── Persistence.cs
│   │   ├── ConfigManager.cs
│   └── ServiceStatusBot.csproj
├── config.json
├── state.json
└── README.md
```

---

## Configuration Files

### config.json
Stores global settings and monitored services.
```json
{
  "Token": "DISCORD_BOT_TOKEN",
  "ChannelId": 123456789012345678,
  "PollIntervalSeconds": 60,
  "Services": [
    { "Name": "MainSite", "Type": "HTTP", "Url": "https://example.com" },
    { "Name": "API", "Type": "TCP", "Host": "api.example.com", "Port": 443 },
    { "Name": "DNS", "Type": "ICMP", "Host": "8.8.8.8" }
  ]
}
```

### state.json
Stores message IDs and uptime history.
```json
{

  ---

  ## Configuration Files

  ### `config.json`
  Stores global settings and monitored services.
  ```json
  {
    "Token": "DISCORD_BOT_TOKEN",
    "ChannelId": 123456789012345678,
    "PollIntervalSeconds": 60,
    "Services": [
      { "Name": "MainSite", "Type": "HTTP", "Url": "https://example.com" },
      { "Name": "API", "Type": "TCP", "Host": "api.example.com", "Port": 443 },
      { "Name": "DNS", "Type": "ICMP", "Host": "8.8.8.8" }
    ]
  }
  ```

  ### `state.json`
  Stores message IDs and uptime history.
  ```json
  {
    "Messages": { "MainSite": 0, "API": 0 },
    "Statuses": {
      "MainSite": { "Online": true, "LastChange": "2025-01-01T00:00:00Z", "Uptime": 99.8 },
      "API": { "Online": false, "LastChange": "2025-01-01T12:00:00Z", "Uptime": 97.4 }
    }
  }
  ```

  ---

  ## Component Overview

  ### Program.cs
  - Entry point.
  - Uses `Host.CreateDefaultBuilder()`.
  - Registers all services (singleton/background).
  - Runs hosted services concurrently.

  ### ConfigManager
  - Loads `config.json` at startup.
  - Watches for file changes (hot reload).
  - Notifies other services on config updates.

  ### StatusMonitor
  - Polls each service on interval:
    - HTTP: `HttpClient.GetAsync()`
    - TCP: `TcpClient.ConnectAsync()`
    - ICMP: `Ping.SendPingAsync()`
  - Updates `StatusStore` and persists to `state.json`.

  ### DiscordUpdater
  - Ensures one embed per service.
  - Updates/creates Discord messages as needed.
  - Stores message IDs in `state.json`.

  ### Persistence
  - Manages `state.json` (atomic writes, concurrency-safe).
  - Loads state at startup.

  ### ApiHost
  - Minimal ASP.NET Core API.
  - Routes:
    - `GET /api/status` (all statuses)
    - `GET /api/status/{service}` (single service)
  - Uses injected `StatusStore`.

  ### StatusStore
  - Thread-safe singleton (`ConcurrentDictionary<string, ServiceStatus>`).
  - Central source for current service state.

  ---

  ## API Response Example

  `GET /api/status`
  ```json
  {
    "MainSite": {
      "Online": true,
      "LastChange": "2025-01-01T00:00:00Z",
      "LastChecked": "2025-10-15T10:00:00Z",
      "UptimePercent": 99.92
    },
    "API": {
      "Online": false,
      "LastChange": "2025-10-15T09:45:00Z",
      "LastChecked": "2025-10-15T10:00:00Z",
      "UptimePercent": 96.30
    }
  }
  ```

  ---

  ## Lifecycle

  **Startup**
  - Load config and state.
  - Start API, Discord, and checker services.

  **Loop**
  - Every minute: check statuses, update state, push to Discord if changed.
  - API always serves live data.

  **Shutdown**
  - Flush state to disk.
  - Dispose resources cleanly.

  ---

  ## Suggested Upgrades

  - Add logging (structured, per service).
  - Add health check endpoint (`/api/health`).
  - Support for service groups/tags.
  - Retry logic for Discord/API failures.
  - Configurable alerting (webhook, email).
  - Unit/integration tests for core services.
  - Dockerfile for containerized deployment.

  ---
