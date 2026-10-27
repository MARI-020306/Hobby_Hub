# Base SQL aislada de HobbyHub — estado vigente

Actualizado: 2026-10-08. La autorización integral permitió crear y preparar la instancia nueva **HobbyHubPhase1C_20261008** y la base nueva **HobbyHub_Development_Phase1C_20261008**, después de comprobar que no existían bajo el usuario Windows propietario.

La preparación inicial se ejecutó y los módulos sociales se añadieron únicamente a esa base. **No ejecutar nuevamente el script de preparación inicial en este equipo**: rechaza instancias existentes y 001 exige una base vacía. No se utilizaron MSSQLLocalDB, bases remotas ni datos de otras aplicaciones.

## Instancia y configuración

- LocalDB dedicado, motor 15.0.4382.1, autenticación Windows.
- Development carga appsettings.Development.Isolated.json; los archivos personales siguen ignorados por Git.
- El arranque valida exactamente el servidor (localdb)\HobbyHubPhase1C_20261008 y la base HobbyHub_Development_Phase1C_20261008.
- No hay alternativa a la conexión compartida. Production/otros entornos se rechazan antes de seleccionar esa conexión.
- Testing usa aislamiento; la suite offline impide abrir EF. Las pruebas SQL requieren HOBBYHUB_SQL_TESTS=1 y fuerzan un destino constante a la base dedicada.
- Establecer ASPNETCORE_ENVIRONMENT=Development y DOTNET_ENVIRONMENT=Development. Los perfiles de VS Code/launchSettings ya lo hacen.
- La aplicación no crea/altera/migra el esquema al abrir páginas ni al arrancar. El aprovisionamiento SQL es independiente y revisable.

## Scripts aplicados

| Orden | Script | Consecuencia |
|---|---|---|
| 1 | 001_EmptyDevelopmentBaseline.sql | Seis tablas iniciales a partir del modelo existente; solo base sin tablas de usuario. |
| 2 | 002_ExistingFeatureExtensions.sql | Columnas de categoría/portada/imagen, membresías, likes e imágenes de perfil anteriormente creadas desde controladores. |
| 3 | 003_DevelopmentRoles.sql | Roles ordinarios Usuario y Creador; no cuentas ni contraseñas predeterminadas. |
| 4 | 005_AccountSessionsSecurity.sql | Versión de seguridad, intentos/bloqueo y sesiones revocables. |
| 5 | 006_RecoveryAndAudit.sql | Tokens de recuperación hasheados/caducables y auditoría sin secretos. |
| 6 | 007_SocialModules.sql | Seguimientos, guardados, galerías, mensajes, notificaciones; privacidad, preferencias, reglas, respuestas y roles Moderador/Admin. |
| 7 | 008_ModerationConcurrency.sql | Caducidad de suspensión y rowversion para resolución concurrente de reportes. |
| 8 | 009_PersonalBlocksAndMessageReports.sql | Bloqueos personales, estado de mensajes y vínculo del reporte al mensaje recibido. |
| 9 | 004_ReadOnlySchemaInventory.sql | Inventario exclusivamente de metadatos; no modifica esquema ni consulta filas de usuarios. Actualizado para las 18 tablas. |

El inventario 004 también se ejecutó después de la preparación inicial de las fases anteriores, antes de los scripts sociales. Los cambios posteriores se comprobaron por metadatos y por las pruebas SQL reales.

Los scripts de cambios usan transacciones, rechazan bases no permitidas y no contienen DROP, TRUNCATE ni DELETE. 007–009 exigen el nombre exacto autorizado. Si falla un script, revierte su propia transacción; los anteriores siguen aplicados. No hay limpieza automática ni recreación de bases.

Prepare-IsolatedDevelopment.ps1 requiere -Approved, comprueba nombres y verifica que LocalDB haya confirmado la creación antes de arrancar o ejecutar SQL. Esto corrige el caso de herramientas LocalDB que imprimen un error de registro sin devolver un código de salida útil. **No reutiliza instancias existentes**, incluso si el nombre coincide con el configurado.

