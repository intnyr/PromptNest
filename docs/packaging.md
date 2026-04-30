# PromptNest Packaging

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
- Windows 10/11 SDK tools, including `makeappx.exe` and `signtool.exe`, or the restored `Microsoft.Windows.SDK.BuildTools` package
- PowerShell with access to the current-user certificate store

Set `PROMPTNEST_CERT_PASSWORD` in CI or secure local environments when you do not want the script to generate a local password file.

Use `-SkipMsix` when only a portable ZIP is needed. Without `-SkipMsix`, missing `makeappx.exe` or `signtool.exe` is a hard failure with an explicit message.

## Release Trigger Policy

PromptNest releases are tag-driven:

- Push `v<major>.<minor>.<patch>` to create a stable GitHub Release.
- Push `v<major>.<minor>.<patch>-beta.<n>` to create a prerelease GitHub Release.
- Use `workflow_dispatch` with an explicit version for a dry-run artifact build; dispatch runs upload artifacts but do not create a GitHub Release unless the workflow is running for a tag.

`GitVersion.yml` is the source of version derivation for normal development builds. Release artifacts use the tag semver, with MSIX versions normalized to four numeric components such as `1.0.0.0`.

## Artifact Verification

After packaging and Velopack packing, run:

```powershell
powershell -ExecutionPolicy Bypass -File build\scripts\Test-ReleaseArtifacts.ps1 -Version 1.0.0.0
```

The verifier checks:

- MSIX, certificate, portable ZIP, and Velopack `RELEASES` manifest exist.
- The portable ZIP contains `PromptNest.App.exe`.
- The MSIX has an Authenticode signature status.
- MSIX and ZIP sizes stay within the configured artifact size limit.

After a tag release, CI runs:

```powershell
gh release view v1.0.0 --json tagName,name,isPrerelease,assets
```

That command confirms the GitHub Release exists and exposes the expected assets.
