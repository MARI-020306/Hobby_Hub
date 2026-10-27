-- Optional development-only seed. Does not create users or privileged accounts.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'HobbyHub_Development'
   AND LEFT(DB_NAME(), LEN(N'HobbyHub_Development_')) <> N'HobbyHub_Development_'
    THROW 51000, 'Select an isolated HobbyHub_Development database before running this script.', 1;
IF OBJECT_ID(N'dbo.Roles', N'U') IS NULL
    THROW 51001, 'Roles table is missing. Review scripts 001 and 002 first.', 1;
BEGIN TRY
    BEGIN TRANSACTION;
    IF NOT EXISTS (SELECT 1 FROM dbo.Roles WITH (UPDLOCK, HOLDLOCK) WHERE Nombre = 'Usuario')
        INSERT INTO dbo.Roles (Nombre) VALUES ('Usuario');
    IF NOT EXISTS (SELECT 1 FROM dbo.Roles WITH (UPDLOCK, HOLDLOCK) WHERE Nombre = 'Creador')
        INSERT INTO dbo.Roles (Nombre) VALUES ('Creador');
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
