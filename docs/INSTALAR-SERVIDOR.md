# Instalar Sidecil Tickets en Windows / IIS

Configuración elegida: portal React en Coolify y API/SQL/Worker en tu servidor. Seguir también COOLIFY.md para los dos dominios, CORS, callbacks OAuth y VITE_API_URL. El paquete IIS conserva los recursos del widget embebido; el portal principal se publica por separado.

## Qué debes subir

Desde la raíz del proyecto, con PowerShell 7, Node >=20.19 y el SDK indicado por global.json:

```powershell
./scripts/Publish-Server.ps1
```

Si hay varias instalaciones de Node, se puede indicar `-NodeExecutable "C:\ruta\node.exe"`. El script verifica la versión antes de compilar. Las pruebas de identidad requieren la base de desarrollo inicializada. Cada ejecución crea una entrega nueva en `artifacts/servidor/AAAAMMDD-HHMMSS` y un ZIP junto a ella. No modifica los paquetes de la demostración local en ejecución. Si el proceso falla, esa carpeta parcial no es una entrega válida: utiliza únicamente la que termine con «Entrega verificada».

- `iis/`: backend, frontend compilado y web.config. Copiar su contenido al sitio de IIS.
- `worker/`: proceso de correo. Copiar a una carpeta separada e instalar como servicio.
- `sql/schema.sql`: esquema y migraciones idempotentes; NO contiene los tickets ni cuentas de tu base local.
- `version.json` y `sha256.json`: revisión de origen y huellas de los archivos.
- Guías y plantillas: completar únicamente en el servidor. Las plantillas `.example` no se cargan automáticamente.

El paquete depende de .NET 10 instalado en el servidor. No necesita Node ni el SDK para ejecutarse. No es un paquete de contenedores para Coolify.

## 1. Preparar el servidor

1. Windows Server compatible con .NET 10, de 64 bits, IIS y ASP.NET Core Hosting Bundle 10 actualizado. Instalar IIS antes del bundle; si IIS se instaló después, reparar el bundle. Reiniciar los servicios o el servidor según el instalador, considerando otros sitios alojados.
2. SQL Server accesible y una base dedicada llamada, por ejemplo, SidecilTickets. No usar LocalDB en el servidor.
3. Dominio o subdominio propio y certificado HTTPS válido. Configurar DNS y puertos 80/443 según la infraestructura. Mantener SQL accesible solamente desde los servidores autorizados, preferiblemente por red privada/VPN.
4. Crear carpetas, por ejemplo `C:\Sitios\SidecilTickets`, `C:\Servicios\SidecilTicketsMail` y `C:\ProgramData\SidecilTickets\keys`.
5. Crear un pool IIS dedicado, «No Managed Code», 64 bits y «Load User Profile = True». Su identidad necesita lectura/ejecución del sitio y modificación de la carpeta de claves. Evitar permisos para Everyone. Los secretos de configuración solo deben ser legibles por administradores y la identidad correspondiente.

## 2. Configurar la API

Copiar `iis/` a la carpeta del sitio. En IIS conservar el sitio y binding HTTPS existentes de sintesiserp.com.co. Para esta instalación, crear una aplicación IIS de alias `soporte` dentro del sitio existente sintesiserp.com.co, con pool propio y ruta física a esta carpeta. Quedará en `https://sintesiserp.com.co/soporte`. No reemplazar el sitio ERP existente. El script publica los recursos con base `/soporte/`; para otra ruta usar -ApiPath.

Copiar `appsettings.Production.json.example` como `appsettings.Production.json` en el servidor y completar:

| Campo | Qué configurar |
|---|---|
| Frontend:PublicBaseUrl | Origen HTTPS del portal en Coolify; habilita CORS y retornos OAuth |
| AllowedHosts | Dominio de la API, sin https ni ruta; varios separados por punto y coma |
| ConnectionStrings:Tickets | Conexión SQL de ejecución; cifrado y certificado válido |
| DataProtection:Path | Carpeta persistente de claves, fuera del sitio |
| Mail:PublicBaseUrl | URL HTTPS pública exacta |
| Mail:Mode | Disabled hasta conectar el correo; luego Smtp |
| ExternalLogin | Credenciales de las aplicaciones Microsoft/Google si se habilitan |

Establecer `ASPNETCORE_ENVIRONMENT=Production` en la configuración del sitio/pool. Las variables de entorno usan doble guion bajo, por ejemplo `ConnectionStrings__Tickets`, y prevalecen sobre el JSON. Una variable creada en una terminal no configura automáticamente IIS ni un servicio.

La plantilla SQL usa autenticación integrada: hay que conceder acceso a la identidad real del pool y del Worker. Para SQL remoto usar una identidad de dominio apropiada o autenticación SQL con una cuenta de ejecución dedicada. Si se usa SQL login, sustituir Integrated Security por User ID y Password; guardar la contraseña solamente en la configuración privada del servidor. No dar sysadmin/db_owner al proceso normal. No desactivar la validación del certificado SQL como solución permanente.

Las claves se cifran con DPAPI en Windows. Conservar la identidad del pool y su perfil entre despliegues. Respaldar las claves y planificar su recuperación en otra máquina; copiar solo los XML no garantiza poder descifrarlos.

## 3. Preparar SQL y el administrador

Crear una base vacía y ejecutar `sql/schema.sql` contra ESA base desde SSMS con una identidad de despliegue. Revisar el script y hacer copia de seguridad antes de aplicarlo a una base existente. La aplicación no migra automáticamente al arrancar.

