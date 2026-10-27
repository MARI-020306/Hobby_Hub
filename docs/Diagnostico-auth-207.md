# Diagnóstico local de registro y autenticación (2026-10-20)

El esquema real consultado en `(localdb)\HobbyHubPhase1C_20261008`, base
`HobbyHub_Development_Phase1C_20261008`, tiene únicamente las seis columnas
originales de `dbo.AuditoriasSeguridad`. Faltan `CorrelacionId`, `TipoRecurso`
e `IdRecurso`. El modelo y HobbyHubContext mapean las tres propiedades.
El script incremental `010_AuditCorrelation.sql` está preparado pero no aplicado.
No es una propiedad equivocada de Usuario ni una relación con una clave sombra.

La prueba optativa AuthSchemaTests reconstruye la configuración Development
(archivos aislados, archivo local opcional, secretos de usuario y entorno), exige
la instancia/base esperadas y verifica el catálogo abierto. No imprime cadenas
de conexión. Contra SQL real se comprobaron por separado:

- AuthController.Registro con datos sintéticos: DbUpdateException / SQL 207,
  exactamente CorrelacionId, IdRecurso y TipoRecurso. SaveChanges incluye
  AccountCreated y falla al guardar la auditoría, dentro de la misma transacción.
- AccountSecurityService.LoginAsync con cuenta sintética inexistente: la consulta
  inicial funciona; EfSecurityAudit falla al guardar LoginDenied con SQL 207 y
  las mismas tres columnas. AuthController captura ese error y responde 503.

Las transacciones de diagnóstico se revierten. No se leyeron credenciales de
usuarios reales. El login histórico no puede reconstruirse con certeza: su log
anterior solo guardaba un mensaje genérico, sin excepción ni correlación.
No se afirma haber comprobado un login válido contra SQL después de reparar el
esquema: esa reparación requiere autorización y sigue pendiente.

## Reparación propuesta

Ejecutar exclusivamente `Proyecto_HobbyHub/Database/Scripts/010_AuditCorrelation.sql`
en la base indicada, después de autorización. Agrega tres columnas varchar NULL
(64, 64 y 128 caracteres), sin reescribir datos históricos, dentro de una transacción.
No hace falta ejecutar 001–009 ni crear otras columnas. No hay migraciones EF
automáticas al arrancar. No se ha modificado el esquema durante este diagnóstico.

## Cambios en código

PersistenceDiagnostics recorre la excepción SQL internamente y emite solamente
el tipo, número SQL, nombres de columnas reconocidos en el modelo, correlación
y estado HTTP previsto. Nunca emite mensajes crudos, SQL, parámetros o secretos.
Se usa también en el catch de login. Las respuestas al navegador son genéricas.

SecurityEvent (Attempt/ActionReturned/Failed) ya no lleva un StatusCode prematuro.
ActionReturned no significa éxito. RequestCompleted lleva el estado HTTP final
y un resultado funcional independiente: Rejected, Unavailable, Authenticated o
AccountCreated en estos flujos. Otros flujos sin evidencia explícita indican
Unconfirmed. SecurityAudit conserva la distinción entre guardado, rechazo y
transacción externa todavía pendiente.

## Logs y consulta

Archivos JSONL en:
`C:\Users\herna\Documents\Hobby_Hub\Proyecto_HobbyHub\.local\logs\hobbyhub-*.jsonl`.
También consola y salida Debug de Visual Studio. Rotación predeterminada: 5 MiB,
14 archivos y 14 días. Los logs técnicos no se insertan en una tabla.

Desde la raíz del checkout:

```powershell
Get-ChildItem .\Proyecto_HobbyHub\.local\logs\hobbyhub-*.jsonl |
    Select-String -SimpleMatch '4da1308d232f4580a34728fa15445a70'
```

Auditoría funcional en la misma base, tabla `dbo.AuditoriasSeguridad`:

```sql
SELECT TOP (100) IdAuditoria, FechaUtc, Accion, Exito
FROM dbo.AuditoriasSeguridad ORDER BY IdAuditoria DESC;
```

Después de aplicar 010 se puede filtrar por CorrelacionId. Antes, esa columna
no existe. Un guardado fallido no deja su auditoría en SQL porque se revierte.
Exito=0 puede ser un intento o rechazo; HTTP 200 no demuestra éxito funcional.

## Verificación

Compilación de la aplicación y proyectos de pruebas correcta. 46 pruebas de
autenticación MVC, seguridad de cuentas y revisión de peticiones pasaron; usan
almacenes falsos y no acreditan disponibilidad de SQL. Las 7 pruebas de logging
pasaron, incluyendo separación de intento/estado final y HTTP 200 rechazado.
Una prueba SQL optativa confirmó los dos errores anteriores y sus columnas.
El diagnóstico SQL es deliberadamente una prueba del esquema ANTERIOR a 010;
no debe usarse como criterio de éxito después de la actualización.

Pendiente: autorización/aplicación de 010, registro y login válidos de extremo
a extremo contra SQL y verificación de sus auditorías persistidas. No hubo
commit ni push.
