$ErrorActionPreference = 'Stop'
$profile = Join-Path $PSScriptRoot '..\profiles\pubg_s10lite.json'
if (-not (Test-Path $profile)) { throw "Missing profile: $profile" }
$obj = (Get-Content -Raw -Path $profile) | ConvertFrom-Json
if (-not $obj) { throw 'Profile JSON is empty or invalid.' }
if (-not $obj.coordinateSystem -or $obj.coordinateSystem.type -ne 'normalized') { throw 'coordinateSystem.type must be normalized.' }
function Test-Point($name, $p) {
  if ($null -eq $p) { throw "Missing point: $name" }
  if ($p.x -lt 0 -or $p.x -gt 1 -or $p.y -lt 0 -or $p.y -gt 1) { throw "Out-of-range normalized coordinate for ${name}: $($p.x),$($p.y)" }
}
Test-Point 'movement.center' $obj.input.movement.center
Test-Point 'mouseLook.center' $obj.input.mouseLook.center
foreach ($property in $obj.buttons.PSObject.Properties) { Test-Point "buttons.$($property.Name)" $property.Value }
if (-not $obj.input.mouseLook.enabled) { throw 'Mouse look must be enabled.' }
if ($obj.input.movement.keys.W -ne 'up' -or $obj.input.movement.keys.A -ne 'left' -or $obj.input.movement.keys.S -ne 'down' -or $obj.input.movement.keys.D -ne 'right') { throw 'WASD movement mapping is invalid.' }
Write-Host "Profile validation passed: $($obj.buttons.PSObject.Properties.Count) buttons + movement + mouse look."
