# Multi-stage Dockerfile for StatusBot
# Builds the app using the .NET SDK and produces a small runtime image.

#############################################
# Build stage
#############################################
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore first to leverage Docker layer caching
COPY src/StatusBot.csproj ./
RUN dotnet restore ./StatusBot.csproj

# Copy rest of repo
COPY . ./

# Publish a linux-x64 self-contained build suitable for Docker Hub (single platform image)
RUN dotnet publish src/StatusBot.csproj -c Release -r linux-x64 --self-contained true -o /app/publish --no-restore

#############################################
# Runtime image (self-contained runtime)
#############################################
FROM mcr.microsoft.com/dotnet/runtime-deps:8.0 AS runtime
WORKDIR /app

# Copy published output from build stage
COPY --from=build /app/publish ./

# Create a place for defaults and for host-mounted config
RUN mkdir -p /app/config /app/defaults && chown -R 1000:1000 /app

# Add small default config files so first-run has sensible values.
# These will be copied into /app/config at container start only if the host did not mount one.
RUN printf '{\n  "Token": "YOUR_DISCORD_BOT_TOKEN",\n  "ChannelId": 123456789012345678,\n  "PresenceText": "",\n  "PollIntervalSeconds": 60,\n  "Services": [ { "Name": "MainSite", "Type": "HTTP", "Url": "https://example.com" } ]\n}\n' > /app/defaults/config.json
RUN printf '{\n  "Version": "2",\n  "Statuses": {}\n}\n' > /app/defaults/state.json

# Expose default API port used by ApiHost (default 4130)
EXPOSE 4130

# Make an entrypoint script that will populate /app/config from /app/defaults if empty
COPY --chown=1000:1000 --from=build /src/ /src/
RUN printf '#!/bin/sh\nset -e\n# If /app/config is empty, copy defaults so users get a starting config they can edit.\nif [ -d "/app/config" ]; then\n  if [ -z "$(ls -A /app/config)" ]; then\n    echo "Config directory empty — copying default config"\n    cp -a /app/defaults/. /app/config/\n    chown -R 1000:1000 /app/config\n  fi\nelse\n  mkdir -p /app/config\n  cp -a /app/defaults/. /app/config/\n  chown -R 1000:1000 /app/config\nfi\n\n# Exec the app as user 1000\nexec /app/StatusBot "$@"\n' > /app/entrypoint.sh
RUN chmod +x /app/entrypoint.sh

# Mark config as a volume so host can mount/edit it
VOLUME ["/app/config"]

# Create a non-root user with consistent UID so file ownership is predictable
RUN adduser -u 1000 -D statusbot || true
USER 1000

# Entrypoint: run the entrypoint which will ensure config defaults are present then start the app
ENTRYPOINT ["/app/entrypoint.sh"]

# Notes:
# - This Dockerfile produces a linux-x64 self-contained image (uses --self-contained on publish)
#   which is suitable for pushing to Docker Hub and running on standard Linux hosts.
# - The image contains `/app/defaults/config.json` and `/app/defaults/state.json`.
#   On container start the entrypoint copies those files into `/app/config` if the folder is empty.
#   Mount a host folder to `/app/config` to edit/inspect configuration and state files persistently.