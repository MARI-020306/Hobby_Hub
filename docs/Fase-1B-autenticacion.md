# Fase 1B — Autenticación, roles y sesiones

Trabajo local en `rama/Fernando`, continuando los archivos de la Fase 1A. No se
hicieron commits, push, cambios de rama, conexiones a la base compartida, migraciones
ni ejecuciones de scripts SQL. El PDF de referencia no se modificó.

## Funcionalidades implementadas

- Login normaliza espacios del correo y conserva SHA-256 del correo y BCrypt para
  contraseñas. Valida correo, longitudes y el límite de BCrypt de 72 bytes UTF-8.
  No trunca contraseñas. Un hash malformado falla sin autenticar.
- Mensaje común para credenciales incorrectas, cuenta inexistente, no activa o
  bloqueada. Fallos de infraestructura devuelven un mensaje genérico y HTTP 503.
- Cinco intentos incorrectos bloquean la cuenta durante 15 minutos. El contador y
  plazo se persisten en Usuarios mediante actualizaciones atómicas. Al bloquear,
  rota la versión de seguridad y las sesiones anteriores no vuelven a servir
  cuando termina el bloqueo. Un login correcto reinicia el contador.
- Límite adicional de 10 POST de login por minuto y dirección IP, sin cola,
  HTTP 429 y Retry-After. Este límite es por proceso; el bloqueo de cuenta sí es
  persistente. Antes de despliegue con proxies o varias réplicas se debe definir
  el tratamiento de IP confiables y coordinación del límite. No se confía en
  encabezados Forwarded/X-Forwarded-For enviados por el cliente.
- Sesiones persistidas con identificador aleatorio, versión de seguridad,
  creación, expiración absoluta de ocho horas y revocación. La creación valida
  nuevamente estado, versión, contraseña y rol en una transacción SQL, después
  de comprobar BCrypt, para evitar emitir sesión tras un cambio concurrente.
- Cada solicitud autenticada reconsulta sesión y usuario. Rechaza estados distintos
  de Activo, bloqueo temporal, revocación, expiración, usuario ausente, cambios de
  contraseña, rol o versión y claims de rol adicionales o incoherentes. Si la
  consulta falla, rechaza la autenticación. Cookies antiguas sin sesión se rechazan.
- SaveChanges/SaveChangesAsync rotan VersionSeguridad al cambiar contraseña,
  RolId o Estado. RevokeAllAsync revoca explícitamente todas las sesiones.
  Las futuras escrituras masivas/SQL deben rotar la versión en la misma transacción.
  La revalidación impide nuevas solicitudes; no cancela una ya en ejecución.
- Logout solamente por POST con antiforgery; revoca en servidor antes de eliminar
  la cookie. No permite reutilizar la cookie capturada antes de cerrar sesión.
  Si no puede persistir la revocación responde 503 para permitir reintentar.
- Los cuatro menús actuales reutilizan el formulario de logout, manteniendo
  estilos de inicio, perfil y comunidades y adaptación móvil existente.
- Política de autenticación por defecto; registro/login/error/acceso denegado son
  excepciones públicas explícitas. Políticas Administrador, Moderador/Administrador
  y Creador para crear comunidades. Convención adicional protege controladores
  llamados Administracion y Moderacion. Cualquier futuro controlador administrativo
  con otro nombre deberá declarar la política correspondiente explícitamente.
- Registro mantiene únicamente Usuario/Creador; valida nombre, correo y contraseña
  y maneja colisiones de correo único durante guardados concurrentes. No cambia
  los formularios ni introduce roles privilegiados seleccionables.
- Cookie HttpOnly, SameSite=Lax, Secure fuera de Development, sin persistencia de
  navegador ni renovación deslizante. ReturnUrl acepta únicamente rutas locales.
- Data Protection usa nombre de aplicación constante y claves fuera de wwwroot.
  En Windows persiste en `.local/keys`, ignorado por Git, con protección DPAPI
  del usuario del proceso. Testing usa claves efímeras. Otros despliegues requieren
  configurar protección de claves apropiada antes de arrancar. No se generaron ni
  publicaron claves durante estas pruebas.
- JavaScript evita deshabilitar el botón ante validación fallida, impide dobles
  envíos y restaura el botón al volver desde el historial.

## Archivos de esta subfase

Todos los caminos siguientes son relativos a la raíz del repositorio. Los archivos
creados en 1A permanecen sin commit; se distinguen aquí de las incorporaciones de 1B.

### Modificados

- `.gitignore` (excluye claves locales).
- `Proyecto_HobbyHub/Program.cs`.
- `Proyecto_HobbyHub/Controllers/AuthController.cs`, `HomeController.cs`,
  `PerfilController.cs`, `ComunidadesController.cs`.
- `Proyecto_HobbyHub/Models/Usuario.cs`, `HobbyHubContext.cs`, `SecurityHelper.cs`.
- `Proyecto_HobbyHub/ViewModels/LoginViewModel.cs`, `RegistroViewModel.cs`.
- `Proyecto_HobbyHub/Views/Auth/Login.cshtml`, `Home/Index.cshtml`,
  `Perfil/Index.cshtml`, `Comunidades/Index.cshtml`, `Comunidades/Detalle.cshtml`.
