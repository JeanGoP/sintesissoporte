# Invitaciones de usuarios

Desde Personas y acceso, un administrador crea el usuario indicando nombre, correo, organización, rol y equipo (para agentes). Ya no define una contraseña inicial. El usuario y el correo de invitación se guardan en una misma transacción; el servicio de correo existente envía la invitación desde la cola.

El destinatario abre el enlace y establece su contraseña. El enlace vence en 24 horas y queda invalidado al utilizarse. La contraseña debe cumplir las reglas de Identity: mínimo 12 caracteres, mayúsculas, minúsculas, números y símbolos. La activación confirma el correo, conserva el rol y equipo asignados y requiere ingresar posteriormente con correo y contraseña.

Las cuentas pendientes muestran Reenviar invitación. Solo un administrador puede reenviar; esto invalida los enlaces anteriores. No se envían invitaciones automáticamente a cuentas anteriores que ya tengan contraseña o acceso vinculado con Microsoft/Google. Estas últimas conservan su mecanismo de acceso.

Las invitaciones usan tokens protegidos por ASP.NET Core Identity, con propósito exclusivo, caducidad y SecurityStamp. No se guardan contraseñas en correos. El token se transporta en el fragmento del enlace, evitando que llegue a los registros de navegación del servidor. La aceptación usa POST, CSRF y límite de solicitudes. No cambia roles ni inicia una sesión automáticamente. La cola de correo contiene el enlace hasta su procesamiento y debe conservar los permisos restringidos existentes.

## Publicación

No hay migraciones nuevas. Publicar con scripts/Publish-Backend.ps1 y desplegar el frontend en Coolify. Actualizar los binarios del backend en IIS conservando appsettings.Production.json y mailsettings.Production.json. Mantener las claves de Data Protection fuera de publish para que las invitaciones sobrevivan a una actualización.

Mail:Mode debe estar habilitado y el servicio de correo funcionando. El enlace utiliza Frontend:PublicBaseUrl y, si no existe, Mail:PublicBaseUrl. Los errores de entrega se consultan en Correo de atención. No es necesario volver a configurar Turbo o Gmail.

Pruebas: PortalOidcTests incluye registro externo y activación de invitaciones para Admin, Agent y Requester; verifica token inválido, caducidad, reenvío, uso único, contraseña débil, permisos y rol final. invitation.spec.ts comprueba el formulario público, confirmación de contraseña y limpieza del enlace después de activar.
