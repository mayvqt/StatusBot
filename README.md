# StatusBot

Lightweight .NET 8 monitor that watches HTTP/TCP/ICMP endpoints and posts a single consolidated status embed to Discord.

Minimal, reliable, and easy to run.

---

## Features

- Checks: HTTP (2xx), TCP connect, ICMP (ping)
- Single consolidated Discord embed (prevents duplicate messages)
- Persistent uptime tracking across restarts
- Safe atomic state writes with corruption backups
- Built-in minimal REST API: `/api/status` and `/api/status/{service}`

---

## Quick start

1. Edit `config/config.json` (the first run will create `config/` if missing).
2. Build and run:

```powershell
dotnet build src
dotnet run --project src
```

By default the API binds to `http://0.0.0.0:4130` unless overridden via `ASPNETCORE_URLS`.

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
