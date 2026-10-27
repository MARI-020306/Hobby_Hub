# Hobby_Hub: diagnóstico y cinco propuestas de mejora

Fecha de revisión: 23 de octubre de 2026. Alcance: archivos actuales del espacio de trabajo, metadatos SQL autorizados, registros técnicos locales y verificaciones que no modifican el sistema.

## Diagnóstico general

Hobby_Hub dispone de modelos y relaciones para cuentas, sesiones, recuperación, comunidades, membresías, publicaciones, comentarios, reacciones, mensajes, notificaciones y moderación. No procede afirmar que todo el sistema está mal ni que carece de validación. Se encontraron controles concretos de autorización por recurso, antifalsificación, límites de solicitudes, restricciones SQL y transacciones. También se comprobaron diferencias que afectan operaciones específicas: un esquema de revisión desactualizado, límites incompatibles para la descripción de comunidades, almacenamiento insuficiente para ciertos caracteres y filtros distintos para comentarios de cuentas suspendidas. La trazabilidad existe, pero no permite reconstruir todos los resultados y decisiones de manera inequívoca.

Se proponen **exactamente cinco mejoras**, desarrolladas más adelante. P0 significa bloqueo de operaciones en el destino afectado; P1, impacto alto en integridad, privacidad o trazabilidad; P2, fallo funcional localizado. Las prioridades no equivalen a vulnerabilidades explotadas.

### Método, resultados y alcance de la evidencia

- Se revisaron `Models/HobbyHubContext.cs` y su parcial social, entidades, ViewModels de entrada y salida, controladores, servicios de seguridad, vistas Razor, JavaScript, configuración, scripts SQL y pruebas. Las referencias de líneas corresponden al archivo actual, incluyendo cambios locales anteriores a esta revisión.
- El repositorio ya tenía numerosos archivos modificados y sin seguimiento. Se consultó `git status` con una excepción de confianza válida solamente para ese comando; no se cambió la configuración global ni se hizo commit, push o despliegue.
- Se ejecutaron las dos suites JavaScript con `node --test Proyecto_HobbyHub.Tests/auth.test.cjs Proyecto_HobbyHub.Tests/navigation.test.cjs`: **9 pruebas aprobadas, 0 fallidas**. Verifican comportamiento simulado de formularios y navegación; no acreditan SQL, MVC completo ni autorización real.
- SDK disponible: **10.0.401**; los proyectos apuntan a `net8.0`. No se compiló ni se restauraron dependencias porque generan archivos adicionales. No se ejecutó `dotnet test`: algunas pruebas escriben SQL, archivos de imágenes, buzón o logs; incluso las fábricas offline construyen la aplicación con un proveedor de archivos de logs. La autorización de esta etapa permite crear únicamente este informe.
- No se inició la aplicación, no se enviaron formularios ni se hicieron peticiones que pudieran generar auditoría, sesiones o logs. No se ejecutaron migraciones, aprovisionamiento, escrituras SQL ni pruebas contra bases compartidas. Una prueba con rollback también realiza escrituras y por ello se excluyó.
- Los logs se leyeron desde `Proyecto_HobbyHub/.local/logs/hobbyhub-*.jsonl`; no se fabricaron capturas ni resultados posteriores a una corrección.

### Bases comprobadas, sin credenciales

El acceso inicial a LocalDB desde el usuario restringido falló al consultar su registro. Con autorización de ejecución se realizaron exclusivamente consultas `SELECT` mediante autenticación Windows. `DB_NAME()` confirmó ambos destinos en la instancia local dedicada `(localdb)\HobbyHubPhase1C_20261008`:

| Destino | Confirmación directa de SQL | Estado de `dbo.AuditoriasSeguridad` |
|---|---|---|
| `HobbyHub_Development_Phase1C_20261008` | Base de desarrollo documentada y seleccionada explícitamente en la consulta | Nueve columnas: las seis originales más `CorrelacionId`, `TipoRecurso`, `IdRecurso`. La ausencia anterior de estas tres columnas **ya está resuelta en esta base**. |
| `HobbyHub_Development_Visual_20261008_6c76fa09` | Base identificada por la configuración local de revisión y por `DB_NAME()` | Solo seis columnas: `IdAuditoria`, `IdActor`, `IdObjetivo`, `Accion`, `Exito`, `FechaUtc`. **Las tres columnas todavía faltan aquí**. |

En la base visual se comprobaron **18 tablas**, **29 claves foráneas**, ninguna deshabilitada ni no confiable. Hay unicidad de correo, nombre de comunidad y pares publicación/usuario en likes; membresías, seguimientos y guardados tienen claves compuestas. Se encontraron cinco CHECK: evitar automensajes, autoseguimientos y autobloqueos, y comprobar caducidad posterior a creación en sesiones y recuperación. Esto acredita esas restricciones, no todas las reglas posibles de negocio ni ausencia de cualquier dato inconsistente.

La base visual tiene collation `SQL_Latin1_General_CP1_CI_AS`. SQL confirmó `Comunidades.Descripcion varchar(500)`, `Publicaciones.Contenido varchar(2000)`, `Comentarios.Contenido varchar(1000)` y `MensajesPrivados.Contenido varchar(2000)`.

`tools/Start-VisualReview.ps1:5-19` selecciona la base visual mediante una variable de entorno antes de iniciar el puerto 5088. `Program.cs:17-39` carga configuración aislada, overrides locales, secretos de usuario y variables; `DatabaseConnectionPolicy.Resolve` selecciona el destino. **Se confirmó la base efectiva de cada conexión de diagnóstico y la selección del script; no se inspeccionó el entorno privado de un proceso web activo.** No se atribuyen automáticamente los logs históricos a una de estas bases: no contienen ese dato. No se abrió ni se publicó ninguna configuración con credenciales privadas.

### Contraste de los flujos relevantes

Los archivos de esta tabla están bajo `Proyecto_HobbyHub/`, salvo indicación distinta. “Existe” significa comprobado en código; la ejecución completa actual queda pendiente cuando exige escrituras.

