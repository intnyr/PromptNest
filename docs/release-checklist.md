# v1.0 Release Checklist

Use this checklist for the final release readiness issue.

## Required Gates

- [x] All non-deferred Linear issues for PromptNest v1 are Done.
- [x] `powershell -ExecutionPolicy Bypass -File build\scripts\Invoke-Verification.ps1` passes locally or has documented environment exceptions.
- [x] GitHub Actions CI is green on the release commit.
- [x] Release workflow succeeds from a `v<major>.<minor>.<patch>` tag.
- [x] Release contains `.msix`, `.cer`, portable `.zip`, Velopack packages, and `RELEASES`.
- [x] `build\scripts\Test-ReleaseArtifacts.ps1` passes for the release version.
- [x] `gh release view v<version> --json tagName,name,isPrerelease,assets` returns the public release and assets.
- [x] README install and trust instructions match the actual release files.
- [x] `docs\security-privacy-checklist.md` is complete with no failing release blockers.

## Evidence

- Local verification passed on 2026-05-01 with `powershell -ExecutionPolicy Bypass -File build\scripts\Invoke-Verification.ps1`.
- Local release artifact verification passed with `powershell -ExecutionPolicy Bypass -File build\scripts\Test-ReleaseArtifacts.ps1 -Version 1.0.0.0`.
- Release tag: `v1.0.0`.
- Release workflow: https://github.com/intnyr/PromptNest/actions/runs/25191788023
- Public release: https://github.com/intnyr/PromptNest/releases/tag/v1.0.0
- Published assets: `PromptNest-1.0.0-full.nupkg`, `PromptNest-1.0.0.0-x64-portable.zip`, `PromptNest-1.0.0.0-x64.msix`, `PromptNest-win-Setup.exe`, `PromptNest.cer`, `RELEASES`.

## Documented Exceptions

Record any exception here with a linked follow-up Linear issue before marking v1.0 complete.

- None blocking for v1.0. The local accessibility probe records a skip when Accessibility Insights CLI is absent; this is documented by `build\scripts\Invoke-Verification.ps1` output and does not block release because keyboard/accessibility coverage is provided by ViewModel tests and docs in the current v1 scope.
