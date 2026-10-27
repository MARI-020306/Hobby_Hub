using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Proyecto_HobbyHub.Configuration;
using Proyecto_HobbyHub.Models;
using Proyecto_HobbyHub.Security;

namespace Proyecto_HobbyHub.Tests;

public sealed class SqlFactAttribute : FactAttribute
{
    public SqlFactAttribute() { if(Environment.GetEnvironmentVariable("HOBBYHUB_SQL_TESTS")!="1") Skip="Explicit isolated SQL test opt-in required."; }
}
public sealed record SyntheticAccount(int Id,string Email,string Password,HttpClient Client);
public sealed class SqlApplicationFactory : WebApplicationFactory<Program>
{
    public static string Connection {
        get {
            string database=Environment.GetEnvironmentVariable("HOBBYHUB_VISUAL_TEST_DATABASE")??"HobbyHub_Development_Phase1C_20261008";
            if(database!="HobbyHub_Development_Phase1C_20261008"&&!Regex.IsMatch(database,"^HobbyHub_Development_Visual_20261008_[a-f0-9]{8}$"))throw new InvalidOperationException("Invalid isolated test database.");
            return $"Server=(localdb)\\HobbyHubPhase1C_20261008;Database={database};Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=5";
        }
    }
    public string Run {get;}=Guid.NewGuid().ToString("N")[..10];
    public MemoryRecoveryMailbox Mailbox {get;}=new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");builder.ConfigureLogging(l=>l.ClearProviders());
        builder.ConfigureTestServices(services=>{
            services.RemoveAll<HobbyHubContext>();services.RemoveAll<DbContextOptions<HobbyHubContext>>();
            services.AddScoped<DbContextOptions<HobbyHubContext>>(_=>new DbContextOptionsBuilder<HobbyHubContext>().UseSqlServer(Connection).Options);
            services.AddScoped<HobbyHubContext>();
            services.RemoveAll<IRecoveryMailSender>();services.AddSingleton<IRecoveryMailSender>(Mailbox);
        });
    }
    public HttpClient Client()=>CreateClient(new WebApplicationFactoryClientOptions{BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false});
    public async Task<SyntheticAccount> Account(string role="Usuario",bool registration=false)
    {
        string email=$"integration-{Run}-{Guid.NewGuid():N}@example.test",password=Guid.NewGuid().ToString("N");var client=Client();int id;
        using(var scope=Services.CreateScope()) {
            var db=scope.ServiceProvider.GetRequiredService<HobbyHubContext>();
            Assert.Equal(new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(Connection).InitialCatalog,db.Database.GetDbConnection().Database);
            var r=await db.Roles.SingleAsync(r=>r.Nombre==role);
            if(registration) {
                var result=await Post(client,"/Auth/Registro",new(){["Nombre"]="Synthetic "+role+" "+Run,["Correo"]=email,["Password"]=password,["ConfirmarPassword"]=password,["RolId"]=r.IdRol.ToString()},"/Auth/Registro");
                Assert.Equal(HttpStatusCode.Redirect,result.StatusCode);
            } else {db.Usuarios.Add(new(){Nombre="Synthetic "+role+" "+Run,Correo=SecurityHelper.HashEmail(email),Password=SecurityHelper.HashPassword(password),Estado="Activo",RolId=r.IdRol,FechaRegistro=DateTime.Now});await db.SaveChangesAsync();}
            id=await db.Usuarios.Where(u=>u.Correo==SecurityHelper.HashEmail(email)).Select(u=>u.IdUsuario).SingleAsync();
        }
        var login=await Post(client,"/Auth/Login",new(){["Email"]=email,["Password"]=password},"/Auth/Login");Assert.Equal(HttpStatusCode.Redirect,login.StatusCode);
        return new(id,email,password,client);
    }
    public static async Task<string> Token(HttpClient client,string path="/Cuenta/Seguridad")
    {
        var response=await client.GetAsync(path);Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        string html=await response.Content.ReadAsStringAsync();string token=WebUtility.HtmlDecode(Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);Assert.NotEmpty(token);return token;
    }
    public static async Task<HttpResponseMessage> Post(HttpClient client,string path,Dictionary<string,string> fields,string tokenPath="/Cuenta/Seguridad")
    {
        fields["__RequestVerificationToken"]=await Token(client,tokenPath);return await client.PostAsync(path,new FormUrlEncodedContent(fields));
    }
    public async Task<T> Read<T>(Func<HobbyHubContext,Task<T>> read)
    {
        using var scope=Services.CreateScope();return await read(scope.ServiceProvider.GetRequiredService<HobbyHubContext>());
    }
    public async Task<int> Community(SyntheticAccount owner,bool isPrivate=false)
    {
        string name="Synthetic community "+Run+" "+Guid.NewGuid().ToString("N")[..6];
        var response=await Post(owner.Client,"/Comunidades/Guardar",new(){["Formulario.Nombre"]=name,["Formulario.Descripcion"]="Synthetic hobby community",["Formulario.Categoria"]="Fotografía"});Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);
        int id=await Read(db=>db.Comunidades.Where(c=>c.Nombre==name).Select(c=>c.IdComunidad).SingleAsync());
        if(isPrivate) {
            var privacy=await Post(owner.Client,$"/Comunidades/Administrar/{id}",new(){["Id"]=id.ToString(),["Name"]=name,["Description"]="Synthetic private community",["Rules"]="Respeto",["Category"]="Fotografía",["Private"]="true"});Assert.Equal(HttpStatusCode.Redirect,privacy.StatusCode);
        }
        return id;
    }
    public async Task<int> Publication(SyntheticAccount author,int community,bool image=false)
    {
        var form=new MultipartFormDataContent();form.Add(new StringContent(community.ToString()),"CommunityId");form.Add(new StringContent("Synthetic hobby post "+Run),"Content");form.Add(new StringContent(await Token(author.Client)),"__RequestVerificationToken");
        if(image){var file=new ByteArrayContent(ImageValidationTests.Png());file.Headers.ContentType=new("image/png");form.Add(file,"Images","synthetic.png");}
        var response=await author.Client.PostAsync("/Social/Nueva",form);Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);
        return await Read(db=>db.Publicaciones.Where(p=>p.IdUsuario==author.Id&&p.IdComunidad==community).OrderByDescending(p=>p.IdPublicacion).Select(p=>p.IdPublicacion).FirstAsync());
    }
}
