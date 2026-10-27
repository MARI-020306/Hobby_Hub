using System.Text.Json;
using Proyecto_HobbyHub.Models;
using Microsoft.EntityFrameworkCore;

namespace Proyecto_HobbyHub.Tests;
public class VisualTestSeed
{
    [SqlFact]
    public async Task CreateOnlySyntheticBrowserRolesInNewVisualDatabase()
    {
        string? name=Environment.GetEnvironmentVariable("HOBBYHUB_VISUAL_TEST_DATABASE");
        if(name is null)return;
        Assert.Matches("^HobbyHub_Development_Visual_20261008_[a-f0-9]{8}$",name);
        string file=Path.Combine(SchemaSafetyTests.RepositoryRoot(),".local","visual-privileged-accounts.json");
        if(File.Exists(file)){
            using var existing=JsonDocument.Parse(await File.ReadAllTextAsync(file));
            Assert.Equal(name,existing.RootElement.GetProperty("Database").GetString());
            return;
        }
        using var db=new HobbyHubContext(new DbContextOptionsBuilder<HobbyHubContext>().UseSqlServer(SqlApplicationFactory.Connection).Options);
        var accounts=new List<object>();
        foreach(string role in new[]{"Administrador","Moderador"}){
            string email=$"visual-{Guid.NewGuid():N}@example.test",password=Guid.NewGuid().ToString("N");
            var user=new Usuario{Nombre="Visual sintético "+role,Correo=SecurityHelper.HashEmail(email),Password=SecurityHelper.HashPassword(password),RolId=await db.Roles.Where(r=>r.Nombre==role).Select(r=>r.IdRol).SingleAsync(),Estado="Activo",FechaRegistro=DateTime.Now};
            db.Usuarios.Add(user);await db.SaveChangesAsync();accounts.Add(new{Role=role,Id=user.IdUsuario,Email=email,Password=password});
        }
        await File.WriteAllTextAsync(file,JsonSerializer.Serialize(new{Database=name,Accounts=accounts}));
    }
}
