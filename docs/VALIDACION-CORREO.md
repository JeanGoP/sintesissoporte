# Validación de correo — versión 0.2

Fecha: 24 de septiembre de 2026.

## Entorno y resultado

Backend y Worker: .NET 10. SQL Server LocalDB, base SidecilTicketsDev. Interfaz: React/TypeScript, Chromium, Node 24.19. Correo: modo Pickup, archivos MIME locales; ninguna comunicación con servidores SMTP/IMAP externos.

- Compilación de API y Worker correcta; paquetes independientes en artifacts/iis y artifacts/worker.
- Migración EmailConversations aplicada sin borrar tickets anteriores; SQL idempotente regenerado en artifacts/sql/schema.sql.
- 18 pruebas .NET aprobadas, incluidas las diez nuevas de plantillas, conversión HTML, delimitación de citas y autenticación del proveedor.
- Escenario de navegador y Worker real aprobado: creación registrada, confirmación personalizada, respuesta pública saliente, recepción en el mismo ticket, duplicados, remitente diferente, respuesta automática y asunto sin referencia.
- Escenario de invitado aprobado: formulario público, correo de verificación, confirmación, mismo número al repetir el enlace, recepción de respuesta del invitado y acceso denegado desde otra cuenta.
- Cuatro escenarios previos de regresión aprobados: API/permisos, escritorio, móvil y cuentas/sesiones.
- Verificadas visualmente la pantalla de plantilla y la conversación con mensajes de correo.
- npm no reporta vulnerabilidades en el archivo de dependencias actualizado.

## Límites

No se validó entrega en un proveedor real, ni OAuth2 con renovación automática, ni autenticación DMARC sobre cabeceras de un buzón empresarial. El modo de pruebas omite esa verificación solo para archivos locales y está bloqueado en Production. Las reglas de aceptación de cabeceras se verificaron con pruebas unitarias positivas y negativas.

No se ha instalado el Worker en el Administrador de servicios de Windows ni desplegado IIS en un servidor empresarial. No se garantiza entrega exactamente una vez en SMTP. Los correos con adjuntos quedan en revisión porque la importación de archivos sigue pendiente.

Los correos de prueba, sus enlaces y credenciales están en .local, excluido del repositorio. No compartir esos archivos. Los tests generan únicamente datos de demostración y no eliminan datos existentes.
