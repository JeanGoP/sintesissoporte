# Validación de la primera entrega

Fecha local: 23 de septiembre de 2026. Entorno: Windows, .NET SDK 10.0.401, SQL Server LocalDB, Node 20.15.1 y Chromium de Playwright. Se verificó la aplicación publicada en artifacts/iis mediante Kestrel en localhost:5080.

## Resultados

| Comprobación | Resultado |
|---|---|
| Compilación del backend | Correcta, sin errores ni advertencias en la compilación inicial; publicación Release correcta |
| Pruebas .NET de dominio y acceso | 8 aprobadas |
| Pruebas de API/navegador sobre SQL real | 4 aprobadas; última ejecución completa: 14,9 segundos |
| Compilación React/TypeScript | Correcta; archivos de JS divididos sin advertencia de tamaño |
| Dependencias npm | 0 vulnerabilidades reportadas en la instalación final |
| Formato del frontend | Prettier sin diferencias |
| Migraciones | Esquema inicial aplicado correctamente a SidecilTicketsDev |
| Paquete IIS | Generado con web.config, AspNetCoreModuleV2 y hosting in-process |
| SQL para despliegue | artifacts/sql/schema.sql, script idempotente generado |
| Revisión visual | Bandeja y conversación en escritorio, portal y conversación en móvil |

## Escenarios comprobados

- Un cliente crea un ticket y el empleado de otra organización obtiene 404 al consultarlo o intentar responder.
- Una cuenta nueva de la misma organización tampoco obtiene acceso a tickets de otros solicitantes.
- Un solicitante no puede publicar notas internas, cambiar estados ni administrar organizaciones.
- La respuesta API del solicitante no incluye notas internas ni historial interno. La búsqueda no revela el contenido de notas privadas.
- Una escritura sin CSRF válido se rechaza.
- Una modificación con ETag obsoleto devuelve 412 después de un cambio real en SQL Server.
- Creación y recuperación de mensajes, respuesta pública, nota privada y transición de estado funcionan y persisten tras recargar el navegador.
- Búsqueda sin resultados, creación desde móvil, permisos de interfaz y cierre de sesión por pérdida de cookies funcionan.
- No se detectaron excepciones JavaScript en el flujo de agente.

## Hallazgos corregidos durante la validación

- Actualización de React Router a 7.18.4 después de detectar avisos de seguridad.
- División del JavaScript por dependencias para evitar un único archivo grande.
- Corrección de desbordamiento móvil y adaptación de la bandeja a tarjetas en pantallas pequeñas.
- Recuperación de la pantalla de login al caducar una sesión.

## Alcance pendiente

Estos resultados no certifican producción, seguridad integral, WCAG ni rendimiento bajo carga. No se instaló un sitio IIS real, no se probaron correo/adjuntos/MFA/Worker porque todavía no están implementados, ni se ensayó restauración o alta disponibilidad. Tampoco se ha realizado una comparación medida contra osTicket.

Las capturas y el paquete están en artifacts, excluido del repositorio. Los tests crearon únicamente datos de demostración en la base local.
