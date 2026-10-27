-- Baseline derived from HobbyHubContext; not a dump of the shared database.
-- Run manually ONLY on a new, empty development database. No DROP/DELETE/TRUNCATE.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'HobbyHub_Development'
   AND LEFT(DB_NAME(), LEN(N'HobbyHub_Development_')) <> N'HobbyHub_Development_'
    THROW 51000, 'Select an isolated HobbyHub_Development database before running this script.', 1;
IF EXISTS (SELECT 1 FROM sys.tables WHERE is_ms_shipped = 0)
    THROW 51002, 'Baseline requires an empty database. Existing tables must be inventoried, never replaced.', 1;

BEGIN TRY
    BEGIN TRANSACTION;
    CREATE TABLE dbo.Roles (
        IdRol INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Roles PRIMARY KEY,
        Nombre VARCHAR(30) NOT NULL CONSTRAINT UQ_Roles_Nombre UNIQUE
    );
    CREATE TABLE dbo.Usuarios (
        IdUsuario INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Usuarios PRIMARY KEY,
        Nombre VARCHAR(100) NOT NULL,
        Correo VARCHAR(150) NOT NULL CONSTRAINT UQ_Usuarios_Correo UNIQUE,
        Password VARCHAR(255) NOT NULL,
        RolId INT NOT NULL,
        Estado VARCHAR(20) NOT NULL CONSTRAINT DF_Usuarios_Estado DEFAULT ('Activo'),
        Celular VARCHAR(64) NULL,
        Direccion VARCHAR(255) NULL,
        FechaRegistro DATETIME NOT NULL CONSTRAINT DF_Usuarios_FechaRegistro DEFAULT (GETDATE()),
        CONSTRAINT FK_Usuarios_Roles FOREIGN KEY (RolId) REFERENCES dbo.Roles(IdRol)
    );
    CREATE TABLE dbo.Comunidades (
        IdComunidad INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Comunidades PRIMARY KEY,
        Nombre VARCHAR(100) NOT NULL CONSTRAINT UQ_Comunidades_Nombre UNIQUE,
        Descripcion VARCHAR(500) NULL,
        IdCreador INT NOT NULL,
        FechaCreacion DATETIME NOT NULL CONSTRAINT DF_Comunidades_FechaCreacion DEFAULT (GETDATE()),
        CONSTRAINT FK_Comunidades_Creador FOREIGN KEY (IdCreador) REFERENCES dbo.Usuarios(IdUsuario)
    );
    CREATE TABLE dbo.Publicaciones (
        IdPublicacion INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Publicaciones PRIMARY KEY,
        Contenido VARCHAR(2000) NOT NULL,
        IdUsuario INT NOT NULL,
        IdComunidad INT NOT NULL,
        Estado VARCHAR(20) NOT NULL CONSTRAINT DF_Publicaciones_Estado DEFAULT ('Visible'),
        FechaPublicacion DATETIME NOT NULL CONSTRAINT DF_Publicaciones_FechaPublicacion DEFAULT (GETDATE()),
        CONSTRAINT FK_Publicaciones_Usuario FOREIGN KEY (IdUsuario) REFERENCES dbo.Usuarios(IdUsuario),
        CONSTRAINT FK_Publicaciones_Comunidad FOREIGN KEY (IdComunidad) REFERENCES dbo.Comunidades(IdComunidad)
    );
    CREATE TABLE dbo.Comentarios (
        IdComentario INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Comentarios PRIMARY KEY,
        Contenido VARCHAR(1000) NOT NULL,
        IdUsuario INT NOT NULL,
        IdPublicacion INT NOT NULL,
        Estado VARCHAR(20) NOT NULL CONSTRAINT DF_Comentarios_Estado DEFAULT ('Visible'),
        FechaComentario DATETIME NOT NULL CONSTRAINT DF_Comentarios_FechaComentario DEFAULT (GETDATE()),
        CONSTRAINT FK_Comentarios_Usuario FOREIGN KEY (IdUsuario) REFERENCES dbo.Usuarios(IdUsuario),
        CONSTRAINT FK_Comentarios_Publicacion FOREIGN KEY (IdPublicacion) REFERENCES dbo.Publicaciones(IdPublicacion)
    );
    CREATE TABLE dbo.Reportes (
        IdReporte INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Reportes PRIMARY KEY,
        IdUsuarioReporta INT NOT NULL,
        IdPublicacion INT NULL,
        IdComentario INT NULL,
        IdUsuarioReportado INT NULL,
        Motivo VARCHAR(255) NOT NULL,
        Estado VARCHAR(20) NOT NULL CONSTRAINT DF_Reportes_Estado DEFAULT ('Pendiente'),
        FechaReporte DATETIME NOT NULL CONSTRAINT DF_Reportes_FechaReporte DEFAULT (GETDATE()),
        CONSTRAINT FK_Reportes_UsuarioReporta FOREIGN KEY (IdUsuarioReporta) REFERENCES dbo.Usuarios(IdUsuario),
        CONSTRAINT FK_Reportes_UsuarioReportado FOREIGN KEY (IdUsuarioReportado) REFERENCES dbo.Usuarios(IdUsuario),
        CONSTRAINT FK_Reportes_Publicacion FOREIGN KEY (IdPublicacion) REFERENCES dbo.Publicaciones(IdPublicacion),
        CONSTRAINT FK_Reportes_Comentario FOREIGN KEY (IdComentario) REFERENCES dbo.Comentarios(IdComentario)
    );
    CREATE INDEX IX_Usuarios_RolId ON dbo.Usuarios(RolId);
    CREATE INDEX IX_Comunidades_IdCreador ON dbo.Comunidades(IdCreador);
    CREATE INDEX IX_Publicaciones_IdUsuario ON dbo.Publicaciones(IdUsuario);
    CREATE INDEX IX_Publicaciones_IdComunidad ON dbo.Publicaciones(IdComunidad);
    CREATE INDEX IX_Comentarios_IdUsuario ON dbo.Comentarios(IdUsuario);
    CREATE INDEX IX_Comentarios_IdPublicacion ON dbo.Comentarios(IdPublicacion);
    CREATE INDEX IX_Reportes_IdUsuarioReporta ON dbo.Reportes(IdUsuarioReporta);
    CREATE INDEX IX_Reportes_IdUsuarioReportado ON dbo.Reportes(IdUsuarioReportado);
    CREATE INDEX IX_Reportes_IdPublicacion ON dbo.Reportes(IdPublicacion);
    CREATE INDEX IX_Reportes_IdComentario ON dbo.Reportes(IdComentario);
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
