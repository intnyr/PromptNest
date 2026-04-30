# MSIX Packaging Assets

This folder is reserved for static MSIX packaging assets that are not part of the WinUI 3 app project.

The active MSIX manifest lives next to the app: `src/PromptNest.App/Package.appxmanifest`.
The active MSIX visual assets live next to the app: `src/PromptNest.App/Assets/*.png`.

The mapping file consumed by `makeappx.exe pack` is generated at packaging time by `build/scripts/New-Release.ps1` and written to `artifacts/package/msix-mapping.txt` so it always reflects the current publish output.

Drop additional packaging-only resources here (for example, future `.pri` overrides, package signing helper scripts, or `AppxBlockMap.xml` overrides) instead of adding them to the app project.
