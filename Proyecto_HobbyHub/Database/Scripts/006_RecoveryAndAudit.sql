-- Review only until explicitly authorized. Additive schema for isolated development.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'HobbyHub_Development'
   AND LEFT(DB_NAME(), LEN(N'HobbyHub_Development_')) <> N'HobbyHub_Development_'
    THROW 51000, 'Select an isolated HobbyHub_Development database before running this script.', 1;
IF OBJECT_ID(N'dbo.Usuarios', N'U') IS NULL OR COL_LENGTH(N'dbo.Usuarios', N'VersionSeguridad') IS NULL
    THROW 51001, 'Review and apply scripts 001-003 and 005 first.', 1;
BEGIN TRY
    BEGIN TRANSACTION;
    IF OBJECT_ID(N'dbo.RecuperacionesCuenta', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.RecuperacionesCuenta (
            IdRecuperacion UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_RecuperacionesCuenta PRIMARY KEY,
            IdUsuario INT NOT NULL,
            TokenHash VARCHAR(64) NOT NULL,
            VersionSeguridad UNIQUEIDENTIFIER NOT NULL,
            CreadaUtc DATETIME2 NOT NULL,
            ExpiraUtc DATETIME2 NOT NULL,
            ConsumidaUtc DATETIME2 NULL,
            CONSTRAINT FK_RecuperacionesCuenta_Usuarios FOREIGN KEY (IdUsuario) REFERENCES dbo.Usuarios(IdUsuario),
            CONSTRAINT CK_RecuperacionesCuenta_Expiry CHECK (ExpiraUtc > CreadaUtc)
        );
        CREATE UNIQUE INDEX IX_RecuperacionesCuenta_TokenHash ON dbo.RecuperacionesCuenta(TokenHash);
        CREATE INDEX IX_RecuperacionesCuenta_IdUsuario_ConsumidaUtc ON dbo.RecuperacionesCuenta(IdUsuario, ConsumidaUtc);
    END;
    IF OBJECT_ID(N'dbo.AuditoriasSeguridad', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.AuditoriasSeguridad (
            IdAuditoria BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuditoriasSeguridad PRIMARY KEY,
            IdActor INT NULL,
            IdObjetivo INT NULL,
            Accion VARCHAR(64) NOT NULL,
            Exito BIT NOT NULL,
            FechaUtc DATETIME2 NOT NULL
        );
        -- Actor/target are historical identifiers, intentionally without cascading FKs.
        CREATE INDEX IX_AuditoriasSeguridad_FechaUtc_IdObjetivo ON dbo.AuditoriasSeguridad(FechaUtc, IdObjetivo);
    END;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
