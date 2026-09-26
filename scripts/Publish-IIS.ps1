$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    $sdk = Join-Path $root '.tools/dotnet/dotnet.exe'
    if (!(Test-Path $sdk)) { $sdk = 'dotnet' }
    npm --prefix src/sidecil-tickets-web ci --no-fund
    if ($LASTEXITCODE -ne 0) { throw 'Falló npm ci.' }
    npm --prefix src/sidecil-tickets-web run build
    if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación del frontend.' }
    & $sdk test Sidecil.Tickets.slnx -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Fallaron las pruebas.' }
    & $sdk publish src/Sidecil.Tickets.Api -c Release -o artifacts/iis --no-self-contained
    if ($LASTEXITCODE -ne 0) { throw 'Falló la publicación.' }
    & $sdk publish src/Sidecil.Tickets.Worker -c Release -o artifacts/worker --no-self-contained
    if ($LASTEXITCODE -ne 0) { throw 'Falló la publicación del servicio de correo.' }
    $ef = Join-Path $root '.tools/ef/dotnet-ef.exe'
    if (!(Test-Path $ef)) { & $sdk tool install dotnet-ef --version 10.0.12 --tool-path .tools/ef }
    if (!(Test-Path $ef)) { throw 'No se encontró dotnet-ef.' }
    if (Test-Path (Join-Path $root '.tools/dotnet')) {
        $env:DOTNET_ROOT = Join-Path $root '.tools/dotnet'
        $env:PATH = $env:DOTNET_ROOT + ';' + $env:PATH
    }
    New-Item -ItemType Directory -Force artifacts/sql | Out-Null
    & $ef migrations script --configuration Release --no-build --idempotent --project src/Sidecil.Tickets.Infrastructure --startup-project src/Sidecil.Tickets.Api --output artifacts/sql/schema.sql
    if ($LASTEXITCODE -ne 0) { throw 'Falló la generación del SQL.' }
    Write-Host 'Paquete: artifacts/iis. Migración revisable: artifacts/sql/schema.sql.'
} finally { Pop-Location }
