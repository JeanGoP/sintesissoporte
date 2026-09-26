$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
$sdk = Join-Path $root '.tools/dotnet/dotnet.exe'
if (!(Test-Path $sdk)) { $sdk = 'dotnet' }
$env:ASPNETCORE_ENVIRONMENT = 'Development'
& $sdk run --project src/Sidecil.Tickets.Api --no-launch-profile --urls http://localhost:5080
