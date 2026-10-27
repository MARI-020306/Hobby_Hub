-- Manual review/execution only, against an isolated development database.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'HobbyHub_Development'
   AND LEFT(DB_NAME(), LEN(N'HobbyHub_Development_')) <> N'HobbyHub_Development_'
    THROW 51000, 'Select an isolated HobbyHub_Development database before running this script.', 1;
IF OBJECT_ID(N'dbo.Usuarios', N'U') IS NULL
    THROW 51001, 'Required Usuarios table is missing. Review scripts 001-003 first.', 1;
BEGIN TRY
    BEGIN TRANSACTION;
    IF COL_LENGTH(N'dbo.Usuarios', N'VersionSeguridad') IS NULL
        ALTER TABLE dbo.Usuarios ADD VersionSeguridad UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_Usuarios_VersionSeguridad DEFAULT NEWID() WITH VALUES;
    IF COL_LENGTH(N'dbo.Usuarios', N'IntentosFallidos') IS NULL
        ALTER TABLE dbo.Usuarios ADD IntentosFallidos INT NOT NULL
            CONSTRAINT DF_Usuarios_IntentosFallidos DEFAULT 0 WITH VALUES;
    IF COL_LENGTH(N'dbo.Usuarios', N'BloqueoHastaUtc') IS NULL
        ALTER TABLE dbo.Usuarios ADD BloqueoHastaUtc DATETIME2 NULL;
    IF OBJECT_ID(N'dbo.SesionesUsuario', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.SesionesUsuario (
            IdSesion UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SesionesUsuario PRIMARY KEY,
            IdUsuario INT NOT NULL,
            VersionSeguridad UNIQUEIDENTIFIER NOT NULL,
            FechaCreacionUtc DATETIME2 NOT NULL,
            ExpiraUtc DATETIME2 NOT NULL,
            RevocadaUtc DATETIME2 NULL,
            CONSTRAINT FK_SesionesUsuario_Usuarios FOREIGN KEY (IdUsuario)
                REFERENCES dbo.Usuarios (IdUsuario),
            CONSTRAINT CK_SesionesUsuario_Expiry CHECK (ExpiraUtc > FechaCreacionUtc)
        );
        CREATE INDEX IX_SesionesUsuario_IdUsuario_RevocadaUtc_ExpiraUtc
            ON dbo.SesionesUsuario (IdUsuario, RevocadaUtc, ExpiraUtc);
    END;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
