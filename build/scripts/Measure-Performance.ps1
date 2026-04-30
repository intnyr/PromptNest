param(
  [string]$Configuration = "Debug",
  [string]$ArtifactsDirectory = "artifacts/performance",
  [int]$StartupTimeoutSeconds = 10
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$exe = Join-Path $repoRoot "src\PromptNest.App\bin\$Configuration\net8.0-windows10.0.19041.0\win-x64\PromptNest.App.exe"
if (-not (Test-Path $exe)) {
  throw "PromptNest executable not found at '$exe'. Build the app first."
}

New-Item -ItemType Directory -Force -Path $ArtifactsDirectory | Out-Null
$smokeDataRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("PromptNest.Perf." + [guid]::NewGuid().ToString("N"))

$startInfo = [System.Diagnostics.ProcessStartInfo]::new($exe)
$startInfo.UseShellExecute = $false
$startInfo.Environment["PROMPTNEST_LOCALAPPDATA"] = $smokeDataRoot

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$process = [System.Diagnostics.Process]::Start($startInfo)
Start-Sleep -Seconds $StartupTimeoutSeconds
$stopwatch.Stop()

$stillRunning = -not $process.HasExited
$workingSetMb = if ($stillRunning) { [math]::Round($process.WorkingSet64 / 1MB, 2) } else { 0 }

if ($stillRunning) {
  $null = $process.CloseMainWindow()
  if (-not $process.WaitForExit(3000)) {
    $process.Kill($true)
  }
}

$result = [pscustomobject]@{
  timestampUtc = [DateTimeOffset]::UtcNow.ToString("O")
  coldStartObservationMs = $stopwatch.ElapsedMilliseconds
  stayedRunning = $stillRunning
  idleWorkingSetMb = $workingSetMb
  paletteShowLatencyMs = $null
  searchLatencyMs = $null
  copyActionLatencyMs = $null
  notes = "Cold-start and idle-memory smoke are automated. Palette/search/copy timing requires the interactive UI smoke path until Appium/WinAppDriver is enabled."
}

$result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $ArtifactsDirectory "performance.json")
$result | Format-List

if (-not $stillRunning) {
  throw "PromptNest exited during the startup performance smoke."
}