| Flujo | Formulario → servidor y negocio | Persistencia → respuesta → auditoría | Evaluación acotada |
|---|---|---|---|
| Registro | `Views/Auth/Registro.cshtml`, `RegistroViewModel`, `AuthController.Registro`: nombre, correo, contraseña/confirmación, rol ordinario; celular y dirección opcionales. Se validan anotaciones, rol permitido y duplicado. | Correo normalizado y hasheado, contraseña BCrypt, datos personales hasheados; índice único y captura de carrera 2601/2627; éxito después de `SaveChangesAsync`; `AccountCreated` por contexto. | Controles presentes. El límite 254 del correo de entrada frente a 150 de SQL no es truncamiento del correo: se guarda un hash de 64 caracteres. La base visual incompatible sí impide guardar también la auditoría. |
| Acceso y cierre | `LoginViewModel`, `AuthController.Login/Logout`, `AccountSecurityService`: validación, mensajes genéricos, bloqueo tras cinco fallos, sesión revocable; logout POST. | `EfAccountSecurityStore.CreateSessionAsync/RevokeSessionAsync`: transacción con `Login`/`Logout`; cookie después de persistir y cierre después de revocar. | No faltan eventos de login/logout exitoso. Falta confirmar su commit en el log técnico y ciertos rechazos por concurrencia. |
| Sesiones y permisos | `RevalidatingCookieEvents.ValidatePrincipal`, `AccountSecurityService.ValidateAsync`: usuario, versión, rol, expiración, revocación y estado. Políticas en `Program.cs` y propiedad/membresía en recursos. | Cambios de contraseña/rol/estado rotan versión. `ExecuteUpdate` de servicios de cuenta mantiene explícitamente versión y auditoría. | Se rechaza autenticación ante error de infraestructura; no se encontró un evento explícito para invalidación ordinaria de cookie. No se presume bypass de permisos. |
| Recuperar/restablecer/cambiar contraseña | `Views/Cuenta/*`, `AccountSecurityViewModels`, `CuentaController`, `AccountRecoveryService`: correo, token de formato acotado y contraseña; límite BCrypt en bytes; respuesta genérica para no enumerar cuentas. | Token aleatorio hasheado, 30 minutos, consumo único, transacción y revocación; eventos `RecoveryIssued`, `PasswordReset`, `PasswordChanged`, `SessionsRevoked` y denegaciones. | El único remitente es buzón local cifrado: no existe entrega real de correo. Es una limitación explícita del entorno, no prueba de pérdida de cuenta. Falta resultado de entrega local correlacionado. |
| Crear/editar comunidades y miembros | `ComunidadFormViewModel`, `CommunitySettings`, `ComunidadesController` y parcial Management: rol para crear, propietario para administrar, categorías permitidas, nombres duplicados y estados de membresía. | FK de creador, nombre único, clave compuesta; éxito después de guardar; auditoría automática de comunidad/membresía. | Incompatibilidad comprobada 2000/500 en descripción al administrar. No se inventa obligatoriedad para reglas o portada. |
| Publicaciones y fotografías | `ComposePost`, formularios de comunidad y social: texto 2000, comunidad válida y permiso; máximo seis imágenes; `ImageUploadService` limita tamaño/dimensiones y recodifica. | `SocialController.Nueva` usa transacción y elimina archivos nuevos ante fallo; imágenes fuera de webroot y rutas autorizadas. Crear/editar/retirar publicación se audita por contexto. | No se confirma persistencia por redirección. Falta auditoría específica al retirar una imagen de galería; texto Unicode requiere mejora 3. |
| Comentarios, respuestas, likes y guardados | Longitudes y blanco controlados; respuesta exige padre visible de la misma publicación; permisos sobre publicación; likes/guardados con unicidad. | Guardado antes de redirigir; comentario auditado; reacciones y guardados sin auditoría de dominio explícita. | Discrepancia de filtros de comentarios entre rutas; los límites 1000/2000 sí coinciden en los flujos principales de texto. Las FK del padre no garantizan por sí solas misma publicación: el servidor sí lo comprueba. |
| Seguidores, bloqueos y perfil | `SocialController.Seguir/Solicitud/Bloquear/Ajustes`, `PerfilController.Editar`: decisiones de privacidad y propiedad; nombre, biografía e intereses acotados. | Claves compuestas, rechazo de autorrelaciones; `ProfileChanged` y `UserBlocked/UserUnblocked`; no se encontró auditoría específica de aceptación/retiro de seguimiento. | Celular admite 20 caracteres al registrar y 10 al editar; el formato admitido es decisión pendiente. Intereses son texto libre separado por comas y el feed compara categorías; no hay catálogo obligatorio documentado. |
| Mensajes y notificaciones | `SocialController.Enviar/Mensajes/LeerMensajes/LeerNotificacion`, `SocialAccess.Notify`: remitente autenticado, destinatario activo, bloqueos, privacidad y preferencias. | Mensajes 2000, FK y prohibición de automensaje; `MessageCreated` automático. Lecturas de mensajes con `ExecuteUpdate`; notificaciones limitadas al titular. | La pertenencia a conversación está filtrada. No se propone registrar contenido privado. Conservar historial de mensajes tras bloqueo es una decisión pendiente, no un fallo demostrado. |
| Reportes, moderación y administración | Reporte valida tipo, recurso y motivo 255; solo destinatario reporta mensaje. Moderación valida operación/motivo/días; administración valida rol/estado y evita autoedición. | Reportes pendientes se comprueban bajo serializable; resolución con rowversion y transacción. Eventos `ReportCreated/Changed`, `ModerationAction`, cambios de rol/estado y accesos administrativos. | Hay protección de concurrencia, pero el evento de decisión no identifica explícitamente operación anterior/nueva; mejora 5. |
| Errores y solicitudes rechazadas | Antiforgery global; `SocialWriteConvention`, `RequestLimits` y políticas; vistas de estado sin información interna. | `SafePersistenceExceptionFilter` responde 409/503; `PersistenceDiagnostics` limita diagnóstico; middleware correlaciona excepciones y HTTP. | Existen logs de rechazo y fallos. La reejecución de error pierde contexto de ruta original en el registro final; no demuestra por sí sola un enlace roto específico. |

No se encontró un contrato general separado de DTOs de API: las entradas relevantes usan ViewModels y algunos parámetros escalares; `SocialPage`, tarjetas y modelos de perfil son salidas MVC. No se considera obligatorio crear una API o duplicar estos tipos.

### Estado de los cuatro puntos observados anteriormente

| Punto | Evidencia actual | Conclusión |
|---|---|---|
| `SavedPendingTransaction` | `RequestSecurityLog.Persisted:16-22`, `HobbyHubContext.LogAudits:112-117`. Se contaron 12 coincidencias en los ocho JSONL disponibles; la última es del 22/10/2026 00:49:28 UTC. | Sigue en código y registros. Describe un guardado anterior al commit de una transacción externa; **no significa rollback ni pérdida de datos**. Falta el evento técnico posterior al commit/rollback. |
| `Result=Unconfirmed` | `TechnicalRequestMiddleware.InvokeAsync:25-31`; solo Login y Registro asignan `FunctionalResult` en los controladores actuales. 210 coincidencias; última 22/10/2026 00:49:41 UTC. | Sigue presente. En GET puede ser simplemente una lectura sin resultado funcional etiquetado; en escrituras impide distinguir validación, no operación y cambio confirmado. No equivale a éxito falso. |
| `Home.Estado` y 404 | `Program.cs` reejecuta códigos en `/Home/Estado/{0}`; `HomeController.Estado:141-160` conserva deliberadamente el código original. 40 registros `Action=Home.Estado` en el conjunto leído; última coincidencia 22/10/2026 00:49:38 UTC y con 404. | La ruta existe. Los registros repetidos son comprobables; no se ha identificado el recurso original ni demostrado un bucle. Puede tratarse de recursos o rutas originales inexistentes. Falta preservar una clasificación segura del origen. |
| Columnas de correlación | SELECT de `sys.columns`, modelo `AuditoriaSeguridad:11-13` y mapping `HobbyHubContext:455-457`. | Resueltas en desarrollo, pendientes en visual. Cinco registros históricos `InvalidColumns=...` terminan el 20/10/2026 22:17:48 UTC; no demuestran un fallo actual de desarrollo. |

Las cantidades anteriores describen únicamente archivos locales disponibles y pueden variar por retención. Los eventos SQL más recientes leídos en la base visual fueron `ModerationAccess`, del 08/10/2026; se ordenaron por `FechaUtc DESC, IdAuditoria DESC`. No se usó esa muestra como prueba de eventos inexistentes ni de cuál base produjo los logs posteriores.

## Las cinco mejoras

### 1. Reconciliar el esquema de auditoría de la base visual — P0 para revisión visual

