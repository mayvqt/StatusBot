Build scripts
-------------

This repository includes two helper scripts to produce publish artifacts for multiple platforms:

- `build.bat` — Windows (cmd / PowerShell friendly)
- `build.sh` — Linux / macOS (bash)

These scripts auto-detect the project (look for `src/*.csproj` or accept `--project <path>`) and publish into a repository-root `build/<project-name>/<config>/net8.0/<rid>/publish` folder. This makes packaging and CI simpler.

Common options

- `--self-contained`   Produce self-contained publish (bundles .NET runtime)
- `--single-file`      Produce a single-file executable (may break reflection-heavy libs)
- `--trim`             Enable publish trimming (smaller output; test carefully)
- `--clean`            Remove previous `build/<project-name>` output before publishing
- `--config <name>`    Configuration to publish (Debug or Release). Default: Release
- `--project <path>`   Path to .csproj or project directory. Defaults to `./src` if present, otherwise current dir

Additional options

- `--rids <list>`      Comma-separated RIDs to build (e.g. `--rids linux-x64,osx-x64`). Defaults to a common set.
- `--zip`              Create a zip archive for each RID publish under `build/<project>/<config>/<rid>.zip`.
- `--dry-run`          Show commands without executing them.
- `--parallel`         Run publishes in parallel (only implemented in `build.sh`; `build.bat` will run sequentially).
- `--ci` or `--no-pause`  Run in CI mode (no interactive pause at the end).

Examples

Windows (framework-dependent):

    build.bat

Clean and publish self-contained single-file trimmed for Linux x64:

    ./build.sh --self-contained --single-file --trim --clean

Run only specific RIDs and create zips (dry-run):

    ./build.sh --rids linux-x64,osx-x64 --zip --dry-run

Parallel publishes (bash only):

    ./build.sh --parallel

Output

Published artifacts are placed under:

    build/<project-name>/<config>/net8.0/<rid>/publish

Notes

- Trimming/single-file may break reflection-heavy libraries (e.g., Discord.NET). Test thoroughly before enabling trimming.
- Use `--self-contained` if you want to run without installing .NET on the target host.
- `--clean` removes the `build/<project-name>` folder before publishing.

