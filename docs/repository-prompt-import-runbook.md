# Repository Prompt Import Runbook

This runbook covers bulk prompt discovery from repositories and safe import into PromptNest. Use it for local automation and Linear-tracked import batches.

## Privacy Rules

* PromptNest remains the system of record for prompt content.
* Do not paste raw prompt bodies, secrets, or PII into Linear comments.
* Linear batch reports must be redacted summaries: counts, warnings, source repo names, artifact paths, and verification status only.
* Keep generated import JSON in local artifacts unless the team explicitly approves sharing it.

## Safety Defaults

The scanner skips common dependency and build folders such as `.git`, `.vs`, `bin`, `obj`, `node_modules`, `packages`, `dist`, `build`, `coverage`, and `artifacts`.

Default caps:

* scanner file limit: 512 KB
* prompt body limit: `PromptLimits.MaxPromptBodyBytes` / 64 KB
* import count caps: 10,000 prompts, 2,000 folders, 2,000 tags

Default conflict mode is `skip`. Use `overwrite` only after reviewing the dry-run summary.

## Recommended Linear Workflow

Create one Linear batch issue per import run.

Suggested states:

* `Todo`: batch planned
* `In Progress`: scan or dry-run running
* `In Review`: redacted report is ready for human approval
* `Done`: apply completed and verification passed

If an import cannot proceed, leave the issue out of `Done` and add a comment with the blocker, affected repositories, and the safest next action.

## Scan

Generate PromptNest import JSON and a redacted Markdown report:

```powershell
dotnet run --project src\PromptNest.Cli -- scan `
  --repo D:\Repos\RepositoryA `
  --repo D:\Repos\RepositoryB `
  --out artifacts\repo-prompts.import.json `
  --report artifacts\repo-prompts.report.md
```

Optional Linear reporting:

```powershell
$env:LINEAR_API_KEY = '<token>'
dotnet run --project src\PromptNest.Cli -- scan `
  --repo D:\Repos\RepositoryA `
  --out artifacts\repo-prompts.import.json `
  --report artifacts\repo-prompts.report.md `
  --linear-batch-issue TAS-123 `
  --batch-name "May repository prompt import"
```

If `LINEAR_API_KEY` is not set, the CLI still writes local output and reports that Linear publishing was skipped.

## Validate

Validate generated JSON before applying:

```powershell
dotnet run --project src\PromptNest.Cli -- validate `
  --file artifacts\repo-prompts.import.json
```

Use a temporary data root to test without touching the real PromptNest library:

```powershell
dotnet run --project src\PromptNest.Cli -- validate `
  --file artifacts\repo-prompts.import.json `
  --data-root D:\Temp\PromptNestImportTest
```

## Dry Run

Run the importer without mutating the database:

```powershell
dotnet run --project src\PromptNest.Cli -- import `
  --file artifacts\repo-prompts.import.json `
  --conflict skip `
  --dry-run
```

Review:

* prompts created, updated, and skipped
* folders and tags prepared
* validation errors and warnings
* duplicate and potential secret counts

## Apply

Apply only after review:

```powershell
dotnet run --project src\PromptNest.Cli -- import `
  --file artifacts\repo-prompts.import.json `
  --conflict skip `
  --backup-before-apply `
  --linear-batch-issue TAS-123
```

The desktop Settings pane can also import/export PromptNest JSON and creates a backup before UI imports.

## Export Backup

Create a portable JSON export:

```powershell
dotnet run --project src\PromptNest.Cli -- export `
  --out artifacts\promptnest-before-import.json
```

The CLI `--backup-before-apply` option creates a SQLite database backup before mutating imports.

## Rollback

Preferred rollback path:

1. Close PromptNest.
2. Restore the latest `library.db.bak.*` file from the configured backup folder to `library.db`.
3. Start PromptNest and verify search, folders, tags, and imported prompt counts.
4. Update the Linear batch issue with the rollback reason and verification result.

If only a small number of prompts were imported incorrectly, use PromptNest search and tags such as `imported`, `source:repo`, and the repository slug to locate and remove them.

## Verification

Run non-interactive tests:

```powershell
dotnet test tests\PromptNest.Core.Tests\PromptNest.Core.Tests.csproj
dotnet test tests\PromptNest.Cli.Tests\PromptNest.Cli.Tests.csproj
dotnet test tests\PromptNest.UiTests\PromptNest.UiTests.csproj
```

The desktop smoke suite remains opt-in because it requires an unlocked Windows session.