1. **Estado comprobado:** la base visual tiene seis columnas y el modelo exige nueve; desarrollo sí tiene las nueve. `tools/Prepare-VisualTestDatabase.ps1:12-20` incluye actualmente 010, pero prepara una base nueva y no actualiza la existente. `Database/Prepare-IsolatedDevelopment.ps1:26-28` omite 010 en su orden actual. El script 010 está preparado y exige el nombre exacto de desarrollo.
2. **Problema:** el código actual genera consultas/inserciones sobre columnas inexistentes al usar la base visual. Afecta operaciones que escriben auditoría y lecturas de auditoría, no significa que todas las tablas o pantallas estén corruptas. Es un bloqueo de compatibilidad comprobado por metadatos; no se provocó un error web nuevo.
3. **Ejemplo:** iniciar sesión en el entorno 5088 con el código actual requiere guardar `Login`; la operación puede fallar al persistir auditoría aunque la cuenta y contraseña sean válidas. Un registro también incorpora `AccountCreated`.
4. **Evidencia:** `Models/AuditoriaSeguridad.cs:11-13`; `Models/HobbyHubContext.cs:88-109,450-460`; `Security/EfAccountSecurityStore.cs:42-57`; `Database/Scripts/010_AuditCorrelation.sql:1-27`; tabla `dbo.AuditoriasSeguridad` de ambos destinos.
5. **Información faltante:** `CorrelacionId varchar(64)`, `TipoRecurso varchar(64)`, `IdRecurso varchar(128)` en visual. No faltan las seis columnas originales ni se requiere inventar datos históricos.
6. **Solución propuesta:** preparar una actualización acotada y revisable para la base visual existente, con comprobación explícita del destino y columnas nullable; conservar filas. Reutilizar el propósito de 010, sin ejecutar a ciegas su guarda de desarrollo sobre visual. Incorporar 010 al aprovisionamiento ordinario y una comprobación de metadatos de compatibilidad previa a la revisión. Actualizar `Database/README.md` para distinguir estados reales de ambos destinos. No repetir los aprovisionamientos ni recrear bases.
7. **Base de datos:** sí, solo donde falten columnas, con autorización posterior. Ningún cambio en desarrollo por esta ausencia ya resuelta. El chequeo debe ser de solo lectura y no migrar automáticamente al iniciar el servidor.
8. **Aceptación/prueba:** ambos destinos autorizados muestran nueve columnas con tipos/longitudes correctos; un destino no autorizado se rechaza sin escrituras. En base exclusiva de pruebas, registro y login dejan tanto entidad/sesión como auditoría correlacionada después del commit. Fallo inducido revierte ambos. Adaptar `Verification/AuthSchemaTests.ConfiguredSqlRegistrationAndUnknownLoginDiagnoseSeparately:28-78`: actualmente **espera error SQL 207**, realiza escrituras aunque revierta y no sirve como prueba de salud después de corregir. Incluir explícitamente el proyecto Verification, que no está en la solución. Actualizar también `Proyecto_HobbyHub.Tests/SchemaSafetyTests.cs:34-84`: las listas de scripts omiten 010 y la comprobación de todas las columnas del modelo no lo concatena; por inspección, no puede encontrar las tres columnas nuevas en ese conjunto. No se ejecutó para atribuirle un resultado de runner. En `Proyecto_HobbyHub.Tests/SqlApplicationFactory.cs:20-25`, eliminar dentro de esta mejora la selección implícita de desarrollo cuando falta `HOBBYHUB_VISUAL_TEST_DATABASE`: el código actual sí tiene ese fallback aunque el README exige ambas variables. La prueba debe rechazar SQL habilitado sin un destino exclusivo explícito.
9. **Capturas antes:** E01 y E02, resultados de metadatos en ambos destinos y mapping. No hace falta forzar un fallo de login.
10. **Capturas después:** E03, nueve columnas y pruebas de persistencia/rollback en base de prueba, pendientes.

### 2. Hacer coincidir la validación de descripción de comunidades — P2

1. **Estado comprobado:** `CommunitySettings.Description` permite 2000; SQL y EF permiten 500. El formulario de administración usa ese ViewModel. El formulario ordinario de creación/edición ya limita a 500.
2. **Problema:** una entrada de 501 a 2000 caracteres supera la validación del servidor de administración y llega a un guardado que SQL no puede aceptar íntegramente. No se comprobó truncamiento de una fila ni una respuesta falsa de éxito; el filtro global trata fallos de persistencia como 503.
3. **Ejemplo:** el propietario escribe una descripción de 600 caracteres en Administrar, pulsa Guardar y recibe una indisponibilidad en vez de una indicación de longitud en el campo. Afecta esa edición; no impide descripciones de 500 o menos.
4. **Evidencia:** `ViewModels/SocialHubViewModels.cs:61-69`, propiedad Description; `ViewModels/ComunidadFormViewModel.cs:14-16`; `Controllers/ComunidadesController.Management.cs:25-33`, método Administrar POST; `Views/Comunidades/Administrar.cshtml:4`; `Models/HobbyHubContext.cs:225-227`; `dbo.Comunidades.Descripcion varchar(500)` comprobado.
5. **Información insuficientemente validada:** longitud de Description; el GET no presenta un límite coherente con el almacenamiento y el POST usa un 400 genérico ante ModelState inválido.
6. **Solución propuesta:** mantener el límite ya establecido de 500 en `CommunitySettings`, expresar el máximo en el textarea y devolver la vista con error de campo, preservando texto y reconstruyendo miembros/candidatos. Aplicar en los tres archivos anteriores de ViewModel, controlador Management y vista Administrar. No ampliar SQL a 2000 sin una necesidad de negocio documentada.
7. **Base de datos:** no. Se mantiene la capacidad existente. Si se aprueba la mejora 3, Unicode cambiará representación, no el máximo funcional acordado.
8. **Aceptación/prueba:** 500 caracteres se guardan y releen idénticos; 501 y 2000 se rechazan antes de `SaveChanges`, incluso sin JavaScript; el texto no se pierde, no hay mensaje de éxito ni `CommunityChanged` para el rechazo. Otro usuario no puede administrar ese recurso. Añadir casos límite a pruebas MVC y SQL exclusivas de prueba.
9. **Capturas antes:** E04: anotaciones, controlador y definición SQL. Reproducción con escritura no realizada.
10. **Capturas después:** E05: validación visible con 501 caracteres y relectura de 500, pendientes.

### 3. Preservar texto Unicode en contenido y perfiles — P1 de integridad

1. **Estado comprobado:** numerosos campos de texto usan `IsUnicode(false)` y las columnas de contenido examinadas son `varchar` bajo una collation no UTF-8. Un SELECT constante, sin tocar filas, devolvió entrada `漢` y vuelta por `varchar` igual a `?`.
2. **Problema:** la interfaz y validación por longitud no restringen esos caracteres, pero la representación SQL no puede conservarlos. Afecta ciertos alfabetos y símbolos; no significa que el texto ASCII o todos los acentos españoles se pierdan. La demostración acredita pérdida en la conversión SQL, no una publicación real dañada.
3. **Ejemplo:** una comunidad sobre idiomas comparte `漢` en un comentario; el contenido puede releerse como `?`. La misma representación limita mensajes privados y nombres, aunque la operación aparente ser válida.
4. **Evidencia:** `Models/HobbyHubContext.cs:197-199,225-239,272-274,330-332,388-390`; `Models/HobbyHubContext.Social.cs:26-28,32,53`; entradas `ComposePost.Content` y `ProfileSettings` en `ViewModels/SocialHubViewModels.cs:48-59`; columnas reales antes descritas. SELECT de conversión en E06.
5. **Información afectada:** caracteres fuera de la página de códigos de los textos ingresados. No afecta al hash hexadecimal de correo o token, nombres internos de eventos ni identificadores técnicos ASCII.
6. **Solución propuesta:** inventariar los campos de texto humano del contexto y scripts, migrarlos de forma coherente a `nvarchar` y mapping Unicode, conservando máximos funcionales. Incluir contenido, nombres, descripciones/reglas, biografía/intereses y motivo/resolución; revisar los índices únicos de nombres en esa misma revisión. Mantener hashes y códigos técnicos como ASCII. Validar longitud conforme a la representación elegida; para emojis con pares sustitutos documentar qué cuenta el límite. No intentar reconstruir caracteres ya perdidos sin una fuente verificable.
7. **Base de datos:** sí, ALTER controlado posterior de columnas seleccionadas y revisión de dependencias/índices; no un cambio de collation global ni rediseño completo. Evaluar espacio y restricciones antes de aplicar.
8. **Aceptación/prueba:** en base exclusiva de pruebas, guardar y releer mediante EF y MVC textos con español, `漢` y emojis; comparación exacta, búsqueda y edición; límites y nombres duplicados siguen funcionando. No se aceptan pruebas solo contra un proveedor en memoria. Logs/auditoría no deben contener esos cuerpos ni mensajes privados. Actualizar el reconocimiento de tipos en `SchemaSafetyTests.ReviewedScriptsCoverEveryCurrentEntityTableAndColumn`: actualmente su expresión enumera VARCHAR pero no NVARCHAR. Pendiente comprobar impacto de cada columna dependiente.
9. **Capturas antes:** E06 y E07: SELECT sintético y mapping/tipos reales; sin mostrar textos de usuarios.
10. **Capturas después:** E08: roundtrip exacto en datos sintéticos y resultado de pruebas, pendientes.

