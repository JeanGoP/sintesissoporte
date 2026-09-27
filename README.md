# Sidecil Tickets

Primera versión funcional de la arquitectura aprobada: ASP.NET Core .NET 10, React + TypeScript y SQL Server. Atiende empleados y clientes externos con permisos diferenciados. Este es un entorno de desarrollo y validación, todavía no una puesta en producción.

## Correo y clientes sin cuenta (versión 0.2)

Ya están implementados el formulario público /solicitar, la verificación de correo, la confirmación con número de ticket y plantilla personalizable, el envío de respuestas públicas y la incorporación de respuestas al ticket mediante un Worker independiente. En el entorno local se simulan los correos; falta conectar el buzón empresarial. Ver docs/CORREO.md para configuración y límites.

## Funciones implementadas

- Acceso con ASP.NET Core Identity, cookies HttpOnly, protección CSRF, bloqueo por intentos y límite de solicitudes de login.
- Roles de administrador, agente y solicitante. Agentes limitados a su equipo; solicitantes limitados a sus tickets y organización.
- Creación de tickets, categorías, prioridades, búsqueda por asunto/número, filtros, bandejas y paginación.
- Conversación con respuestas públicas y notas internas filtradas desde la API.
- Asignación de responsables autorizados, transiciones validadas y motivo obligatorio.
- Concurrencia real de SQL Server con rowversion y ETag/If-Match. Un conflicto devuelve HTTP 412 y la interfaz conserva el texto escrito.
- Historial interno de creación, mensajes, asignación y cambios de estado, guardado en la misma transacción que el ticket.
- Resumen operativo calculado desde los datos autorizados; sin indicadores simulados.
- Creación administrativa de organizaciones y usuarios; cambio de contraseña.
- Interfaz responsive con publicación independiente en Coolify; API en IIS. Se mantiene el modo local de origen único.
- Migración inicial de SQL Server y datos de demostración separados de producción.

## Ejecutar localmente

Requisitos: SDK .NET 10 compatible con global.json, Node.js 20.19 o superior (recomendado 22/24 LTS), PowerShell 7 y SQL Server LocalDB o SQL Server accesible. El SDK instalado durante este trabajo está en .tools/dotnet y no modifica el SDK global.

Desde la raíz:

```powershell
npm --prefix src/sidecil-tickets-web ci
npm --prefix src/sidecil-tickets-web run build
./scripts/Initialize-Development.ps1
./scripts/Start-Development.ps1
```

Abrir http://localhost:5080. La inicialización crea SidecilTicketsDev y guarda una contraseña aleatoria de demostración en .local/demo-credentials.json, excluido del repositorio. Cuentas: admin@sidecil.local, agente@sidecil.local, empleado@sidecil.local y cliente@sidecil.local. Solo esta demostración utiliza una contraseña compartida generada localmente; no trasladar estas cuentas a producción.

La inicialización no restablece contraseñas ni sobrescribe datos si ya hay usuarios. Si se elimina el archivo de credenciales pero se conserva la base, una contraseña nueva no reemplazará la existente. Mantener juntos los recursos locales o crear otra base de pruebas.

Se puede sustituir LocalDB definiendo ConnectionStrings__Tickets. Para desarrollo en vivo, mantener la API en 5080 y ejecutar npm --prefix src/sidecil-tickets-web run dev; Vite abre el puerto 5173 y redirige /api a la API. En producción no se necesita un servidor Node.

## Validación

```powershell
./.tools/dotnet/dotnet.exe test Sidecil.Tickets.slnx
$env:PLAYWRIGHT_BROWSERS_PATH = Join-Path (Get-Location) '.tools/browsers'
# Solo la primera vez:
npm --prefix src/sidecil-tickets-web exec -- playwright install chromium
# Con la aplicación local iniciada y la base demo inicializada:
npm --prefix src/sidecil-tickets-web test
```

