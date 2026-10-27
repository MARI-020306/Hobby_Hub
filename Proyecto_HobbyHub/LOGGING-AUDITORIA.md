# Logs tecnicos y auditoria

Implementacion para .NET 8. Se reutilizan ILogger, ISecurityAudit, AuditoriasSeguridad,
los almacenes de autenticacion/recuperacion y los interceptores de SaveChanges existentes.
No se ejecuto ninguna migracion, ni se hizo commit, push o despliegue.

## Archivos de esta intervencion

- .gitignore (nuevo, dentro del proyecto; el .gitignore padre ya excluia .local y Logs)
- Program.cs
- Proyecto_HobbyHub.csproj
- appsettings.json
- Models/AuditoriaSeguridad.cs
- Models/HobbyHubContext.cs
- Security/IAccountRecoveryStore.cs
- Security/AccountRecoveryService.cs
- Security/ManagementAuditFilter.cs
- Security/WriteRequestAuditFilter.cs
- Security/RequestSecurityLog.cs
- Security/SafePersistenceExceptionFilter.cs
- Security/AuditEventBudget.cs (nuevo)
- Security/RollingFileLoggerProvider.cs (nuevo)
- Security/TechnicalRequestMiddleware.cs (nuevo)
- Controllers/ComunidadesController.cs
- Controllers/ComunidadesController.Management.cs
- Views/Shared/_AuditTable.cshtml
- Database/Scripts/010_AuditCorrelation.sql (nuevo, sin ejecutar)
- Verification/LoggingAudit.Tests.csproj (nuevo)
- Verification/LoggingAuditTests.cs (nuevo)
- LOGGING-AUDITORIA.md (este documento)

El repositorio contenia muchos cambios previos; esta lista corresponde exclusivamente
a los archivos tocados en esta intervencion. No se modificaron los archivos de pruebas
existentes del proyecto hermano.

## Logs tecnicos

Carpeta: `.local/logs` relativa a ContentRootPath, es decir:
`C:\Users\herna\Documents\Hobby_Hub\Proyecto_HobbyHub\.local\logs`.
Archivos `hobbyhub-*.jsonl`, fuera de wwwroot y excluidos de Git.
Proveedor propio ILoggerProvider compatible con .NET 8, sin paquete adicional.
Tambien se escribe a consola y al proveedor Debug de Visual Studio.

Configuracion en `TechnicalLogs`: MaxFileBytes=5242880, RetainedFiles=14,
RetentionDays=14. Rota por tamano o dia UTC; limpia al iniciar y al escribir.
Limite aproximado de archivos: 70 MiB con valores predeterminados y una instancia.
No hay tarea de limpieza mientras la aplicacion esta detenida.
Niveles Information (actividad), Warning (rechazos/persistencia), Error (errores del
servidor). Se aceptan solo categorias revisadas de la aplicacion. Se suprimen los
logs crudos de ASP.NET/EF para evitar SQL, URLs, excepciones y secretos.

Disparadores:
- RequestCompleted: solicitud MVC terminada, accion, estado, duracion en milisegundos
  y correlacion generada por servidor. No incluye URL, query, cuerpo o cabeceras.
- ServerFailure: excepcion no controlada; tipo de excepcion y correlacion, sin mensaje
  interno ni stack trace. Respuesta generica con referencia.
- PersistenceFailure: errores SQL/EF, tipo y numero SQL; respuesta comprensible.
- SecurityEvent: intentos POST, retorno de accion, rechazos de autorizacion y limites.
  ActionReturned no significa que la modificacion se guardo.
- SecurityAudit: auditoria guardada; SavedPendingTransaction indica que una transaccion
  externa aun podria revertirse. La base de datos es la fuente de verdad del exito.
- Advertencias existentes de autenticacion, recuperacion y revalidacion cuando no se
  puede consultar la base de datos.

Muestreo de diagnostico/intentos: presupuestos globales de 30 eventos por minuto
por instancia del servicio correspondiente, sin diccionarios basados en entrada externa.
Los rechazos existentes conservan su presupuesto de 10/minuto y resumen de suprimidos.
Las modificaciones persistidas conservan su auditoria completa en SQL; su copia tecnica
si se muestrea. Se conservan los limites de solicitudes preexistentes.

## Persistencia de auditoria

Tabla existente: `dbo.AuditoriasSeguridad`.
Columnas existentes: IdAuditoria (bigint identity), IdActor (int nullable),
IdObjetivo (int nullable: usuario afectado), Accion (varchar(64)), Exito (bit),
FechaUtc (datetime2, generado por el servidor).
Columnas nuevas opcionales: CorrelacionId varchar(64), TipoRecurso varchar(64),
IdRecurso varchar(128). Los registros historicos conservan NULL en estas columnas.

`010_AuditCorrelation.sql` es una migracion SQL incremental e idempotente preparada
segun la convencion del repositorio. Agrega solo tres columnas opcionales, sin borrar
datos. Tiene proteccion para bases aisladas HobbyHub_Development*. No se ejecuto.
Debe revisarse y aplicarse con autorizacion ANTES de usar esta version contra SQL:
EF consultara/insertara las columnas nuevas; con el esquema anterior las operaciones
que las utilizan fallaran de forma controlada. No hay actualizacion automatica al iniciar.

Acciones persistentes:
- Login / LoginDenied: sesion guardada / credenciales o estado rechazados.
  LoginDenied tiene IdActor NULL; IdObjetivo puede identificar la cuenta consultada,
  lo cual no demuestra que esa persona haya realizado el intento.