### 4. Aplicar una política coherente a la visibilidad de comentarios — P1 funcional y de privacidad

1. **Estado comprobado:** `SocialController.Publicacion` exige comentario Visible y autor Activo, pero `PerfilController.Index` y `HomeController.ObtenerPublicacionesAsync` solo exigen comentario Visible. `SocialController.Cards` también cuenta comentarios sin filtrar el estado del autor. La consulta de publicaciones sí usa `SocialAccess.VisiblePosts`.
2. **Problema:** se puede ocultar un comentario por suspensión del autor en el detalle social y conservar su texto/nombre en vistas antiguas o de perfil sobre una publicación de otro autor activo. Los contadores también pueden anunciar comentarios no mostrados. Esto no demuestra que se acceda a publicaciones privadas completas ni que haya fuga de mensajes.
3. **Ejemplo:** B comenta una publicación de A; B queda suspendido. El detalle de la publicación excluye el comentario, mientras el perfil de A puede seguir proyectándolo entre sus primeros cinco comentarios. Se deduce de los filtros; no se generaron cuentas ni suspensión para reproducirlo.
4. **Evidencia:** `Controllers/SocialController.cs:24,56-61`; `Controllers/PerfilController.cs:62-73`; `Controllers/HomeController.cs:194-210`, método ObtenerPublicacionesAsync usado por InicioClasico; `Security/SocialAccess.cs:8-18`. Tablas `Comentarios` (`Estado`, `IdUsuario`, `IdPublicacion`) y `Usuarios` (`Estado`).
5. **Información insuficientemente utilizada:** estado del autor al proyectar y contar comentarios en rutas alternativas. `BloqueosUsuario` se usa para publicaciones/personas, pero no se aplica de forma uniforme a comentarios; la regla concreta sobre comentarios de bloqueados o perfiles privados no está documentada y debe decidirse, no suponerse.
6. **Solución propuesta:** definir una consulta compartida de comentarios visibles en `SocialAccess` y usarla tanto para texto como para conteos, incluidos perfil y ruta clásica. Como mínimo conservar coherentemente la regla ya existente de autor activo y comentario visible. Documentar la política de comentarios de bloqueados/privados y respuestas cuyo padre se oculta; implementarla solo una vez acordada, sin deducir automáticamente que todo comentario público debe heredar privacidad de perfil.
7. **Base de datos:** no para la coherencia del estado del autor. Usar relaciones existentes; no eliminar comentarios históricos por suspensión.
8. **Aceptación/prueba:** con publicación de A y comentario de B, comparar Social/Publicacion, Perfil y Home/InicioClasico antes/después de suspender B en una base exclusiva de prueba: mismo conjunto visible y conteos compatibles; al reactivar aplicar la política acordada. Probar comentarios retirados y padres ocultos. Añadir regresión a `SqlSocialIntegrationTests`, distinta de la ya existente `PrivateProfilePostsCannotLeakThroughOldOrNewViews:140`, que cubre publicaciones privadas y no garantiza este caso.
9. **Capturas antes:** E09: filtros de las tres rutas y conteos; prueba visual pendiente de un entorno autorizado para escritura.
10. **Capturas después:** E10: comparación de pantallas con cuentas sintéticas y prueba automatizada, pendientes. Sin nombres reales ni texto sensible.

### 5. Completar resultados, decisiones y correlación sobre la auditoría existente — P1

1. **Estado comprobado:** existen `RequestSecurityLog`, middleware técnico, filtro de POST, auditoría automática en `HobbyHubContext`, servicios de cuenta y tabla SQL. No se propone otra implementación paralela. Las confirmaciones de transacciones externas no llegan al log; las decisiones de moderación y membresía suelen quedar como cambios genéricos; ciertas operaciones no entran en el switch de auditoría. Los rechazos y logs comparten presupuestos limitados.
2. **Problema:** no siempre puede reconstruirse quién decidió qué sobre cuál recurso, ni distinguir acción retornada, validación rechazada y commit. `Exito=false` se usa tanto para intentos como para denegaciones, pero `_AuditTable` lo muestra siempre como “Denegado”. Una captura HTTP 302 o `SavedPendingTransaction` no acredita persistencia final.
3. **Ejemplo:** al resolver un reporte queda `ModerationAction` y `ReportChanged`, pero no una operación semántica inequívoca (`revision`, `resolver`, `ocultar` o `suspender`) con transición de estado. Al bloquear a otra persona se escribe `IdObjetivo=Me`; si el ID llegó en formulario, no puede confiarse en inferir el usuario bloqueado desde route values.
4. **Evidencia:** `Models/HobbyHubContext.cs:46-71,88-155,167-188`; `Security/RequestSecurityLog.cs:16-52`; `Security/WriteRequestAuditFilter.cs:8-23`; `Security/TechnicalRequestMiddleware.cs:8-31`; `Security/AuditEventBudget.cs:5-20`; `Controllers/ModeracionController.cs:30-50`; `Controllers/ComunidadesController.Management.cs:35-50`; `Controllers/SocialController.cs:286-298`; `Views/Shared/_AuditTable.cshtml:2`. La matriz siguiente detalla puntos de integración y pruebas.
5. **Información faltante:** commit/rollback externo, resultado funcional tipado, operación concreta y transiciones no sensibles, recursos explícitos en eventos manuales y resultado de entrega local. No faltan universalmente fecha UTC, actor, acción o éxito: muchos eventos ya los tienen.
6. **Solución propuesta:** extender el mecanismo actual con eventos y campos de la matriz. Los servicios que poseen transacciones deben anunciar commit solo después de `CommitAsync`; rollback/fallo al salir sin confirmación. La auditoría persistente del cambio debe seguir en la misma transacción. Asignar `FunctionalResult` según validación/negocio y resultado real. Capturar en middleware la clasificación original segura antes de reejecutar la página de error. Corregir recursos explícitos y presentación de intentos. Separar presupuesto de eventos críticos del tráfico informativo y emitir agregados de supresión. Evitar duplicar evento automático y manual para la misma decisión.
7. **Base de datos:** primero depende de mejora 1. Los campos actuales bastan para muchos eventos si se eligen nombres específicos y recurso explícito. Si se requieren transiciones consultables, añadir campos acotados como `Resultado`, `EstadoAnterior`, `EstadoNuevo` y `Operacion`, con catálogo permitido; nunca un JSON libre del formulario. Es una extensión propuesta, no columnas que se afirme que ya existen. No inventar valores históricos de transiciones.
8. **Aceptación/prueba:** matriz de commit, rollback, validación, concurrencia, acceso rechazado y entrega fallida. En cambio confirmado se observan fila de dominio y auditoría por correlación tras abrir una nueva conexión; rollback no deja ninguna, aunque sí un fallo técnico. Intentos no se rotulan como denegaciones por defecto. Saturar lecturas no suprime todos los errores críticos; los conteos agregados explican muestreo. Pruebas con marcadores sensibles deben acreditar que tokens, contraseñas, cookies, correos y cuerpos privados no aparecen en archivos ni consola.
9. **Capturas antes:** E11-E13: código, resúmenes de JSONL y auditoría SQL mínima; registros actuales o históricos fechados, sin provocar escrituras.
10. **Capturas después:** E14-E15: correlación, resultado final, decisión explícita y regresiones, pendientes.

