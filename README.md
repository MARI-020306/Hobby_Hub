# HobbyHub

Red social académica para compartir hobbies y participar en comunidades. ASP.NET Core MVC, C#, Entity Framework Core y SQL Server; interfaz Razor responsive, JavaScript y diseño compartido basado en Comunidades.

Esta versión está preparada para revisión **local**, no para despliegue público. Se conservan los 15 módulos y las fases de seguridad anteriores. No contiene cuentas ni contraseñas predeterminadas.

## Requisitos

- Windows y SQL Server Express LocalDB **15.0** para el aprovisionamiento incluido; herramientas `SqlLocalDB` y `sqlcmd` en PATH.
- SDK compatible con .NET 8 y runtime ASP.NET Core 8 actualizado. En la revisión se utilizó SDK 10.0.401, runtime 8.0.31 y destino `net8.0`.
- VS Code con soporte C# o Visual Studio compatible; PowerShell.
- Para navegador: Microsoft Edge instalado, Node.js y npm. Se probaron Node 24.20.0, npm 11.19.0 y Playwright 1.58.2. Python 3 para el verificador de archivos de entrega.

## Seguridad de configuración y bases

`appsettings.json` no contiene una conexión privada. Development carga `appsettings.Development.Isolated.json`; los overrides personales quedan en archivos locales ignorados o variables de entorno. La aplicación valida el destino y nunca utiliza la conexión compartida como alternativa. Production y otros entornos no autorizados se rechazan.

El entorno ordinario está restringido a la instancia `(localdb)\HobbyHubPhase1C_20261008` y la base `HobbyHub_Development_Phase1C_20261008`. La revisión automatizada utiliza **otra base**, con nombre `HobbyHub_Development_Visual_20261008_<ocho caracteres hexadecimales>`, y `VisualTesting:Enabled=true`. Las conexiones usan autenticación Windows; no se versionan contraseñas SQL.

No ejecutar migraciones ni scripts sobre una base existente. No hay CREATE TABLE, ALTER TABLE, EnsureCreated ni migraciones automáticas al abrir páginas. El esquema se prepara mediante scripts revisables fuera del servidor web.

### Equipo actual: conservar los datos existentes

La base de revisión está preparada y su configuración está en `.local/visual-test-database.json`. **No repetir el aprovisionamiento.** Se utiliza el puerto **5088**; el servidor del usuario en 5062 y sus binarios permanecen independientes.

### Instalación en un equipo nuevo

1. Obtener el checkout autorizado y comprobar `git branch --show-current`; trabajar en `rama/Fernando`.
2. Inventariar con `SqlLocalDB info` y `SqlLocalDB versions`. Si el nombre reservado ya existe, detener el aprovisionamiento: no sobrescribir ni reutilizar una instancia ajena.
3. Revisar [Database/README](Proyecto_HobbyHub/Database/README.md) y los scripts. Solo en un equipo donde se haya confirmado que la instancia no existe, el propietario puede ejecutar:

```powershell
./Proyecto_HobbyHub/Database/Prepare-IsolatedDevelopment.ps1 -Approved
```

El procedimiento crea una instancia y base nuevas; aplica 001, 002, 003, 005, 006, 007, 008, 009 y el inventario de metadatos 004. Requiere LocalDB 15.0. No instala SQL Server ni reutiliza otros destinos.

4. Para separar las pruebas de los datos de desarrollo, ejecutar una sola vez:

```powershell
./tools/Prepare-VisualTestDatabase.ps1
```

Este segundo procedimiento crea un nombre aleatorio, comprueba su ausencia y aplica copias de los scripts con guardas para esa base nueva. Guarda la configuración solo en `.local`. Si falla, revisar el error; no borrar ni recrear bases para intentar solucionarlo.

## Compilación y ejecución desde VS Code

Abrir la raíz donde está `Proyecto_HobbyHub.sln`. Detener **solo el servidor 5088** con Ctrl+C antes de recompilar: Windows bloquea los DLL que están en uso.

```powershell
dotnet restore Proyecto_HobbyHub.sln --artifacts-path .local/visual-build
dotnet build Proyecto_HobbyHub.sln --artifacts-path .local/visual-build --no-restore
./tools/Start-VisualReview.ps1
```

Abrir **http://127.0.0.1:5088**. El script configura Development, selecciona la base de revisión, usa claves locales separadas y una cookie de revisión propia. No escribe en los binarios ordinarios del servidor 5062.

