param(
  [switch]$SkipTest
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

Write-Host '== Build (single-node MSBuild) =='
# -m:1 is required: multi-node MSBuild needs named pipes and fails silently in confined environments.
# Keep this file ASCII-only: Windows PowerShell 5.1 reads scripts without a BOM using the ANSI code page,
# and non-ASCII bytes can swallow the following newline, breaking the script.
& dotnet build MelonAdapter.slnx -c Release -m:1 -nodeReuse:false
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE" }

if (-not $SkipTest) {
  Write-Host '== Self test =='
  $selfTest = Join-Path $root 'tests\MelonAdapter.SelfTest\bin\Release\net472\MelonAdapter.SelfTest.exe'
  if (-not (Test-Path $selfTest)) { throw "Self test executable not found: $selfTest" }
  & $selfTest
  if ($LASTEXITCODE -ne 0) { throw "Self test failed with exit code $LASTEXITCODE" }
}

Write-Host '== Stage UMM package =='
$dist = Join-Path $root 'dist\MelonAdapter'
$zip = Join-Path $root 'dist\MelonAdapter.zip'
if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
if (Test-Path $zip) {
  try {
    Remove-Item $zip -Force -ErrorAction Stop
  } catch {
    # The archive may still be open in an explorer preview or an archiver. Keep that file and
    # write a versioned archive next to it instead of failing the whole release.
    $version = (Get-Content (Join-Path $root 'packaging\UMM\MelonAdapter\Info.json') -Raw | ConvertFrom-Json).Version
    $zip = Join-Path $root ("dist\MelonAdapter-" + $version + ".zip")
    Write-Host "The existing archive is in use; writing $zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
  }
}
New-Item -ItemType Directory -Path (Join-Path $dist 'Dependencies') -Force | Out-Null

$outputs = [ordered]@{
  'src\MelonLoader.Facade\bin\Release\net472\MelonLoader.dll' = 'MelonLoader.dll'
  'src\MelonAdapter.Runtime\bin\Release\net472\MelonAdapter.Runtime.dll' = 'MelonAdapter.Runtime.dll'
  'src\MelonAdapter.Host\bin\Release\net472\MelonAdapter.dll' = 'MelonAdapter.dll'
  'src\MelonAdapter.Runtime\Localization\lang.json' = 'lang.json'
  'packaging\UMM\MelonAdapter\Info.json' = 'Info.json'
  'packaging\UMM\MelonAdapter\README.txt' = 'README.txt'
}
foreach ($source in $outputs.Keys) {
  $full = Join-Path $root $source
  if (-not (Test-Path $full)) { throw "Missing build output: $full" }
  Copy-Item $full (Join-Path $dist $outputs[$source])
}

$harmony = Join-Path $root 'libs\0Harmony.dll'
if (Test-Path $harmony) {
  Copy-Item $harmony (Join-Path $dist 'Dependencies\0Harmony.dll')
} else {
  Write-Warning 'libs\0Harmony.dll not found; the package will start without a Harmony provider.'
}

Compress-Archive -Path (Join-Path $dist '*') -DestinationPath $zip
Write-Host "Packaged: $zip"