## Matriz de logs existentes y faltantes

### Cobertura actual

“SQL” indica implementación de persistencia; su funcionamiento con el modelo actual en visual depende de mejora 1. “Técnico” usa consola y archivos JSONL con rotación. El presupuesto puede omitir emisiones; no se afirma cobertura exhaustiva de cada solicitud.

| Acción existente | Archivo / clase / método | Eventos o destino comprobados | Hueco acotado |
|---|---|---|---|
| Registro | `AuthController.Registro`; `HobbyHubContext.AddContentAudit` | `AccountCreated`, SQL transaccional y espejo técnico | Alta anónima con actor null es válida; completar recurso generado ya se implementa. Destino visual incompatible. |
| Acceso/cierre/revocaciones | `AccountSecurityService.LoginAsync`; `EfAccountSecurityStore.CreateSessionAsync/RevokeSessionAsync/RevokeAllAsync` | `LoginDenied`, `Login`, `Logout`, `SessionsRevoked`; SQL y técnico | No falta login/logout exitoso. Faltan ciertos rechazos por elegibilidad concurrente y confirmación técnica del commit. |
| Recuperación y contraseña | `AccountRecoveryService`; `EfAccountRecoveryStore.IssueAsync/ResetAsync/ChangeAsync` | `RecoveryRequested` no elegible, `RecoveryIssued`, `PasswordResetDenied`, `PasswordChangeDenied`, `PasswordReset`, `PasswordChanged`, `SessionsRevoked` | Emitir token no demuestra entrega. El fallo de envío acaba en advertencia genérica de CuentaController sin correlación explícita en el texto. |
| Publicaciones/comentarios/reportes | `HobbyHubContext.AddContentAudit` | `PublicationCreated/Changed/Deleted`, `CommentCreated/Changed/Deleted`, `ReportCreated/Changed/Deleted`, SQL | Retirar contenido normalmente cambia Estado: se registra Changed. Falta semántica específica si se necesita distinguir edición/retiro. No duplicar altas ya cubiertas. |
| Comunidad/membresía/perfil | mismo método; `RotateSecurityVersions` | `Community*`, `Membership*`, `ProfileChanged`, `ProfileImage*`, `AccountRoleChanged`, `AccountStateChanged`, `AccountPasswordChanged` | No incluye valores anterior/nuevo ni decisión concreta. No registrar hash de contraseña para explicar un cambio. |
| Mensajes/bloqueos/sesión individual | `SocialController.Enviar/Bloquear/CerrarSesion` y contexto | `MessageCreated/Changed`, `UserBlocked/UserUnblocked`, `SessionRevoked` | Recurso/objetivo explícito de bloqueo insuficiente; lectura con ExecuteUpdate no pasa por switch. |
| Moderación/administración | `ModeracionController.Resolver`; `AdministracionController.Comunidad`; `ManagementAuditFilter.OnActionExecutionAsync` | `ModerationAction`, `AdminCommunityChanged`, `AdministrationAttempt/Access`, `ModerationAttempt/Access` | Intento también en GET; `Exito=false` no siempre es denegación. Falta operación y recurso explícito; no afirmar que no hay auditoría. |
| Denegación de permisos y límite | middleware de `Program.cs`; `RequestLimits.Configure` callback OnRejected; `RequestSecurityLog.Rejected` | `AuthorizationDenied`, `RateLimitRejected`, técnico; resumen `SecurityRejectionsSuppressed` | Rechazo individual sale a Information por Write; 10/min más presupuesto de eventos. Desafío anónimo 302 a Login no está identificado como autorización denegada. 404 por ocultación no distingue causa. |
| Fallos/HTTP | `TechnicalRequestMiddleware.InvokeAsync`, `SafePersistenceExceptionFilter.OnException`, `PersistenceDiagnostics.Log` | `ServerFailure`, `PersistenceFailure`, `RequestCompleted`, consola/JSONL | Clasificación original puede perderse al reejecutar Estado; cuerpo genérico seguro. El proveedor ignora scopes: cada evento crítico necesita correlación explícita. |

### Integraciones faltantes o insuficientes dentro de la mejora 5

Convención propuesta común: fecha UTC de `TimeProvider`, `CorrelacionId` generado por servidor, actor solo después de validar autenticación; si es anónimo o cookie inválida, actor null. Recurso y objetivo se obtienen de la entidad cargada/autorizada, no se presentan IDs recibidos como identidades confiables. Resultado es un código de catálogo. IDs de cuentas son seudónimos internos y deben ocultarse al compartir capturas.

