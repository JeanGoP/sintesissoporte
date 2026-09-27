# Correo y solicitudes sin cuenta

Implementado el 24 de septiembre de 2026. El entorno local usa simulación: no se han enviado mensajes reales ni conectado un buzón empresarial.

## Uso

- El administrador encuentra **Correo de atención** en el menú. Puede editar asunto, mensaje y firma de la confirmación y ver la cola de salientes y el registro de entrantes.
- Variables de la plantilla: {nombre}, {numero}, {asunto}, {firma}. El número SC-xxxxx siempre se incluye en el asunto. Los cambios aplican a los correos que se preparen después de guardar; el contenido de mensajes ya encolados no se cambia.
- Un solicitante registrado crea el ticket y su confirmación queda en la cola en la misma transacción SQL.
- Un cliente sin cuenta utiliza /solicitar. Recibe un enlace de verificación válido por 24 horas; al pulsar Confirmar se crea el ticket y se prepara la confirmación personalizada. Repetir la confirmación durante su vigencia devuelve el mismo número.
- El invitado no obtiene una contraseña ni acceso a tickets previos de una cuenta que use el mismo correo. Su seguimiento se realiza por correo, no mediante un portal anónimo que exponga el historial.
- Las respuestas públicas del agente al solicitante generan correo. Las notas internas nunca se encolan. No se envían acuses a cada respuesta entrante, evitando bucles.
- La respuesta del cliente se añade como mensaje público con origen «Por correo», conserva responsable/equipo y marca «Respuesta del cliente» en la bandeja. La pestaña «Respuestas de clientes» permite filtrarlas. Se limpia esa marca al responder públicamente un agente.
- Si el ticket esperaba al solicitante o estaba resuelto, una respuesta válida lo devuelve a En curso. Un ticket cerrado/cancelado requiere revisión manual.

## Servicio de Windows

Sidecil.Tickets.Worker ejecuta el envío y la recepción en ciclos independientes, fuera del application pool de IIS. Los fallos del SMTP no bloquean el ciclo IMAP. Cada ciclo espera 10 segundos tras terminar; no es una promesa de entrega en 10 segundos.

La cola SQL utiliza reclamación atómica, arrendamientos y hasta cinco intentos con espera creciente. Un mensaje que agota intentos queda Fallido; el administrador puede reintentarlo. Los enlaces de verificación vencidos no se envían. Un reinicio recupera trabajos cuyo arrendamiento expiró.

SMTP no permite garantizar exactamente una entrega: si el servidor acepta un mensaje y la conexión cae antes de registrar el resultado, un reintento puede duplicarlo. Se conserva el mismo Message-ID en los reintentos. Las respuestas entrantes se deduplican por cuenta y Message-ID, además del identificador de origen.

Ejecutar un único lector IMAP por buzón en esta versión. El envío utiliza arrendamientos, pero no se ha validado una instalación de múltiples trabajadores lectores sobre un mismo buzón.

## Activación con el buzón real

Hace falta configurar el buzón que atenderá Sidecil; no se han supuesto credenciales ni proveedor.

En **la API de IIS**:

- Mail__Mode=Smtp
- Mail__PublicBaseUrl: URL pública HTTPS del portal. Se utiliza para los enlaces de confirmación; nunca se toma del Host enviado por el cliente.

En **el servicio Worker**:

| Variable | Uso |
|---|---|
| ConnectionStrings__Tickets | Misma base SQL Server que la API |
| Mail__Mode | Smtp |
| Mail__PublicBaseUrl | Misma URL HTTPS del portal |
| Mail__FromAddress / Mail__FromName | Buzón real de soporte y nombre visible |
| Mail__SmtpHost / Mail__SmtpPort | Servidor y puerto SMTP: 587 con STARTTLS obligatorio o 465 con TLS directo |
| Mail__SmtpUser / Mail__SmtpPassword | Credenciales o contraseña de aplicación, si el proveedor lo permite |
| Mail__ImapHost / Mail__ImapPort | Servidor IMAP con TLS directo; habitualmente 993 |
| Mail__ImapUser / Mail__ImapPassword | Acceso al buzón de respuestas |
| Mail__ImapFolder | Carpeta dedicada, inicialmente INBOX |
| Mail__AccountKey | Identificador estable del buzón, usado para deduplicación |
| Mail__TrustedAuthenticationService | authserv-id exacto del Authentication-Results insertado por el proveedor receptor |

