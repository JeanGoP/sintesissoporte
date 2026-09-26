# Chat embebido de Sidecil · primera versión

## Alcance implementado

- Botón flotante y panel aislado del CSS del ERP mediante Shadow DOM e iframe.
- Recepción guiada: nombre, correo, módulo, asunto, descripción y categoría.
- Resumen editable antes de enviar; borrador recuperable durante 24 horas en la misma pestaña cuando el navegador permite sessionStorage.
- Hasta 3 archivos por solicitud, de 5 MB cada uno: PNG, JPG, PDF y TXT UTF-8. Se comprueban extensión, tamaño y firma básica de contenido.
- Verificación por correo sin crear una cuenta. Al confirmar, se crea un solo ticket con la conversación y los archivos. Los reintentos conservan ese ticket.
- El chat consulta la confirmación cada 15 segundos mientras está visible. La atención posterior continúa por correo.
- Descarga de archivos desde el detalle del ticket, con los mismos permisos de administrador, equipo del agente o solicitante.
- Panel de administración para registrar sistemas, copiar el código, previsualizar y desactivar integraciones.

Es un asistente guiado, sin modelo de IA ni acceso a la base de datos del ERP. No incluye todavía inicio de sesión compartido con el ERP, respuestas documentales ni chat con agentes en vivo. Una cuenta existente no se vincula por coincidencia de correo: se evita tratar un correo escrito por el visitante como una identidad autenticada.

## Probar localmente

1. Inicia sesión como administrador y abre http://localhost:5080/admin/chat.
2. Registra un sistema con su nombre y origen. Para una página local puedes usar http://localhost:5080 o el puerto donde esté tu ERP de prueba.
3. Usa la vista previa o copia el código en una página del origen registrado.
4. Completa la conversación, adjunta archivos y pulsa **Confirmar y enviar solicitud**.
5. En modo Pickup, busca el mensaje de verificación en `.local/mail/out`. No se envían correos reales.
6. Abre su enlace, confirma y revisa el número en el chat y el ticket en la bandeja.

## Instalar en un ERP publicado

Publica Sidecil Tickets en IIS con HTTPS, configura el correo real y abre el panel desde ese dominio. Registra el origen exacto del ERP, por ejemplo `https://erp.empresa.com`, y copia el código generado:

```html
<script src="https://soporte.empresa.com/sidecil-chat.js"
        data-site="IDENTIFICADOR_GENERADO_EN_EL_PANEL" defer></script>
```

No pongas contraseñas, API keys, nombres de clientes ni datos de sesión dentro del script. El identificador de integración es público y no autentica usuarios ni empresas.

Si el ERP tiene Content-Security-Policy, permite el dominio de Sidecil Tickets en `script-src`, `style-src` y `frame-src`. El widget carga su hoja de estilos externa y su iframe desde ese dominio. No captura automáticamente la URL ni los datos de la pantalla del ERP.

En IIS, evita que una regla global `X-Frame-Options: DENY` bloquee `/chat-widget`. El backend administra `frame-ancestors` para esa ruta: solo el origen registrado y el propio portal. El resto del portal conserva `frame-ancestors 'none'`. Desactivar una integración revoca las sesiones de su chat, pero no borra los tickets ya creados.

## Seguridad y datos

El iframe hace peticiones a su mismo origen con `credentials: omit`. No depende de cookies de terceros. Cada conversación utiliza un token aleatorio de 256 bits; SQL Server guarda solamente su hash. El token se transmite por Authorization y se conserva, si es posible, en sessionStorage; nunca va en la URL. Los borradores expiran a las 24 horas.

Solo `/api/v1/chat` prescinde del token CSRF del portal: ignora la autenticación por cookies, exige `X-Sidecil-Widget` y requiere su propio bearer para leer o modificar conversaciones. El resto de la API conserva CSRF. No se habilita CORS global. La restricción de origen controla dónde se puede embeber el iframe; no debe considerarse autenticación contra clientes HTTP arbitrarios.

Los endpoints tienen límites por IP. Cada conversación permite un solo envío; las mutaciones usan una transacción y bloqueo de su fila para evitar carreras entre adjuntos y envío. Después del envío no se modifican los datos ni los archivos. La comprobación final del correo sigue utilizando el flujo existente de solicitudes invitadas.

Los archivos se guardan en SQL Server en `chat.Attachments`, fuera de wwwroot. Se descargan como `application/octet-stream` con Content-Disposition attachment y nosniff. Su validación no sustituye un antivirus: el análisis antimalware queda pendiente antes de habilitar cargas públicas en producción. Los archivos no se adjuntan automáticamente al correo del agente.

La limpieza automática de borradores vencidos todavía no está implementada. Define retención, copias de seguridad y análisis antimalware antes de publicar este canal para clientes externos. Los borradores expirados no se pueden consultar desde el widget aunque permanezcan en SQL Server.

## Base de datos y despliegue

La migración `20260926015321_EmbeddedChat` añade `chat.Sites`, `chat.Conversations` y `chat.Attachments`. No modifica ni borra tickets existentes. El script idempotente actualizado está en `artifacts/migrations.sql`; aplicar con el procedimiento de despliegue habitual. API y Worker deben publicarse desde la misma versión.

## Verificación

- `ChatTests.cs`: validación de orígenes, límites y contenido de archivos.
- `chat.spec.ts`: integración desde otro origen, recuperación de borradores, adjuntos, confirmación por correo, reintentos, aislamiento de sesiones, permisos de descarga, desactivación y adaptación móvil.
- El ensayo por correo usa archivos locales y destinatarios de prueba; no envía correo externo.

## Próximas etapas

1. Identidad del ERP mediante intercambio de tokens firmados entre servidores, con asociación explícita a usuario y empresa.
2. Base de conocimiento aprobada y búsqueda; respuestas con referencias y derivación al equipo.
3. Proveedor de IA conectado desde el backend, con acciones limitadas y creación de tickets confirmada por el cliente.
4. Conversación con agentes en tiempo real y continuidad de mensajes desde el chat.

## Inicio de sesión del portal

Microsoft y Google corresponden al sistema principal. El chat continúa recibiendo solicitudes sin cuenta. Consulta [la guía de acceso al portal](ACCESO-EXTERNO.md).
