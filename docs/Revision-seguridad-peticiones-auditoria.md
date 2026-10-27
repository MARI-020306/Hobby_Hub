# Revisión de seguridad, peticiones y auditoría

Revisión sobre el árbol de trabajo recibido, con muchos cambios previos sin registrar. Se conservaron los cambios existentes. No se hicieron commits, push, despliegues, migraciones, aprovisionamiento ni escrituras en SQL Server. No se modificaron vistas, JavaScript, estilos, colores ni menús. No se encontraron archivos AGENTS.md dentro del repositorio en la búsqueda realizada; se revisaron README y las restricciones de aislamiento existentes.

## Alcance y mapa de datos

Aplicación MVC Razor `net8.0`, EF Core SQL Server, BCrypt y SkiaSharp; SDK instalado 10.0.401. No se actualizó ninguna dependencia sin poder completar su auditoría. El checkout conserva la restricción previa de ejecución en Development/Testing y la selección de SQL aislado.

| Módulos | Datos y controles examinados |
|---|---|
| Auth, Cuenta | ViewModels explícitos para registro, login, cambio y recuperación; hash de correo, BCrypt, tokens con hash, expiración, bloqueo y sesiones revocables. Roles de registro Usuario/Creador; privilegios limitados por servidor. |
| Home, Social | Feed, publicaciones, comentarios, likes, guardados, personas, seguidores, mensajes y notificaciones; filtros por usuario, privacidad, membresía y bloqueos. Las escrituras usan POST. |
| Comunidades y gestión | Propietario, miembros, invitaciones, solicitudes, publicaciones y reportes; comprobaciones de propiedad y permisos en servidor. |
| Perfil | Datos del propio usuario transformados a ViewModels; no se devuelven hashes personales. La consulta de publicaciones se proyecta ahora en SQL. |
| Moderacion, Administracion | Roles existentes, reportes, estados, suspensiones, reactivaciones y auditoría; conservación de un administrador activo, versiones de seguridad y rowversion de reportes. |
| Razor y JS | Se revisaron formularios, acciones, renderizado de datos y búsquedas de Html.Raw, innerHTML, localStorage, sessionStorage, serialización JSON y SQL directo fuera de bibliotecas. No se identificaron APIs que serialicen entidades EF completas. Tener entidades como modelo interno de Razor no las serializa automáticamente al navegador. |
| Imágenes | Rutas autorizadas, nombres generados, archivos fuera de wwwroot, decodificación y recodificación, dimensiones limitadas, seis imágenes y 5 MB por imagen. |

El navegador puede inspeccionar nombres, contenido visible e identificadores necesarios. Esos identificadores no son secretos ni sustituyen la autorización. No se renombraron propiedades, no se ofuscaron respuestas y no se añadió cifrado en JavaScript. Los hashes SHA-256 existentes de correo/teléfono/dirección no son cifrado y no resisten por sí solos la enumeración de datos de baja entropía; no se cambiaron porque requeriría una decisión de modelo y tratamiento de datos históricos. Las contraseñas conservan BCrypt con salt y límite de 72 bytes.

## Hallazgos y correcciones

| Importancia | Hallazgo | Cambio o límite pendiente |
|---|---|---|
| Alta | Registro sin limitador y lecturas sin límites; escrituras autenticadas agrupadas por IP. | Políticas configurables para registro, consultas, búsquedas, escrituras y recuperación; partición por usuario cuando corresponde. |
| Media | Retry-After constante de 60 segundos para recuperación limitada por 15 minutos. | Se obtiene MetadataName.RetryAfter del limitador y se redondea hacia arriba. Respuesta 429 en español y texto UTF-8. |
| Media | Acceso administrativo marcado como exitoso antes de ejecutar la acción. | Intento separado con Exito=false; AdministrationAccess/ModerationAccess se conserva después del retorno sin error. No significa que una mutación haya ocurrido ni que Razor haya terminado de renderizar. |
| Media | Cobertura incompleta de creación/edición/retiro de contenido y reportes. | Auditoría en el mismo SaveChanges y la misma transacción de los cambios. Una transacción revertida tampoco conserva sus nuevos registros de auditoría. |
| Media | Comprobación de reporte pendiente separada del guardado. | En los tres flujos de reportes, comprobación y escritura dentro de transacción Serializable para impedir inserciones fantasma entre solicitudes concurrentes. Un deadlock puede exigir reintento; no hay reintentos automáticos de POST. Falta verificar concurrencia con SQL real. |
| Media | Perfil cargaba todas sus publicaciones, likes y comentarios antes de limitar en memoria. | Proyección a ViewModels: diez publicaciones, cinco comentarios por publicación y contadores calculados por SQL; no se cambia la cantidad mostrada. Traducción y rendimiento con SQL real pendientes. |
| Media | Protección CSRF dependía de recordar atributos en cada futura acción. | AutoValidateAntiforgeryToken global, conservando los atributos actuales. Solo las páginas sin mutaciones Estado/Error permiten la reejecución de errores con el método original. |
| Media | Rechazos y operaciones sin correlación estructurada suficiente. | X-Correlation-ID generado por servidor; logs estructurados con hora UTC, acción, resultado, actor y metadatos limitados. Errores de persistencia registran tipo y número SQL, sin parámetros ni mensajes SQL. |
| Media | Peticiones canceladas podían continuar algunas consultas/guardados. | RequestAborted propagado a operaciones sin argumentos de cancelación en controladores. No todas las consultas con predicados ni servicios heredados reciben todavía cancelación. |
| Baja | Tamaño total no declarado globalmente. | Techo MVC de 32 MiB; se conserva el límite de 6 MiB del endpoint de foto, 32 MiB para galerías y 5 MiB por imagen. IIS/proxy puede imponer límites inferiores. |

