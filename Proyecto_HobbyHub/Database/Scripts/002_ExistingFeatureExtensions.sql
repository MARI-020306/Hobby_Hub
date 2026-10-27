-- Manual execution only. Never invoked by the application.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'HobbyHub_Development'
   AND LEFT(DB_NAME(), LEN(N'HobbyHub_Development_')) <> N'HobbyHub_Development_'
    THROW 51000, 'Select an isolated HobbyHub_Development database before running this script.', 1;

-- Additive replacement for the schema helpers removed from the controllers.
-- Base tables must already match 001 or the reviewed existing schema.
IF OBJECT_ID(N'dbo.Usuarios', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Comunidades', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Publicaciones', N'U') IS NULL
    THROW 51001, 'Required base tables are missing. Review script 001 first.', 1;

BEGIN TRY
    BEGIN TRANSACTION;
    IF COL_LENGTH(N'dbo.Comunidades', N'Categoria') IS NULL
        ALTER TABLE [dbo].[Comunidades] ADD [Categoria] VARCHAR(50) NULL;

    IF COL_LENGTH(N'dbo.Comunidades', N'ImagenPortada') IS NULL
        ALTER TABLE [dbo].[Comunidades] ADD [ImagenPortada] VARCHAR(500) NULL;

    IF COL_LENGTH(N'dbo.Publicaciones', N'ImagenUrl') IS NULL
        ALTER TABLE [dbo].[Publicaciones] ADD [ImagenUrl] VARCHAR(500) NULL;

    IF OBJECT_ID(N'[dbo].[MiembrosComunidad]', N'U') IS NULL
    BEGIN
        CREATE TABLE [dbo].[MiembrosComunidad]
        (
            [IdComunidad] INT NOT NULL,
            [IdUsuario] INT NOT NULL,
            [FechaUnion] DATETIME NOT NULL
                CONSTRAINT [DF_MiembrosComunidad_FechaUnion] DEFAULT (GETDATE()),
            CONSTRAINT [PK_MiembrosComunidad] PRIMARY KEY ([IdComunidad], [IdUsuario]),
            CONSTRAINT [FK_MiembrosComunidad_Comunidades]
                FOREIGN KEY ([IdComunidad]) REFERENCES [dbo].[Comunidades]([IdComunidad]) ON DELETE CASCADE,
            CONSTRAINT [FK_MiembrosComunidad_Usuarios]
                FOREIGN KEY ([IdUsuario]) REFERENCES [dbo].[Usuarios]([IdUsuario]) ON DELETE CASCADE
        );
    END

    IF OBJECT_ID(N'[dbo].[PublicacionLikes]', N'U') IS NULL
    BEGIN
        CREATE TABLE [dbo].[PublicacionLikes]
        (
            [IdLike] INT IDENTITY(1, 1) NOT NULL,
            [IdPublicacion] INT NOT NULL,
            [IdUsuario] INT NOT NULL,
            [FechaLike] DATETIME NOT NULL
                CONSTRAINT [DF_PublicacionLikes_FechaLike] DEFAULT (GETDATE()),
            CONSTRAINT [PK_PublicacionLikes] PRIMARY KEY ([IdLike]),
            CONSTRAINT [UQ_PublicacionLikes_Publicacion_Usuario]
                UNIQUE ([IdPublicacion], [IdUsuario]),
            CONSTRAINT [FK_PublicacionLikes_Publicaciones]
                FOREIGN KEY ([IdPublicacion]) REFERENCES [dbo].[Publicaciones]([IdPublicacion]),
            CONSTRAINT [FK_PublicacionLikes_Usuarios]
                FOREIGN KEY ([IdUsuario]) REFERENCES [dbo].[Usuarios]([IdUsuario])
        );
    END

    IF OBJECT_ID(N'[dbo].[UsuarioImagenes]', N'U') IS NULL
    BEGIN
        CREATE TABLE [dbo].[UsuarioImagenes]
        (
            [IdImagen] INT IDENTITY(1, 1) NOT NULL,
            [IdUsuario] INT NOT NULL,
            [Url] VARCHAR(500) NOT NULL,
            [Tipo] VARCHAR(20) NOT NULL,
            [EsPrincipal] BIT NOT NULL
                CONSTRAINT [DF_UsuarioImagenes_EsPrincipal] DEFAULT ((1)),
            [FechaSubida] DATETIME NOT NULL
                CONSTRAINT [DF_UsuarioImagenes_FechaSubida] DEFAULT (GETDATE()),
            CONSTRAINT [PK_UsuarioImagenes] PRIMARY KEY ([IdImagen]),
            CONSTRAINT [FK_UsuarioImagenes_Usuarios]
                FOREIGN KEY ([IdUsuario]) REFERENCES [dbo].[Usuarios]([IdUsuario])
        );
    END
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
