#!/usr/bin/env bash
# Cross-project build helper for Linux/macOS
# Usage: ./build.sh [--self-contained] [--single-file] [--trim] [--clean] [--config <Debug|Release>] [--project <path>]
#
# Options:
#   --self-contained   Produce self-contained publish (bundles .NET runtime)
#   --single-file      Produce a single-file executable (may break reflection-heavy libs)
#   --trim             Enable publish trimming (smaller output; test carefully)
#   --clean            Remove previous build/<project> output before publishing
#   --config <name>    Configuration to publish (Debug or Release). Default: Release
#   --project <path>   Path to .csproj or project directory. Defaults to ./src or current dir
#   --rids <list>      Comma-separated list of runtime identifiers to build for
#   --zip              Create ZIP archives of published applications
#   --dry-run          Show commands without executing them
#   --parallel         Build multiple targets concurrently
#   --ci / --no-pause  CI mode (no interactive pauses)

set -euo pipefail

# Default options
SELF_CONTAINED=false
SINGLE_FILE=false
TRIM=false
CLEAN=true
ZIP=false
DRY_RUN=false
PARALLEL=false
CI=false
CONFIG=Release
PROJECT=""
RIDS="win-x64,linux-x64,linux-arm64,osx-x64,osx-arm64"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --self-contained)
      SELF_CONTAINED=true; shift;;
    --single-file)
      SINGLE_FILE=true; shift;;
    --trim)
      TRIM=true; shift;;
    --config)
      if [[ -z "${2:-}" ]]; then
        echo "Missing value for --config"
        exit 1
      fi
      CONFIG="$2"; shift 2;;
    --project)
      if [[ -z "${2:-}" ]]; then
        echo "Missing value for --project"
        exit 1
      fi
      PROJECT="$2"; shift 2;;
    --clean)
      CLEAN=true; shift;;
    --rids)
      if [[ -z "${2:-}" ]]; then
        echo "Missing value for --rids"
        exit 1
      fi
      RIDS="$2"; shift 2;;
    --zip)
      ZIP=true; shift;;
    --dry-run)
      DRY_RUN=true; shift;;
    --parallel)
      PARALLEL=true; shift;;
    --ci|--no-pause)
      CI=true; shift;;
    *)
      echo "Unknown argument: $1"; exit 1;;
  esac
done


# Determine script dir and default project if not provided
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)"

if [[ -z "$PROJECT" ]]; then
  if [[ -d "$SCRIPT_DIR/src" ]]; then
    PROJECT="$SCRIPT_DIR/src"
  else
    PROJECT="$PWD"
  fi
fi

# Resolve publish target: if PROJECT is a .csproj use it; if dir, find first .csproj inside
PUBLISH_TARGET="$PROJECT"
CS_PROJ_PATH=""

if [[ -f "$PROJECT" ]]; then
  # Check if project is a file ending in .csproj
  if [[ "$PROJECT" == *.csproj ]]; then
    CS_PROJ_PATH="$(cd "$(dirname "$PROJECT")" && pwd)/$(basename "$PROJECT")"
  fi
fi

if [[ -z "$CS_PROJ_PATH" ]]; then
  # Look for a csproj inside the directory
  if [[ -d "$PROJECT" ]]; then
    csproj=$(ls "$PROJECT"/*.csproj 2>/dev/null | head -n 1 || true)
    if [[ -n "$csproj" ]]; then
      CS_PROJ_PATH="$csproj"
    fi
  fi
fi

if [[ -n "$CS_PROJ_PATH" ]]; then
  PUBLISH_TARGET="$CS_PROJ_PATH"
  PROJECT_NAME="$(basename "${CS_PROJ_PATH%.*}")"
else
  PROJECT_NAME="$(basename "$PROJECT")"
fi

echo "Project publish target: $PUBLISH_TARGET"
echo "Project name: $PROJECT_NAME"
echo "Output base folder: $SCRIPT_DIR/build/$PROJECT_NAME"

if [[ "$CLEAN" == true ]]; then
  echo "Cleaning output folder: $SCRIPT_DIR/build/$PROJECT_NAME ..."
  if [[ -d "$SCRIPT_DIR/build/$PROJECT_NAME" ]]; then
    if ! rm -rf "$SCRIPT_DIR/build/$PROJECT_NAME"; then
      echo "Warning: failed to remove existing build folder."
    else
      echo "Cleaned previous build output."
    fi
  else
    echo "No existing build folder to remove."
  fi
fi

# Build options assembled once
OPTS=()
if [[ "$SELF_CONTAINED" == "true" ]]; then
  OPTS+=(--self-contained true)
fi
if [[ "$SINGLE_FILE" == "true" ]]; then
  OPTS+=("-p:PublishSingleFile=true")
fi
if [[ "$TRIM" == "true" ]]; then
  OPTS+=("-p:PublishTrimmed=true")
fi

# RIDs to publish for
# Convert comma-separated RIDS into array
IFS=',' read -r -a RIDS_ARRAY <<< "$RIDS"

publish_one() {
  local RID="$1"
  local OUTDIR="$SCRIPT_DIR/build/$PROJECT_NAME/$CONFIG/net8.0/$RID/publish"
  echo ""
  echo "Publishing for $RID to $OUTDIR"
  echo "Running: dotnet publish \"$PUBLISH_TARGET\" -c \"$CONFIG\" -r \"$RID\" -o \"$OUTDIR\" ${OPTS[*]}"
  
  if [[ "$DRY_RUN" == "true" ]]; then
    echo "Dry-run: skipping dotnet publish for $RID"
    return 0
  fi
  
  if ! dotnet publish "$PUBLISH_TARGET" -c "$CONFIG" -r "$RID" -o "$OUTDIR" "${OPTS[@]}"; then
    echo "Publish for $RID failed with exit code $?"
    exit $?
  fi

  if [[ "$ZIP" == "true" ]]; then
    if [[ "$DRY_RUN" == "true" ]]; then
      echo "Dry-run: would zip $OUTDIR to $SCRIPT_DIR/build/$PROJECT_NAME/$CONFIG/$RID.zip"
    else
      echo "Creating zip for $RID"
      mkdir -p "$SCRIPT_DIR/build/$PROJECT_NAME/$CONFIG"
      (cd "$OUTDIR" && zip -r "$SCRIPT_DIR/build/$PROJECT_NAME/$CONFIG/$RID.zip" .)
    fi
  fi
}

if [[ "$PARALLEL" == "true" ]]; then
  echo "Note: Running parallel builds for ${#RIDS_ARRAY[@]} targets"
fi

pids=()
for RID in "${RIDS_ARRAY[@]}"; do
  if [[ "$PARALLEL" == "true" ]]; then
    publish_one "$RID" &
    pids+=("$!")
  else
    publish_one "$RID"
  fi
done

if [[ "$PARALLEL" == "true" && ${#pids[@]} -gt 0 ]]; then
  echo "Waiting for ${#pids[@]} background publish jobs..."
  for pid in "${pids[@]}"; do
    if ! wait "$pid"; then
      echo "One or more parallel builds failed"
      exit 1
    fi
  done
fi

echo "All publishes completed."
