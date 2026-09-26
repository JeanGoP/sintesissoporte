# Instalación en IIS — paquete inicial

Este procedimiento prepara el despliegue técnico de la versión 0.1. Resolver primero los requisitos de producción pendientes del README, especialmente MFA, administración de acceso, configuración real del correo, restauración y pruebas operativas. No se ha realizado despliegue en un servidor de Sidecil. El Worker de correo se publica por separado: consultar docs/CORREO.md.

## Preparación

1. Windows Server soportado por .NET 10, IIS y Hosting Bundle .NET 10 actualizado. Instalar IIS antes del bundle o reparar el bundle después.
2. SQL Server y base dedicada. LocalDB se utiliza exclusivamente para desarrollo.
3. Sitio HTTPS con certificado válido y un application pool dedicado, No Managed Code, identidad de servicio con permisos mínimos.
4. Directorio de la aplicación con acceso de lectura/ejecución para el pool; directorio de claves Data Protection separado del despliegue con ACL de lectura/escritura limitada a la identidad del pool.
5. Definir variables del sitio/app pool: ASPNETCORE_ENVIRONMENT=Production, AllowedHosts=<dominio real>, ConnectionStrings__Tickets=<conexión privada con certificado SQL válido>, DataProtection__Path=<directorio persistente de claves>.

No guardar secretos en el repositorio. Si se utiliza autenticación integrada SQL, la identidad real del pool debe tener acceso a la base. No habilitar TrustServerCertificate en producción. Conservar las claves de Data Protection en respaldos protegidos; perderlas invalida las sesiones y los tokens. La protección DPAPI de Windows requiere conservar la identidad y las claves correspondientes al restaurar en otra máquina.

## Publicación

- Generar artifacts/iis con scripts/Publish-IIS.ps1. El SDK genera web.config para AspNetCoreModuleV2 y alojamiento in-process.
- Revisar y ejecutar artifacts/sql/schema.sql con una identidad de despliegue, distinta de la cuenta de ejecución. Esta última no necesita permisos DDL.
- Crear el administrador inicial mediante una ejecución explícita del binario con --bootstrap y variables SIDECIL_ADMIN_EMAIL y SIDECIL_BOOTSTRAP_PASSWORD. El bootstrap ejecuta migraciones y requiere permisos de despliegue; usarlos solo para esa operación. No utilizar --seed-demo en producción (está bloqueado por código).
- Copiar el paquete al directorio del sitio durante una ventana controlada. No sobrescribir claves persistentes ni secretos.
- La API no aplica migraciones durante su arranque normal. Validar /health/live, /health/ready y el login a través de HTTPS.
- Probar acceso por roles, notas privadas, cambio simultáneo y reciclado del pool en el servidor destino.

La aplicación utiliza una única URL para frontend y API; no necesita CORS abierto, reescritura SPA global de IIS ni un proceso Node. El fallback de rutas de interfaz se resuelve en ASP.NET Core y no intercepta /api.

## Operación y recuperación

- Copias de seguridad de SQL y claves/configuración, con restauración ensayada antes de producción.
- Restringir acceso a logs y monitorizar readiness, errores y latencia. Los logs no deben incluir contraseñas ni cuerpos de conversaciones.
- Registrar versión desplegada y migración aplicada. Un rollback de binarios requiere esquema compatible.
- La instalación de un solo nodo no ofrece alta disponibilidad. No prometer RPO/RTO ni disponibilidad contractual sin validar infraestructura y operación.
