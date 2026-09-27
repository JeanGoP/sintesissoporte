#requires -Version 5.1
#requires -RunAsAdministrator
param()
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$serviceName = 'Sidecil Tickets Mail'
$apiPath = Join-Path $root 'appsettings.Production.json'
$mailPath = Join-Path $root 'mailsettings.Production.json'
$worker = Join-Path $root 'Sidecil.Tickets.Worker.exe'
if (!(Test-Path -LiteralPath $apiPath) -or !(Test-Path -LiteralPath $worker)) { throw 'Ejecuta este archivo dentro de la carpeta publicada de la API en el servidor.' }
$apiOriginal = [IO.File]::ReadAllText($apiPath)
$apiConfig = $apiOriginal | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($apiConfig.ConnectionStrings.Tickets)) { throw 'Falta la conexion SQL en appsettings.Production.json.' }
# El servicio usa LocalService, no la identidad del grupo IIS.
$connection = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$connection.set_ConnectionString($apiConfig.ConnectionStrings.Tickets)
if ($connection.get_IntegratedSecurity()) { throw 'La instalacion automatica requiere una conexion SQL con usuario y contrasena. Con autenticacion Windows se debe configurar primero una cuenta de servicio con acceso SQL.' }
foreach ($entry in [Environment]::GetEnvironmentVariables('Machine').GetEnumerator()) {
    if ($entry.Key -like 'Mail__*' -or $entry.Key -eq 'ConnectionStrings__Tickets') {
        throw 'Hay variables globales de correo o SQL. Revisalas antes de usar este instalador para evitar que sustituyan la configuracion privada.'
    }
}
$existing = Get-CimInstance Win32_Service | Where-Object { $_.Name -eq $serviceName }
$command = '"' + $worker + '" --environment Production --contentRoot "' + $root + '"'
if ($existing -and ($existing.PathName -ne $command -or $existing.StartName -ne 'NT AUTHORITY\LocalService')) { throw 'Ya existe un servicio con otra ruta o identidad. No se modifico.' }
$from = Read-Host 'Direccion remitente autorizada en Turbo'
$inbox = Read-Host 'Direccion de Gmail para recibir respuestas'
foreach ($address in @($from, $inbox)) {
    $parsed = New-Object System.Net.Mail.MailAddress($address)
    if ($parsed.Address -ne $address) { throw 'Escribe solamente la direccion de correo.' }
}
$key = Read-Host 'Consumer Key de Turbo' -AsSecureString
$secret = Read-Host 'Consumer Secret de Turbo' -AsSecureString
$gmail = Read-Host 'Contrasena de aplicacion de Gmail' -AsSecureString
function PlainText($secure) {
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
}
function Set-Property($object, $name, $value) { $object | Add-Member -NotePropertyName $name -NotePropertyValue $value -Force }
function Save-PrivateConfig($path, $text) {
    if (!(Test-Path -LiteralPath $path)) { [IO.File]::WriteAllText($path, '') }
    # Solo administradores, SYSTEM y la cuenta del servicio tienen acceso a estas claves.
    $acl = New-Object System.Security.AccessControl.FileSecurity
    $acl.SetAccessRuleProtection($true, $false)
    foreach ($sid in @('S-1-5-32-544', 'S-1-5-18')) {
        $identity = New-Object System.Security.Principal.SecurityIdentifier($sid)
        $rule = New-Object System.Security.AccessControl.FileSystemAccessRule($identity, 'FullControl', 'Allow')
        $acl.AddAccessRule($rule)
    }
    $identity = New-Object System.Security.Principal.SecurityIdentifier('S-1-5-19')
    $rule = New-Object System.Security.AccessControl.FileSystemAccessRule($identity, 'Read', 'Allow')
    $acl.AddAccessRule($rule)
    Set-Acl -LiteralPath $path -AclObject $acl
    [IO.File]::WriteAllText($path, $text, (New-Object System.Text.UTF8Encoding($false)))
}
$previousEnvironment = @{}
$mailOriginal = if (Test-Path -LiteralPath $mailPath) { [IO.File]::ReadAllText($mailPath) } else { $null }
$created = $false
$changed = $false
$wasRunning = $existing -and $existing.State -eq 'Running'
try {
    $mail = [ordered]@{
        Mode = 'Smtp'; FromAddress = $from; FromName = 'Sidecil Soporte'; ReplyToAddress = $inbox
        PublicBaseUrl = 'https://soporte.sintesiserp.com.co'
        SmtpHost = 'pro.turbo-smtp.com'; SmtpPort = 465
        SmtpUser = (PlainText $key); SmtpPassword = (PlainText $secret); SmtpAccessToken = ''
        ImapHost = 'imap.gmail.com'; ImapPort = 993; ImapUser = $inbox
        ImapPassword = ((PlainText $gmail) -replace '\s', ''); ImapAccessToken = ''; ImapFolder = 'INBOX'
        AccountKey = $inbox.ToLowerInvariant(); TrustedAuthenticationService = 'mx.google.com'
    }
    if (!$mail.SmtpUser -or !$mail.SmtpPassword -or !$mail.ImapPassword) { throw 'Las claves no pueden estar vacias.' }
    if ($mailOriginal) {
        $old = $mailOriginal | ConvertFrom-Json
        if ($old.Mail.ImapUser -and $old.Mail.ImapUser -ne $inbox) { throw 'Cambiar de buzon requiere revisar antes el historial de importacion.' }
        if ($old.Mail.AccountKey) { $mail.AccountKey = $old.Mail.AccountKey }
    }
    foreach ($entry in $mail.GetEnumerator()) {
        $name = 'Mail__' + $entry.Key
        $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
        [Environment]::SetEnvironmentVariable($name, [string]$entry.Value, 'Process')
    }
    if ($env:ConnectionStrings__Tickets) { throw 'Existe ConnectionStrings__Tickets en el entorno. Unifica la conexion en appsettings.Production.json antes de instalar.' }
    Write-Host 'Comprobando SQL, Turbo y Gmail sin enviar ni importar correos...'
    & $worker --environment Production --contentRoot $root --check-mail
    if ($LASTEXITCODE -ne 0) { throw 'No se activaron las solicitudes sin cuenta. Corrige la comprobacion indicada y ejecuta de nuevo.' }
    # El servicio debe leer bibliotecas y la conexion SQL existente, sin permisos de escritura.
    & icacls.exe $root /grant '*S-1-5-19:(OI)(CI)(RX)' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'No fue posible conceder lectura al servicio.' }
    & icacls.exe $apiPath /grant '*S-1-5-19:(R)' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'No fue posible conceder lectura a la configuracion SQL.' }
    if ($existing) { Stop-Service -Name $serviceName; (Get-Service $serviceName).WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30)) }
    $changed = $true
    Save-PrivateConfig $mailPath (@{ Mail = $mail } | ConvertTo-Json -Depth 10)
    if (!$existing) {
        $emptyPassword = New-Object System.Security.SecureString
        try {
            $serviceIdentity = New-Object System.Management.Automation.PSCredential('NT AUTHORITY\LocalService', $emptyPassword)
            New-Service -Name $serviceName -BinaryPathName $command -StartupType Automatic -Credential $serviceIdentity -DisplayName 'Sidecil - Correo de soporte' | Out-Null
        } finally { $emptyPassword.Dispose() }
        $created = $true
    }
    Start-Service -Name $serviceName
    (Get-Service $serviceName).WaitForStatus('Running', [TimeSpan]::FromSeconds(30))
    if (!$apiConfig.Mail) { Set-Property $apiConfig 'Mail' ([PSCustomObject]@{}) }
    Set-Property $apiConfig.Mail 'Mode' 'Smtp'
    Set-Property $apiConfig.Mail 'PublicBaseUrl' $mail.PublicBaseUrl
    [IO.File]::WriteAllText($apiPath, ($apiConfig | ConvertTo-Json -Depth 30), (New-Object System.Text.UTF8Encoding($false)))
    Write-Host 'Servicio iniciado. Recicla SOLO el grupo IIS de la API y prueba una solicitud sin cuenta.'
    Write-Host 'Conserva appsettings.Production.json y mailsettings.Production.json en futuras actualizaciones.'
    Write-Host 'Las respuestas que no superen la verificacion de remitente quedan en revision en Correo de atencion.'
} catch {
    if ($changed) {
        Stop-Service -Name $serviceName -ErrorAction SilentlyContinue
        [IO.File]::WriteAllText($apiPath, $apiOriginal, (New-Object System.Text.UTF8Encoding($false)))
        if ($null -ne $mailOriginal) { Save-PrivateConfig $mailPath $mailOriginal }
        elseif (Test-Path -LiteralPath $mailPath) { Remove-Item -LiteralPath $mailPath }
        if ($created) { & sc.exe delete $serviceName | Out-Null }
        elseif ($wasRunning) { Start-Service -Name $serviceName }
    }
    throw
} finally {
    foreach ($name in $previousEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], 'Process') }
    $key.Dispose(); $secret.Dispose(); $gmail.Dispose()
    $mail = $null; $mailOriginal = $null; $apiOriginal = $null
}