Para ejecución ordinaria, una vez preparada la base de desarrollo dedicada, se puede usar el perfil `http` de `Proyecto_HobbyHub/Properties/launchSettings.json`; reserva 5062. **No utilizarlo en el equipo actual mientras el usuario tenga su servidor allí.** Los perfiles de `.vscode` se conservan para desarrollo ordinario.

## Roles y permisos

| Rol | Permisos principales |
|---|---|
| Usuario | Comunidades, publicaciones como miembro, comentarios, likes, guardados, perfiles, seguidores, mensajes, notificaciones, reportes y seguridad de su cuenta. |
| Creador | Lo anterior y creación de comunidades. Administra su propia comunidad y sus membresías. |
| Moderador | Participación social y revisión de reportes: resolver, ocultar contenido reportado o suspender temporalmente cuentas según los permisos del recurso. |
| Administrador | Moderación, gestión de cuentas/roles/estado, privacidad de comunidades y auditoría general. |

El registro solo permite Usuario/Creador. No puede asignar roles privilegiados. La autorización se comprueba en el servidor, incluida propiedad, membresía, seguidores aceptados, bloqueos y privacidad; ocultar un botón no sustituye esa comprobación. La propiedad de una comunidad se valida independientemente del rol actual.

### Cuentas sintéticas para la revisión

Después de compilar, preparar los roles de pruebas exclusivamente en la base indicada por la configuración local:

```powershell
$config=Get-Content .local/visual-test-database.json -Raw | ConvertFrom-Json
$env:HOBBYHUB_SQL_TESTS='1'
$env:HOBBYHUB_VISUAL_TEST_DATABASE=$config.Database
dotnet test Proyecto_HobbyHub.sln --artifacts-path .local/visual-build --no-build --no-restore --filter FullyQualifiedName~VisualTestSeed
npm --prefix tools/browser ci
# Mantener el servidor 5088 iniciado en otra terminal.
node tools/browser/prepare-review-account.cjs
```

Administrador/Moderador se guardan en `.local/visual-privileged-accounts.json` y Creador en `.local/visual-browser-accounts.json`. Son datos locales sensibles, aleatorios y sintéticos; no copiar su contenido a documentación, capturas ni Git. En este equipo ya están preparados y no deben sobrescribirse. Un Usuario también puede registrarse desde la interfaz.

## Funcionamiento de los 15 módulos

1. Autenticación: registro, login/logout POST, bloqueo temporal, recuperación y sesiones revocables.
2. Inicio: feed por intereses, personas seguidas y comunidades; paginación.
3. Explorar: búsqueda de personas, intereses, comunidades y contenido visible.
4. Publicaciones: texto, galerías JPG/PNG y edición/retiro del contenido propio.
5. Interacciones: comentarios y respuestas, likes, guardados y actividad propia.
6. Comunidades: creación, descubrimiento, ingreso público o solicitudes privadas y abandono.
7. Administración de comunidad: reglas, privacidad, invitaciones y gestión de miembros por su propietario.
8. Perfiles: biografía, preferencias, fotografías, seguidores y privacidad.
9. Mensajes: conversaciones privadas, lectura, bloqueos y reportes de mensajes recibidos.
10. Notificaciones: actividad social y estados de lectura, con preferencias individuales.
11. Reportes: publicaciones, comentarios, cuentas y mensajes recibidos.
12. Moderación: evidencia, estados y decisiones auditadas; concurrencia controlada.
13. Administración: cuentas, roles, estados y privacidad de comunidades.
14. Seguridad: cambio de contraseña, privacidad, suspensión y revocación de sesiones.
15. Actividad/auditoría: interacciones propias, sesiones abiertas y registros administrativos filtrables.

Los mensajes y notificaciones son persistentes; no incorporan actualización en tiempo real. Se actualizan mediante navegación o recarga. Las tablas y permisos se comprueban con SQL Server real aislado; no son pantallas estáticas.

## Contraseñas, imágenes y recuperación

- BCrypt y límite de 72 bytes de UTF-8 para contraseñas; cinco fallos causan bloqueo temporal de 15 minutos. También hay límites de solicitudes por IP.
- Cookies HttpOnly, SameSite y seguridad HTTPS fuera del modo local; sesiones revalidadas contra estado/versión y revocadas tras cambios de contraseña, rol o suspensión.
- Antiforgery en escrituras, codificación Razor, consultas EF parametrizadas y autorización por recurso.
- Hasta seis imágenes por publicación, 5 MB cada una. Se decodifican y vuelven a codificar con SkiaSharp; hay límites de dimensiones y área. Archivos fuera de `wwwroot`; acceso a imágenes mediante rutas autorizadas.
- Recuperación con tokens aleatorios, hash, caducidad de 30 minutos y consumo único. `IRecoveryMailSender` desacopla el envío; el único remitente implementado es el buzón local cifrado. **No se envían correos reales.**

