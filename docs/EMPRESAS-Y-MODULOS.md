# Empresas, categorías y módulos

## Configuración

En **Personas y acceso**, el administrador puede:

1. Crear empresas con **Nueva organización**.
2. Crear, editar y desactivar categorías y módulos en **Categorías y módulos**. Cada módulo pertenece a una categoría.
3. Seleccionar los módulos al crear un agente o cambiar sus permisos desde **Empresas y módulos**.
4. Asignar empresas adicionales a un solicitante. La empresa principal se conserva.

Se incluyen las categorías anteriores y un módulo General en cada una como punto de partida. Se pueden renombrar y desactivar. Un módulo usado no se puede mover de categoría; se debe crear otro para preservar el historial.

## Solicitudes y acceso

- Aplicación: el cliente elige una empresa autorizada (si tiene una sola se muestra seleccionada), categoría y módulo. Las cuentas externas sin empresa registrada indican su nombre.
- Solicitudes sin cuenta y chat: se pide el nombre de la empresa. Es un dato declarado para atención, no una autorización para consultar información de esa empresa. No se expone una lista pública de clientes.
- El cliente nunca selecciona agente. El ticket queda en la bandeja de quienes tienen el módulo asignado. El agente puede tomarlo y el administrador puede asignarlo a un responsable autorizado.
- El permiso por módulo se aplica también al detalle, mensajes, cambios de estado y descarga de archivos. El equipo anterior ya no concede acceso.
- Retirar un módulo libera las asignaciones del agente en ese módulo y revoca su acceso. Un agente sin módulos no ve tickets. Los administradores conservan acceso completo.
- Las respuestas de correo y del chat continúan en el ticket original y conservan su clasificación. La bandeja y su indicador de respuesta del cliente usan los mismos permisos.
- Los clientes solo consultan sus propios tickets. Indicar otra empresa no concede acceso a tickets de terceros.

## Tickets anteriores

La migración conserva tickets, conversaciones, archivos y cuentas. Los tickets antiguos mantienen módulo vacío y quedan disponibles para el administrador y su solicitante. La pestaña **Por configurar** permite encontrar tickets sin módulo o sin agentes habilitados. En el detalle, **Clasificar ticket** permite establecer empresa y módulo.

Después de publicar, asignar explícitamente los módulos de los agentes existentes. No se conceden permisos automáticamente durante la migración.

## Publicación

Migración: `20260927143225_SupportCatalogAndAgentModules`. Agrega tablas y columnas; no borra datos existentes. Probar primero en desarrollo y aplicarla antes de ejecutar el backend nuevo. El usuario autorizó ejecutar las migraciones compatibles usando la conexión local privada de producción.

La entrega está en `publish`, generada con `scripts/Publish-Backend.ps1`. Actualizar el backend y servicio de correo en IIS, conservando los archivos privados `appsettings.Production.json` y `mailsettings.Production.json`. Para reemplazar los binarios puede ser necesario detener brevemente el pool de la API y el servicio de correo. No intervenir el sitio ERP padre.

Desplegar el frontend en Coolify después del backend. El widget actualizado está incluido en `publish/wwwroot`; no omitirlo. No volver a configurar SMTP ni OAuth.

## Validación

Pruebas de integración: empresa no autorizada, módulo de otra categoría, catálogos restringidos al administrador, listado y descarga por módulo, asignación, notas internas, retirada de permisos y módulos desactivados. Regresiones de chat, OAuth, invitaciones y adjuntos. Pruebas de navegador de maestros, permisos y formularios dependientes.

## Bajas de agentes y eliminación de errores

En **Personas y acceso**, cada agente muestra su estado y botones para **Desactivar**, **Reactivar** y **Eliminar agente**, con confirmación previa.

- Desactivar bloquea contraseña y Microsoft/Google, invalida sesiones y libera tickets pendientes. Conserva historial y módulos. Reactivar exige un nuevo inicio de sesión y no recupera automáticamente los tickets liberados.
- Eliminar solo se admite para agentes sin historial de tickets, mensajes, eventos o conversaciones. Un agente que tuvo tickets pendientes liberados al desactivarlo conserva constancia de esa participación y tampoco se elimina.
- Estas acciones no permiten borrar administradores ni solicitantes.
- En la edición de una categoría o módulo está **Eliminar**. La categoría debe estar sin módulos ni solicitudes históricas; un módulo debe estar sin tickets, solicitudes, conversaciones o asignaciones a agentes. El mensaje indica qué relación lo impide. Desactivar sigue disponible cuando existe historial.
- No requiere migración: la baja usa el bloqueo persistente de Identity y renueva el sello de seguridad. Se comprueba ese sello en cada petición autenticada para revocar sesiones sin esperar al vencimiento de la cookie.