Se conservan cookies HttpOnly, SameSite=Lax, duración de ocho horas sin renovación deslizante y validación de sesión/rol/versión contra el servidor. Secure depende de HTTPS en Development para compatibilidad con el entorno local; fuera de Development se exige Secure. No se desactivó HTTPS ni autorización. No se añadieron proxies de confianza ficticios ni lectura de X-Forwarded-For.

## Límites configurables

En `Security:RateLimits:<política>:{PermitLimit,WindowSeconds}` de appsettings.json. Las variables de entorno usan, por ejemplo, `Security__RateLimits__SocialWrites__PermitLimit`. Reiniciar la aplicación después de cambiarlos. Permisos entre 1 y 10000 y ventanas entre 1 y 86400 segundos; valores inválidos impiden configurar el limitador.

| Política | Valor inicial | Partición y motivo |
|---|---|---|
| LoginAttempts | 10/minuto | Siempre IP; conserva el umbral existente y limita el trabajo de BCrypt. El bloqueo de cuenta de cinco fallos sigue vigente. |
| Registration | 5/15 minutos | Siempre IP; evita creación automatizada intensa sin bloquear la navegación del formulario. |
| AccountRecovery | 5/15 minutos | Usuario autenticado o IP anónima; conserva la protección del flujo de recuperación. Solicitar código, restablecer y cambiar contraseña comparten presupuesto. |
| SocialWrites | 60/minuto | Usuario autenticado o IP anónima; admite interacción normal y cubre también logout/revocación. |
| Queries | 120/minuto | Usuario autenticado o IP anónima; permite navegación y carga de imágenes autorizadas. |
| Searches | 30/minuto | Usuario autenticado o IP anónima; acciones Explorar, Index y Administrar sin política específica, para contener listados/búsquedas más costosos. |

Ventanas fijas, sin cola. GET y POST de login tienen presupuestos separados. Las políticas se comparten entre acciones de la misma categoría; no crean un presupuesto por cada URL o búsqueda arbitraria. Los usuarios autenticados en una misma red tienen presupuestos independientes; login/registro anónimos aún comparten IP y pueden requerir ajuste para aulas o redes corporativas. Cabeceras reenviadas no alteran la partición.

Los limitadores viven en memoria por proceso y se reinician con la aplicación. Varias instancias multiplican el presupuesto: para despliegue se requiere coordinación externa o límites en un gateway. La autenticación se ejecuta antes del limitador para identificar al usuario y puede consultar SQL; estos controles no evitan todo coste previo. No constituyen protección DDoS. No se realizaron pruebas contra producción o servicios externos.

## Fechas y eventos de auditoría

Fechas existentes de registro, comunidad, publicación, comentario, like, membresía, imagen y reporte: DateTime en columnas datetime con usos de DateTime.Now/GETDATE. No tienen zona/offset histórico acreditado y se conservaron. Las fechas de sesiones, bloqueos, recuperación, suspensiones y nuevas tablas sociales/auditoría son DateTime en datetime2 con significado UTC explícito. No se transformaron registros existentes ni se reinterpretaron fechas locales como UTC.

Los nuevos eventos del contexto usan TimeProvider.GetUtcNow().UtcDateTime para mantener el esquema. Los logs nuevos usan DateTimeOffset UTC. La tabla actual no admite estados detallados ni recursos/correlación: Accion diferencia intentos, Exito es booleano y IdObjetivo sigue siendo un usuario, nunca el ID de una publicación o reporte.

