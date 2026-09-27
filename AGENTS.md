# Instrucciones del proyecto

## Git y sincronización

- Repositorio: https://github.com/JeanGoP/sintesissoporte.git.
- Rama principal: main.
- Por solicitud del usuario, todos los mensajes de commit deben estar en español.
- Al terminar cada cambio solicitado, ejecutar las verificaciones apropiadas, crear un commit y sincronizarlo con origin mediante push. No hace falta volver a pedir autorización para este flujo.
- Revisar el diff antes de confirmar. No incluir credenciales, secretos, bases de datos, correos, archivos de sesión ni artefactos locales.
- Conservar el trabajo ajeno y no sobrescribir el historial remoto con force push. Si la sincronización falla, informar el motivo y conservar los cambios locales.

## Distribución de producción acordada

- Frontend React únicamente en Coolify: https://soporte.sintesiserp.com.co.
- Backend C# en IIS: https://sintesiserp.com.co/soporte, como aplicación IIS con pool dedicado; no reemplazar el sitio ERP padre.
- SQL Server y Worker permanecen en el servidor/red de Sidecil.
- Mantener soporte de PathBase /soporte en API, OAuth, recursos y widget; frontend usa VITE_API_URL y API usa Frontend__PublicBaseUrl.
- El widget embebido conserva recursos en IIS para la política de enmarcado por integración. No borrar wwwroot del paquete IIS.
