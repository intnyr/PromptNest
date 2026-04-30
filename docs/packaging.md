# PromptNest Packaging Notes

PromptNest is packaged for direct GitHub Releases distribution, not Microsoft Store submission.

## MSIX Identity

- Package identity: `PromptNest.Desktop`
- Display name: `PromptNest`
- Publisher placeholder: `CN=PromptNest Dev`
- Minimum OS: Windows 10 1809, `10.0.17763.0`
- Protocol: `promptnest://`

The local signing certificate used by packaging scripts must use the same subject as the manifest publisher unless the manifest is intentionally stamped during release packaging.

## Capabilities

- `runFullTrust`: required for the desktop WinUI app and platform integrations such as global hotkey registration, tray behavior, SQLite file access, and clipboard integration.
- `internetClient`: required only for Velopack update checks. Runtime code safely no-ops when the update feed is not configured or update checks are disabled.

No file associations are declared for v1 because the app currently implements protocol routing only.

## Local Packaging

Run packaging from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File build\scripts\New-Release.ps1
```

The script restores, builds, tests, publishes `win-x64`, creates a portable ZIP, generates a local self-signed MSIX certificate when needed, and writes artifacts under `artifacts\package`.

Prerequisites:

- .NET 8 SDK
- Windows 10/11 SDK tools, including `makeappx.exe` and `signtool.exe`
- PowerShell with access to the current-user certificate store

Set `PROMPTNEST_CERT_PASSWORD` in CI or secure local environments when you do not want the script to generate a local password file.
