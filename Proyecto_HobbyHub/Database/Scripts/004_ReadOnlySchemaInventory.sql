-- Read-only metadata inventory. No user rows, passwords or configuration values.
-- Review output against Models/HobbyHubContext.cs before proposing schema changes.
SET NOCOUNT ON;
SELECT s.name AS SchemaName, t.name AS TableName, c.column_id AS ColumnOrder,
       c.name AS ColumnName, ty.name AS SqlType, c.max_length AS MaxLengthBytes,
       c.precision AS NumericPrecision, c.scale AS NumericScale,
       c.is_nullable AS IsNullable, c.is_identity AS IsIdentity,
       CASE WHEN c.name IN ('Password','Correo','Celular','Direccion','Url','ImagenPortada','ImagenUrl','IdSesion','VersionSeguridad','TokenHash')
            THEN CASE WHEN dc.object_id IS NULL THEN NULL ELSE '<redacted>' END
            ELSE dc.definition END AS DefaultExpression
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.columns c ON c.object_id = t.object_id
JOIN sys.types ty ON ty.user_type_id = c.user_type_id
LEFT JOIN sys.default_constraints dc ON dc.object_id = c.default_object_id
WHERE s.name = 'dbo' AND t.name IN
    ('Usuarios','Roles','Comunidades','MiembrosComunidad','Publicaciones',
     'Comentarios','PublicacionLikes','UsuarioImagenes','Reportes','SesionesUsuario','RecuperacionesCuenta','AuditoriasSeguridad','Seguimientos','PublicacionesGuardadas','PublicacionImagenes','MensajesPrivados','Notificaciones','BloqueosUsuario')
ORDER BY t.name, c.column_id;

SELECT t.name AS TableName, i.name AS IndexName, i.is_primary_key AS IsPrimaryKey,
       i.is_unique AS IsUnique, ic.key_ordinal AS KeyOrder, c.name AS ColumnName
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.indexes i ON i.object_id = t.object_id
JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE s.name = 'dbo' AND t.name IN
    ('Usuarios','Roles','Comunidades','MiembrosComunidad','Publicaciones',
     'Comentarios','PublicacionLikes','UsuarioImagenes','Reportes','SesionesUsuario','RecuperacionesCuenta','AuditoriasSeguridad','Seguimientos','PublicacionesGuardadas','PublicacionImagenes','MensajesPrivados','Notificaciones','BloqueosUsuario')
ORDER BY t.name, i.name, ic.key_ordinal;

SELECT fk.name AS ForeignKeyName, OBJECT_NAME(fk.parent_object_id) AS SourceTable,
       pc.name AS SourceColumn, OBJECT_NAME(fk.referenced_object_id) AS TargetTable,
       rc.name AS TargetColumn, fk.delete_referential_action_desc AS OnDelete,
       fk.is_disabled AS IsDisabled, fk.is_not_trusted AS IsNotTrusted
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fc ON fc.constraint_object_id = fk.object_id
JOIN sys.columns pc ON pc.object_id = fc.parent_object_id AND pc.column_id = fc.parent_column_id
JOIN sys.columns rc ON rc.object_id = fc.referenced_object_id AND rc.column_id = fc.referenced_column_id
WHERE OBJECT_SCHEMA_NAME(fk.parent_object_id) = 'dbo'
  AND OBJECT_NAME(fk.parent_object_id) IN
    ('Usuarios','Roles','Comunidades','MiembrosComunidad','Publicaciones',
     'Comentarios','PublicacionLikes','UsuarioImagenes','Reportes','SesionesUsuario','RecuperacionesCuenta','AuditoriasSeguridad','Seguimientos','PublicacionesGuardadas','PublicacionImagenes','MensajesPrivados','Notificaciones','BloqueosUsuario')
ORDER BY SourceTable, ForeignKeyName;
