# StatusBot

Lightweight .NET 8 monitor that watches HTTP/TCP/ICMP endpoints and posts a single consolidated status embed to Discord.

Minimal, reliable, and easy to run.

---

## Features

- Checks: HTTP (2xx), TCP connect, ICMP (ping)
- Single consolidated Discord embed (prevents duplicate messages)
- Persistent uptime tracking across restarts
- Safe atomic state writes with corruption backups
## Highlights
- Single consolidated Discord embed (prevents duplicate messages)
- Persistent uptime tracking across restarts (state stored in `config/state.json`)
- Bot presence configurable via `PresenceText` (falls back to first HTTP host)
- Minimal REST API: `/api/status` and `/api/status/{service}` (default bind: `http://0.0.0.0:4130`)
- Safe file writes (atomic replace), corruption backups, and robust retry/cooldown logic

## Quick start

1. Edit `config/config.json` (first run will create `config/` if missing).
2. Build and run (development):

```powershell
dotnet build src -c Release
dotnet run --project src
```

To change the API bind, set `ASPNETCORE_URLS` (example):

```powershell
# $env:ASPNETCORE_URLS = "http://0.0.0.0:4130"  # PowerShell example
dotnet run --project src
```

## API examples

List all statuses:

```bash
curl http://localhost:4130/api/status
```

Get one service:

```bash
curl http://localhost:4130/api/status/MainSite
```

## Configuration (minimal)

Add `PresenceText` if you want a custom presence string shown by the bot. If left empty the bot will attempt to use the host from the first HTTP service.

```json
{
  "Token": "YOUR_DISCORD_BOT_TOKEN",
  "ChannelId": 123456789012345678,
  "PollIntervalSeconds": 60,
  "PresenceText": "Monitoring nobyl.net",
  "Services": [
    { "Name": "MainSite", "Type": "HTTP", "Url": "https://example.com" },
    { "Name": "API", "Type": "TCP", "Host": "api.example.com", "Port": 443 }
  ]
}
```

Environment overrides (example):

```powershell
StatusBot__Token=your_bot_token
StatusBot__ChannelId=123456789012345678
ASPNETCORE_URLS="http://0.0.0.0:4130"
StatusBot__AllowedOrigins="http://localhost:3000"
```

## State & migration notes

- State file: `config/state.json` (atomic writes + `.corrupt.*.bak` backups)
- Schema v2: single `StatusMessageId` and `Version` field. This replaces older per-service message tracking.
- Upgrading from older releases: deleting `config/state.json` is safe — the bot will recreate or discover the single dashboard message on startup.

## Build & publish (examples)

Self-contained Windows x64 (includes .NET runtime):

```powershell
dotnet publish src -c Release -r win-x64 --self-contained true -o .\\publish\\win-x64
```

Framework-dependent (requires .NET on target):

```powershell
dotnet publish src -c Release -o .\\publish\\framework
```

Notes: avoid trimming (`-p:PublishTrimmed=true`) unless you test thoroughly — Discord.Net and Newtonsoft.Json rely on reflection.

## Troubleshooting

- Bot not posting: verify `Token`, `ChannelId`, and bot permissions (Send Messages, Embed Links).
- Duplicate messages: delete old status messages and restart; the bot will find/create a single dashboard message.
- Missing services in embed: check `config/config.json` and `/api/status` to confirm the monitor has loaded all services.

If you want the presence to include the number of services (e.g. "Monitoring 5 services") or templated text (`{count}`, `{host}`), I can add simple template replacement for `PresenceText`.

## License

MIT

---

## API (examples)

- List all services

```bash
curl http://localhost:4130/api/status
```

- Get a single service

```bash
curl http://localhost:4130/api/status/MainSite
```

---

## Minimal sample `config/config.json`

```json
{
  "Token": "YOUR_DISCORD_BOT_TOKEN",
  "ChannelId": 123456789012345678,
  "PollIntervalSeconds": 60,
  "Services": [
    { "Name": "MainSite", "Type": "HTTP", "Url": "https://example.com" },
    { "Name": "API", "Type": "TCP", "Host": "api.example.com", "Port": 443 }
  ]
}
```

---

## Notes

- If you upgrade from an older release that stored per-service message IDs, you can delete `config/state.json`; the bot will recreate or discover the consolidated message.
- For cross-origin dashboards, set `StatusBot__AllowedOrigins` or `ASPNETCORE_URLS`.

---

## License

MIT
