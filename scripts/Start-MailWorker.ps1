param([switch]$Once)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Set-Location $root
$env:DOTNET_ENVIRONMENT='Development'
$env:Mail__Mode='Pickup'
$env:Mail__PickupDirectory=Join-Path $root '.local/mail/out'
$env:Mail__InboxDirectory=Join-Path $root '.local/mail/in'
$env:ConnectionStrings__Tickets='Server=(localdb)\MSSQLLocalDB;Database=SidecilTicketsDev;Trusted_Connection=True;TrustServerCertificate=True'
$sdk=Join-Path $root '.tools/dotnet/dotnet.exe'
if (!(Test-Path $sdk)) { $sdk='dotnet' }
if ($Once) { & $sdk run --project src/Sidecil.Tickets.Worker --no-launch-profile -- --once }
else { & $sdk run --project src/Sidecil.Tickets.Worker --no-launch-profile }
