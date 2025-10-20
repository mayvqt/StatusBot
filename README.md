# StatusBot

StatusBot monitors services (HTTP/TCP/ICMP) and posts status updates to Discord. It also exposes a small HTTP API to retrieve current status.

This repository contains a minimal, production-oriented .NET 8 background service with utilities for configuration, persistence, and publishing messages to Discord.

Quick features
- Monitor HTTP, TCP and ICMP (ping) endpoints
- Post and update status messages in a Discord channel
- Persist state (message IDs, previous statuses) to disk
- Expose a basic HTTP API at `/api/status` and `/api/status/{service}`
- Cross-platform publish scripts for Windows/Linux/macOS
- Basic runtime resilience and error handling added across the codebase

Files of interest
- `src/` — application source (Program, Services, Models)
- `build.bat`, `build.sh` — cross-platform publish scripts (see `BUILDING.md`)
- `BUILDING.md` — quick build/publish reference

Getting started (development)
1. Ensure .NET 8 SDK is installed on your machine.
2. Restore/build from the repo root:

```powershell
dotnet build src
```

3. Run locally (development config):

```powershell
dotnet run --project src
```

Production publish (recommended)
Use the included scripts to produce artifacts for your target OS. The scripts publish to a `build/<project-name>/<config>/net8.0/<rid>/publish` folder next to the script, making artifacts easy to package.

Examples:

Windows (framework-dependent):

```powershell
build.bat
```

Linux (framework-dependent):

```bash
./build.sh
```

Self-contained single-file (test before use):

```bash
./build.sh --self-contained --single-file --trim --clean
```

CI and containers
- To run inside CI, remove any interactive pauses and call `build.sh` in your pipeline.
- For containers, prefer a multi-stage Dockerfile (build in SDK image, run from runtime image). I can add a Dockerfile if you want.

Configuration and secrets
- The sample config is under `src/config/config.json` (created by `SetupHelper` on first run).
- Do NOT commit real Discord tokens. Use environment variables or a secret manager for production.
- You can override values via environment variables or by editing `config/config.json` on the host.

Troubleshooting and tips
- If publish fails for `PublishTrimmed` or `PublishSingleFile` builds, try removing those flags — reflection-heavy libraries (Discord.NET) may break when trimmed.
- If `state.json` can't be replaced due to concurrent writes, the app performs a retry fallback and logs warnings. Ensure no other process is locking the file.

Next improvements (recommended)
- Integrate structured logging (Serilog + sinks)
- Add health checks and Prometheus metrics
- Add CI workflow (GitHub Actions) to build and optionally push images
- Add unit tests for Persistence and ConfigManager

See `BUILDING.md` for detailed publish script usage and examples.

Inspecting state.json
---------------------

StatusBot persists runtime state in a `state.json` file next to the running process. Recent versions store a few helpful pieces of information you can inspect when diagnosing issues:

- `MessageMetadata`: a dictionary (service name -> object) containing the last Discord message info for each service. Each entry looks like:
	- `Id` (ulong) — the Discord message id that StatusBot posts/updates for a service
	- `LastUpdatedUtc` (ISO UTC timestamp) — when the bot last successfully updated that message

- `Statuses`: a dictionary (service name -> object) containing monitoring information. Important fields:
	- `Online` (bool) — last observed online state
	- `LastChange` (UTC timestamp) — when the status last flipped (up↔down)
	- `LastChecked` (UTC timestamp) — when the service was last polled
	- `MonitoringSince` (UTC timestamp) — when StatusBot began tracking this service (persisted so uptime survives restarts)
	- `CumulativeUpSeconds` (number) — total seconds observed "up" since `MonitoringSince`
	- `UptimePercent` (number) — calculated as CumulativeUpSeconds / (now - MonitoringSince) * 100



How to inspect
- From a shell you can pretty-print the file (example using PowerShell):

```powershell
Get-Content state.json | ConvertFrom-Json | Select-Object -Property MessageMetadata, Statuses | ConvertTo-Json -Depth 5
```

- Or open `state.json` in any editor (it's pretty-printed by the application) and look for the fields above.

If you need help interpreting a particular entry, paste the relevant small JSON snippet and I can explain what it means.