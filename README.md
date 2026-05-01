# PromptNest

PromptNest is a local-first Windows prompt library built with WinUI 3, .NET 8, SQLite, and MVVM. It stores prompts on the current machine, supports a global quick palette, and ships from GitHub Releases as either a self-signed MSIX package or a portable ZIP.

## Supported Windows Versions

- Windows 10 1809 or newer.
- Windows 11 recommended for the best WinUI backdrop and shell behavior.
- x64 is the primary v1 release target.

## Install From GitHub Releases

Open the latest release and choose one install option:

- `PromptNest-<version>-x64.msix`: clean install/uninstall package. Requires trusting the published `PromptNest.cer` certificate once because v1 uses a self-signed certificate.
- `PromptNest-<version>-x64-portable.zip`: no install. Extract to a writable folder and run `PromptNest.App.exe`.
- `PromptNest.cer`: public certificate used to sign the MSIX.
- Velopack files: update feed artifacts used by the app updater.

PromptNest v1 is distributed directly from GitHub Releases. It is not submitted to the Microsoft Store.

## Trust The MSIX Certificate

Self-signed packages show a Windows trust warning until the certificate is installed for the current user. After downloading `PromptNest.cer`, run PowerShell from the folder containing the certificate:

```powershell
Import-Certificate -FilePath .\PromptNest.cer -CertStoreLocation Cert:\CurrentUser\TrustedPeople
```

Then open the `.msix` file. This trust step is only for PromptNest packages signed by the matching published certificate.

## Portable ZIP

Extract the ZIP to a normal user-writable folder, such as `%LOCALAPPDATA%\Programs\PromptNest` or a tools folder you manage. Run `PromptNest.App.exe` from the extracted directory. Delete the folder to remove the portable copy.

## Uninstall

- MSIX: uninstall PromptNest from Windows Settings > Apps.
- Portable ZIP: close PromptNest and delete the extracted folder.

Prompt data is stored under the current user's local app data path. Uninstalling the app package does not intentionally delete the local library.

## Updates

The app can check GitHub Releases through Velopack when update checks are enabled. Stable releases use normal `v1.2.3` tags. Beta releases use prerelease semver tags such as `v1.2.3-beta.1`.

Update checks are the only network behavior expected in v1. They can be disabled in Settings when the app is running.

## Build

```powershell
dotnet restore PromptNest.sln
dotnet build PromptNest.sln
```

Developer setup, verification, packaging, and troubleshooting are documented in `docs/developer-setup.md`.

Repository prompt import automation is documented in `docs/repository-prompt-import-runbook.md`.
