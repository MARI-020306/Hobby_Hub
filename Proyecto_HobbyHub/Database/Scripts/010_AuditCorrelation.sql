-- Prepared migration only. Do not execute without authorization.
-- Adds nullable metadata; historical rows retain NULL. No data deletion.
SET XACT_ABORT ON;
IF DB_NAME() <> N'HobbyHub_Development_Phase1C_20261008'
 THROW 51000, 'Select HobbyHub_Development_Phase1C_20261008. No schema changes performed.', 1;
IF OBJECT_ID(N'dbo.AuditoriasSeguridad', N'U') IS NULL
 THROW 51001, 'Expected dbo.AuditoriasSeguridad table is missing. Stop and review the target.', 1;
BEGIN TRY
BEGIN TRANSACTION;
IF COL_LENGTH('dbo.AuditoriasSeguridad', 'CorrelacionId') IS NULL
 ALTER TABLE dbo.AuditoriasSeguridad ADD CorrelacionId varchar(64) NULL;
IF COL_LENGTH('dbo.AuditoriasSeguridad', 'TipoRecurso') IS NULL
 ALTER TABLE dbo.AuditoriasSeguridad ADD TipoRecurso varchar(64) NULL;
IF COL_LENGTH('dbo.AuditoriasSeguridad', 'IdRecurso') IS NULL
 ALTER TABLE dbo.AuditoriasSeguridad ADD IdRecurso varchar(128) NULL;
COMMIT;
END TRY
BEGIN CATCH
 IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;