La URL pública y el modo deben configurarse en ambos procesos. Las credenciales SMTP/IMAP solo hacen falta en el Worker. Almacenar secretos en configuración protegida de la cuenta de servicio, fuera del repositorio; reiniciar el servicio tras cambiarlos.

Hay soporte SASL OAuth2 mediante Mail__SmtpAccessToken / Mail__ImapAccessToken en lugar de contraseñas. **La adquisición y renovación automática de tokens de Microsoft 365/Google aún no están implementadas**: si el proveedor exige OAuth2, conectar primero su mecanismo de renovación o adaptar el conector al proveedor. No activar producción con un token temporal sin renovación operativa.

Configurar en el dominio SPF, DKIM y DMARC según el proveedor. La validación de recepción exige que el proveedor elimine cabeceras falsificadas con su authserv-id y coloque su resultado de autenticación primero. Solo se confía en ese primer Authentication-Results, con DMARC pass y dominio alineado con From. Si esto no está verificado, los mensajes quedan en revisión, no se añaden al ticket automáticamente.

El Worker no cambia flags, no borra mensajes ni mueve correos en el buzón IMAP: lo abre en modo lectura. Usa UID y UIDVALIDITY persistentes, por lo que un mensaje marcado como leído por una persona también puede procesarse. La primera ejecución recorre el contenido de la carpeta desde el principio, en lotes de hasta 50; utilizar un buzón/carpeta dedicado. Cambiar AccountKey puede provocar una nueva importación y requiere un procedimiento operativo.

La entrega actual publish contiene solo el backend. El servicio de correo sigue en el código fuente, pero no se publica en esta carpeta; su instalación se preparará por separado cuando se active el buzón. No ejecutarlo dentro de IIS. El servicio no ejecuta migraciones al iniciar.

## Correlación y revisión

Solo se acepta una respuesta si In-Reply-To o References identifica un correo saliente del sistema, el ticket resultante es único, el remitente coincide con el solicitante y destinatario original, y supera la validación del proveedor. El número de ticket en el asunto por sí solo no autoriza nada. Se ignoran respuestas automáticas y reportes de entrega.

Quedan para revisión: remitentes diferentes, referencias ambiguas/ausentes, autenticación no verificable, correos malformados, mensajes mayores de 5 MB, cuerpos vacíos o mayores de 12.000 caracteres, adjuntos y tickets cerrados/cancelados. El administrador ve la causa en Correo de atención y revisa el mensaje original en el buzón. **Los adjuntos todavía no se incorporan ni se descargan desde la aplicación.** No hay reasignación automática de mensajes en revisión.

La conversión HTML a texto elimina contenido activo. La vista de conversación renderiza texto, sin ejecutar HTML del correo. Un delimitador propio reduce el texto citado; no se promete eliminar perfectamente todas las firmas o citas de todos los clientes de correo.

## Pruebas locales

scripts/Start-MailWorker.ps1 inicia el trabajador en Development/Pickup; -Once ejecuta un ciclo. Los salientes se guardan como .eml en .local/mail/out. Se pueden colocar respuestas .eml en .local/mail/in con Message-ID único y referencias válidas; luego se archivan en la subcarpeta processed. Usar nombres de archivo únicos.

Pickup solo se permite en Development, no requiere Internet y no aplica DMARC porque los archivos son fixtures locales controlados. No apuntar estos directorios a cargas de archivos públicas. Los enlaces dentro de los .eml contienen tokens privados: no subirlos al repositorio ni compartirlos.

