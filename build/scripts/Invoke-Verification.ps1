param(
  [string]$Configuration = "Debug",
  [switch]$IncludeInteractiveUiSmoke
)

$ErrorActionPreference = "Stop"

dotnet restore PromptNest.sln
dotnet format PromptNest.sln --verify-no-changes --no-restore
dotnet build PromptNest.sln --configuration $Configuration --no-restore
dotnet test PromptNest.sln --configuration $Configuration --no-build --collect:"XPlat Code Coverage" --settings coverlet.runsettings --results-directory artifacts/test-results

dotnet restore tests\PromptNest.SmokeTests\PromptNest.SmokeTests.csproj
dotnet build tests\PromptNest.SmokeTests\PromptNest.SmokeTests.csproj --configuration $Configuration --no-restore
dotnet test tests\PromptNest.SmokeTests\PromptNest.SmokeTests.csproj --configuration $Configuration --no-build

if ($IncludeInteractiveUiSmoke) {
  $env:PROMPTNEST_RUN_UI_SMOKE = "1"
  try {
    dotnet test tests\PromptNest.SmokeTests\PromptNest.SmokeTests.csproj --configuration $Configuration --no-build
  } finally {
    Remove-Item Env:\PROMPTNEST_RUN_UI_SMOKE -ErrorAction SilentlyContinue
  }
}

& (Join-Path $PSScriptRoot "Test-CoverageThresholds.ps1")
& (Join-Path $PSScriptRoot "Measure-Performance.ps1") -Configuration $Configuration
& (Join-Path $PSScriptRoot "Test-Accessibility.ps1")
