Build scripts
-------------

This repository includes two helper scripts to produce publish artifacts for multiple platforms:

- `build.bat` — Windows (cmd / PowerShell friendly)
- `build.sh` — Linux / macOS (bash)

Basic usage examples:

Windows (framework-dependent builds):

    build.bat

Linux (self-contained single-file, trimmed — test thoroughly):

    ./build.sh --self-contained --single-file --trim

Scripts publish to `src/bin/<Configuration>/net8.0/<rid>/publish`.

Notes:
- Trimming/single-file may break reflection-heavy libraries. Test thoroughly before enabling.
- Use the `--self-contained` flag if you want a bundle that doesn't require a pre-installed .NET runtime on the host.