| Integración exacta | Momento y evento propuesto | Campos y origen adicional | Destino y justificación | Severidad / repetición | Prueba que lo demuestra |
|---|---|---|---|---|---|
| `HobbyHubContext.LogAudits`; propietarios de transacción `EfAccountSecurityStore.CreateSessionAsync/RevokeSessionAsync/RevokeAllAsync/EndSuspension`, `EfAccountRecoveryStore.IssueAsync/ResetAsync/ChangeAsync`, `SocialController.Nueva/Reportar/ReportarMensaje/Bloquear`, `ModeracionController.Resolver`, `AdministracionController.Cuenta` y reportes/eliminación de Comunidades | Después de `CommitAsync`: `TransactionCommitted`; fallo o salida sin commit: `TransactionRolledBack`/`TransactionFailed`. No emitir commit desde SaveChanges con transacción externa. | IDs de auditorías guardadas, tipo/ID de recurso, acción y correlación de esa unidad de trabajo; tipo de excepción/número SQL permitido, sin mensaje bruto. | Técnico; las filas de auditoría ya deben compartir la transacción SQL. No insertar una falsa confirmación fuera de esa transacción. | Information commit, Warning rollback esperado/conflicto, Error fallo inesperado; una terminal por transacción, presupuesto crítico separado. | Forzar fallo después de SaveChanges y antes de commit en base exclusiva: cero filas nuevas de dominio/auditoría al releer; log de rollback y ningún commit. |
| `WriteRequestAuditFilter.OnActionExecutionAsync`, ramas de validación/permiso de controladores y `TechnicalRequestMiddleware.InvokeAsync` | `WriteRejected` al rechazar datos/negocio; `WriteCommitted` solo al confirmar; conservar `Attempt/ActionReturned` como etapas. | Resultado permitido (`ValidationRejected`, `Conflict`, `Denied`, `Committed`, `Unavailable`, `NoChange`), controlador/acción estáticos, recurso validado; HTTP al finalizar. | Técnico para diagnóstico de todas las escrituras; SQL únicamente decisiones de seguridad o cambios duraderos, evitando una fila por cada error de formulario. | Information validación/no cambio; Warning rechazo/conflicto; Error infraestructura inesperada. Muestrear validación repetida y agregar conteos por categoría acotada. | Mismo 200 en formulario inválido y vista válida debe tener resultados distintos; 302 sin cambio nunca se etiqueta commit. |
| `TechnicalRequestMiddleware.InvokeAsync`; reejecución configurada en `Program.cs`; `HomeController.Estado` | Al terminar error HTTP: `RequestRejected`/`RequestNotFound`, preservando endpoint original y endpoint de presentación por separado. | Nombre de endpoint/controlador/acción original si existía, verbo permitido, código, duración y correlación; para rutas desconocidas etiqueta `Unmatched`, sin URL/query sin filtrar. | Técnico; no es una modificación de negocio que justifique SQL por cada 404. | Warning agregado para repetidos; presupuesto separado de errores críticos, sin agrupar por entradas arbitrarias del atacante. | Ruta inexistente conserva 404 y origen Unmatched; un POST rechazado conserva su acción original. Un 404 no provoca una secuencia infinita ni se atribuye a fallo de Estado. |
| `AccountRecoveryService.RequestAsync:12-26`; `LocalRecoveryMailbox.SendAsync:24`; `CuentaController.Recuperar` catch | Tras retorno satisfactorio de SendAsync: `RecoveryDeliverySucceeded`; catch: `RecoveryDeliveryFailed`; `IssueAsync=false`: `RecoveryIssueRejected`. | Resultado del proveedor `LocalMailbox`, ID de cuenta elegible derivado de snapshot, correlación; código de causa permitido. Nunca token, destinatario, ruta de buzón o archivo exportado. | Ambos para resultado de entrega relevante de seguridad; log técnico si SQL está caído. La emisión y entrega son etapas distintas, sin prometer recepción en correo real. | Information éxito, Warning rechazo/fallo esperado, Error fallo de infraestructura; limitar intentos/denegaciones y agregar conteos; no suprimir confirmaciones de cambios críticos. | Remitente falso que falla: RecoveryIssued puede existir, no hay DeliverySucceeded; respuesta externa genérica idéntica para no enumerar cuentas. |
| `RevalidatingCookieEvents.ValidatePrincipal:11-29`; `AccountSecurityService.LoginAsync` cuando CreateSessionAsync devuelve false; `EfAccountSecurityStore.RecordFailureAsync:29-39` | `SessionValidationDenied` al invalidar cookie; `LoginEligibilityRejected` en carrera; `AccountLockoutStarted` solo cuando realmente se cruza umbral de bloqueo. | Causa enum producida por validación del servidor; usuario confirmado en snapshot si lo hay, no claims sin validar; inicio/fin del bloqueo desde reloj/estado confirmado. Nunca valor de cookie o ID de sesión reutilizable. | Técnico para cookie inválida repetida; SQL + técnico para transición de bloqueo y rechazo significativo de login. Auditar bloqueo atómicamente con actualización. | Warning; una transición por bloqueo; denegaciones con presupuesto y resumen, sin crear auditoría por cada recurso estático. | Cookie revocada se rechaza y deja evento sin token; cinco fallos generan una transición, el sexto no duplica bloqueo; carrera de sesión produce rechazo identificable. |
| `ModeracionController.Resolver:30-50`; `ComunidadesController.Miembro:35-50`; `AdministracionController.Cuenta/Comunidad`; `HobbyHubContext.RotateSecurityVersions` | Tras confirmar la transacción, espejo técnico de decisión; SQL en la misma transacción del cambio: `ReportReviewStarted`, `ReportResolved`, `ReportedContentHidden`, `AccountSuspended`, `MembershipInvited/Accepted/Blocked/Unblocked`, eventos existentes de rol/estado enriquecidos. | Reporte/comunidad/usuario explícitos desde entidad; operación validada, estado anterior/nuevo, rol anterior/nuevo y vencimiento si aplica. Motivo completo permanece en Reportes, no se copia al log. | Ambos: decisión persistente y diagnóstico correlacionado. Reutilizar/enriquecer auditoría existente en lugar de multiplicar Changed y otra decisión indistinguible. | Information cambio confirmado; Warning rechazo; una decisión por cambio real, sin muestrear auditoría de permisos; intento repetido/conflicto agregado. | Dos moderadores resuelven simultáneamente: una decisión confirmada, el otro conflicto; SQL conserva quién, reporte, operación y transición correcta. |
| `SocialController.Bloquear:286-298`; `SocialController.Seguir/Solicitud:178-198`; `HobbyHubContext.AddContentAudit` | `UserBlocked/Unblocked` existentes con objetivo correcto; `FollowRequested/Accepted/Removed/Rejected` en transacción del cambio y espejo después del commit. | Actor Me autenticado; otro usuario desde entidad; clave compuesta o tipo de recurso claro, estado anterior/nuevo. No usar `IdObjetivo=Me` como sustituto del bloqueado. | Ambos para cambios en relaciones que conceden/revocan acceso a perfil privado. No es necesario auditar todo contador social. | Information; evento solo al cambiar relación, duplicado/rechazo Warning acotado; persistencia sin muestreo de cambios de acceso. | Aceptar seguimiento privado permite acceso y deja evento con ambos IDs sintéticos; quitar/bloquear revoca y queda trazabilidad sin cuerpo privado. |
| `SocialController.RetirarImagen:126-130`; `PublicacionImagen` no está en switch de `AddContentAudit` | `PublicationImageRemoved` dentro de guardado SQL; después del commit registro técnico. Fallo al borrar archivo: `ImageFileRemovalFailed`. | GUID de imagen como recurso, publicación autorizada y actor; no ruta física/URL sensible. | Ambos para retiro de contenido; fallo de limpieza solo técnico. Un borrado de archivo no prueba borrado SQL y viceversa. | Information retiro, Warning limpieza fallida; un evento por imagen y resumen para reintentos. | Retiro ajeno se rechaza; propio elimina referencia y crea auditoría atómica; simular fallo de archivo mantiene diagnóstico sin afirmar limpieza completa. |
| Middleware de autorización `Program.cs`, `RequestLimits.Configure.OnRejected`, filtros de antiforgery/resultados, `RequestSecurityLog.Rejected` | Mantener `AuthorizationDenied`, `RateLimitRejected`; añadir `AuthenticationChallenge` para desafío anónimo, `AntiforgeryRejected` y rechazo de recurso explícito en ramas conocidas (sin revelar existencia al cliente). | Política estática, resultado, actor validado o null, endpoint clasificado, contador suprimido y correlación. Identificar recurso solo si se cargó de forma autorizada. | Técnico; SQL solo en decisiones administrativas especialmente sensibles y acotadas, no por cada 429/400. Los eventos ya existentes se enriquecen, no se duplican. | Warning para rechazos; desafíos normales Information; agregado periódico incluso si cesa el tráfico, sin cubetas por texto libre; reservar Error para fallo del servicio. | CSRF inválido nunca ejecuta acción y deja rechazo; 429 incluye Retry-After real; saturación conserva conteo agregado y un error crítico posterior. |

No se encontró auditoría de dominio específica para likes/guardados ni para marcar mensajes/notificaciones leídos. Existen filtros técnicos de POST, pero no confirman esos cambios. **Decisión de alcance:** likes/guardados y lecturas ordinarias pueden permanecer solo en log técnico con resultado correcto; no hay requisito documentado que obligue a persistir cada lectura en auditoría de seguridad. Si se exige trazabilidad de lectura, el punto sería `SocialController.LeerMensajes` inmediatamente después de `ExecuteUpdateAsync`, con número afectado y conversación propia, sin texto ni IDs de sesión. Esto forma parte del alcance de la mejora 5, no una sexta propuesta.

No registrar contraseñas ni hashes de contraseñas, tokens ni sus valores de recuperación, cookies, cadenas de conexión, correos, teléfonos, direcciones, mensajes privados ni cuerpos completos de solicitudes. IP, User-Agent y características aproximadas de dispositivo no prueban identidad; IP compartida o cambiante tampoco distingue usuarios. El diseño actual no guarda dispositivo en auditoría y eso no es por sí solo una carencia. Si se adopta una clasificación aproximada de dispositivo, debe ser mínima, de catálogo, con retención definida y nunca usarse como sustituto del actor autenticado. No se necesitan huellas digitales del navegador para las cinco mejoras.

## Plan de evidencias

No se generaron capturas en esta revisión. “Disponible” indica que el dato o archivo fue leído o la prueba se ejecutó, y puede capturarse sin realizar escrituras. **Toda evidencia posterior a implementación está pendiente**, al igual que cualquier reproducción que requiera crear usuarios, enviar formularios o alterar estados. Usar cuentas sintéticas; recortar nombres/IDs de personas reales, credenciales, tokens, contenido privado, configuración local completa y pestañas del navegador con datos personales.

Los nombres de SQL usados abajo fueron comprobados en metadatos. Abrir explícitamente el destino en SSMS antes de ejecutar consultas; no pegar cadenas de conexión en la entrega. Consultas Q1-Q5 son exclusivamente SELECT.