Las pruebas de navegador crean tickets de prueba en la base configurada. Ejecutarlas solo en desarrollo; no tienen limpieza destructiva automática. Las capturas se generan en artifacts/screenshots. Los resultados y trazas se excluyen del repositorio porque pueden contener información de sesión.

Las pruebas cubren reglas de acceso y estados, API real sobre SQL Server, CSRF, notas internas, persistencia, conflictos de edición y flujos de escritorio/móvil. Consultar docs/VALIDACION.md para la primera entrega y docs/VALIDACION-CORREO.md para la actualización de correo.

## Publicar el backend

Ejecutar `./scripts/Publish-Backend.ps1`. La publicación queda directamente en la carpeta `publish` de la raíz. Esa es la única carpeta que se copia al servidor para la aplicación `/soporte`. La configuración privada se completa en `publish/appsettings.Production.json`; no se sube a GitHub.

## Límites de esta entrega y siguiente trabajo

La arquitectura describe el producto objetivo; esta entrega implementa el núcleo de tickets. Aún faltan:

1. Activar y validar el buzón SMTP/IMAP real sobre el Worker y la cola de correo ya implementados. Renovación automática OAuth2, notificaciones adicionales y supervisión operativa siguen pendientes.
2. SLA contractual con calendarios, pausas, primera respuesta y escalamientos. Hoy existe solo un objetivo de resolución en horas corridas: urgente 4 h, alta 8 h, normal 24 h, baja 72 h. Esperar al solicitante no pausa este contador.
3. Adjuntos con cuarentena y análisis antimalware; por ahora no se admiten cargas de archivos.
4. MFA/SSO, invitaciones y recuperación de contraseña, revocación administrativa de usuarios y administración completa de equipos. Son requisitos previos a producción.
5. SignalR. Esta versión actualiza la bandeja cada 30 segundos; el detalle tiene actualización manual para proteger borradores.
6. Campos personalizados, reglas, base de conocimiento, satisfacción, webhooks, exportaciones y reportes históricos.
7. Idempotencia para crear tickets, paginación por cursor y Full-Text Search. La versión inicial usa páginas limitadas y búsqueda por asunto/número.
8. Pruebas de carga, auditoría de accesibilidad, ensayo de restauración y validación en IIS real. El navegador y la API se comprobaron con Kestrel y SQL Server LocalDB, no contra un servidor IIS instalado.

El equipo inicial es Atención Sidecil. Las nuevas solicitudes se enrutan a ese equipo; la administración de múltiples equipos y reglas de enrutamiento es posterior. Cada usuario pertenece actualmente a una sola organización. La membresía múltiple y el representante de organización de la arquitectura son ampliaciones pendientes.

No se ha medido todavía una superioridad frente a osTicket; se conserva como objetivo de producto verificable.

## Chat en tus sistemas

El panel `/admin/chat` permite registrar integraciones y obtener el widget para un ERP. Recoge solicitudes y adjuntos, verifica el correo y crea el ticket con su conversación. Consulta [la guía del chat embebido](docs/CHAT-EMBEBIDO.md) para probarlo e instalarlo. Esta entrega es un asistente guiado; la IA y la identidad compartida del ERP corresponden a las siguientes etapas.

## Acceso al portal con Microsoft y Google

Los botones de acceso externo están en el inicio de sesión principal. Cada usuario vincula su cuenta desde **Mi cuenta**, conservando sus permisos. Para activarlos consulta [la configuración del acceso externo](docs/ACCESO-EXTERNO.md). Las pruebas OIDC usan SQL Server de desarrollo; para pruebas sin base de datos agrega `--filter FullyQualifiedName!~PortalOidcTests`.

## Frontend en Coolify y API en IIS

La distribución elegida usa `VITE_API_URL` en la compilación React y `Frontend__PublicBaseUrl` en IIS. La API permite CORS únicamente para ese origen, conserva CSRF y devuelve allí los accesos externos. Consultar [la guía de despliegue separado](docs/COOLIFY.md). El Dockerfile `deploy/coolify/Dockerfile` contiene únicamente el frontend; SQL Server y el Worker permanecen en tu servidor.