Las pruebas de navegador requieren Node >=20.19 (recomendado Node 22/24 LTS), Chromium de Playwright y el Worker publicado. El escenario tests/email.spec.ts utiliza la aplicación local y el Worker real con SQL Server, pero no contacta SMTP/IMAP externos. En esta sesión se usó Node 24.19 del runtime de Codex.

Antes de ejecutar la suite de correo, detener el Worker continuo local; la prueba invoca --once para controlar cada paso. Para producción faltan la configuración del proveedor, las pruebas reales de entrega y autenticación, la supervisión del servicio y una política de retención para la cola, los borradores de invitados y los registros de correo.

## Referencias técnicas

- MailKit SMTP: https://mimekit.net/docs/html/Overload_MailKit_Net_Smtp_SmtpClient_SendAsync.htm
- MailKit IMAP: https://mimekit.net/docs/html/T_MailKit_Net_Imap_ImapClient.htm
- SASL OAuth2: https://mimekit.net/docs/html/T_MailKit_Security_SaslMechanismOAuth2.htm

## Instalacion de TurboSMTP y Gmail en el servidor

El servicio y Configurar-Correo.ps1 se entregan junto a la API en la misma carpeta publish. No se instalan servicios en el equipo de desarrollo.

1. Detener el grupo IIS dedicado de la API y, en actualizaciones, el servicio `Sidecil Tickets Mail`.
2. Copiar los binarios y Configurar-Correo.ps1 a la carpeta de la API. Conservar los archivos privados appsettings.Production.json y mailsettings.Production.json del servidor; no reemplazarlos con los de desarrollo.
3. Abrir Windows PowerShell como administrador, entrar a esa carpeta y ejecutar `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Configurar-Correo.ps1`.
4. Introducir el remitente autorizado en Turbo, la direccion de Gmail, Consumer Key, Consumer Secret y contrasena de aplicacion de Gmail. Las claves se solicitan con entrada oculta. No usar la contrasena habitual de Google.
5. El instalador prueba tablas SQL, autenticacion SMTP (TLS directo, puerto 465) e IMAP (TLS, puerto 993) sin enviar ni importar mensajes durante la comprobacion. Luego configura e inicia el servicio con LocalService y habilita Mail.Mode=Smtp en la API. Requiere SQL con usuario y contrasena; la autenticacion integrada necesita configurar una identidad de servicio aparte.
6. Iniciar/reciclar solamente el grupo IIS de la API. Crear una solicitud de prueba desde otro correo, confirmar el enlace, comprobar el acuse y responderlo. Revisar que el texto aparezca en el ticket.

La comprobacion de autenticacion no garantiza entrega ni autorizacion del remitente: la prueba real del punto 6 es necesaria. En Correo de atencion se ven los envios fallidos y respuestas en revision. Los errores del servicio quedan en el Visor de eventos de Windows.

Las claves de correo se guardan en mailsettings.Production.json, fuera de wwwroot, con lectura limitada a LocalService y acceso de administradores/SYSTEM. La API solo recibe el modo y la URL publica. El instalador conserva las claves Microsoft/Google y la conexion SQL existentes. Los archivos privados se preservan en las publicaciones siguientes.

FromAddress controla el remitente de Turbo; ReplyToAddress dirige las respuestas a Gmail. Si ReplyToAddress queda vacio, se conserva el comportamiento anterior (responder al remitente). El buzon INBOX se lee sin modificar ni borrar correos, empezando por su historial la primera vez; utilizar un buzon dedicado.

Gmail se configura con mx.google.com como servicio de autenticacion esperado. Las respuestas solo se incorporan automaticamente cuando la primera cabecera Authentication-Results valida DMARC y el remitente corresponde al solicitante del ticket. Un correo que no cumpla queda en revision; comprobar este comportamiento con mensajes reales antes de dar la recepcion por validada. Los adjuntos entrantes siguen requiriendo revision manual.