| Evidencia | Mejora | Dónde abrir | Pasos exactos | Resultado esperado / estado | Qué demuestra y qué no |
|---|---|---|---|---|---|
| E01. Esquemas distintos | 1 | SSMS, ambas bases y Q1 | Seleccionar visual, ejecutar Q1; repetir en desarrollo; capturar nombre de base y columnas. | Disponible: 6 frente a 9 columnas. | Comprueba el desfase real; no identifica por sí solo la base de un proceso web. |
| E02. Código y destino de revisión | 1 | `Models/AuditoriaSeguridad.cs`, `HobbyHubContext.cs:450`, `tools/Start-VisualReview.ps1:5`, `tools/Prepare-VisualTestDatabase.ps1:12` | Abrir archivos con números de línea; mostrar mapping y variable de destino omitiendo el valor de conexión. | Disponible: modelo exige tres columnas; script selecciona base visual. | Prueba dependencia y selección prevista; no demuestra un login ejecutado ahora. |
| E03. Compatibilidad y atomicidad después | 1 | SQL Q1/Q4 y terminal de prueba específica | Tras autorización e implementación, usar base exclusiva de prueba, correr registro/login y caso de rollback; abrir otra conexión y ejecutar consulta filtrada por correlación sintética. | **Pendiente:** nueve columnas; entidad y auditoría confirmadas, rollback sin filas. | Demuestra persistencia real y atomicidad; una captura del mensaje de éxito sola no lo hace. |
| E04. Límite incompatible antes | 2 | ViewModel social:65, ComunidadForm:16, Management:30 y SQL Q2 | Capturar 2000 de Description junto con 500 de EF/SQL y campo de vista Administrar. | Disponible: discrepancia. | Demuestra contrato incompatible; no una escritura fallida actual. |
| E05. Límite correcto después | 2 | `/Comunidades/Administrar/{id}` y prueba MVC/SQL | En base exclusiva, propietario sintético ingresa 501 caracteres sin JS y envía; luego 500 y relee; capturar error del campo y resultado del test de relectura. | **Pendiente:** 501 rechazado sin cambio/auditoría de cambio; 500 conservado. | Prueba interfaz/servidor/persistencia, sin atribuirlo solo a validación cliente. |
| E06. Conversión Unicode antes | 3 | SSMS, Q3 en visual | Ejecutar SELECT constante; capturar entrada, vuelta e igualdad binaria. | Disponible: `漢` → `?`, igualdad 0. | Prueba incapacidad de conversión; no expone filas ni demuestra corrupción de una fila histórica. |
| E07. Tipos y mapping | 3 | SQL Q2 y `HobbyHubContext.cs` / parcial social | Capturar tipos de Contenido y `.IsUnicode(false)` de los mismos campos. | Disponible: varchar y mapping no Unicode. | Vincula riesgo al almacenamiento real; no prueba que todos los caracteres fallen. |
| E08. Texto íntegro después | 3 | Pantalla de publicación sintética, SSMS Q2 y terminal | En base exclusiva, prueba con español/漢/emoji; guardar, abrir contexto nuevo y comparar; mostrar solo marcador sintético público y resumen de pruebas. | **Pendiente:** nvarchar y texto exacto; búsquedas/edición conservan caracteres. | Verifica roundtrip; no recuperar texto perdido previamente. No capturar mensajes privados. |
| E09. Filtros distintos antes | 4 | SocialController:59, PerfilController:64, HomeController:198 y SocialController:24 | Mostrar filtros y conteos con números de línea; anotar autor activo en una ruta y ausencia en otras. | Disponible por código; reproducción visual **pendiente**. | Demuestra inconsistencia estática, no explotación observada. |
| E10. Coherencia después | 4 | `/Social/Publicacion/{id}`, `/Perfil`, `/Home/InicioClasico` y terminal | En base exclusiva, A publica, B comenta y se suspende B; comparar texto/conteos de A en las tres rutas y ejecutar regresión. | **Pendiente:** exclusión coherente y conteos correctos según política. | Demuestra el caso probado; no toda política de privacidad ni contenido de todos los usuarios. |
| E11. Logs anteriores existentes | 5 | Terminal, JSONL locales | Usar comando L1; capturar únicamente resumen y fechas. Para detalle abrir una línea concreta y ocultar identificadores. | Disponible: 12 Pending, 210 Unconfirmed, 40 Estado y 5 InvalidColumns en archivos leídos. | Demuestra presencia histórica fechada; no fallo actual de desarrollo, recurso 404 original ni commit. |
| E12. Semántica de auditoría antes | 5 | `RequestSecurityLog:16`, middleware:25, `_AuditTable:2`, `ModeracionController:48`, `SocialController:297` | Mostrar resultado pendiente, fallback Unconfirmed, etiqueta Denegado y eventos manuales. | Disponible: huecos de significado y objetivo. | Prueba lo que registra el código; no que falte toda auditoría. |
| E13. Auditoría persistida actual | 5 | SSMS visual, Q4 básico | Seleccionar visual; ejecutar variante básica de Q4, que no usa columnas ausentes; capturar fecha/acción/éxito/ID de auditoría. | Disponible: muestra reciente ordenada con ModerationAccess de 08/10. | Prueba filas persistidas de esa base; no cuenta de usuario ni vínculo con una solicitud posterior. |
| E14. Resultado y decisión después | 5 | Logs nuevos, `/Administracion/Auditoria`, Q4 completo | Tras implementar en base de prueba, ejecutar una decisión y un rollback; localizar correlación sintética en técnico y SQL; capturar operación/transición y ausencia de éxito para rollback. | **Pendiente:** resultado terminal inequívoco, identidad/recurso correctos. | Demuestra casos concretos de trazabilidad; no garantiza captura infinita ante caídas o saturación. |
| E15. Pruebas y compilación | 1–5 | Terminal en raíz, solución y proyecto Verification | Hoy: repetir solo N1. Después, ejecutar N2 con autorización para artefactos; luego pruebas SQL únicamente en base exclusiva revisada, con las nuevas regresiones; compilar Verification explícitamente. | N1 disponible: 9/9. Compilación, suite .NET actual y pruebas posteriores **pendientes**. | Compilar prueba coherencia del código; SQL de relectura demuestra persistencia. “Skipped” no significa aprobado. |

### Consultas reproducibles de solo lectura

**Q1. Destino y esquema de auditoría.** Ejecutar por separado en cada base; no requiere ver filas de cuentas.

```sql
SELECT DB_NAME() AS BaseEfectiva;
SELECT c.name AS Columna, ty.name AS Tipo, c.max_length AS LongitudBytes,
       c.is_nullable AS PermiteNull
FROM sys.columns AS c
JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID(N'dbo.AuditoriasSeguridad')
ORDER BY c.column_id;
```

**Q2. Capacidad real de texto.** `max_length` mide bytes: un futuro `nvarchar(500)` mostrará 1000, no capacidad funcional de 1000 caracteres.

```sql
SELECT t.name AS Tabla, c.name AS Columna, ty.name AS Tipo,
       c.max_length AS LongitudBytes, c.is_nullable AS PermiteNull
FROM sys.tables AS t
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
JOIN sys.columns AS c ON c.object_id = t.object_id
JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
WHERE s.name = N'dbo'
  AND ((t.name = N'Comunidades' AND c.name = N'Descripcion')
    OR (t.name IN (N'Publicaciones', N'Comentarios', N'MensajesPrivados')
        AND c.name = N'Contenido'))
ORDER BY t.name, c.column_id;
```

**Q3. Conversión constante, sin datos personales.** La primera conversión se ejecutó y devolvió `?`; la comparación binaria se incluye como comprobación adicional reproducible, no como resultado ejecutado en esta revisión.

