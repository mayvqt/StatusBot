@echo off
REM Cross-project build helper for Windows (cmd.exe / PowerShell)
REM Usage: build.bat [--self-contained] [--single-file] [--trim] [--clean] [--config <Debug|Release>] [--project <path>]
REM
REM Options:
REM   --self-contained   Produce self-contained publish (bundles .NET runtime)
REM   --single-file      Produce a single-file executable (may break reflection-heavy libs)
REM   --trim             Enable publish trimming (smaller output; test carefully)
REM   --clean            Remove previous build/<project> output before publishing
REM   --config <name>    Configuration to publish (Debug or Release). Default: Release
REM   --project <path>   Path to .csproj or project directory. Defaults to ./src or current dir
REM
setlocal EnableDelayedExpansion

:: (No delegation) This batch file is the canonical Windows build wrapper.

:: Default options
set "SELF_CONTAINED=false"
set "SINGLE_FILE=false"
set "TRIM=false"
set "CLEAN=true"
set "ZIP=false"
set "DRY_RUN=false"
set "PARALLEL=false"
set "CI=false"
set "CONFIG=Release"
set "PROJECT="
set "RIDS=win-x64 linux-x64 linux-arm64 osx-x64 osx-arm64"

:parse_args
if "%~1"=="" goto end_parse
if "%~1"=="--self-contained" (
    set "SELF_CONTAINED=true" & shift & goto parse_args
)
if "%~1"=="--single-file" (
    set "SINGLE_FILE=true" & shift & goto parse_args
)
if "%~1"=="--trim" (
    set "TRIM=true" & shift & goto parse_args
)
if "%~1"=="--clean" (
    set "CLEAN=true" & shift & goto parse_args
)
if "%~1"=="--rids" (
    if "%~2"=="" (
        echo Missing value for --rids
        exit /b 1
    )
    set "RIDS=%~2" & shift & shift & goto parse_args
)
if "%~1"=="--zip" (
    set "ZIP=true" & shift & goto parse_args
)
if "%~1"=="--dry-run" (
    set "DRY_RUN=true" & shift & goto parse_args
)
if "%~1"=="--parallel" (
    set "PARALLEL=true" & shift & goto parse_args
)
if "%~1"=="--ci" (
    set "CI=true" & shift & goto parse_args
)
if "%~1"=="--no-pause" (
    set "CI=true" & shift & goto parse_args
)
if "%~1"=="--config" (
    if "%~2"=="" (
        echo Missing value for --config
        exit /b 1
    )
    set "CONFIG=%~2" & shift & shift & goto parse_args
)
if "%~1"=="--project" (
    if "%~2"=="" (
        echo Missing value for --project
        exit /b 1
    )
    set "PROJECT=%~2" & shift & shift & goto parse_args
)
echo Unknown argument: %~1
exit /b 1

:end_parse

:: Determine script dir and default project if not provided
set "SCRIPT_DIR=%~dp0"
if "%PROJECT%"=="" (
    if exist "%SCRIPT_DIR%src\" (
        set "PROJECT=%SCRIPT_DIR%src"
    ) else (
        set "PROJECT=%CD%"
    )
)

:: Resolve publish target: if PROJECT is a .csproj use it; if dir, find first .csproj inside
set "PUBLISH_TARGET=%PROJECT%"
set "CS_PROJ_PATH="
if exist "%PROJECT%" (
    rem check if project is a file ending in .csproj
    for %%F in ("%PROJECT%") do (
        if /I "%%~xF"==".csproj" (
            set "CS_PROJ_PATH=%%~fF"
        )
    )
)
if not defined CS_PROJ_PATH (
    rem look for a csproj inside the directory
    for %%F in ("%PROJECT%\*.csproj") do (
        set "CS_PROJ_PATH=%%~fF"
        goto found_csproj
    )
)
:found_csproj

if defined CS_PROJ_PATH (
    set "PUBLISH_TARGET=%CS_PROJ_PATH%"
    for %%N in ("%CS_PROJ_PATH%") do set "PROJECT_NAME=%%~nN"
else
    for %%D in ("%PROJECT%") do set "PROJECT_NAME=%%~nD"
)

echo Project publish target: %PUBLISH_TARGET%
echo Project name: %PROJECT_NAME%
echo Output base folder: %SCRIPT_DIR%build\%PROJECT_NAME%
if "%CLEAN%"=="true" (
    echo Cleaning output folder: %SCRIPT_DIR%build\%PROJECT_NAME% ...
    if exist "%SCRIPT_DIR%build\%PROJECT_NAME%" (
        rmdir /s /q "%SCRIPT_DIR%build\%PROJECT_NAME%"
        if ERRORLEVEL 1 (
            echo Warning: failed to remove existing build folder.
        ) else (
            echo Cleaned previous build output.
        )
    ) else (
        echo No existing build folder to remove.
    )
)

:: Build options assembled once
set "OPTS="
if "%SELF_CONTAINED%"=="true" set "OPTS=%OPTS% --self-contained true"
if "%SINGLE_FILE%"=="true" set "OPTS=%OPTS% -p:PublishSingleFile=true"
if "%TRIM%"=="true" set "OPTS=%OPTS% -p:PublishTrimmed=true"

:: RIDs to publish for
:: Expand comma-separated RIDS into tokens and call a subroutine to avoid nested-paren parsing issues
set "_rids=%RIDS%"
set "_rids=!_rids:,= !"

if "%PARALLEL%"=="true" (
    echo Note: parallel publish is not implemented in build.bat; running sequentially.
)

for %%R in (!_rids!) do (
    call :PublishOne %%R
)

goto :after_publishes

:PublishOne
rem %1 is RID
set "RID=%~1"
set "OUTDIR=%SCRIPT_DIR%build\%PROJECT_NAME%\%CONFIG%\net8.0\%RID%\publish"
echo.
echo Publishing for %RID% to %OUTDIR%
echo Running: dotnet publish "%PUBLISH_TARGET%" -c "%CONFIG%" -r "%RID%" -o "%OUTDIR%" %OPTS%
if "%DRY_RUN%"=="true" (
    echo Dry-run: skipping dotnet publish for %RID%
    goto :eof
)
dotnet publish "%PUBLISH_TARGET%" -c "%CONFIG%" -r "%RID%" -o "%OUTDIR%" %OPTS%
if ERRORLEVEL 1 (
    echo Publish for %RID% failed with exit code %ERRORLEVEL%
    endlocal
    exit /b %ERRORLEVEL%
)

if "%ZIP%"=="true" (
    if "%DRY_RUN%"=="true" (
        echo Dry-run: would zip %OUTDIR% to %SCRIPT_DIR%build\%PROJECT_NAME%\%CONFIG%\%RID%.zip
    ) else (
        echo Creating zip for %RID%
        powershell -NoProfile -Command "Compress-Archive -Path '%OUTDIR%\*' -DestinationPath '%SCRIPT_DIR%build\%PROJECT_NAME%\%CONFIG%\%RID%.zip' -Force"
    )
)
goto :eof

:after_publishes

echo All publishes completed.
endlocal

if "%CI%"=="false" (
    pause
)

