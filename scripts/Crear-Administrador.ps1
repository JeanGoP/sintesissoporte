#requires -Version 5.1
[CmdletBinding()]
param([string]$CarpetaBackend = $PSScriptRoot)
$ErrorActionPreference = 'Stop'
$CarpetaBackend = (Resolve-Path -LiteralPath $CarpetaBackend).Path
if (!(Test-Path -LiteralPath (Join-Path $CarpetaBackend 'Sidecil.Tickets.Api.dll'))) {
    throw 'Copia este script junto a Sidecil.Tickets.Api.dll en el servidor y ejecutalo desde esa carpeta.'
}
if (!(Test-Path -LiteralPath (Join-Path $CarpetaBackend 'appsettings.Production.json'))) {
    throw 'Falta appsettings.Production.json con la conexion a SintesisCloudSoporte.'
}
$null = Get-Command dotnet -ErrorAction Stop
$correo = (Read-Host 'Correo del administrador').Trim()
try { $direccion = [System.Net.Mail.MailAddress]::new($correo) } catch { throw 'El correo no es valido.' }
if ($direccion.Address -ne $correo) { throw 'Escribe solo el correo, sin nombre ni otros caracteres.' }
Write-Host 'Usa minimo 12 caracteres, mayuscula, minuscula, numero y simbolo.'
$clave = Read-Host 'Contrasena del administrador' -AsSecureString
if ($clave.Length -lt 12) { $clave.Dispose(); throw 'La contrasena debe tener al menos 12 caracteres.' }
$anteriores = @{}
foreach ($nombre in @('ASPNETCORE_ENVIRONMENT','SIDECIL_ADMIN_EMAIL','SIDECIL_BOOTSTRAP_PASSWORD')) {
    $anteriores[$nombre] = [Environment]::GetEnvironmentVariable($nombre, 'Process')
}
Push-Location -LiteralPath $CarpetaBackend
try {
    $env:ASPNETCORE_ENVIRONMENT = 'Production'
    $env:SIDECIL_ADMIN_EMAIL = $correo
    $env:SIDECIL_BOOTSTRAP_PASSWORD = [System.Net.NetworkCredential]::new('', $clave).Password
    & dotnet .\Sidecil.Tickets.Api.dll --bootstrap
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo inicializar el administrador. Revisa el mensaje anterior.' }
    Write-Host 'Proceso terminado. Si aparece Administrador inicial creado, ingresa con el correo y la contrasena que acabas de indicar.'
    Write-Host 'Si la base ya tiene usuarios, el backend no crea ni modifica cuentas.'
} finally {
    foreach ($nombre in $anteriores.Keys) {
        [Environment]::SetEnvironmentVariable($nombre, $anteriores[$nombre], 'Process')
    }
    $clave.Dispose()
    Pop-Location
}
