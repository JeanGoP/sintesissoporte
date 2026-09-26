$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    $sdk = Join-Path $root '.tools/dotnet/dotnet.exe'
    if (!(Test-Path $sdk)) { $sdk = 'dotnet' }
    New-Item -ItemType Directory -Force '.local' | Out-Null
    $credentialsPath = Join-Path $root '.local/demo-credentials.json'
    if (!(Test-Path $credentialsPath)) {
        $random = [System.Security.Cryptography.RandomNumberGenerator]::GetBytes(24)
        $password = [Convert]::ToBase64String($random) + 'aA1!'
        @{ email='admin@sidecil.local'; password=$password; accounts=@('admin@sidecil.local','agente@sidecil.local','empleado@sidecil.local','cliente@sidecil.local') } |
            ConvertTo-Json | Set-Content -LiteralPath $credentialsPath
    }
    $credentials = Get-Content -Raw -LiteralPath $credentialsPath | ConvertFrom-Json
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:SIDECIL_ADMIN_EMAIL = $credentials.email
    $env:SIDECIL_BOOTSTRAP_PASSWORD = $credentials.password
    & $sdk run --project src/Sidecil.Tickets.Api --no-launch-profile -- --seed-demo
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo inicializar la base de desarrollo.' }
    Write-Host 'Credenciales de desarrollo: .local/demo-credentials.json (archivo excluido del repositorio).'
} finally {
    Remove-Item Env:SIDECIL_BOOTSTRAP_PASSWORD -ErrorAction SilentlyContinue
    Pop-Location
}
