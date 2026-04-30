param(
  [string]$Configuration = "Release",
  [string]$Platform = "x64",
  [string]$Version = "1.0.0.0",
  [string]$ArtifactsRoot = "artifacts/package",
  [switch]$SkipMsix,
  [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $repoRoot

$rid = "win-$Platform"
$publishDir = Join-Path $repoRoot "src\PromptNest.App\bin\$Configuration\net8.0-windows10.0.19041.0\$rid\publish"
$artifactsDir = Join-Path $repoRoot $ArtifactsRoot
$msixStaging = Join-Path $artifactsDir "msix-staging"
$msixOut = Join-Path $artifactsDir "PromptNest-$Version-$Platform.msix"
$zipOut = Join-Path $artifactsDir "PromptNest-$Version-$Platform-portable.zip"
$certDir = Join-Path $artifactsDir "cert"
$pfxPath = Join-Path $certDir "PromptNest.pfx"
$cerPath = Join-Path $certDir "PromptNest.cer"
$mappingFile = Join-Path $artifactsDir "msix-mapping.txt"

New-Item -ItemType Directory -Force -Path $artifactsDir | Out-Null

dotnet restore PromptNest.sln
dotnet build PromptNest.sln --configuration $Configuration --no-restore

if (-not $SkipTests) {
  dotnet test PromptNest.sln --configuration $Configuration --no-build --collect:"XPlat Code Coverage" --settings coverlet.runsettings --results-directory (Join-Path $repoRoot "artifacts/test-results")
}

dotnet publish src\PromptNest.App\PromptNest.App.csproj `
  --configuration $Configuration `
  --runtime $rid `
  --self-contained false `
  -p:Platform=$Platform `
  -p:WindowsAppSDKSelfContained=true `
  -p:GenerateAppxPackageOnBuild=false

if (-not (Test-Path $publishDir)) {
  throw "Publish output not found at $publishDir"
}

if (Test-Path $zipOut) { Remove-Item $zipOut -Force }
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipOut -Force
Write-Host "Portable ZIP: $zipOut"

if ($SkipMsix) { return }

$sdkRoot = "C:\Program Files (x86)\Windows Kits\10\bin"
$makeAppx = Get-ChildItem -Path $sdkRoot -Recurse -Filter "makeappx.exe" -ErrorAction SilentlyContinue |
  Where-Object { $_.FullName -match "\\x64\\" } |
  Sort-Object FullName -Descending |
  Select-Object -First 1
$signTool = Get-ChildItem -Path $sdkRoot -Recurse -Filter "signtool.exe" -ErrorAction SilentlyContinue |
  Where-Object { $_.FullName -match "\\x64\\" } |
  Sort-Object FullName -Descending |
  Select-Object -First 1

if (-not $makeAppx -or -not $signTool) {
  Write-Warning "Windows SDK tools (makeappx.exe / signtool.exe) not found. Skipping MSIX."
  return
}

if (Test-Path $msixStaging) { Remove-Item $msixStaging -Recurse -Force }
New-Item -ItemType Directory -Force -Path $msixStaging | Out-Null
Copy-Item -Recurse -Force -Path (Join-Path $publishDir "*") -Destination $msixStaging

$manifestSource = Join-Path $repoRoot "src\PromptNest.App\Package.appxmanifest"
$manifestDest = Join-Path $msixStaging "AppxManifest.xml"
[xml]$manifestXml = Get-Content $manifestSource
$manifestXml.Package.Identity.Version = $Version
$manifestXml.Save($manifestDest)

"[Files]" | Out-File -Encoding ascii -FilePath $mappingFile
Get-ChildItem -Path $msixStaging -Recurse -File | ForEach-Object {
  $rel = $_.FullName.Substring($msixStaging.Length + 1)
  '"{0}" "{1}"' -f $_.FullName, $rel
} | Add-Content -Encoding ascii -Path $mappingFile

New-Item -ItemType Directory -Force -Path $certDir | Out-Null
if (-not (Test-Path $pfxPath)) {
  $publisher = "CN=PromptNest Dev"
  $cert = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject $publisher `
    -KeyAlgorithm RSA `
    -KeyLength 2048 `
    -CertStoreLocation Cert:\CurrentUser\My `
    -NotAfter (Get-Date).AddYears(2) `
    -KeyUsage DigitalSignature `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
  $password = $env:PROMPTNEST_CERT_PASSWORD
  if (-not $password) {
    $bytes = New-Object byte[] 24
    [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    $password = [Convert]::ToBase64String($bytes)
    $passFile = Join-Path $certDir "password.txt"
    Set-Content -Encoding utf8 -Path $passFile -Value $password
    Write-Warning "Generated cert password written to $passFile (local dev only)"
  }
  $secure = ConvertTo-SecureString -String $password -Force -AsPlainText
  Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $secure | Out-Null
  Export-Certificate -Cert $cert -FilePath $cerPath | Out-Null
}

if (Test-Path $msixOut) { Remove-Item $msixOut -Force }
& $makeAppx.FullName pack /v /h SHA256 /f $mappingFile /p $msixOut
if ($LASTEXITCODE -ne 0) { throw "makeappx pack failed" }

$pfxPwd = $env:PROMPTNEST_CERT_PASSWORD
if (-not $pfxPwd) {
  $passFile = Join-Path $certDir "password.txt"
  if (Test-Path $passFile) { $pfxPwd = (Get-Content $passFile -Raw).Trim() }
}
if ($pfxPwd) {
  & $signTool.FullName sign /fd SHA256 /a /f $pfxPath /p $pfxPwd $msixOut
  if ($LASTEXITCODE -ne 0) { throw "signtool sign failed" }
}

Write-Host "MSIX: $msixOut"
Write-Host "Cert: $cerPath"