| Acción/eventos | Cuándo se genera la hora en servidor | Datos registrados y resultado |
|---|---|---|
| Login / LoginDenied | Al validar credenciales o crear la sesión. | Actor/objetivo cuando se conoce, acción, Exito, FechaUtc; éxito persistido junto a sesión, rechazo con Exito=false. |
| Logout / SessionsRevoked / SessionRevoked | Al revocar realmente sesiones. | Usuario, acción, Exito, FechaUtc. No cookies ni identificadores de sesión en logs nuevos. |
| RecoveryRequested / RecoveryIssued / PasswordResetDenied / PasswordReset / PasswordChangeDenied / PasswordChanged | Al solicitar, emitir, validar o consumir recuperación y cambiar contraseña. | Usuario si corresponde, acción y resultado. Ningún token o contraseña; emisión/consumo/cambio exitosos se conservan con la transacción. |
| AccountRoleChanged / AccountStateChanged / AccountPasswordChanged | Antes del guardado de cambios detectados, persistido atómicamente con ellos. | Actor, usuario objetivo, acción, Exito=true y hora UTC; nunca valores anteriores o nuevos de contraseña/hash. |
| AccountCreated / ProfileChanged / ProfileImageCreated, Changed, Deleted | Al preparar SaveChanges. | Actor si existe y objetivo usuario; en altas de cuenta el objetivo es null porque su identidad aún no fue asignada. Solo perdura si el guardado/transacción tiene éxito. |
| Publication, Comment, Community + Created/Changed/Deleted | Al preparar SaveChanges. | Actor, usuario propietario, tipo de acción, Exito y FechaUtc. El retiro lógico es Changed, no un borrado físico ficticio. |
| Membership + Created/Changed/Deleted; eventos existentes CommunityChanged/MembershipChanged/CommunityLeft | Al preparar el cambio o SaveChanges. | Actor y usuario afectado, acción y FechaUtc. Se evita duplicar el mismo nombre/objetivo en el mismo guardado. |
| ReportCreated / ReportChanged / ModerationAction | Al preparar el reporte, cambio de estado u operación de moderación. | Actor y usuario objetivo correspondiente al evento; Exito y hora UTC dentro de la transacción. |
| SuspensionExpired; AdminCommunityChanged; UserBlocked/UserUnblocked; MessageCreated/Changed | En la operación existente o preparación de SaveChanges. | IDs de usuario, acción, resultado y UTC. No razones libres, cuerpos de mensajes ni contenido. |
| AdministrationAttempt / ModerationAttempt | Antes de invocar una acción administrativa autorizada. | Actor, Exito=false, UTC. Su nombre significa intento; la vista histórica presenta false como Denegado por no tener un estado específico de intento. |
| AdministrationAccess / ModerationAccess | Después de que la acción retorna sin excepción ni resultado de error conocido. | Acceso autorizado completado a nivel de acción; nunca prueba de una mutación. Las mutaciones conservan sus eventos propios. |
| Logs de peticiones POST | Entrada y retorno/fallo de acción. | DateTimeOffset UTC, acción, Attempt/ActionReturned/Failed, actor, controlador, ID numérico de ruta si existe y correlación. ActionReturned no afirma éxito de negocio ni resultado final del renderizado. |
| SecurityAudit | Después de SaveChanges exitoso. | Hora del evento, acción, actor, objetivo usuario, AuditId y correlación. Con transacción explícita pendiente: SavedPendingTransaction, nunca Committed. La fila durable se consulta tras el commit. Si falla SaveChanges no se emite este log. |
| AuthorizationDenied / RateLimitRejected | Al retornar rechazo por autorización o 429. | UTC, actor cuando existe, correlación y metadatos de ruta limitados. Máximo diez eventos detallados por minuto por proceso, compartidos entre ambas clases; los suprimidos se resumen al llegar un rechazo en otra ventana. |

En operaciones por lote el contexto puede agrupar la misma acción/usuario objetivo por guardado; la tabla no ofrece trazabilidad individual completa de cada recurso. Los logs enlazan AuditId con CorrelationId, pero no sustituyen una ampliación de esquema para registrar cada recurso y su estado exacto. Para IDs de formularios, GUIDs, recursos nuevos y fallos de renderizado la cobertura detallada de petición es limitada.

PC, teléfono y tableta llaman al mismo servidor: el servidor genera el timestamp. No se usa hora del dispositivo para permisos, expiración u orden confiable. No se registra User-Agent ni se generan huellas de dispositivo. Para orden total usar el identificador de auditoría además de la fecha; relojes de varios servidores deben sincronizarse.