- `Proyecto_HobbyHub/wwwroot/js/auth.js` y CSS `inicio.css`, `perfil.css`, `comunidades.css`.
- `Proyecto_HobbyHub/Database/README.md`, `Scripts/004_ReadOnlySchemaInventory.sql`.
- `Proyecto_HobbyHub.Tests/OfflineApplicationFactory.cs`, `OfflineMvcTests.cs`,
  `SchemaSafetyTests.cs`.

### Creados

- `Proyecto_HobbyHub/Controllers/AuthenticatedController.cs`.
- `Proyecto_HobbyHub/Models/SesionUsuario.cs`.
- `Proyecto_HobbyHub/Security/AccountSecurityService.cs`, `IAccountSecurityStore.cs`,
  `EfAccountSecurityStore.cs`, `RevalidatingCookieEvents.cs`, `SecurityPolicies.cs`,
  `BcryptPasswordAttribute.cs`.
- `Proyecto_HobbyHub/Views/Auth/AccesoDenegado.cshtml`, `Views/Shared/_LogoutForm.cshtml`.
- `Proyecto_HobbyHub/wwwroot/css/security.css`.
- `Proyecto_HobbyHub/Database/Scripts/005_AccountSessionsSecurity.sql`.
- `Proyecto_HobbyHub.Tests/AccountSecurityTests.cs`, `AuthenticationMvcTests.cs`,
  `FakeAccountSecurityStore.cs`, `SecurityTestControllers.cs`, `auth.test.cjs`.
- `docs/Fase-1B-autenticacion.md`.

## Esquema preparado, sin ejecución

Script 005 agrega `VersionSeguridad UNIQUEIDENTIFIER NOT NULL`, `IntentosFallidos INT
NOT NULL` y `BloqueoHastaUtc DATETIME2 NULL` a Usuarios, con valores iniciales sin
eliminar datos. Agrega SesionesUsuario, FK sin cascada, índice para sesiones por
usuario y comprobación de expiración. Comprueba nombre de base de desarrollo,
usa transacción y omite objetos ya existentes; hay que revisar compatibilidad si
esos objetos ya existieran con otro esquema. No hay DDL ejecutado por la aplicación.

Antes de iniciar contra SQL, revisar y aplicar manualmente 001–003 y 005 en una base
vacía dedicada según Database/README.md; 004 es inventario de metadatos. El esquema
anterior no soporta el modelo nuevo. No iniciar contra la base compartida sin un
proceso independiente autorizado de revisión y actualización.

## Verificación

- `dotnet restore Proyecto_HobbyHub.sln`.
- `dotnet build Proyecto_HobbyHub.sln --no-restore`.
- `dotnet test Proyecto_HobbyHub.sln --no-build --no-restore`.
- `node --test Proyecto_HobbyHub.Tests/auth.test.cjs`.

Resultado final: restore correcto; compilación con cero advertencias y cero errores;
75 pruebas de .NET aprobadas, ninguna omitida; cuatro pruebas de JavaScript aprobadas.
Total: 79 pruebas aprobadas. La suite .NET tenía 36 pruebas al finalizar la Fase 1A.

Las pruebas HTTP ejecutan MVC, Razor, middleware de cookies, políticas, antiforgery,
redirecciones y rate limiter reales, sustituyendo exclusivamente la persistencia
de seguridad por un doble de pruebas. Toda apertura EF/SQL está bloqueada por un
interceptor. Los controladores administrativos de pruebas se cargan solamente en
el host de pruebas; no crean pantallas administrativas en la aplicación.

Se mantienen las pruebas existentes de normalización, registro inválido, formularios
de perfil/publicaciones/comentarios y rutas anónimas. Esto verifica regresiones sin
SQL, no demuestra los flujos completos con datos reales.

## Pendientes con SQL Server aislado

- Aplicación y reejecución del script 005, columnas, FK, índices y valores iniciales.
- Traducción/ejecución de ExecuteUpdate y transacciones bajo intentos concurrentes,
  bloqueo persistente y reinicio del proceso.
- Registro real, correo duplicado concurrente, roles existentes y hashes históricos.
  Contraseñas históricas superiores a 72 bytes necesitarán recuperación/cambio seguro.
- Login, logout, revocación y cambios de estado/contraseña/rol entre varias sesiones.
- Recorrido real de perfil, comunidades, membresías y publicaciones con el esquema
  nuevo; revisión visual en navegador de escritorio/móvil y cookies sobre HTTPS.
- Protección y persistencia de claves con la identidad Windows que ejecutará la app.

## Propuesta para Fase 1C, pendiente de autorización

Completar configuración de cuenta y cambio de contraseña con comprobación de la
contraseña actual y revocación; diseñar recuperación/verificación de cuenta con
tokens de un solo uso y expiración, sin incorporar envío externo sin aprobación;
añadir auditoría de autenticación y acciones de seguridad sin material sensible;
reforzar permisos por recurso y privacidad en los flujos existentes y validar
persistencia/concurrencia en SQL aislado. No se implementó esta fase siguiente.
