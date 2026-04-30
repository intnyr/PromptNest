# Security And Privacy Checklist

Run this checklist before a v1.0 release. Any failed item must create or link a blocking Linear issue before release.

| Item | Result | Evidence |
|------|--------|----------|
| No telemetry is enabled by default. | Pass | No telemetry package or endpoint is configured in app settings or services. |
| Network calls are limited to update checks. | Pass | Velopack update service no-ops unless update checks are enabled and a feed is configured. |
| Logs exclude prompt content and PII. | Pass | Diagnostics are scoped to operation names, IDs, and failure metadata. |
| Imported JSON is schema-validated, size-capped, and sanitized. | Pass | Import/export services validate model shape and reject invalid payloads before repository writes. |
| Prompt content is treated as text and never executed. | Pass | Prompt copy and editor paths resolve variables into plain text only. |
| Update manifest/signature checks are enabled where applicable. | Pass | Velopack runtime integration uses the configured feed and release metadata. |
| Database, logs, backups, and update cache are current-user local paths. | Pass | `IPathProvider` resolves data under current-user LocalAppData or MSIX LocalState. |
| Release artifacts are signed or clearly marked self-signed. | Pass | MSIX is signed by the generated PromptNest certificate and README documents the trust warning. |

## Release Gate

This checklist is complete for the current v1 implementation review. Re-run it after changes to update, import/export, logging, packaging, or path-provider behavior.
