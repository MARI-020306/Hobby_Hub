# Discrepancia de esquema de auditoría — 2026-10-20

## Aplicación autorizada posterior al diagnóstico

El usuario autorizó exclusivamente 010 sobre la base ordinaria. Antes de aplicarlo,
se resolvió la configuración Development con los proveedores .NET: JSON base y de
entorno, JSON aislado/local, secretos de usuario y variables de entorno, bajo el
usuario propietario. El destino efectivo coincidió exactamente con
`(localdb)\HobbyHubPhase1C_20261008` / `HobbyHub_Development_Phase1C_20261008`.
No había un proceso de la aplicación en ejecución ni un listener en 5062/5088/7125;
no había argumentos de lanzamiento de aplicación adicionales que resolver.

Se ejecutó únicamente 010_AuditCorrelation.sql con sqlcmd -b y terminó con código 0.
La consulta posterior de sys.columns/sys.types confirmó CorrelacionId varchar(64),
TipoRecurso varchar(64) e IdRecurso varchar(128), todas NULL.
No se ejecutaron otros scripts ni se modificaron otras bases.

Prepare-VisualTestDatabase.ps1 ahora incluye 010 después de 009. Conserva la
sustitución existente del nombre ordinario por el nombre exacto de la nueva base
visual en la copia del script, incluyendo su guardia IF DB_NAME(). El original
010 continúa restringido a la base ordinaria. Se validaron sintaxis PowerShell y
sustitución de la guardia, sin ejecutar el preparador ni crear bases.

`dotnet build Proyecto_HobbyHub/Proyecto_HobbyHub.csproj --no-restore` pasó con cero
errores y advertencias. La utilidad diagnóstica local compiló con aviso NU1900
por imposibilidad de consultar el servicio de vulnerabilidades NuGet.

No se encontraron credenciales locales de una cuenta sintética para esta base.
Se llamó al AccountSecurityService.LoginAsync real con una identidad aleatoria
inexistente. Devolvió denegación y persistió LoginDenied, leído después mediante
otro DbContext: IdAuditoria=10627, Exito=false, TipoRecurso=Account,
CorrelacionId=audit-schema-check-e84438f2182b4dea8e27bc8ed56bea68. Sin SQL 207.
No se crearon cuentas ni sesiones. No se verificó login exitoso ni el flujo HTTP.
Para completar esa prueba: iniciar la aplicación en Development, entrar con una
cuenta de pruebas autorizada y comprobar un evento nuevo Accion='Login', Exito=1
con su IdActor/IdObjetivo. No compartir las credenciales.

El diagnóstico original siguiente documenta el estado ANTERIOR a esta aplicación;
sus referencias a columnas ausentes y script pendiente son históricas.

Comprobación realizada exclusivamente con SELECT sobre metadatos de SQL Server,
sin escribir registros ni ejecutar DDL o migraciones.

Destino consultado: `(localdb)\HobbyHubPhase1C_20261008`, base
`HobbyHub_Development_Phase1C_20261008`, esquema predeterminado `dbo`.
Coincide con appsettings.Development.Isolated.json y con el destino ordinario
exigido por Program.cs en Development. Los argumentos o la configuración de otro
proceso en ejecución no se inspeccionaron; existe además un destino separado de
pruebas visuales.

## Causa comprobada

HobbyHubContext mapea AuditoriaSeguridad a AuditoriasSeguridad. Las propiedades
CorrelacionId y TipoRecurso son varchar(64) NULL; IdRecurso es varchar(128) NULL.
Se derivan de propiedades string? y HasMaxLength/IsUnicode(false).

sys.tables/sys.schemas/sys.columns/sys.types confirman que dbo.AuditoriasSeguridad
solo tiene IdAuditoria bigint IDENTITY NOT NULL, IdActor int NULL,
IdObjetivo int NULL, Accion varchar(64) NOT NULL, Exito bit NOT NULL y
FechaUtc datetime2 NOT NULL. Faltan exactamente los tres campos nuevos del modelo.
La clave primaria y el índice (FechaUtc, IdObjetivo) existen y están habilitados.
No se encontró tabla de historial de migraciones.

EfSecurityAudit.WriteAsync añade la entidad y llama SaveChangesAsync.
HobbyHubContext.PendingAudits completa la correlación y el recurso; por ello
el login también intenta persistir los campos nuevos. No es un nombre de tabla
equivocado: el esquema conserva la versión creada por 006_RecoveryAndAudit.sql.
010_AuditCorrelation.sql ya estaba preparado, pero su efecto no está presente.
No puede determinarse con los metadatos si nunca se ejecutó o fue revertido.

## Corrección preparada, pendiente de aplicación

Proyecto_HobbyHub/Database/Scripts/010_AuditCorrelation.sql contiene únicamente
las tres adiciones necesarias, condicionales y dentro de una transacción.
Se restringió al nombre exacto de la base comprobada y se añadió rollback ante
error. No ejecutar todos los scripts ni dotnet ef database update.

Las filas existentes conservarán sus datos y tendrán NULL en las tres columnas;
no hay valores predeterminados ni actualización retrospectiva. La operación DDL
requiere un bloqueo de esquema y puede esperar o bloquear solicitudes concurrentes.
No se ha aplicado: el login puede seguir devolviendo 503 hasta su aplicación y
posterior validación funcional.

## Alcance relacionado comprobado

Las columnas, tipos y nulabilidad de SesionesUsuario y RecuperacionesCuenta
coinciden con sus modelos. Sus claves e índices configurados existen, están
habilitados y mantienen la unicidad esperada. También coinciden los campos
VersionSeguridad, IntentosFallidos, BloqueoHastaUtc y SuspensionHastaUtc de Usuarios.
No se encontraron otras diferencias en estos metadatos revisados; no se realizó
una auditoría completa de todas las tablas, restricciones o permisos.

Prepare-VisualTestDatabase.ps1 enumera scripts hasta 009 y omite 010: las bases
que prepara tampoco incorporan estos campos por ese procedimiento. El script
010 ahora exige la base ordinaria exacta y no debe ejecutarse sobre una base
visual sin revisar y preparar expresamente su destino.

No se alteró el mapeo, la autenticación, los límites de intentos, las sesiones,
la auditoría ni el manejo de errores para simular un login satisfactorio.
