# Repository Prompt Import Contract

PromptNest repository imports use the existing `PromptNestExport` JSON shape as the injection boundary. Automation should never write directly to SQLite because repository writes maintain prompt tags, variables, and search indexes.

## Scanner Output

Scanners produce `RepositoryPromptCandidate` records:

```json
{
  "repositoryRoot": "D:\\Repos\\Example",
  "repositoryName": "Example",
  "relativePath": "prompts/review.md",
  "titleHint": "Review Prompt",
  "startLine": 12,
  "endLine": 42,
  "format": "markdown-fence",
  "confidence": 0.82,
  "tags": ["markdown-fence", "md"],
  "body": "Review this change for ..."
}
```

Candidate bodies are untrusted text. They are never executed, and raw bodies must not be posted to Linear by default.

## Deterministic IDs

Normalized prompts use stable SHA-256 based IDs:

```text
prompt-{sha256(repoName|relativePath|startLine|normalizedBodyHash)[0..24]}
folder-{sha256(folderPath)[0..24]}
```

This makes repeat scans idempotent when the source prompt has not changed. If the body changes, the generated prompt ID changes unless the importer later chooses overwrite behavior for an existing ID.

## Folder Layout

Repository-derived prompts are placed under a deterministic root folder:

```text
Repository Imports
  <repository-name>
```

The root folder defaults to `Repository Imports` and can be changed through normalization options.

## Reserved Tags

Every normalized repo prompt receives:

```text
imported
source:repo
<repository-name-slug>
<detected-format>
<file-extension>
```

Tags are lowercase, trimmed, deduplicated, and limited to 64 characters.

## Provenance

For v1 of this automation, provenance is represented by deterministic folder placement, reserved tags, and the redacted batch report. PromptNest does not add source repository/path columns to the prompt schema yet.

Open decision: expose source repo/path in the UI later if users need per-prompt provenance beyond tags/folders.

## Conflict And Dry-Run Behavior

Imports support the existing conflict modes:

* `skip`: existing prompt IDs are left unchanged.
* `overwrite`: existing prompt IDs are updated after validation.
* `duplicate`: existing prompt IDs are imported with new generated IDs.

All automation must run a dry-run first. Dry-run validates and summarizes the import without mutating repositories.

## Validation Rules

Before any write, imports validate:

* schema version is supported
* title and body are present
* body is at or below `PromptLimits.MaxPromptBodyBytes`
* folder references exist in the import payload
* prompt, folder, and tag identifiers are unique where required
* tag names are present and at most 64 characters
* configured prompt/folder/tag count caps are not exceeded

Validation and Linear reports must not include full prompt bodies.
