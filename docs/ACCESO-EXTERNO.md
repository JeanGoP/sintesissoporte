# Microsoft y Google para ingresar a Sidecil Tickets

Los botones están en la **pantalla principal de inicio de sesión**, junto al acceso con correo y contraseña. El chat embebido no utiliza estos proveedores.

## Uso por cada usuario

Los clientes nuevos pueden pulsar **Continuar con Google** o **Continuar con Microsoft**. Si la identidad externa no está vinculada y el correo no pertenece a una cuenta existente, se crea un usuario **Requester**, sin contraseña local ni equipo, en la organización de solicitudes externas. Solo puede consultar sus propios tickets. Las solicitudes anteriores sin cuenta no se asocian por correo. Google debe entregar un correo verificado; Microsoft debe entregar un correo válido, que se conserva como contacto sin marcarlo confirmado. Si falta, se solicita usar otra cuenta o contactar a soporte.

Para cuentas existentes, agentes y administradores:

1. El usuario conserva la cuenta, organización y rol asignados por el administrador.
2. El usuario ingresa con su correo y contraseña y abre **Mi cuenta**.
3. En **Acceso con Microsoft y Google**, confirma su contraseña actual y pulsa **Vincular** junto al proveedor.
4. Completa el acceso en Microsoft o Google. Regresa al portal con la cuenta vinculada.
5. Desde entonces puede ingresar con **Continuar con Microsoft** o **Continuar con Google** en la pantalla principal.

Se conservan el mismo usuario, rol, organización y tickets. No se vinculan cuentas existentes por coincidencia de correo. Una cuenta externa que todavía no está vinculada recibe una indicación para ingresar con contraseña y vincularla. La desvinculación se realiza en **Mi cuenta**, confirmando la contraseña. Las cuentas creadas con un proveedor muestran su acceso vinculado y no ofrecen cambiar una contraseña inexistente ni desvincular su único acceso.

## Activar Microsoft

En Microsoft Entra, registra una aplicación **web** compatible con cuentas de cualquier directorio organizativo y cuentas Microsoft personales. Registra la URI exacta:

`https://www.sintesiserp.com.co/API_SoporteSidecil/signin-microsoft`

Configura el identificador de aplicación y su secreto en el servidor:

- `ExternalLogin__Microsoft__ClientId`
- `ExternalLogin__Microsoft__ClientSecret`

## Activar Google

Configura la pantalla de consentimiento y crea un cliente OAuth de tipo **aplicación web**. Durante pruebas añade las cuentas de prueba autorizadas. Registra la URI exacta:

`https://www.sintesiserp.com.co/API_SoporteSidecil/signin-google`

Configura:

- `ExternalLogin__Google__ClientId`
- `ExternalLogin__Google__ClientSecret`

Sustituye el dominio por el de la API de Sidecil en IIS. Configura Frontend__PublicBaseUrl con el origen del portal en Coolify para que el retorno termine allí. Para desarrollo registra también las URI `http://localhost:5080/signin-microsoft` y `http://localhost:5080/signin-google`, según lo permita cada proveedor. Usa las rutas del portal, no las antiguas rutas del chat.

El archivo `deploy/external-login.example.json` muestra la estructura sin credenciales. Guarda los secretos en la configuración protegida de IIS, nunca en el repositorio, JavaScript o mensajes de soporte. Reinicia la API después de configurarlos. Los proveedores se activan de forma independiente; **Mi cuenta** muestra su estado. Mientras falten credenciales, los botones aparecen deshabilitados y sigue disponible el acceso habitual.

## Permisos y controles

Solo se solicitan `openid profile email`; no se lee el buzón, contactos, calendario ni archivos. La identidad externa es el proveedor más el hash de emisor y subject validados; no se usa el correo ni los roles declarados por el proveedor para asignar permisos locales.

Se usa el middleware oficial ASP.NET Core OpenID Connect, autorización por código y PKCE, con comprobación de firma, emisor, audiencia, vigencia, nonce y correlación. Los enlaces de inicio caducan a los cinco minutos y son de un solo uso. El callback de vinculación exige que siga presente la sesión local original y su SecurityStamp, además de la comprobación previa de contraseña. Un cambio de sesión o una respuesta repetida no puede vincular la identidad a otro usuario.

Las cuentas se guardan en ASP.NET Core Identity (`AspNetUserLogins`). Identity aplica bloqueo y requisitos de acceso al iniciar sesión externamente; no se omite una eventual exigencia de segundo factor. No se guardan tokens de acceso o renovación de los proveedores. La aplicación conserva la sesión con su cookie HttpOnly habitual.

Las operaciones iniciales y la vinculación/desvinculación usan CSRF y límites de solicitudes. No hay asociaciones por correo ni creación automática de equipos o roles; el registro externo siempre asigna Requester, ignorando los roles del proveedor.

## Despliegue y pruebas

El registro automático no requiere una nueva migración. La migración `20260926043108_PortalExternalLogin` añade `identity.ExternalLoginAttempts`. Las tablas experimentales anteriores del chat se conservan sin usarse para evitar borrar datos durante la corrección. Sus endpoints y controles de acceso externo fueron retirados.

`PortalOidcTests` valida el middleware completo con tokens firmados de prueba: vinculación, inicio de sesión con ambos proveedores, mantenimiento de usuario y rol, desvinculación, rechazo de tokens alterados, repetición y cuentas no vinculadas. Requiere SQL Server de desarrollo con las migraciones aplicadas y limpia los usuarios de prueba que crea. Para pruebas sin SQL usa `--filter FullyQualifiedName!~PortalOidcTests`.

La verificación real de consentimiento y retorno con aplicaciones y cuentas de Sidecil queda pendiente de configurar sus credenciales. Las pruebas simuladas no la sustituyen.

Referencias oficiales: [Microsoft ID tokens](https://learn.microsoft.com/en-us/entra/identity-platform/id-token-claims-reference) y [Google OpenID Connect](https://developers.google.com/identity/openid-connect/openid-connect).