No hay zona horaria de aplicación configurada. La vista de auditoría conserva su encabezado Fecha UTC y su representación UTC. Las fechas locales históricas mantienen su presentación original. No se asume America/Mexico_City a partir del equipo. Para presentar toda la aplicación en una zona única hacen falta identificar la zona de origen histórica, configurar una zona válida y acordar una conversión explícita de cada familia de datos.

## Propuesta de esquema, sin ejecutar

Para auditoría completa por recurso, preparar una migración independiente con columnas opcionales `TipoRecurso varchar(64)`, `IdRecurso varchar(64)`, `Correlacion varchar(128)`, `Resultado varchar(24)` y `TimestampUtc datetimeoffset(7)` en AuditoriasSeguridad. Mantener IdObjetivo como usuario y FechaUtc por compatibilidad. No rellenar históricos con recursos/zonas inventados. Revisar índices de correlación y recurso/fecha, costes de almacenamiento y permisos SQL; actualizar consulta/vista y pruebas antes de aplicarla. Ninguna de estas columnas ni migraciones se añadió al modelo actual ni se aplicó a SQL Server.

## Acceso, conservación y secretos

Auditoría general restringida a Administrador; Actividad restringe registros a actor/objetivo del usuario actual. No se crearon endpoints públicos de logs. Los logs estructurados dependen de los proveedores ILogger del host: esta revisión no instala un almacén durable externo. Para visualizarlos en desarrollo, usar consola o ventana de salida con nivel Information para Proyecto_HobbyHub.Security y preservar scopes si el proveedor lo permite. El texto escrito en los campos no se registra.

Política propuesta: conservar auditoría durable 90 días y logs técnicos 30 días con rotación por tamaño, acceso mínimo y protección de respaldos. Resumir métricas de abuso sin retener eventos detallados indefinidamente. La eliminación/archivo debe ejecutarla una tarea operativa autorizada con retención también en respaldos; no se instaló ni ejecutó limpieza. La aplicación mantiene su protección contra edición/borrado de auditoría.

El verificador de candidatos Git no encontró coincidencias de secretos según sus patrones. También se comprobaron sin imprimir valores las configuraciones disponibles de HEAD para contraseñas SQL literales/destinos remotos, sin coincidencias. No se auditó exhaustivamente todo el historial ni el contenido de los dos binarios detectados; no se afirma ausencia absoluta de secretos. No se identificó un secreto concreto que rotar en el alcance comprobado. Si un secreto fue compartido en revisiones anteriores, debe revocarse/rotarse en su proveedor aunque ya esté eliminado del árbol actual. Configuración personal, claves de Data Protection, archivos y buzón permanecen fuera de recursos públicos y excluidos de Git.

## Verificación y límites de evidencia

Resultado final: compilación correcta; 160 pruebas .NET aprobadas, 20 omitidas y cero fallidas (180 en total, 21 segundos en la última ejecución). Nueve pruebas JavaScript aprobadas. Escaneo final: 256 candidatos, 254 archivos de texto, cero coincidencias según los patrones y dos binarios pendientes de inspección interna. Informe TRX local en Proyecto_HobbyHub.Tests/TestResults/security-review.trx; resumen del escáner en .local/final-review/repository-safety.json.

Comandos desde la raíz:

```powershell
$env:HOBBYHUB_SQL_TESTS='0'
dotnet restore Proyecto_HobbyHub.sln --artifacts-path .local/security-review -p:NuGetAudit=true -p:NuGetAuditMode=all
dotnet test Proyecto_HobbyHub.sln --artifacts-path .local/security-review --no-restore --logger 'trx;LogFileName=security-review.trx'
node --test Proyecto_HobbyHub.Tests/auth.test.cjs Proyecto_HobbyHub.Tests/navigation.test.cjs
python tools/Verify-RepositorySafety.py
```

Las pruebas compilan aplicación y proyecto de pruebas. El host offline impide abrir conexiones SQL y usa cuentas/sesiones/recuperación sintéticas. Se comprobaron acceso anónimo/roles, políticas de recursos ajenos, cookies/CSRF, contraseñas, expiración, recuperación, validación de imágenes y regresiones existentes. Las pruebas nuevas cubren: 429 de recuperación con Retry-After de 15 minutos, 120 GET de login seguidos de 429 sin aceptar cabeceras de IP falsas, particiones independientes para usuarios en la misma IP, configuración inválida, intento administrativo fallido sin acceso exitoso, generación de auditoría pendiente ante fallo de guardado sin alterar fechas históricas y muestreo de diez rechazos con resumen de quince suprimidos.

