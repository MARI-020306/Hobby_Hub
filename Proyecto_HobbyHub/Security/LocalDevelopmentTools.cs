using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Proyecto_HobbyHub.Models;

namespace Proyecto_HobbyHub.Security;

public static class LocalDevelopmentTools
{
    public static async Task<bool> Run(WebApplication app)
    {
        bool setup=app.Configuration.GetValue<bool>("LocalSetup:CreateAdministrator");
        string? export=app.Configuration["LocalMailbox:Export"];
        if(!setup&&export is null)return false;
        if(!app.Environment.IsDevelopment())throw new InvalidOperationException("Herramientas disponibles únicamente en desarrollo local.");
        string directory=Path.Combine(app.Environment.ContentRootPath,".local");Directory.CreateDirectory(directory);
        if(export is not null) {
            if(!Guid.TryParseExact(export,"N",out var id))throw new InvalidOperationException("Indica el identificador del archivo de buzón local.");
            string path=Path.Combine(directory,"mailbox",id.ToString("N")+".mail");
            string content=app.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("HobbyHub.LocalRecoveryMailbox.v1").Unprotect(await File.ReadAllTextAsync(path));
            await File.WriteAllTextAsync(Path.Combine(directory,"mailbox",id.ToString("N")+".local.json"),content);
            Console.WriteLine("Mensaje exportado únicamente dentro de .local/mailbox. Su contenido es sensible y no debe compartirse.");return true;
        }
        using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<HobbyHubContext>();
        var connection=new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(db.Database.GetConnectionString());
        if(connection.DataSource!="(localdb)\\HobbyHubPhase1C_20261008"||connection.InitialCatalog!="HobbyHub_Development_Phase1C_20261008")
            throw new InvalidOperationException("La creación sintética exige exactamente la instancia y base aisladas autorizadas.");
        string destination=Path.Combine(directory,"synthetic-administrator.json");
        if(File.Exists(destination))throw new InvalidOperationException("Ya existe un archivo de administrador local; no se sobrescribirá.");
        string email=$"local-admin-{Guid.NewGuid():N}@example.test",password=Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var role=await db.Roles.SingleAsync(r=>r.Nombre=="Administrador");
        db.Usuarios.Add(new(){Nombre="Administrador sintético local",Correo=SecurityHelper.HashEmail(email),Password=SecurityHelper.HashPassword(password),RolId=role.IdRol,Estado="Activo",FechaRegistro=DateTime.Now});
        await db.SaveChangesAsync();await File.WriteAllTextAsync(destination,JsonSerializer.Serialize(new{Email=email,Password=password}));
        Console.WriteLine("Cuenta sintética creada solo en SQL aislado. Sus datos se guardaron en .local/synthetic-administrator.json; no se imprimen ni versionan.");return true;
    }
}
