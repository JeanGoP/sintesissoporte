# Chat: consultar y continuar tickets

El widget permite verificar el correo mediante un código de 8 dígitos y luego elegir un ticket pendiente o crear uno nuevo. Escribir un correo registrado nunca revela tickets ni evita la verificación.

- Código válido 10 minutos y máximo 5 intentos. Se permiten 3 correos de código por dirección en una hora, con al menos un minuto entre envíos. Se aplican también límites por IP.
- La verificación se conserva durante la sesión del navegador, con límite absoluto de 24 horas desde el inicio. Crear otra solicitud conserva esa caducidad; no la prolonga.
- Cada conversación utiliza un token propio; se guarda en sessionStorage cuando el navegador lo permite. Una sesión vencida permite empezar nuevamente.
- El botón Usar mi sesión de Sidecil intercambia explícitamente la sesión autenticada y el token de conversación, protegido por CSRF. Solo accede a tickets propios del usuario, incluso para agentes o administradores. Para incluir sus solicitudes sin cuenta, además se requiere correo confirmado. Si el navegador bloquea las cookies del iframe, se utiliza el código de correo.
- La sesión del ERP por sí sola no identifica al cliente en Sidecil. No se confía en parámetros de URL ni en mensajes del ERP que simplemente incluyan un correo. Una integración SSO específica requeriría autenticar el intercambio en el servidor del ERP.
- Con correo verificado se muestran tickets propios asociados a esa dirección, tanto de cuenta registrada como sin cuenta. Solo se ofrecen estados pendientes: Nuevo, En curso, Esperando respuesta y Esperando tercero. No se muestran notas internas ni datos de otros clientes.
- Continuar un ticket agrega un mensaje público con origen Chat y sus adjuntos. Marca respuesta del cliente y, si esperaba al solicitante, lo pasa a En curso. Los reintentos de envío sobre la misma conversación no duplican el mensaje.
- Crear un ticket nuevo lo vincula al usuario existente cuando corresponde; de lo contrario, conserva la recepción sin cuenta. Envía el correo de recepción, sin otro correo de confirmación.
- Las conversaciones antiguas enviadas y aún pendientes de confirmar conservan su enlace anterior.

## Actualización en IIS

Esta versión agrega cuatro columnas de verificación a chat.Conversations. No elimina tickets ni requiere cambiar credenciales.

1. Detener el grupo IIS dedicado de la API y el servicio Sidecil Tickets Mail.
2. Copiar el contenido de publish, incluyendo wwwroot y Actualizar-BaseDeDatos.ps1, conservando appsettings.Production.json y mailsettings.Production.json del servidor.
3. Desde la carpeta de la API, ejecutar powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Actualizar-BaseDeDatos.ps1. Usa la conexión existente de producción; requiere permisos para aplicar migraciones. Se puede ejecutar de nuevo sin duplicar columnas.
4. Si termina correctamente, iniciar el servicio y el grupo IIS. Desplegar main en Coolify y recargar el ERP.

La migración se comprobó en SidecilTicketsDev. El servidor de producción debe actualizarse con el paso anterior antes de usar los binarios nuevos.

Pruebas: ChatIdentityTests comprueba identidad verificada, límite de intentos, caducidad, separación de usuarios, tickets cerrados, envío idempotente y nueva solicitud sin nuevo código. ChatPortalTests comprueba la sesión autenticada, CSRF, token de conversación y expiración. chat-existing-tickets.spec.ts recorre la interfaz completa en navegador.