- Logout, SessionRevoked, SessionsRevoked: cierre y revocacion de sesiones.
- RecoveryRequested (sin cuenta valida), RecoveryIssued (solicitud emitida),
  PasswordReset, PasswordResetDenied, PasswordChanged, PasswordChangeDenied:
  recuperacion/cambio de credenciales. RecoveryIssued no demuestra entrega del correo.
- Publication, Comment, Community, Report, Membership, Message, ProfileImage, Account,
  Profile con sufijo Created/Changed/Deleted segun la operacion EF correspondiente.
  Se eliminaron las llamadas duplicadas de comunidad/membresia de los controladores.
- AccountRoleChanged, AccountStateChanged, AccountPasswordChanged: cambios de rol,
  estado o credencial detectados por el contexto. SuspensionExpired: fin de suspension.
- ModerationAction: resolucion/moderacion de reportes; AdminCommunityChanged:
  ajustes administrativos de comunidad. Sus cambios de entidades tambien producen
  los eventos de recurso correspondientes.
- AdministrationAttempt/ModerationAttempt: invocacion autorizada, Exito=false indica
  intento, no necesariamente error. AdministrationAccess/ModerationAccess: lectura
  administrativa/moderacion completada. Los POST no generan Access como prueba de exito.
- UserBlocked/UserUnblocked: cambios de bloqueo existentes.

Los eventos de contenido se guardan junto con sus modificaciones. Para claves identity,
una transaccion completa IdRecurso despues del primer guardado antes de confirmar.
Los eventos de cuentas identifican el recurso por IdObjetivo cuando se conoce.
Los intentos fallidos persistentes se muestrean; las acciones exitosas guardadas no.
No hay edicion/eliminacion de auditoria por operaciones normales: el contexto rechaza
esos estados y no hay endpoints de modificacion. La finalizacion interna del ID del
recurso forma parte de la transaccion inicial. Esto no impide cambios por un DBA ni
SQL directo fuera de las operaciones normales.

## Consulta

- Visual Studio: iniciar con Development y la base aislada configurada; ver Salida >
  Depurar, la consola del proceso y abrir los .jsonl con Archivo > Abrir. La carpeta
  .local puede mostrarse mediante Mostrar todos los archivos. X-Correlation-ID en la
  respuesta permite buscar el mismo valor en los archivos y SQL.
- SQL Server: conectar a la instancia/base aislada configuradas en el entorno; abrir
  dbo.AuditoriasSeguridad desde SQL Server Object Explorer o SSMS. No imprimir cadenas
  de conexion ni ejecutar la migracion sin autorizacion.
- Aplicacion: /Social/Actividad muestra solo eventos donde el usuario actual es actor
  u objetivo y sus propias sesiones. /Administracion/Auditoria exige Administrador en
  el servidor. Se conservan filtros/paginacion. Al pasar el cursor sobre una fila se
  muestra referencia y recurso, sin cambiar la estructura visual de la tabla.

Consulta de solo lectura DESPUES de aplicar la migracion:

```sql
SELECT TOP (100)
    IdAuditoria, FechaUtc, Accion, Exito,
    IdActor, IdObjetivo, TipoRecurso, IdRecurso, CorrelacionId
FROM dbo.AuditoriasSeguridad
ORDER BY IdAuditoria DESC;
```

ANTES de aplicar la migracion, consulta compatible con el esquema existente:

```sql
SELECT TOP (100) IdAuditoria, FechaUtc, Accion, Exito, IdActor, IdObjetivo
FROM dbo.AuditoriasSeguridad
ORDER BY IdAuditoria DESC;
```

Estas consultas administrativas se ejecutan con permisos SQL del operador; no deben
exponerse como un endpoint anonimo. El controlador de actividad aplica el filtro del
usuario autenticado en el servidor.

## Verificacion

Compilacion y pruebas sin modificar SQL. Las suites existentes seleccionadas cubren
inicio de sesion correcto, contrasena erronea, acceso administrativo no autorizado,
logout/CSRF, recuperacion y fallos de conexion con mensajes seguros (70 pruebas).
Las cuatro pruebas nuevas verifican rotacion/retencion y niveles, excepcion sin secretos
con correlacion, auditoria pendiente ante fallo de guardado y bloqueo de edicion/borrado.
Los artefactos se escriben dentro de .local, excluida de Git.

```powershell
dotnet build --no-restore
dotnet test Verification/LoggingAudit.Tests.csproj --artifacts-path .local/verification
dotnet test ../Proyecto_HobbyHub.Tests/Proyecto_HobbyHub.Tests.csproj --artifacts-path .local/audit-tests --filter "FullyQualifiedName~RequestSecurityReviewTests|FullyQualifiedName~AuthenticationMvcTests|FullyQualifiedName~AccountRecoveryTests|FullyQualifiedName~AccountSecurityTests"
```

No se verifico el ciclo completo de escritura/lectura real en SQL Server ni la
transaccion de claves identity contra SQL: requieren aplicar la migracion autorizada.
Las pruebas MVC usan almacenes en memoria y bloquean acceso a SQL; las de contexto
simulan el fallo de conexion. No se verifico navegacion visual manual de las pantallas.
NuGet informo NU1900 por falta de acceso al indice de vulnerabilidades; no se pudo
verificar ese catalogo. Las dependencias disponibles permitieron compilar/probar.

Referencia del contrato del proveedor: https://learn.microsoft.com/en-us/dotnet/core/extensions/custom-logging-provider

Resultado final: compilacion correcta con 0 errores y 0 advertencias; 70/70 pruebas existentes seleccionadas y 4/4 pruebas nuevas correctas.