Para exportar un mensaje de recuperación propio, detener solo el servidor de revisión y mantener las variables de la base/claves visuales indicadas en `Start-VisualReview.ps1`; ejecutar el mismo DLL con `--LocalMailbox:Export=<identificador-del-archivo>`. El comando guarda el token exclusivamente en `Proyecto_HobbyHub/.local/mailbox`. No usar `dotnet run` con la configuración ordinaria para exportar mensajes de otra base, ni borrar claves existentes. El proveedor de correo de producción requiere configuración y autorización posteriores.

## Pruebas y revisión de entrega

Desde la raíz; no recompilar mientras 5088 esté usando los DLL:

```powershell
# Todas las pruebas SQL escriben solo datos sintéticos en la base visual separada.
$config=Get-Content .local/visual-test-database.json -Raw | ConvertFrom-Json
$env:HOBBYHUB_SQL_TESTS='1'
$env:HOBBYHUB_VISUAL_TEST_DATABASE=$config.Database
dotnet test Proyecto_HobbyHub.sln --artifacts-path .local/visual-build --no-build --no-restore
node --test Proyecto_HobbyHub.Tests/auth.test.cjs Proyecto_HobbyHub.Tests/navigation.test.cjs

# Con el servidor 5088 iniciado:
node tools/browser/visual-suite.cjs
node tools/browser/final-review.cjs

# Revisión de archivos candidatos sin mostrar los valores detectados:
python tools/Verify-RepositorySafety.py
```

Para pruebas offline, establecer `HOBBYHUB_SQL_TESTS=0`; las pruebas SQL se omiten expresamente. **No habilitar SQL sin indicar también la base visual.** La suite no limpia datos previos. Los scripts históricos `smoke.cjs`, `recovery.cjs` y `capture-before.cjs` no forman parte de la revisión final.

Las capturas y los informes técnicos detallados se guardan en `docs/visual` o `.local/final-review` y no se versionan. El resumen revisado de entrega está en [el informe final](docs/Informe-final-entrega.md).

## Dependencias y mantenimiento

Paquetes .NET en los `.csproj`; herramientas de navegador fijadas en `tools/browser/package-lock.json`. `axe-core` es exclusivamente de pruebas. Las bibliotecas estáticas de `wwwroot/lib` conservan sus licencias; jQuery Validation fue actualizado a **1.22.1** para corregir [CVE-2025-3573](https://github.com/advisories/GHSA-rrj2-ph5q-jxw2). Los mensajes de validación se tratan como texto.

Auditar antes de cada entrega: `dotnet restore ... -p:NuGetAudit=true -p:NuGetAuditMode=all` y `npm --prefix tools/browser audit`. El análisis npm no incluye automáticamente las bibliotecas copiadas en `wwwroot/lib`; también deben revisarse sus versiones. [.NET 8 termina su soporte el 10 de noviembre de 2026](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core); planificar una actualización antes de producción, conservando por ahora el objetivo net8.0 autorizado.

## Archivos a registrar y exclusiones

Registrar código, solución/proyectos, vistas, recursos propios y bibliotecas/licencias revisadas, scripts SQL, pruebas, herramientas reproducibles, manifests/lockfiles y documentación. El inventario exacto está en [Archivos-entrega.txt](docs/Archivos-entrega.txt).

Excluir `.local`, configuraciones personales, `.env`, claves/certificados, bases/backups, archivos subidos, buzón, credenciales sintéticas, capturas, informes de ejecución, `bin`, `obj`, `node_modules`, logs y temporales. `.gitignore` no elimina archivos ya rastreados: el verificador también comprueba ese caso. No se ejecutaron commit, push, merge ni despliegue.

## Documentación y límites

- [Guía de exposición universitaria](docs/Guia-exposicion.md).
- [Informe final: funciones, pruebas, limitaciones y recomendaciones](docs/Informe-final-entrega.md).
- [Esquema SQL y reglas de aislamiento](Proyecto_HobbyHub/Database/README.md).
- [Unificación visual](docs/Unificacion-visual.md) e informes históricos de fases en `docs`.

La preparación de entrega no equivale a certificación de seguridad ni autorización de producción. Pendientes de producción: proveedor de correo, HTTPS/host, permisos SQL mínimos, respaldos/retención, pruebas de carga, revisión completa de accesibilidad y privacidad, y política CSP compatible con los recursos e interacciones existentes. No usar datos reales en la exposición.
