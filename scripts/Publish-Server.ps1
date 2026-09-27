#requires -Version 7.0
[CmdletBinding()]
param([string]$NodeExecutable = 'node')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    $sdk = Join-Path $root '.tools/dotnet/dotnet.exe'
    if (Test-Path $sdk) {
        $env:DOTNET_ROOT = Split-Path $sdk -Parent
        $env:PATH = $env:DOTNET_ROOT + ';' + $env:PATH
    } else { $sdk = 'dotnet' }
    $nodeVersion = (& $NodeExecutable -p 'process.versions.node')
    if ($LASTEXITCODE -ne 0 -or [version]$nodeVersion -lt [version]'20.19.0') { throw 'Se requiere Node >=20.19. Usa -NodeExecutable para seleccionar otra instalación.' }
    $env:PATH = (Split-Path (Get-Command $NodeExecutable -ErrorAction Stop).Source -Parent) + ';' + $env:PATH
    $npmCommand = (Get-Command npm.cmd -ErrorAction Stop).Source
    $npmCli = Join-Path (Split-Path $npmCommand -Parent) 'node_modules/npm/bin/npm-cli.js'
    if (!(Test-Path $npmCli)) { throw 'No se encontró npm-cli.js junto a npm.cmd. Revisa la instalación de Node/npm.' }
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $release = Join-Path $root "artifacts/servidor/$stamp"
    if (Test-Path $release) { throw 'La carpeta de entrega ya existe. Ejecuta de nuevo dentro de un segundo.' }
    & $NodeExecutable $npmCli --prefix src/sidecil-tickets-web ci --no-fund
    if ($LASTEXITCODE -ne 0) { throw 'Falló la instalación del frontend.' }
    & $NodeExecutable $npmCli --prefix src/sidecil-tickets-web run build
    if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación del frontend.' }
    & $sdk test Sidecil.Tickets.slnx -c Release --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw 'Fallaron las pruebas; no se genera una entrega.' }
    foreach ($entry in @(@{Project='Sidecil.Tickets.Api'; Folder='iis'}, @{Project='Sidecil.Tickets.Worker'; Folder='worker'})) {
        $target = Join-Path $release $entry.Folder
        & $sdk publish "src/$($entry.Project)" -c Release --no-self-contained -o $target
        if ($LASTEXITCODE -ne 0) { throw "Falló la publicación de $($entry.Project)." }
        # La entrega nueva no debe transportar configuraciones de desarrollo ni secretos locales.
        Get-ChildItem -LiteralPath $target -Filter 'appsettings*.json' | Where-Object { $_.Name -ne 'appsettings.json' } | ForEach-Object {
            Remove-Item -LiteralPath $_.FullName
        }
        Get-ChildItem -LiteralPath $target -Filter '*.pdb' | ForEach-Object { Remove-Item -LiteralPath $_.FullName }
    }
    $ef = Join-Path $root '.tools/ef/dotnet-ef.exe'
    if (!(Test-Path $ef)) {
        & $sdk tool install dotnet-ef --version 10.0.12 --tool-path .tools/ef
        if ($LASTEXITCODE -ne 0) { throw 'No se pudo instalar la herramienta de migraciones.' }
    }
    New-Item -ItemType Directory -Path (Join-Path $release 'sql') -Force | Out-Null
    & $ef migrations script --configuration Release --no-build --idempotent --project src/Sidecil.Tickets.Infrastructure --startup-project src/Sidecil.Tickets.Api --output (Join-Path $release 'sql/schema.sql')
    if ($LASTEXITCODE -ne 0) { throw 'Falló la generación del SQL.' }
    Copy-Item docs/INSTALAR-SERVIDOR.md (Join-Path $release 'LEEME.md')
    Copy-Item docs/CORREO.md,docs/ACCESO-EXTERNO.md,docs/COOLIFY.md -Destination $release
    Copy-Item deploy/iis/api.production.example.json (Join-Path $release 'iis/appsettings.Production.json.example')
    Copy-Item deploy/iis/worker.production.example.json (Join-Path $release 'worker/appsettings.Production.json.example')
    $commit = & git rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo identificar la revisión Git.' }
    $dirty = [bool](& git status --porcelain)
    [ordered]@{ FechaUtc=[DateTime]::UtcNow.ToString('o'); CommitBase=$commit; CambiosLocales=$dirty; Destino='Windows / IIS'; Runtime='ASP.NET Core 10, dependiente del framework'; Pruebas='dotnet test Release'; } | ConvertTo-Json | Set-Content (Join-Path $release 'version.json') -Encoding utf8
    foreach ($required in @('iis/web.config','iis/Sidecil.Tickets.Api.dll','iis/wwwroot/index.html','worker/Sidecil.Tickets.Worker.exe','sql/schema.sql')) {
        if (!(Test-Path (Join-Path $release $required))) { throw "Falta un archivo obligatorio: $required" }
    }
    $hashes = Get-ChildItem -LiteralPath $release -Recurse -File | ForEach-Object {
        [ordered]@{ Archivo=[IO.Path]::GetRelativePath($release,$_.FullName); SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    }
    $hashes | ConvertTo-Json -Depth 3 | Set-Content (Join-Path $release 'sha256.json') -Encoding utf8
    Compress-Archive -Path (Join-Path $release '*') -DestinationPath "$release.zip"
    Write-Host "Entrega verificada: $release"
    Write-Host "Archivo para transferir: $release.zip"
} finally { Pop-Location }
