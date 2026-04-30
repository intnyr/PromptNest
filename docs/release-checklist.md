# v1.0 Release Checklist

Use this checklist for the final release readiness issue.

## Required Gates

- [ ] All non-deferred Linear issues for PromptNest v1 are Done.
- [ ] `powershell -ExecutionPolicy Bypass -File build\scripts\Invoke-Verification.ps1` passes locally or has documented environment exceptions.
- [ ] GitHub Actions CI is green on the release commit.
- [ ] Release workflow succeeds from a `v<major>.<minor>.<patch>` tag.
- [ ] Release contains `.msix`, `.cer`, portable `.zip`, Velopack packages, and `RELEASES`.
- [ ] `build\scripts\Test-ReleaseArtifacts.ps1` passes for the release version.
- [ ] `gh release view v<version> --json tagName,name,isPrerelease,assets` returns the public release and assets.
- [ ] README install and trust instructions match the actual release files.
- [ ] `docs\security-privacy-checklist.md` is complete with no failing release blockers.

## Documented Exceptions

Record any exception here with a linked follow-up Linear issue before marking v1.0 complete.
