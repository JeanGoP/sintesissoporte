# Próxima etapa: Sidecil Tickets en Coolify

Coolify despliega contenedores en servidores Linux. La entrega `artifacts/servidor/.../iis` está preparada para Windows/IIS y NO se carga como un sitio IIS dentro de Coolify. Esta guía describe el despliegue que hay que preparar; todavía no se han creado ni probado sus Dockerfiles ni se ha desplegado en Coolify.

## Elegir el destino

- Windows/IIS: usar la entrega generada y la guía INSTALAR-SERVIDOR.md.
- Coolify: construir la misma aplicación ASP.NET Core para Linux, con Kestrel dentro de un contenedor, y un segundo contenedor para el Worker. Mantener SQL Server, que puede estar en el servidor Windows existente mediante red privada/VPN.

En ambos casos conviene servir frontend y API bajo el mismo dominio: la aplicación usa cookies y CSRF de mismo origen. Separar el frontend en otro dominio requiere adaptar rutas, cookies y CORS; no está incluido en esta entrega.

## Información necesaria para el despliegue real

1. URL de tu panel Coolify y acceso a un proyecto/servidor autorizado.
2. Servidor Linux conectado a Coolify por SSH, Docker, arquitectura y recursos disponibles. Dimensionar según compilaciones, usuarios, archivos y base; monitorizar almacenamiento y memoria.
3. Dominio de producción, acceso a DNS y certificado HTTPS gestionado por el proxy. DNS debe apuntar al servidor de aplicaciones correcto.
4. SQL Server: host privado, puerto, base, certificado de confianza y cuentas separadas para despliegue y ejecución. LocalDB e Integrated Security de Windows no se trasladan automáticamente a Linux.
5. Buzón SMTP/IMAP, método de autenticación, remitente y política SPF/DKIM/DMARC. Leer CORREO.md sobre OAuth y validación de respuestas.
6. Credenciales OAuth para login, solo si se habilita Microsoft/Google; callbacks bajo el dominio de producción.
7. Acceso de Coolify a GitHub. Para repositorio privado, usar su integración GitHub o clave de despliegue con el menor acceso necesario.

## Trabajo técnico que falta para Coolify

- Dockerfile con compilación Node + SDK .NET, frontend dentro de wwwroot, y runtime ASP.NET Core 10 para Linux. Worker en imagen/proceso separado, sin puerto público. Excluir .local, .tools, artifacts, .git y secretos del contexto mediante .dockerignore.
- Compose o dos recursos coordinados, con reinicio, logs y un único lector IMAP. No exponer SQL ni el puerto interno de Kestrel directamente a Internet.
- Configurar Forwarded Headers de ASP.NET Core para confiar únicamente en el proxy/red conocidos. Hoy la API está configurada para IIS: antes de Coolify hay que adaptar y probar este punto para que HTTPS, cookies, callbacks OAuth e IP de los límites de solicitudes funcionen detrás del proxy. No confiar indiscriminadamente en cabeceras enviadas por clientes.
- Volumen persistente para DataProtection__Path, permisos del usuario del contenedor y respaldo protegido. En Linux el código actual NO cifra las claves con DPAPI: definir protección en reposo acorde al servidor y acceso restringido. No dejar las claves en la capa efímera de la imagen.
- Variables de ejecución privadas: ASPNETCORE_ENVIRONMENT=Production, DOTNET_ENVIRONMENT=Production, AllowedHosts, ConnectionStrings__Tickets, DataProtection__Path y Mail; OAuth opcional. No meter secretos en argumentos de build ni commits.
- Ejecutar migraciones y bootstrap como tareas controladas con credenciales temporales de despliegue; el proceso normal usa credenciales de ejecución. No usar datos demo ni migrar automáticamente cada réplica.
- Probar contenedores, migraciones, proxy, login, roles, correo y reinicio con persistencia antes del cambio de DNS.

## Pasos en el panel cuando la adaptación esté lista

1. Crear proyecto y entorno de producción. Añadir el repositorio JeanGoP/sintesissoporte, rama main, mediante la integración Git autorizada.
2. Elegir Docker Compose o Dockerfile según los archivos que preparemos; no desplegar hoy por autodetección suponiendo que ya está adaptado.
3. Configurar variables privadas y volumen de claves. Para el servicio web, indicar su puerto interno real (por ejemplo 8080 si se configura así) y asignar el dominio HTTPS. El Worker no lleva dominio.
4. Preparar SQL y administrador, desplegar y comprobar logs, `/health/live`, `/health/ready` y flujos reales. En Compose definir explícitamente los healthchecks de los servicios.
5. Activar despliegue automático desde Git solo después de validar la primera entrega y definir cómo se aplicarán futuras migraciones. Un push de código no debe ejecutar cambios de esquema sin control.
6. Configurar alertas, retención de logs, copias de SQL y claves y probar recuperación. Mantener una versión anterior disponible y plan de rollback compatible con la base.

## Fuentes oficiales

- https://coolify.io/docs/start-with-self-hosted
- https://coolify.io/docs/applications/
- https://coolify.io/docs/core/networking-in-coolify
- https://coolify.io/docs/core/networking/domains

No se ha conectado ningún servidor externo ni cambiado DNS. El siguiente paso es preparar y verificar los contenedores para la infraestructura elegida.
