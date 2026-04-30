# PromptNest Implementation Plan

PromptNest is a local-first Windows prompt library built with WinUI 3, .NET 8, SQLite, MVVM, and GitHub Releases distribution.

## Product Decisions

- Product name: PromptNest.
- v1 platform: Windows 10 1809 or newer, x64 primary.
- v1 distribution: GitHub Releases only.
- v1 artifacts: self-signed MSIX, public `.cer`, portable ZIP, Velopack update bundle, and `RELEASES` manifest.
- Microsoft Store submission is out of scope for v1.
- Paid certificates, paid services, and manual release approval steps are out of scope.
- Telemetry is disabled by default; update checks are the only expected network behavior.

## Architecture

- `src/PromptNest.App`: WinUI 3 application, shell, palette, settings, tray, release update integration, and app bootstrap.
- `src/PromptNest.Core`: domain models, services, variable parsing/resolution, validation, import/export, backup, and abstractions.
- `src/PromptNest.Data`: SQLite connection factory, migrations, repositories, FTS search, and persisted settings.
- `src/PromptNest.Platform`: Windows clipboard, hotkey, path, notification, and platform-specific services.
- `tests/PromptNest.Core.Tests`: unit coverage for core services and variable behavior.
- `tests/PromptNest.Data.Tests`: repository, migration, and SQLite integration tests.
- `tests/PromptNest.UiTests`: ViewModel and UI-adjacent workflow tests.
- `tests/PromptNest.SmokeTests`: opt-in Windows UI Automation smoke tests for the desktop app.

## Data Model Notes

- Prompt bodies are plain text with a 64 KB v1 limit.
- Variables use `{{name}}` and `{{name|default}}` syntax.
- Parsed variables are stored in `prompts.variables_json` for fast palette and copy workflows.
- Last-used variable values are stored per prompt and variable name.
- Prompts use soft delete via `deleted_at`; normal search and list paths exclude deleted rows.
- Tag counts and prompt-tag relationships are synchronized by repository writes.
- FTS search indexes title, body, and tags. Invalid FTS queries fall back to safe matching behavior.

## Implementation Sequence

The Linear backlog maps the old milestone language into the current phase sequence:

| Phase | Scope |
|-------|-------|
| P-1 | Planning, backlog creation, repo guardrails |
| P0D | Data, import/export, backups, logging, diagnostics |
| P1 | Foundation, migrations, repositories, services |
| P2 | Main UI, editor, navigation, folders, tags |
| P3 | Platform entry points, palette, settings, polish |
| P4 | Unit, integration, ViewModel, UI smoke, quality gates |
| P5 | Windows integration, clipboard, tray, protocol, updates |
| P6 | Packaging, CI, release workflow, versioning, artifacts |
| P7 | User docs, developer docs, security checklist, final release readiness |

The old M1-M9 milestone names are retained only as historical shorthand. P-phase Linear issues are the source of truth.

## Build And Verification

Local verification:

```powershell
powershell -ExecutionPolicy Bypass -File build\scripts\Invoke-Verification.ps1
```

Default gates are non-interactive:

- `dotnet restore`
- `dotnet format --verify-no-changes`
- `dotnet build` with warnings as errors
- `dotnet test` with Cobertura coverage
- coverage threshold script
- startup performance log
- accessibility availability log

Interactive UI smoke tests are opt-in because they require an unlocked desktop session:

```powershell
$env:PROMPTNEST_RUN_UI_SMOKE = '1'
dotnet test tests\PromptNest.SmokeTests\PromptNest.SmokeTests.csproj --configuration Debug --no-build
Remove-Item Env:\PROMPTNEST_RUN_UI_SMOKE
```

## Packaging And Release

Local packaging:

```powershell
powershell -ExecutionPolicy Bypass -File build\scripts\New-Release.ps1 -Configuration Release -Platform x64 -Version 1.0.0.0
```

Release artifact verification:

```powershell
powershell -ExecutionPolicy Bypass -File build\scripts\Test-ReleaseArtifacts.ps1 -Version 1.0.0.0
```

Release trigger policy:

- Push `v1.0.0` style tags for stable GitHub Releases.
- Push `v1.0.0-beta.1` style tags for prerelease GitHub Releases.
- Use `workflow_dispatch` for dry-run artifact builds.
- `GitVersion.yml` controls development version derivation; release tags are authoritative for public artifacts.

## Security And Privacy

- No prompt content is executed.
- Prompt content and PII must not be written to logs.
- Imported JSON must be validated and size-capped before writes.
- Database, logs, backups, and update cache live in current-user local app data or MSIX LocalState.
- Self-signed MSIX trust warnings are documented honestly in README.
- The release security checklist lives in `docs/security-privacy-checklist.md`.

## Definition Of Done

- Linear issues in the scoped backlog are Done or explicitly blocked with justification.
- Build and tests pass.
- Coverage thresholds pass or have documented release exceptions.
- UI smoke environment requirements are documented.
- CI workflow runs on push and pull request.
- Release workflow produces MSIX, `.cer`, portable ZIP, Velopack artifacts, and `RELEASES`.
- Release artifacts pass `Test-ReleaseArtifacts.ps1`.
- README install, trust, portable ZIP, uninstall, and update instructions match the actual artifacts.
- Security and privacy checklist has pass/fail evidence.
- Final release checklist in `docs/release-checklist.md` is complete before v1.0 is declared shipped.
