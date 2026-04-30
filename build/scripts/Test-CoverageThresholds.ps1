param(
  [string]$CoverageRoot = "artifacts/test-results",
  [double]$CoreThreshold = 80,
  [double]$DataThreshold = 70,
  [double]$ViewModelThreshold = 60,
  [string]$OutputPath = "artifacts/quality/coverage-thresholds.json"
)

$ErrorActionPreference = "Stop"

function New-Bucket($name, $threshold, $patterns) {
  [pscustomobject]@{
    Name = $name
    Threshold = $threshold
    Patterns = $patterns
    Lines = @{}
  }
}

$buckets = @(
  (New-Bucket "Core" $CoreThreshold @("PromptNest.Core\Variables\", "PromptNest.Core\Services\PromptService.cs", "PromptNest.Core\Services\SearchService.cs", "Variables\", "Services\PromptService.cs", "Services\SearchService.cs")),
  (New-Bucket "Data" $DataThreshold @("PromptNest.Data\Db\", "PromptNest.Data\Migrations\", "PromptNest.Data\Repositories\", "Db\", "Migrations\", "Repositories\")),
  (New-Bucket "ViewModels" $ViewModelThreshold @("PromptNest.App\ViewModels\", "src\PromptNest.App\ViewModels\", "ViewModels\"))
)

$coverageFiles = Get-ChildItem -Path $CoverageRoot -Recurse -Filter "coverage.cobertura.xml" -ErrorAction SilentlyContinue
if (-not $coverageFiles) {
  throw "No coverage.cobertura.xml files found under '$CoverageRoot'."
}

foreach ($file in $coverageFiles) {
  [xml]$coverage = Get-Content -LiteralPath $file.FullName
  $classes = $coverage.coverage.packages.package.classes.class
  foreach ($class in $classes) {
    $filename = [string]$class.filename
    if ($filename -match "\\obj\\|^obj\\") {
      continue
    }

    foreach ($bucket in $buckets) {
      $matchesBucket = $false
      foreach ($pattern in $bucket.Patterns) {
        if ($filename.IndexOf($pattern, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
          $matchesBucket = $true
          break
        }
      }

      if (-not $matchesBucket) {
        continue
      }

      foreach ($line in $class.lines.line) {
        $lineKey = "$filename`:$($line.number)"
        $isCovered = [int]$line.hits -gt 0
        if (-not $bucket.Lines.ContainsKey($lineKey)) {
          $bucket.Lines[$lineKey] = $false
        }

        $bucket.Lines[$lineKey] = $bucket.Lines[$lineKey] -or $isCovered
      }
    }
  }
}

$results = foreach ($bucket in $buckets) {
  $covered = @($bucket.Lines.Values | Where-Object { $_ }).Count
  $total = $bucket.Lines.Count
  $rate = if ($total -eq 0) { 0 } else { [math]::Round(($covered / $total) * 100, 2) }
  [pscustomobject]@{
    name = $bucket.Name
    threshold = $bucket.Threshold
    coveredLines = $covered
    totalLines = $total
    coverage = $rate
    passed = $rate -ge $bucket.Threshold
  }
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $OutputPath) | Out-Null
$results | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $OutputPath
$results | Format-Table -AutoSize

$failed = @($results | Where-Object { -not $_.passed })
if ($failed.Count -gt 0) {
  throw "Coverage threshold failure: $($failed.name -join ', ')"
}
