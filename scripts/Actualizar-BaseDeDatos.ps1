#requires -Version 5.1
$ErrorActionPreference = 'Stop'
$previousAspnet = $env:ASPNETCORE_ENVIRONMENT
$previousDotnet = $env:DOTNET_ENVIRONMENT
Push-Location $PSScriptRoot
try {
    $env:ASPNETCORE_ENVIRONMENT = 'Production'
    $env:DOTNET_ENVIRONMENT = 'Production'
    & dotnet .\Sidecil.Tickets.Api.dll --update-database
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo actualizar la base. Conserva el mensaje de error para revisarlo.' }
} finally {
    $env:ASPNETCORE_ENVIRONMENT = $previousAspnet
    $env:DOTNET_ENVIRONMENT = $previousDotnet
    Pop-Location
}