```sql
SELECT DB_NAME() AS BaseEfectiva,
       DATABASEPROPERTYEX(DB_NAME(), 'Collation') AS Collation;
SELECT NCHAR(28450) AS EntradaSintetica,
       CONVERT(nvarchar(10), CONVERT(varchar(10), NCHAR(28450))) AS VueltaVarchar,
       CASE WHEN CONVERT(varbinary(20), NCHAR(28450)) =
                 CONVERT(varbinary(20), CONVERT(nvarchar(10),
                   CONVERT(varchar(10), NCHAR(28450))))
            THEN 1 ELSE 0 END AS Identico;
```

**Q4. Eventos recientes con orden explícito.** La variante básica funciona en la base visual actual y fue la forma de consultar filas durante la revisión:

```sql
SELECT TOP (12) IdAuditoria, FechaUtc, Accion, Exito
FROM dbo.AuditoriasSeguridad
ORDER BY FechaUtc DESC, IdAuditoria DESC;
```

La variante completa usa nombres comprobados en desarrollo; **no ejecutarla en visual hasta corregir mejora 1**. Para una prueba posterior sustituir el marcador por la correlación sintética recién generada, sin datos de usuario. No se ejecutó esta variante durante el diagnóstico.

```sql
SELECT TOP (50) IdAuditoria, FechaUtc, Accion, Exito,
       CorrelacionId, TipoRecurso, IdRecurso
FROM dbo.AuditoriasSeguridad
WHERE CorrelacionId = 'CORRELACION_SINTETICA_DE_LA_PRUEBA'
ORDER BY FechaUtc DESC, IdAuditoria DESC;
```

**Q5. Restricciones activas y confiables.** Metadatos equivalentes fueron consultados en visual.

```sql
SELECT name, is_disabled, is_not_trusted
FROM sys.foreign_keys
ORDER BY name;
SELECT name, definition
FROM sys.check_constraints
ORDER BY name;
```

### Terminal: lecturas actuales y verificaciones posteriores

**L1. Resumen seguro de los registros existentes**, sin imprimir cuerpos, actores ni credenciales:

```powershell
$entries = Get-ChildItem Proyecto_HobbyHub/.local/logs -Filter '*.jsonl' |
  ForEach-Object { Get-Content -LiteralPath $_.FullName | ConvertFrom-Json }
foreach ($pattern in @('SavedPendingTransaction','Result=Unconfirmed',
                       'Action=Home.Estado','InvalidColumns=')) {
  $hits = @($entries | Where-Object { $_.Message.Contains($pattern) })
  [pscustomobject]@{
    Patron = $pattern
    Cantidad = $hits.Count
    UltimoUtc = ($hits | Sort-Object TimestampUtc | Select-Object -Last 1).TimestampUtc
  }
}
```

**N1. Pruebas JavaScript ejecutadas en esta revisión**, sin crear archivos:

```powershell
node --test Proyecto_HobbyHub.Tests/auth.test.cjs Proyecto_HobbyHub.Tests/navigation.test.cjs
```

**N2. Compilación/pruebas .NET posteriores, pendientes y no ejecutadas.** Requieren autorización para generar artefactos y revisar escritura de logs; no son operaciones de solo lectura. Usar un directorio de revisión nuevo y detener únicamente el servidor que utilice sus binarios. No detener el servidor del usuario ni sobrescribir binarios en uso. El proyecto Verification no pertenece a la solución:

```powershell
dotnet build Proyecto_HobbyHub.sln --artifacts-path .local/analysis-build
dotnet build Proyecto_HobbyHub/Verification/LoggingAudit.Tests.csproj --artifacts-path .local/analysis-build
$env:HOBBYHUB_SQL_TESTS = '0'
$env:HOBBYHUB_SCHEMA_DIAGNOSTIC = '0'
dotnet test Proyecto_HobbyHub.sln --artifacts-path .local/analysis-build --no-build --no-restore
dotnet test Proyecto_HobbyHub/Verification/LoggingAudit.Tests.csproj --artifacts-path .local/analysis-build --no-build --no-restore --filter FullyQualifiedName~LoggingAuditTests
```

Antes de pruebas SQL posteriores se debe comprobar una base **exclusiva de pruebas**, compatible con el modelo y explícitamente seleccionada en `SqlApplicationFactory`. No se proporciona un comando que habilite escrituras en la base existente por defecto. `AuthSchemaTests` es un diagnóstico histórico con escrituras, aunque use rollback y espere 207; no ejecutarlo como si fuera un inventario SELECT. Tampoco usar `VisualTestSeed`, preparar cuentas de navegador o los scripts de aprovisionamiento como comprobaciones de solo lectura.

## Orden recomendado de implementación

1. **Mejora 1:** resolver compatibilidad de la base visual y demostrar destino/esquema antes de cualquier ensayo con escritura.
2. **Mejora 4:** unificar filtros de comentarios y acordar política de bloqueos/privacidad; comprobar varias rutas con las mismas cuentas sintéticas.
3. **Mejora 2:** ajustar el límite de descripción y el retorno de validación; cambio pequeño, localizado y sin esquema.
4. **Mejora 3:** preparar y revisar actualización Unicode con pruebas de relectura, índices y compatibilidad.
5. **Mejora 5:** completar trazabilidad reutilizando lo existente y validarla con todos los flujos anteriores. Puede diseñarse antes, pero su prueba SQL necesita compatibilidad del esquema.

Este orden prioriza bloqueo de revisión y coherencia de acceso, sin posponer indefinidamente la trazabilidad. No autoriza ejecutar cambios en esta etapa.

## Limitaciones y verificaciones pendientes

- No hubo prueba funcional nueva con cuentas, subida de imágenes, recuperación, moderación ni navegación web: generan datos o registros. Las reproducciones antes/después se describen como pendientes. Las comprobaciones estáticas no sustituyen esos ensayos.
- Se confirmó esquema de auditoría en ambas bases y metadatos adicionales en visual, no todo el esquema de desarrollo ni permisos mínimos de una cuenta de producción. La protección contra editar auditoría existe en `SaveChanges`, pero no demuestra inmutabilidad ante SQL directo con el propietario de LocalDB.
- No se inspeccionaron secretos de usuario ni el entorno de un proceso web; por tanto, no se certifica qué configuración efectiva utiliza un servidor activo. Una captura posterior debe mostrar únicamente servidor/base resueltos, nunca la cadena de conexión.
- No se auditaron vulnerabilidades actuales de dependencias ni infraestructura remota. Esta revisión no consultó fuentes externas: sus conclusiones son sobre código y evidencia local, no una certificación de versiones, seguridad pública o cumplimiento legal.
- La integridad referencial comprobada no demuestra todas las invariantes entre columnas. No se revisaron todas las filas ni contenidos privados en busca de corrupción histórica. Que no aparezca un evento en doce filas recientes no prueba ausencia de implementación.
- Decisiones de negocio pendientes: finalidad y formato del celular/dirección (se guardan hashes irreversibles y no permiten recuperar contacto), si conservarlos es necesario; tratamiento de comentarios de bloqueados/privados y padres ocultos; intereses libres frente a categorías del feed; historial de conversación después de bloqueo; retención y alcance obligatorio de auditoría de lecturas. Ninguna se convirtió en requisito inventado ni en una sexta propuesta.
- Las fechas de contenido mezclan `DateTime.Now`/`getdate()` con fechas de auditoría UTC. No se comprobó un error horario real ni se reinterpretaron fechas históricas; una captura debe etiquetar UTC o zona local correctamente.
- No se produjo evidencia nueva de compilación. Los resultados de informes anteriores se consideran históricos; no se trasladan como pruebas aprobadas hoy. Las pruebas disponibles sobre concurrencia, permisos, recuperación y filtros son una base útil para ampliar las cinco propuestas, no una garantía de que todo funcione en el esquema visual actual.

Único archivo creado por esta revisión: `docs/Analisis-cinco-mejoras.md`. No se implementaron las propuestas ni se modificaron código, base de datos o configuración.
