-- Only the newly created, authorized database. Never run on any other database.
SET XACT_ABORT ON;
SET NOCOUNT ON;
IF DB_NAME() <> N'HobbyHub_Development_Phase1C_20261008'
 THROW 51000, 'Select the authorized isolated HobbyHub database.', 1;
BEGIN TRY
 BEGIN TRANSACTION;
 IF COL_LENGTH('dbo.Usuarios','Biografia') IS NULL ALTER TABLE dbo.Usuarios ADD Biografia VARCHAR(500) NOT NULL DEFAULT '';
 IF COL_LENGTH('dbo.Usuarios','Intereses') IS NULL ALTER TABLE dbo.Usuarios ADD Intereses VARCHAR(200) NOT NULL DEFAULT '';
 IF COL_LENGTH('dbo.Usuarios','Privado') IS NULL ALTER TABLE dbo.Usuarios ADD Privado BIT NOT NULL DEFAULT 0;
 IF COL_LENGTH('dbo.Usuarios','PermitirMensajes') IS NULL ALTER TABLE dbo.Usuarios ADD PermitirMensajes BIT NOT NULL DEFAULT 1;
 IF COL_LENGTH('dbo.Usuarios','RecibirNotificaciones') IS NULL ALTER TABLE dbo.Usuarios ADD RecibirNotificaciones BIT NOT NULL DEFAULT 1;
 IF COL_LENGTH('dbo.Comunidades','Privada') IS NULL ALTER TABLE dbo.Comunidades ADD Privada BIT NOT NULL DEFAULT 0;
 IF COL_LENGTH('dbo.Comunidades','Reglas') IS NULL ALTER TABLE dbo.Comunidades ADD Reglas VARCHAR(2000) NOT NULL DEFAULT '';
 IF COL_LENGTH('dbo.MiembrosComunidad','Estado') IS NULL ALTER TABLE dbo.MiembrosComunidad ADD Estado VARCHAR(20) NOT NULL DEFAULT 'Aceptado';
 IF COL_LENGTH('dbo.Comentarios','IdComentarioPadre') IS NULL ALTER TABLE dbo.Comentarios ADD IdComentarioPadre INT NULL REFERENCES dbo.Comentarios(IdComentario);
 IF COL_LENGTH('dbo.Reportes','Resolucion') IS NULL ALTER TABLE dbo.Reportes ADD Resolucion VARCHAR(500) NULL;
 IF OBJECT_ID('dbo.Seguimientos') IS NULL CREATE TABLE dbo.Seguimientos (
   IdSeguidor INT NOT NULL REFERENCES dbo.Usuarios(IdUsuario), IdSeguido INT NOT NULL REFERENCES dbo.Usuarios(IdUsuario),
   Aceptado BIT NOT NULL, FechaUtc DATETIME2 NOT NULL, PRIMARY KEY(IdSeguidor,IdSeguido), CHECK(IdSeguidor <> IdSeguido));
 IF OBJECT_ID('dbo.PublicacionesGuardadas') IS NULL CREATE TABLE dbo.PublicacionesGuardadas (
   IdUsuario INT NOT NULL REFERENCES dbo.Usuarios(IdUsuario), IdPublicacion INT NOT NULL REFERENCES dbo.Publicaciones(IdPublicacion),
   FechaUtc DATETIME2 NOT NULL, PRIMARY KEY(IdUsuario,IdPublicacion));
 IF OBJECT_ID('dbo.PublicacionImagenes') IS NULL CREATE TABLE dbo.PublicacionImagenes (
   IdImagen UNIQUEIDENTIFIER NOT NULL PRIMARY KEY, IdPublicacion INT NOT NULL REFERENCES dbo.Publicaciones(IdPublicacion),
   Archivo VARCHAR(100) NOT NULL, Tipo VARCHAR(20) NOT NULL, Orden INT NOT NULL);
 IF OBJECT_ID('dbo.MensajesPrivados') IS NULL BEGIN
   CREATE TABLE dbo.MensajesPrivados (
    IdMensaje BIGINT IDENTITY NOT NULL PRIMARY KEY, IdRemitente INT NOT NULL REFERENCES dbo.Usuarios(IdUsuario),
    IdDestinatario INT NOT NULL REFERENCES dbo.Usuarios(IdUsuario), Contenido VARCHAR(2000) NOT NULL,
    FechaUtc DATETIME2 NOT NULL, LeidoUtc DATETIME2 NULL, CHECK(IdRemitente <> IdDestinatario));
   CREATE INDEX IX_MensajesPrivados_IdDestinatario_FechaUtc ON dbo.MensajesPrivados(IdDestinatario,FechaUtc);
 END;
 IF OBJECT_ID('dbo.Notificaciones') IS NULL BEGIN
   CREATE TABLE dbo.Notificaciones (
     IdNotificacion BIGINT IDENTITY NOT NULL PRIMARY KEY, IdUsuario INT NOT NULL REFERENCES dbo.Usuarios(IdUsuario),
     Tipo VARCHAR(40) NOT NULL, IdRecurso INT NULL, Leida BIT NOT NULL, FechaUtc DATETIME2 NOT NULL);
   CREATE INDEX IX_Notificaciones_IdUsuario_Leida_FechaUtc ON dbo.Notificaciones(IdUsuario,Leida,FechaUtc);
 END;
 IF NOT EXISTS(SELECT 1 FROM dbo.Roles WHERE Nombre='Moderador') INSERT dbo.Roles(Nombre) VALUES('Moderador');
 IF NOT EXISTS(SELECT 1 FROM dbo.Roles WHERE Nombre='Administrador') INSERT dbo.Roles(Nombre) VALUES('Administrador');
 COMMIT TRANSACTION;
END TRY
BEGIN CATCH
 IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
 THROW;
END CATCH;
