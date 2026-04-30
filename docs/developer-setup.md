# Developer Setup

This guide covers local development, verification, packaging, and common troubleshooting for PromptNest.

## Prerequisites

- Windows 10 1809 or newer.
- .NET SDK from `global.json`.
- Windows 10/11 SDK with `makeappx.exe` and `signtool.exe` for MSIX packaging.
- GitHub CLI for release inspection commands.
- An unlocked interactive desktop session for opt-in UI smoke tests.

## Restore, Build, Test

```powershell
dotnet restore PromptNest.sln
dotnet build PromptNest.sln --configuration Debug
dotnet test PromptNest.sln --configuration Debug --no-build --collect:"XPlat Code Coverage" --settings coverlet.runsettings --results-directory artifacts/test-results
```

## Full Verification

```powershell
powershell -ExecutionPolicy Bypass -File build\scripts\Invoke-Verification.ps1
```

This runs restore, format verification, build, tests with coverage, coverage thresholds, performance logging, and accessibility logging.

## Interactive UI Smoke

```powershell
dotnet build PromptNest.sln --configuration Debug
$env:PROMPTNEST_RUN_UI_SMOKE = '1'
dotnet test tests\PromptNest.SmokeTests\PromptNest.SmokeTests.csproj --configuration Debug --no-build
Remove-Item Env:\PROMPTNEST_RUN_UI_SMOKE
```

Set `PROMPTNEST_APP_EXE` when testing a packaged or published executable. The smoke suite seeds a temporary database through `PROMPTNEST_LOCALAPPDATA` and does not touch the user's normal library.

## Package

```powershell
powershell -ExecutionPolicy Bypass -File build\scripts\New-Release.ps1 -Configuration Release -Platform x64 -Version 1.0.0.0
```

Artifacts are written to `artifacts\package`. Add `-SkipMsix` to produce only the portable ZIP.

## Verify Release Artifacts

```powershell
powershell -ExecutionPolicy Bypass -File build\scripts\Test-ReleaseArtifacts.ps1 -Version 1.0.0.0
```

## Troubleshooting

- WinUI build errors: confirm the SDK version in `global.json`, restore packages, and verify Windows App SDK packages are restored.
- Missing `makeappx.exe` or `signtool.exe`: restore packages so `Microsoft.Windows.SDK.BuildTools` is available, install the Windows SDK, or use `-SkipMsix` for portable ZIP-only output.
- MSIX signing failure: set `PROMPTNEST_CERT_PASSWORD` or delete `artifacts\package\cert` and let the script generate a fresh local dev certificate.
- Low space on `%TEMP%` or the default .NET tool cache: set `TEMP`, `TMP`, `DOTNET_CLI_HOME`, and `NUGET_PACKAGES` to repo-local folders before installing tools or running Velopack.
- SQLite FTS5 failures: confirm `e_sqlite3` native assets are present in build output and rerun migrations against a fresh test database.
- Global hotkey conflict: choose another hotkey in Settings or close the other app using `Win+Shift+Space`.
- UI smoke cannot find controls: run from an unlocked desktop session with the test runner and app at the same integrity level.
- Accessibility Insights unavailable: install the CLI to produce full scan artifacts; otherwise the script records an explicit skip artifact.

## Local Data And Logs

Unpackaged development data defaults to `%LOCALAPPDATA%\PromptNest`. Tests override that path with temporary roots. Logs are written under the app data `logs` directory and are expected to exclude prompt body content and PII.