Se trata de pruebas locales de umbral, secuenciales y acotadas, no de una prueba de capacidad ni DDoS. No se ejecutó carga externa. Los scripts JavaScript comprueban nueve comportamientos de validación/navegación; no equivalen a una comparación visual en navegador.

Restauración disponible, pero advertencia NU1900: no se pudo acceder al índice de NuGet para datos de vulnerabilidades, incluso al reintentar fuera del sandbox. No se certifica el estado actual de vulnerabilidades de paquetes .NET, npm o bibliotecas estáticas. No se ejecutó npm audit en esta revisión.

Se omiten expresamente veinte pruebas SQL, incluidas las de datos sintéticos y navegación integral, porque escriben registros y el usuario prohibió modificar la base. Pendientes: persistencia/rollback y concurrencia reales de auditoría/reportes, traducción SQL de la proyección del perfil, rendimiento de consultas, funcionamiento integral de módulos con SQL y revisión visual con PC/teléfono/tableta. También permanecen listados heredados sin paginación en Detalle de comunidad, miembros de comunidad y algunas colecciones de vistas; deben paginarse con navegación antes de usar volúmenes grandes. No se truncó silenciosamente su contenido ni se rediseñaron pantallas.

## Archivos modificados por esta revisión

- Program.cs y appsettings.json: filtros globales, correlación, logging y configuración de límites.
- Security/RequestLimits.cs, RequestSecurityLog.cs y WriteRequestAuditFilter.cs: archivos nuevos.
- Security/SocialWriteConvention.cs, ManagementAuditFilter.cs y SafePersistenceExceptionFilter.cs: clasificación de límites, intentos/resultados y diagnóstico seguro.
- Models/HobbyHubContext.cs: auditoría atómica y enlace de logs mediante AuditId.
- Controllers/AuthController.cs, ComunidadesController.cs, ComunidadesController.Management.cs, HomeController.cs, PerfilController.cs, SocialController.cs, ModeracionController.cs y AdministracionController.cs: propagación de cancelación; además, proyección del perfil, transacciones de reportes y compatibilidad del manejador de errores.
- Proyecto_HobbyHub.Tests/RequestSecurityReviewTests.cs: pruebas nuevas; SecurityTestControllers.cs: endpoint fallido exclusivamente del host de pruebas.
- Este documento. Los demás cambios que aparezcan en git status ya existían al recibir el proyecto.

## Comprobación manual en Visual Studio y navegador

1. Abrir Proyecto_HobbyHub.sln y seleccionar Development. No ejecutar scripts de preparación, migraciones ni pruebas SQL. Compilar con la carpeta independiente indicada arriba desde la terminal integrada para no sobrescribir DLLs de servidores existentes. Ejecutar las pruebas offline desde esa terminal o el explorador con HOBBYHUB_SQL_TESTS=0.
2. Para una revisión visual sin modificar datos, usar exclusivamente el entorno de revisión aislado ya configurado según README y tools/Start-VisualReview.ps1, sin volver a aprovisionarlo ni detener servidores ajenos. El arranque conserva sus restricciones de configuración; no usar una conexión compartida como alternativa.
3. Sin sesión, visitar Perfil y comprobar redirección a login. Con cuentas existentes de Usuario/Administrador, comprobar denegación/acceso al panel de administración y que la auditoría global solo está disponible al administrador. La navegación autenticada puede escribir eventos de acceso y reactivar suspensiones vencidas: realizarla únicamente cuando estén autorizadas esas escrituras de aplicación.
4. En F12, Network, inspeccionar X-Correlation-ID, Cache-Control de páginas protegidas y flags de cookies en conexión HTTPS. No copiar cookies o credenciales a informes.
5. Para comprobar 429 sin login ni escrituras SQL, abrir /Auth/Login en un entorno local con consultas configuradas temporalmente a tres por minuto, reiniciar y repetir GET cuatro veces. Confirmar estado 429 y Retry-After; restaurar después el valor inicial. No repetir contra un servidor de terceros.
6. Una vez autorizadas pruebas de escritura sobre una base de pruebas, con dos cuentas comprobar edición ajena denegada, POST sin antiforgery=400, creación/retiro/comentario/reporte, fechas UTC y registros solo tras commit. Un intento fallido no debe mostrarse como operación completada. Consultar la misma información desde emulación móvil/tableta sin cambiar la hora del servidor.

Implementación de Retry-After basada en la [documentación oficial de ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit-samples?view=aspnetcore-8.0).
