# Fase 1C — Seguridad de cuentas, recuperación, auditoría y permisos

Cambios locales sobre las fases 1A/1B, exclusivamente en `rama/Fernando`. No se
ejecutaron scripts, migraciones, consultas SQL ni creación/arranque de instancias;
no hubo commits ni push. No se instaló un proveedor ni se enviaron correos reales.

## Funcionalidades

- `/Cuenta/Seguridad`: usuario autenticado cambia contraseña verificando la actual,
  la confirmación, mínimo de ocho caracteres y máximo BCrypt de 72 bytes UTF-8.
  El servicio también revalida la sesión, no solo el middleware. Los errores de la
  contraseña actual cuentan para el bloqueo existente de cinco intentos/15 minutos.
- Cambio de contraseña revoca todas las sesiones, rota versión, cancela tokens de
  recuperación y audita cambios/revocaciones en la misma transacción. Se cierra la
  cookie actual y se requiere iniciar sesión nuevamente.
- `/Cuenta/RevocarSesiones`: POST autenticado con antiforgery, revocación de todas
  las sesiones propias y cierre de cookie. No acepta un identificador de otro usuario.
- `/Cuenta/Recuperar`: respuesta genérica para cuentas conocidas, inexistentes,
  suspendidas/bloqueadas o fallo de entrega. Token criptográfico de 32 bytes,
  codificado base64url; solo su SHA-256 se persiste. Cada nueva solicitud invalida
  códigos anteriores. Se mantiene la identidad visual morada y los formularios reales.
- `/Cuenta/Restablecer`: token de un uso, 30 minutos, vinculado a la versión de
  seguridad de la cuenta. Validación de contraseña/confirmación. El consumo es
  condicional y transaccional, con bloqueo de cuenta en orden consistente para
  operaciones concurrentes. No se permite recuperar una cuenta suspendida/bloqueada.
  La posesión del código permite eliminar un bloqueo temporal por intentos fallidos.
- Los códigos se introducen mediante POST, nunca se agregan a URLs ni mensajes de
  registro. Las pantallas usan NoStore y no-referrer. No se muestran credenciales
  en errores. Antiforgery en todas las operaciones y 5 solicitudes por IP/15 minutos
  para recuperación, restablecimiento y cambio de contraseña (por proceso).
- `IRecoveryMailSender` desacopla entrega. Desarrollo almacena mensajes cifrados por
  Data Protection en `.local/mailbox`, fuera de recursos públicos e ignorado por Git.
  No existe ruta HTTP de lectura del buzón. Pruebas usan buzón en memoria para
  recuperar los códigos sintéticos, sin red. El buzón en disco es almacenamiento
  de recogida para depuración local; leerlo exige las claves y la identidad local
  autorizada, no existe aún una herramienta de lectura/cliente de correo.
  Production rechaza el buzón local: proveedor, contenido final y entrega real
  siguen pendientes de aprobación. No se puede ofrecer recuperación por correo
  real hasta habilitar ese proveedor.
- Auditoría persistente: login correcto/fallido, logout, revocación de sesiones,
  emisión/denegación de recuperación, restablecimiento y cambios de contraseña.
  Login/logout/revocaciones exitosos se auditan en la transacción del almacén;
  no se emite autenticación si no puede guardarse sesión/auditoría.
- SaveChanges audita cambios de rol, estado o contraseña y rota versión de seguridad.
  Audita dentro del mismo guardado. Actor obtenido de claims de la solicitud, o null
  para operaciones sin solicitud. No guarda valores anteriores/nuevos sensibles.
  Rechaza modificar o eliminar auditorías mediante SaveChanges.
- Filtro de administración/moderación registra acceso autorizado antes de ejecutar
  la acción. Este registro indica invocación, no garantiza éxito de una futura acción.
  Cambios futuros de dominio deben incluir su auditoría en la misma transacción.
  Los paneles administrativos completos siguen en fases posteriores.
- `ResourceAccess` centraliza propiedad/rol/membresía/visibilidad. Integrado en editar
  y eliminar comunidades, publicar, editar/retirar/reportar publicaciones, editar
  perfil y comentar/dar likes a contenido visible. Sin privilegios globales implícitos
  para borrar recursos ajenos. Membresía se obtiene de SQL, nunca del formulario.
  Preserva interacción en contenido visible de comunidades públicas y reporte por
  creador de comunidad. Privacidad social futura requiere reglas adicionales.
- Navegación: recuperación desde login; seguridad desde perfil y comunidades.

## Archivos creados en 1C

Todos los caminos son relativos a la raíz; los archivos previos siguen sin commit.

- `Proyecto_HobbyHub/Controllers/CuentaController.cs`.
- `Proyecto_HobbyHub/Models/RecuperacionCuenta.cs`, `AuditoriaSeguridad.cs`.
- `Proyecto_HobbyHub/Security/AccountRecoveryService.cs`, `EfAccountRecoveryStore.cs`,
  `IAccountRecoveryStore.cs` (incluye interfaz/implementación de auditoría),
  `RecoveryMail.cs`, `ResourceAccess.cs`, `ManagementAuditFilter.cs`.
- `Proyecto_HobbyHub/ViewModels/AccountSecurityViewModels.cs`.
- `Proyecto_HobbyHub/Views/Cuenta/Seguridad.cshtml`, `Recuperar.cshtml`, `Restablecer.cshtml`.
- `Proyecto_HobbyHub/Views/Shared/_AccountLayout.cshtml`, `wwwroot/css/cuenta.css`.
- `Proyecto_HobbyHub/Database/Scripts/006_RecoveryAndAudit.sql`,
  `Database/Prepare-IsolatedDevelopment.ps1`.
