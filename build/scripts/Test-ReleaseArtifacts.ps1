param(
  [string]$PackageRoot = "artifacts/package",
  [string]$VelopackRoot = "artifacts/velopack",
  [string]$Version = "1.0.0",
  [string]$Platform = "x64",
  [int]$MaxArtifactSizeMb = 50,
  [string]$OutputPath = "artifacts/release/release-artifacts.json"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem

$msix = Join-Path $PackageRoot "PromptNest-$Version-$Platform.msix"
$zip = Join-Path $PackageRoot "PromptNest-$Version-$Platform-portable.zip"
$cert = Join-Path $PackageRoot "cert/PromptNest.cer"
$releases = Join-Path $VelopackRoot "RELEASES"

$checks = @()

function Add-Check($name, $passed, $detail) {
  $script:checks += [pscustomobject]@{
    name = $name
    passed = [bool]$passed
    detail = $detail
  }
}

foreach ($path in @($msix, $zip, $cert)) {
  Add-Check "exists:$(Split-Path -Leaf $path)" (Test-Path -LiteralPath $path) $path
}

if (Test-Path -LiteralPath $zip) {
  $archive = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path $zip))
  try {
    $entryNames = @($archive.Entries | ForEach-Object { $_.FullName })
    Add-Check "zip contains app exe" ($entryNames -contains "PromptNest.App.exe") $zip
  }
  finally {
    $archive.Dispose()
  }
}

if (Test-Path -LiteralPath $msix) {
  $signature = Get-AuthenticodeSignature -FilePath $msix
  Add-Check "msix is signed" ($signature.Status -in @("Valid", "UnknownError")) $signature.StatusMessage
}

if (Test-Path -LiteralPath $releases) {
  Add-Check "velopack RELEASES exists" $true $releases
}
else {
  Add-Check "velopack RELEASES exists" $false $releases
}

foreach ($path in @($msix, $zip)) {
  if (Test-Path -LiteralPath $path) {
    $item = Get-Item -LiteralPath $path
    $sizeMb = [math]::Round($item.Length / 1MB, 2)
    Add-Check "size:$($item.Name)" ($sizeMb -le $MaxArtifactSizeMb) "$sizeMb MB <= $MaxArtifactSizeMb MB"
  }
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $OutputPath) | Out-Null
$checks | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $OutputPath
$checks | Format-Table -AutoSize

$failed = @($checks | Where-Object { -not $_.passed })
if ($failed.Count -gt 0) {
  throw "Release artifact verification failed: $($failed.name -join ', ')"
}
