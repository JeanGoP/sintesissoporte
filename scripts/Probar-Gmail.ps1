#requires -Version 5.1
$ErrorActionPreference = 'Stop'
$worker = Join-Path $PSScriptRoot 'Sidecil.Tickets.Worker.exe'
if (!(Test-Path -LiteralPath $worker)) { throw 'Copia este archivo junto a los archivos publicados del servicio.' }
$email = (Read-Host 'Cuenta de Gmail que genero la contrasena de aplicacion').Trim()
$parsed = New-Object System.Net.Mail.MailAddress($email)
if ($parsed.Address -ne $email) { throw 'Escribe solamente la direccion de correo.' }
$password = Read-Host 'Contrasena de aplicacion de Gmail (16 caracteres)' -AsSecureString
$previous = @{}
$pointer = [IntPtr]::Zero
try {
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($password)
    $plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) -replace '\s', ''
    if ($plain.Length -ne 16) { throw 'La contrasena de aplicacion debe tener 16 caracteres, sin contar espacios.' }
    $values = @{ ImapHost = 'imap.gmail.com'; ImapPort = '993'; ImapUser = $email; ImapPassword = $plain; ImapAccessToken = ''; ImapFolder = 'INBOX' }
    foreach ($entry in $values.GetEnumerator()) {
        $name = 'Mail__' + $entry.Key
        $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
        [Environment]::SetEnvironmentVariable($name, $entry.Value, 'Process')
    }
    & $worker --environment Production --contentRoot $PSScriptRoot --check-imap
    $resultCode = $LASTEXITCODE
} finally {
    foreach ($name in $previous.Keys) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
    if ($pointer -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
    $password.Dispose(); $plain = $null; $values = $null
}
exit $resultCode