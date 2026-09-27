# Adjuntos al crear tickets desde la aplicación

Crear nuevo ticket permite seleccionar y quitar hasta 3 archivos, de 5 MB cada uno, en formatos PNG, JPG, PDF o TXT. Se comprueban tamaño, extensión y contenido en el servidor. Los archivos seleccionados permanecen en el navegador hasta enviar; si falla, se conserva el formulario para corregirlo o reintentar.

La API recibe el formulario y guarda ticket, mensaje inicial, adjuntos y correo de recepción en una transacción. Los adjuntos de la aplicación se guardan en SQL Server, tabla tickets.Attachments. Los del chat mantienen su almacenamiento en chat.Attachments. Ambos aparecen en el detalle del ticket y utilizan la misma ruta de descarga protegida: solo usuarios con acceso al ticket pueden descargarlos. Se descargan como archivos, no como contenido ejecutable en el navegador.

No se agregan los binarios al correo de recepción; los archivos se consultan desde el ticket en la plataforma. Los respaldos de SQL Server incluyen los adjuntos. No se guardan dentro de publish ni en Coolify.

## Actualización

Esta versión crea la tabla tickets.Attachments mediante una migración aditiva. Detener el grupo IIS de la API y Sidecil Tickets Mail, copiar publish incluyendo wwwroot conservando appsettings.Production.json y mailsettings.Production.json, y ejecutar desde la carpeta del servidor:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Actualizar-BaseDeDatos.ps1
```

Al terminar correctamente, iniciar ambos y desplegar main en Coolify. La migración se comprobó primero en SidecilTicketsDev; en producción se usa la conexión existente a SintesisCloudSoporte.

Las pruebas validan contenido falso, exceso de archivos, tamaño máximo, creación atómica, consulta y descarga con permisos. El navegador comprueba selección y eliminación, envío multipart y conservación del formulario tras un error.

## Formulario sin cuenta

La pantalla /solicitar incluye Adjuntar archivos debajo de la descripción con los mismos límites. Antes de confirmar el correo, los archivos se guardan en communications.GuestAttachments y no tienen una ruta pública de descarga. La confirmación traslada los archivos a tickets.Attachments en la misma transacción que crea el ticket. Repetir la confirmación no duplica archivos ni tickets.

El servicio de correo elimina los adjuntos pendientes cuya solicitud venció sin confirmarse, después de 24 horas. Los archivos de tickets confirmados se conservan. El despliegue requiere ejecutar Actualizar-BaseDeDatos.ps1 también para esta ampliación y actualizar el servicio de correo junto con la API.
