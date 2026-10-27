SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'HobbyHub_Development_Phase1C_20261008'
 THROW 51000, 'Select the authorized isolated HobbyHub database.', 1;
BEGIN TRY
 BEGIN TRANSACTION;
 IF OBJECT_ID('dbo.BloqueosUsuario','U') IS NULL
 CREATE TABLE dbo.BloqueosUsuario (
  IdActor INT NOT NULL, IdBloqueado INT NOT NULL, FechaUtc DATETIME2 NOT NULL,
  CONSTRAINT PK_BloqueosUsuario PRIMARY KEY (IdActor,IdBloqueado),
  CONSTRAINT FK_Bloqueos_Actor FOREIGN KEY (IdActor) REFERENCES dbo.Usuarios(IdUsuario),
  CONSTRAINT FK_Bloqueos_Destino FOREIGN KEY (IdBloqueado) REFERENCES dbo.Usuarios(IdUsuario),
  CONSTRAINT CK_Bloqueos_NoSelf CHECK (IdActor<>IdBloqueado));
 IF COL_LENGTH('dbo.MensajesPrivados','Estado') IS NULL
  ALTER TABLE dbo.MensajesPrivados ADD Estado VARCHAR(15) NOT NULL CONSTRAINT DF_Mensajes_Estado DEFAULT 'Visible';
 IF COL_LENGTH('dbo.Reportes','IdMensaje') IS NULL
 BEGIN
  ALTER TABLE dbo.Reportes ADD IdMensaje BIGINT NULL;
  ALTER TABLE dbo.Reportes ADD CONSTRAINT FK_Reportes_Mensaje FOREIGN KEY (IdMensaje) REFERENCES dbo.MensajesPrivados(IdMensaje);
 END;
 COMMIT TRANSACTION;
END TRY
BEGIN CATCH
 IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;
