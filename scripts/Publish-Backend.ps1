#requires -Version 7.0
param([string]$NodeExecutable = 'node')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$previousPath = $env:SIDECIL_API_PATH
$previousUrl = $env:VITE_API_URL
Push-Location $root
try {
    $sdk = Join-Path $root '.tools/dotnet/dotnet.exe'
    if (!(Test-Path $sdk)) { $sdk = 'dotnet' }
    $version = & $NodeExecutable -p 'process.versions.node'
    if ($LASTEXITCODE -ne 0 -or [version]$version -lt [version]'20.19.0') { throw 'Se requiere Node 20.19 o posterior.' }
    $env:PATH = (Split-Path (Get-Command $NodeExecutable).Source -Parent) + ';' + $env:PATH
    $npm = Join-Path (Split-Path (Get-Command npm.cmd).Source -Parent) 'node_modules/npm/bin/npm-cli.js'
    $env:SIDECIL_API_PATH = '/API_SoporteSidecil'
    $env:VITE_API_URL = ''
    # Recursos del chat embebido incluidos en el backend.
    & $NodeExecutable $npm --prefix src/sidecil-tickets-web run build
    if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación de los recursos del chat.' }
    # Servicio autorizado en la misma entrega; no reemplaza appsettings de IIS.
    & $sdk publish src/Sidecil.Tickets.Worker -c Release --no-self-contained -p:MailBundle=true -p:DebugType=None -p:DebugSymbols=false -o publish
    if ($LASTEXITCODE -ne 0) { throw 'Fallo la publicacion del servicio de correo.' }
    Copy-Item scripts/Configurar-Correo.ps1 (Join-Path $root 'publish/Configurar-Correo.ps1')
    & $sdk publish src/Sidecil.Tickets.Api -c Release --no-self-contained -p:DebugType=None -p:DebugSymbols=false -o publish
    if ($LASTEXITCODE -ne 0) { throw 'Falló la publicación del backend.' }
    $development = Join-Path $root 'publish/appsettings.Development.json'
    if (Test-Path $development) { Remove-Item -LiteralPath $development }
    $production = Join-Path $root 'publish/appsettings.Production.json'
    if (!(Test-Path $production)) {
        Copy-Item deploy/backend.production.example.json $production
    }
    Copy-Item scripts/Crear-Administrador.ps1 (Join-Path $root 'publish/Crear-Administrador.ps1')
    Write-Host "Backend publicado en: $(Join-Path $root 'publish')"
} finally {
    Pop-Location
    $env:SIDECIL_API_PATH = $previousPath
    $env:VITE_API_URL = $previousUrl
}
