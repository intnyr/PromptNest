# Release Policy

PromptNest v1 ships only through GitHub Releases. There is no Microsoft Store submission in v1.

## Triggers

- Stable release: push a tag like `v1.0.0`.
- Beta release: push a prerelease tag like `v1.0.0-beta.1`.
- Dry run: run `release.yml` manually with a version. This produces downloadable workflow artifacts but does not create a GitHub Release.

## Versioning

`GitVersion.yml` configures development version derivation from commit history. Release workflows use the pushed tag as the authoritative public version. MSIX package versions are converted to four numeric components, for example `1.0.0` becomes `1.0.0.0`.

## Artifact Names

- `PromptNest-<msix-version>-x64.msix`
- `PromptNest-<msix-version>-x64-portable.zip`
- `PromptNest.cer`
- Velopack package files and `RELEASES`

## Completion Rule

A release is complete only after the workflow builds, tests, packages, verifies artifacts, creates the GitHub Release for tag builds, and `gh release view` can read back the release metadata.