Para otro equipo sin esa instancia, revisar previamente SqlLocalDB info y versiones instaladas. El procedimiento preparado utiliza LocalDB 15.0 y crea la base solo dentro de una instancia recién confirmada. Si el nombre ya está ocupado, detener exclusivamente ese aprovisionamiento; no sobrescribir ni utilizar otra instancia existente.

## Estado comprobado

- 18 tablas de usuario y cuatro roles.
- Cero claves foráneas deshabilitadas o no confiables.
- Persistencia verificada con múltiples cuentas sintéticas: autenticación, comunidades, galerías, interacción, seguidores, mensajes, notificaciones, reportes, moderación, permisos, recuperación concurrente y revocación.
- La suite crea datos sintéticos nuevos por ejecución y conserva registros previos. No elimina tablas ni reinicializa la base.
- Existe una cuenta administrativa sintética con contraseña aleatoria en el archivo local ignorado .local/synthetic-administrator.json; su contenido no se publica.
- Las imágenes y el buzón cifrado se guardan fuera de wwwroot. SQL guarda referencias a imágenes y hashes de tokens, nunca archivos de recuperación sin cifrar.

## Seguridad y evolución

Los cambios de contraseña, rol o estado efectuados con SaveChanges rotan VersionSeguridad y se auditan. Las operaciones masivas futuras con ExecuteUpdate/SQL deben mantener esa rotación y su auditoría dentro de la misma transacción. El restablecimiento de contraseña consume el token y revoca sesiones de forma atómica. Al expirar una suspensión se reactiva la cuenta con una versión nueva: no se habilitan cookies anteriores.

El DbContext rechaza la edición/eliminación de auditoría mediante SaveChanges. LocalDB usa al propietario Windows y **no simula una cuenta de aplicación sin DDL ni permisos UPDATE/DELETE sobre auditoría**. Antes de producción habrá que provisionar esos permisos en una instancia autorizada y separar aprovisionamiento de ejecución. No se instaló ni configuró un servidor de producción.

No hay historial de migraciones EF inventado. Los scripts son la línea base versionada. Antes de adoptar migraciones EF habrá que reconciliar nombres de índices/restricciones heredados y establecer una línea base explícita; no ejecutar dotnet ef database update a ciegas.

Las instrucciones de ejecución y pruebas están en README.md de la raíz; el informe funcional vigente está en docs/Desarrollo-integral.md. Los documentos Fase-1A, Fase-1B y Fase-1C conservan el historial anterior a la autorización de ejecutar SQL.


## Revisión final: base separada de pruebas

La revisión de entrega utiliza exclusivamente `HobbyHub_Development_Visual_20261008_6c76fa09`, en la instancia local `HobbyHubPhase1C_20261008`. No utiliza la base de desarrollo original, bases compartidas ni el servidor del usuario en 5062. La configuración está en `.local/visual-test-database.json`, excluida de Git. El servidor de revisión escucha en 5088, con cookies y claves separadas.

Para las pruebas SQL hay que establecer **ambas** variables: `HOBBYHUB_SQL_TESTS=1` y `HOBBYHUB_VISUAL_TEST_DATABASE` con el nombre de la base de pruebas. Consulte el README raíz para los comandos. La suite comprueba la correspondencia de sus cuentas sintéticas con esa base y conserva los datos existentes. La revisión final agregó dos regresiones SQL: paginación de comunidades y respuestas de error/validación. No requiere cambios de esquema.

En otro equipo, `tools/Prepare-VisualTestDatabase.ps1` prepara una base nueva con sufijo aleatorio y rechaza una base existente. No se debe ejecutarlo repetidamente para revisar el mismo entorno. Los resultados vigentes están en `docs/Informe-final-entrega.md`; los documentos anteriores conservan su contexto histórico.
