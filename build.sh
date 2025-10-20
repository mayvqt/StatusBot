done
#!/usr/bin/env bash
# Cross-project build helper for Linux/macOS
# Usage: ./build.sh [--self-contained] [--single-file] [--trim] [--clean] [--config Debug|Release] [--project <path>]
#
# Options:
#   --self-contained   Produce self-contained publish (bundles .NET runtime)
#   --single-file      Produce a single-file executable (may break reflection-heavy libs)
#   --trim             Enable publish trimming (smaller output; test carefully)
#   --clean            Remove previous build/<project> output before publishing
#   --config <name>    Configuration to publish (Debug or Release). Default: Release
#   --project <path>   Path to .csproj or project directory. Defaults to ./src or current dir

set -euo pipefail
SELF_CONTAINED=false
SINGLE_FILE=false
TRIM=false
CLEAN=false
ZIP=false
DRY_RUN=false
PARALLEL=false
CI=false
CONFIG=Release
PROJECT=""
RIDS=(win-x64 linux-x64 linux-arm64 osx-x64 osx-arm64)

while [[ $# -gt 0 ]]; do
  case "$1" in
    --self-contained)
      SELF_CONTAINED=true; shift;;
    --single-file)
      SINGLE_FILE=true; shift;;
    --trim)
      TRIM=true; shift;;
    --config)
      CONFIG="$2"; shift 2;;
    --project)
      PROJECT="$2"; shift 2;;
    --clean)
      CLEAN=true; shift;;
    --rids)
      IFS=',' read -r -a RIDS <<< "$2"; shift 2;;
    --zip)
      ZIP=true; shift;;
    --dry-run)
      DRY_RUN=true; shift;;
    --parallel)
      PARALLEL=true; shift;;
    --ci|--no-pause)
      CI=true; shift;;
    *)
      echo "Unknown arg: $1"; exit 1;;
  esac


# Determine script dir
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)"

if [[ -z "$PROJECT" ]]; then
  if [[ -d "$SCRIPT_DIR/src" ]]; then
    PROJECT="$SCRIPT_DIR/src"
  else
    PROJECT="$PWD"
  fi
fi

# If PROJECT is a directory, find a .csproj inside it
PUBLISH_TARGET="$PROJECT"
CS_PROJ_PATH=""
if [[ -d "$PROJECT" ]]; then
  csproj=$(ls "$PROJECT"/*.csproj 2>/dev/null | head -n 1 || true)
  if [[ -n "$csproj" ]]; then
    CS_PROJ_PATH="$csproj"
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
  if [[ -d "$SCRIPT_DIR/build/$PROJECT_NAME" ]]; then
    echo "Cleaning output folder: $SCRIPT_DIR/build/$PROJECT_NAME ..."
    rm -rf "$SCRIPT_DIR/build/$PROJECT_NAME"
    echo "Cleaned previous build output."
  else
    echo "No existing build folder to remove."
  fi
fi

# Build options
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

# RIDs to produce
RIDS=(win-x64 linux-x64 linux-arm64 osx-x64 osx-arm64)

pids=()
for RID in "${RIDS[@]}"; do
  OUTDIR="$SCRIPT_DIR/build/$PROJECT_NAME/$CONFIG/net8.0/$RID/publish"
  echo ""
  echo "Publishing for $RID -> $OUTDIR"
  CMD=(dotnet publish "$PUBLISH_TARGET" -c "$CONFIG" -r "$RID" -o "$OUTDIR" "${OPTS[@]}")
  echo "> ${CMD[*]}"
  if [[ "$DRY_RUN" == true ]]; then
    echo "Dry-run: skipping publish for $RID"
  else
    if [[ "$PARALLEL" == true ]]; then
      ("${CMD[@]}") &
      pids+=("$!")
    else
      "${CMD[@]}"
    fi
  fi

  if [[ "$ZIP" == true ]]; then
    if [[ "$DRY_RUN" == true ]]; then
      echo "Dry-run: would zip $OUTDIR to $SCRIPT_DIR/build/$PROJECT_NAME/$CONFIG/$RID.zip"
    else
      mkdir -p "$SCRIPT_DIR/build/$PROJECT_NAME/$CONFIG"
      if [[ "$PARALLEL" == true && "$DRY_RUN" == false ]]; then
        (zip -r "$SCRIPT_DIR/build/$PROJECT_NAME/$CONFIG/$RID.zip" "$OUTDIR" > /dev/null 2>&1) &
      else
        zip -r "$SCRIPT_DIR/build/$PROJECT_NAME/$CONFIG/$RID.zip" "$OUTDIR"
      fi
    fi
  fi
done

if [[ "$PARALLEL" == true && ${#pids[@]} -gt 0 ]]; then
  echo "Waiting for background publish jobs..."
  for pid in "${pids[@]}"; do
    wait "$pid"
  done
fi

echo "All publishes completed."

echo "All publishes completed."
