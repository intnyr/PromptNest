# Build Scripts

Run these entry points from the repository root.

## Verification

```powershell
powershell -ExecutionPolicy Bypass -File build\scripts\Invoke-Verification.ps1
```

Runs restore, format verification, build, tests with coverage, coverage thresholds, performance logging, and accessibility logging.

## Release Package

```powershell
powershell -ExecutionPolicy Bypass -File build\scripts\New-Release.ps1 -Configuration Release -Platform x64 -Version 1.0.0.0
```

Produces a portable ZIP and a self-signed MSIX under `artifacts\package`. The script fails clearly if Windows SDK `makeappx.exe` or `signtool.exe` is unavailable. Use `-SkipMsix` when only the portable ZIP is needed.

Set `PROMPTNEST_CERT_PASSWORD` in CI. Local runs without that variable generate a development password file under `artifacts\package\cert`.

## Release Artifact Verification

```powershell
powershell -ExecutionPolicy Bypass -File build\scripts\Test-ReleaseArtifacts.ps1 -Version 1.0.0.0
```

Verifies the MSIX, certificate, portable ZIP, Velopack `RELEASES` manifest, signature status, and configured artifact size limit.
