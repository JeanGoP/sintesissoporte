# Frontend en Coolify; backend C# en IIS

## Distribución final

| Componente | Destino | Dirección acordada |
|---|---|---|
| Portal React | Coolify, contenedor Nginx estático | https://soporte.sintesiserp.com.co |
| API ASP.NET Core | Servidor Windows / IIS | https://sintesiserp.com.co/soporte |
| SQL Server y Worker | Servidor/red privada de Sidecil | Sin dominio público de base de datos |

El contenedor de Coolify NO contiene C#, SQL Server, Worker ni credenciales. El navegador se comunica directamente por HTTPS con IIS. En IIS crear una APLICACIÓN de alias `soporte` dentro del sitio sintesiserp.com.co, con pool propio «No Managed Code» y ruta física a la carpeta publicada. No basta una carpeta virtual. Conservar los bindings del sitio existente y revisar configuración heredada. La API debe ser accesible desde los equipos de los usuarios; no basta con que Coolify alcance el servidor.

Usar subdominios del MISMO dominio registrable y HTTPS en ambos. Las cookies siguen siendo HttpOnly, específicas del host de la API y SameSite; no se comparte una cookie Domain entre subdominios. Un dominio provisional de Coolify ajeno al dominio de la API no es una configuración soportada para las sesiones actuales. No se ha cambiado SameSite a None ni se depende de cookies de terceros.

## 1. Configurar el servidor IIS

Generar una nueva entrega con scripts/Publish-Server.ps1 y seguir INSTALAR-SERVIDOR.md para Hosting Bundle .NET 10, SQL, claves persistentes, bootstrap y Worker. Configurar en la API:

```text
ASPNETCORE_ENVIRONMENT=Production
AllowedHosts=sintesiserp.com.co
Hosting__PathBase=/soporte
Frontend__PublicBaseUrl=https://soporte.sintesiserp.com.co
ConnectionStrings__Tickets=CONEXION_PRIVADA_SQL
DataProtection__Path=C:\ProgramData\SidecilTickets\keys
Mail__PublicBaseUrl=https://soporte.sintesiserp.com.co
```

Frontend__PublicBaseUrl es el origen exacto autorizado por CORS y el destino fijo de los retornos OAuth. No acepta rutas, comodines ni destinos proporcionados por el navegador. Las operaciones conservan CSRF y ETag/If-Match. No añadir CORS '*' ni cabeceras duplicadas desde IIS.

El Worker conserva su propia conexión SQL y configuración privada del buzón. Mail__PublicBaseUrl debe ser la URL del FRONTEND en ambos procesos: allí se abre /confirmar-solicitud. No poner localhost en producción.

Microsoft y Google deben registrar los callbacks del BACKEND:

```text
https://sintesiserp.com.co/soporte/signin-microsoft
https://sintesiserp.com.co/soporte/signin-google
```

Después de autenticar, IIS devuelve al usuario al frontend configurado. Los secretos OAuth se guardan exclusivamente en IIS. La API sin Frontend__PublicBaseUrl mantiene el modo local anterior, por compatibilidad.

El widget embebido conserva su ruta /chat-widget y sus recursos en IIS para mantener la política de enmarcado específica de cada ERP. El panel alojado en Coolify genera automáticamente el script del widget con el dominio de la API. Por eso no se debe borrar wwwroot del paquete de IIS: contiene los recursos del widget. El portal principal se sirve desde Coolify; la raíz de la API redirige al portal cuando Frontend__PublicBaseUrl está configurado.

## 2. Configurar Coolify

1. Conectar tu servidor Linux a Coolify y configurar el DNS del frontend hacia ese servidor. Mantener el DNS de la API apuntando a IIS. Ambos deben tener HTTPS válido.
2. Crear un recurso Application desde GitHub: repositorio JeanGoP/sintesissoporte, rama main. Si es privado, autorizar acceso mediante la integración GitHub o clave de despliegue.
3. Seleccionar Dockerfile como método de compilación.
4. Directorio base/contexto: raíz del repositorio (`/`). Dockerfile: `/deploy/coolify/Dockerfile`.
5. Añadir `VITE_API_URL=https://sintesiserp.com.co/soporte` como variable disponible DURANTE LA COMPILACIÓN (Build Variable/build argument). Incluir `/soporte`, pero NO `/api/v1`, consultas ni secretos. El Dockerfile declara ARG VITE_API_URL y exige HTTPS.
6. Puerto interno: `80`. Dominio: `https://soporte.sintesiserp.com.co`. Configurar comprobación HTTP `/health` en puerto 80; solo comprueba el frontend, no SQL ni el backend.
7. Desplegar y revisar logs. Las rutas React como /account funcionan al recargar gracias al fallback de Nginx.
8. Al cambiar VITE_API_URL, RECOMPILAR/redeploy: reiniciar el contenedor no cambia los archivos ya compilados.

No añadir conexión SQL, claves de correo ni secretos Microsoft/Google en Coolify ni en variables VITE_. VITE_ es configuración pública incrustada en JavaScript. Nginx entrega archivos estáticos; no necesita volumen de base de datos ni claves de sesión.

## 3. Verificación del despliegue

- Abrir el portal y comprobar en Red del navegador que /api/v1/... va al dominio de IIS y no al de Coolify.
- Iniciar sesión, recargar /account, consultar y actualizar un ticket, descargar un adjunto y cerrar sesión.
- Revisar que los preflight OPTIONS reciban el origen exacto y credenciales permitidas; que orígenes no autorizados no reciban permiso CORS.
- Probar los dos botones OAuth con aplicaciones reales configuradas; la cookie se establece en la API y el retorno termina en el portal.
- Crear una solicitud invitada, confirmar el enlace enviado por correo y verificar la conversación. Probar el widget desde el ERP registrado.
- Comprobar /health/ready en IIS, el servicio Worker, backups de SQL y claves, límites de adjuntos y entrega real del buzón.

## Compilación y pruebas locales

`npm run build:frontend` dentro de src/sidecil-tickets-web genera únicamente `dist/`. `npm run build` conserva el paquete de recursos de IIS. `VITE_API_URL` se define antes de la compilación independiente.

La prueba split-hosting.spec.ts utiliza frontend http://localhost:5180 y API http://localhost:5099/soporte, con Hosting__PathBase=/soporte, Frontend__PublicBaseUrl=http://localhost:5180 y VITE_API_URL=http://localhost:5099/soporte. Usar Development, SQL demo y definir SIDECIL_SPLIT_TEST=1 al ejecutar esa prueba. Comprueba login real por CORS, cookies, CSRF, lectura de ETag y logout. HTTP local solo sirve para desarrollo; no demuestra TLS del servidor final.

Docker local: `docker build -f deploy/coolify/Dockerfile --build-arg VITE_API_URL=https://sintesiserp.com.co/soporte -t sidecil-frontend .`. El motor Docker debe estar activo. La compilación de React y las pruebas locales no sustituyen la prueba de la imagen en el servidor Coolify.

## Actualizaciones

Los cambios se sincronizan con GitHub mediante commits en español. Coolify puede reconstruir automáticamente el frontend desde main cuando se active su integración. Eso NO actualiza IIS: publicar y copiar el backend por separado, ejecutar migraciones revisadas cuando existan y verificar compatibilidad de ambas versiones. Para cambios de API incompatibles, coordinar la publicación; conservar versión anterior y backups.

## Fuentes oficiales

- https://coolify.io/docs/applications/configuration/environment-variables
- https://coolify.io/docs/applications/builds/dockerfile
- https://learn.microsoft.com/es-es/aspnet/core/security/cors?view=aspnetcore-10.0
