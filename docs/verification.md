# Verification

PromptNest quality gates are designed to be non-interactive by default.

## Full Local Suite

```powershell
powershell -ExecutionPolicy Bypass -File build\scripts\Invoke-Verification.ps1
```

This runs restore, format verification, analyzer-backed build with warnings as errors, tests with Cobertura coverage using `coverlet.runsettings`, coverage thresholds, startup performance logging, and accessibility logging.

## Coverage Gates

```powershell
powershell -ExecutionPolicy Bypass -File build\scripts\Test-CoverageThresholds.ps1
```

Current gates are enforced on implemented v1 workflow surfaces:

- Core workflow logic: 80%
- Data repositories and migrations: 70%
- ViewModels: 60%

The script reads Cobertura files under `artifacts/test-results` and writes `artifacts/quality/coverage-thresholds.json`.

## Performance And Accessibility Logs

```powershell
powershell -ExecutionPolicy Bypass -File build\scripts\Measure-Performance.ps1
powershell -ExecutionPolicy Bypass -File build\scripts\Test-Accessibility.ps1
```

Performance writes `artifacts/performance/performance.json`. Accessibility writes `artifacts/accessibility/accessibility.json`; if Accessibility Insights CLI is installed, the script records tool availability, otherwise it records an explicit skip reason.

## Interactive UI Smoke

The interactive UI smoke path is documented in `docs/ui-smoke-tests.md`. It is not part of default CI because it requires an unlocked desktop session.
