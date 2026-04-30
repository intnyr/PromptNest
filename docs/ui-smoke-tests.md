# UI Smoke Tests

PromptNest includes an opt-in Windows UI Automation smoke suite for the desktop shell and global palette.

## Environment

- Windows 10/11 interactive desktop session.
- .NET 8 SDK.
- Built Debug x64 app output at `src/PromptNest.App/bin/Debug/net8.0-windows10.0.19041.0/win-x64/PromptNest.App.exe`, or set `PROMPTNEST_APP_EXE`.
- No elevated desktop isolation between the test runner and the app.

## Run

```powershell
dotnet build PromptNest.sln --no-restore
$env:PROMPTNEST_RUN_UI_SMOKE = '1'
dotnet test tests\PromptNest.SmokeTests\PromptNest.SmokeTests.csproj --no-build
Remove-Item Env:\PROMPTNEST_RUN_UI_SMOKE
```

The test uses `PROMPTNEST_LOCALAPPDATA` for the launched app process and seeds a temporary PromptNest database, so it does not read or mutate the user's local library.

## Covered Workflows

- App launch and shell rendering.
- Search and prompt selection.
- Metadata/body/tag editing, save, and cancel.
- Validation feedback.
- Global palette hotkey, palette search, copy action, and Escape close.

The suite verifies functional workflow health rather than pixel-perfect layout. Visual review remains covered by `docs/visual-qa-checklist.md`.
