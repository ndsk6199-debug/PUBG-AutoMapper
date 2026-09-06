$ErrorActionPreference = 'Stop'
$profile = Join-Path $PSScriptRoot '..\profiles\pubg_s10lite.json'
if (-not (Test-Path $profile)) { throw "Missing profile: $profile" }
$raw = Get-Content -Raw -Path $profile
$obj = $raw | ConvertFrom-Json
if (-not $obj) { throw 'Profile JSON is empty or invalid.' }
if (-not $obj.points) { throw 'Profile must contain points.' }
foreach ($p in $obj.points) {
  if ($p.x -lt 0 -or $p.x -gt 1 -or $p.y -lt 0 -or $p.y -gt 1) {
    throw "Out-of-range normalized coordinate for $($p.name): $($p.x),$($p.y)"
  }
}
Write-Host "Profile validation passed: $($obj.points.Count) points."