Después, desde la carpeta del sitio, abrir PowerShell con acceso de despliegue a SQL y ejecutar:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Production'
$env:SIDECIL_ADMIN_EMAIL = Read-Host 'Correo del administrador inicial'
$sidecilClave = Read-Host 'Contraseña inicial (mínimo 12 caracteres, mayúscula, minúscula, número y símbolo)' -AsSecureString
try {
    $env:SIDECIL_BOOTSTRAP_PASSWORD = [System.Net.NetworkCredential]::new('', $sidecilClave).Password
    # Si el JSON usa una cuenta SQL de ejecución sin DDL, configurar en esta sesión
    # ConnectionStrings__Tickets con una conexión de despliegue privada.
    dotnet .\Sidecil.Tickets.Api.dll --bootstrap
    if ($LASTEXITCODE -ne 0) { throw 'La inicialización falló; revisar el error antes de continuar.' }
} finally {
    Remove-Item Env:SIDECIL_BOOTSTRAP_PASSWORD -ErrorAction SilentlyContinue
    Remove-Item Env:SIDECIL_ADMIN_EMAIL -ErrorAction SilentlyContinue
    Remove-Item Env:ConnectionStrings__Tickets -ErrorAction SilentlyContinue
    $sidecilClave.Dispose()
}
```

Bootstrap también comprueba/aplica migraciones, por lo que exige permisos de despliegue. Si la base ya tiene usuarios no crea ni restablece cuentas. No utilizar `--seed-demo` en producción. Las credenciales locales de demostración no se transfieren. Iniciar después IIS con la cuenta SQL de ejecución, sin permisos de migración.

## 4. Instalar el servicio de correo

Copiar `worker/` a `C:\Servicios\SidecilTicketsMail`. Completar su propia `appsettings.Production.json` a partir de la plantilla. Debe apuntar a la MISMA base SQL y URL pública que la API. Establecer `DOTNET_ENVIRONMENT=Production` para el servicio (Production también es el valor predeterminado si no existe una variable que lo cambie).

Instalar una vez, en PowerShell como administrador, adaptando las rutas:

```powershell
New-Service -Name 'SidecilTicketsMail' -DisplayName 'Sidecil Tickets Mail' -BinaryPathName '"C:\Servicios\SidecilTicketsMail\Sidecil.Tickets.Worker.exe" --contentRoot "C:\Servicios\SidecilTicketsMail"' -StartupType Automatic
```

Antes de iniciarlo, en services.msc seleccionar una cuenta de servicio dedicada en «Iniciar sesión», concederle acceso a la carpeta/configuración y SQL, y configurar recuperación tras fallo. No dejar el servicio operando como LocalSystem por defecto. Iniciar solo después de completar configuración y migraciones.

Para activar correo establecer `Mail:Mode=Smtp` en AMBOS procesos. El Worker necesita SMTP, IMAP, remitente y `TrustedAuthenticationService` verificado con el proveedor. Consultar CORREO.md. Mantener un único lector IMAP por buzón. Permitir conexiones salientes a SMTP/IMAP y, si se usa acceso externo, a los proveedores de identidad.

Microsoft/Google para INICIAR SESIÓN y Microsoft/Google como BUZÓN son configuraciones distintas. La renovación automática OAuth del buzón todavía no está implementada. Sin correo real, los invitados no podrán completar su verificación ni recibirse respuestas por correo. No anunciar esa función como operativa antes de probarla.

## 5. Verificar antes de atender clientes

- `/soporte/health/live` debe devolver 200; `/soporte/health/ready`, 200 cuando SQL sea accesible. Readiness confirma conexión, no que todo el esquema/correo funcione.
- Abrir el FRONTEND en Coolify y entrar con el administrador, crear un agente y un cliente, comprobar permisos, ticket y notas internas.
- Probar un invitado real: verificación, creación, confirmación, respuesta del agente y respuesta por correo del cliente.
- Registrar callbacks HTTPS `/signin-microsoft` y `/signin-google` para el dominio de la API si se activan esos botones; vincular desde Mi cuenta.
- Registrar en Administración de chat los orígenes HTTPS exactos del ERP y probar el widget desde ese dominio.
- Reciclar el pool y reiniciar el Worker: comprobar sesiones, claves, cola y logs.
- Los adjuntos del chat se almacenan en SQL; incluirlos en capacidad/backup. El análisis antimalware y los adjuntos entrantes por correo siguen pendientes.

## Actualizaciones, respaldos y errores

Antes de actualizar respaldar SQL, configuración privada y claves. Detener el Worker y poner el sitio fuera de servicio o detener su pool antes de sustituir binarios. Aplicar el SQL revisado, copiar la nueva entrega y conservar appsettings.Production.json y las claves. Reiniciar y verificar. El rollback de binarios solo es seguro si el esquema continúa siendo compatible; probar restauración de backups.

500.19/500.21: revisar web.config, instalación del módulo ASP.NET Core y permisos. 500.30: revisar Visor de eventos, runtime, conexión y claves; habilitar stdout temporalmente con carpeta de logs privada y volver a desactivarlo. 503 en readiness: revisar red, credenciales y certificado SQL. Correos en cola: comprobar servicio Worker, proveedor y configuración. No publicar logs con secretos.

La entrega se compila y prueba localmente; la validación bajo IIS real y con proveedores externos debe hacerse en tu servidor. Revisar también los pendientes de producto del README del repositorio antes de abrirlo a clientes.

Fuente oficial: https://learn.microsoft.com/en-us/aspnet/core/tutorials/publish-to-iis?view=aspnetcore-10.0