- `Proyecto_HobbyHub.Tests/AccountRecoveryTests.cs`, `RecoveryMvcTests.cs`,
  `ResourceAccessTests.cs`, `LocalMailboxTests.cs`, `FakeAccountRecoveryStore.cs`.
- `docs/Fase-1C-seguridad.md`.

## Archivos modificados en 1C

- `Proyecto_HobbyHub/Program.cs`, `Models/HobbyHubContext.cs`.
- `Proyecto_HobbyHub/Security/AccountSecurityService.cs`, `EfAccountSecurityStore.cs`.
- `Proyecto_HobbyHub/Controllers/ComunidadesController.cs`, `HomeController.cs`, `PerfilController.cs`.
- `Proyecto_HobbyHub/Views/Auth/Login.cshtml`, `Perfil/Index.cshtml`, `Comunidades/Index.cshtml`.
- `Proyecto_HobbyHub/appsettings.Development.Isolated.json` (destino nuevo preparado,
  sin creación de instancia ni conexión).
- `Proyecto_HobbyHub/Database/README.md`, `Scripts/004_ReadOnlySchemaInventory.sql`.
- `Proyecto_HobbyHub.Tests/AccountSecurityTests.cs`, `AuthenticationMvcTests.cs`,
  `FakeAccountSecurityStore.cs`, `OfflineApplicationFactory.cs`, `SchemaSafetyTests.cs`.

## Propuesta SQL que requiere autorización antes de ejecución

Herramientas encontradas: sqlcmd ODBC 17 y motores LocalDB 2019 (15.0.4382.1)
y 2025 (17.0.4025.3), además de
servicios SQL existentes que no se utilizaron. `SqlLocalDB info` solo mostró
`MSSQLLocalDB` bajo la identidad de esta terminal. No certifica disponibilidad bajo
la identidad Windows que ejecutará la aplicación ni se comprobó el contenido de bases.

**Instancia nueva:** `HobbyHubPhase1C_20261008`.
**Base nueva:** `HobbyHub_Development_Phase1C_20261008`.
**Servidor propuesto:** `(localdb)\HobbyHubPhase1C_20261008`.

El procedimiento propone explícitamente motor LocalDB 15.0 (2019), comprueba herramientas y rechaza un nombre de instancia
ya existente antes de crear/arrancar la nueva. Luego crea una base vacía; el comando
falla si ya existe. Orden de scripts:

1. `001_EmptyDevelopmentBaseline.sql`: seis tablas base, exige base vacía.
2. `002_ExistingFeatureExtensions.sql`: extensiones sociales existentes, tres tablas.
3. `003_DevelopmentRoles.sql`: Usuario/Creador; no usuarios ni roles privilegiados.
4. `005_AccountSessionsSecurity.sql`: columnas de seguridad y tabla de sesiones.
5. `006_RecoveryAndAudit.sql`: tablas de recuperación y auditoría e índices.
6. `004_ReadOnlySchemaInventory.sql`: comprobación de metadatos, sin filas de usuarios.

Consecuencias: nueva instancia y base, doce tablas con restricciones/índices y dos
roles ordinarios. Los scripts tienen transacciones individuales, no una transacción
global; si falla uno, los anteriores permanecen aplicados y se detiene el procedimiento.
Sin sobrescritura, borrado ni limpieza automática. No usa servicios/base compartidos.
Este procedimiento **no se ejecutó**, incluido su parámetro `-Approved`.

## Pruebas y límites

Ejecutados restore, build y test de la solución, además de las cuatro pruebas previas
de JavaScript. No se cambió el comportamiento JavaScript en 1C; las nuevas vistas usan
validación unobtrusive existente y validación obligatoria de servidor.

Resultado: restore correcto; build con cero errores/cero advertencias; 134 pruebas
.NET aprobadas, cero omitidas; cuatro pruebas JavaScript aprobadas. Total 138.
El script PowerShell se comprobó con el parser sin invocarlo. `git diff --check`
no encontró problemas de espacios. Hay 59 casos .NET nuevos respecto a los 75 de 1B.

Las pruebas de recuperación usan almacenamiento doble y buzón en memoria. HTTP
ejecuta MVC/Razor, cookies, antiforgery, rate limiter y filtros reales. Pruebas de EF
comprueban metadatos y preparación de auditoría; toda apertura SQL está bloqueada.
La carrera de consumo de token está probada en el doble de almacenamiento, no en
SQL Server. No equivale a validar locks, traducción SQL o rollback en una instancia real.

Pendiente tras autorización SQL: preparar el esquema aislado, comprobar concurrencia
de emisión/consumo/cambio de contraseña y rollback junto con auditoría, persistencia
tras reinicio, recuperación con cuenta temporalmente bloqueada y suspensión, permisos
SQL append-only, claves/buzón bajo identidad Windows, recorrido completo de registro,
perfil, comunidades y publicaciones, y revisión visual de escritorio/móvil.
Pendiente de aprobación independiente: proveedor de correo real y despliegue.

Siguiente desarrollo propuesto: completar comunidades y experiencia social principal
de la Fase 2, después de validar este fundamento con SQL aislado. No se comenzó esa fase.
