param(
  [string]$ArtifactsDirectory = "artifacts/accessibility"
)

$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force -Path $ArtifactsDirectory | Out-Null

$aiCli = Get-Command "accessibilityinsights.exe" -ErrorAction SilentlyContinue
$resultPath = Join-Path $ArtifactsDirectory "accessibility.json"

if ($aiCli) {
  & $aiCli.Source --version | Out-File -FilePath (Join-Path $ArtifactsDirectory "accessibilityinsights-version.txt")
  [pscustomobject]@{
    status = "available"
    tool = $aiCli.Source
    notes = "Accessibility Insights CLI is installed. Run the documented interactive scan against PromptNest for full UI validation."
  } | ConvertTo-Json | Set-Content -LiteralPath $resultPath
} else {
  [pscustomobject]@{
    status = "skipped"
    tool = "Accessibility Insights CLI"
    notes = "CLI not installed on this machine. Keyboard/accessibility smoke remains covered by ViewModel tests and docs; install Accessibility Insights CLI to produce full scan artifacts."
  } | ConvertTo-Json | Set-Content -LiteralPath $resultPath
}

Get-Content -LiteralPath $resultPath
