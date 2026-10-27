using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Proyecto_HobbyHub.Models;

namespace Proyecto_HobbyHub.Tests;
public class FinalDeliveryRegressionTests
{
    [SqlFact]
    public async Task CommunityPagingPreservesSearchCountsAndEditingOlderItems()
    {
        using var app=new SqlApplicationFactory();var owner=await app.Account("Creador");string prefix="Delivery "+app.Run;
        int oldest=await app.Read(async db=>{
            var communities=Enumerable.Range(0,25).Select(i=>new Comunidade{Nombre=prefix+" "+i,Descripcion="Synthetic delivery pagination",Categoria="General",IdCreador=owner.Id,FechaCreacion=DateTime.Now.AddSeconds(i)}).ToArray();
            db.Comunidades.AddRange(communities);await db.SaveChangesAsync();return communities[0].IdComunidad;
        });
        string url="/Comunidades?busqueda="+Uri.EscapeDataString(prefix);
        string first=await owner.Client.GetStringAsync(url),second=await owner.Client.GetStringAsync(url+"&page=2");
        var ids1=Regex.Matches(first,"href=\"/Comunidades/Detalle/(\\d+)\"").Select(m=>m.Groups[1].Value).Distinct().ToArray();
        var ids2=Regex.Matches(second,"href=\"/Comunidades/Detalle/(\\d+)\"").Select(m=>m.Groups[1].Value).Distinct().ToArray();
        Assert.Equal(20,ids1.Length);Assert.Equal(5,ids2.Length);Assert.Empty(ids1.Intersect(ids2));Assert.Contains("page=2",first);Assert.Contains("page=1",second);
        string edit=await owner.Client.GetStringAsync(url+"&editar="+oldest);Assert.Contains("value=\""+oldest+"\"",edit);
        Assert.Equal(HttpStatusCode.BadRequest,(await owner.Client.GetAsync("/Comunidades?busqueda="+new string('x',101))).StatusCode);
    }
    [SqlFact]
    public async Task ErrorPagesAndAuditBoundaryFiltersStaySafeAndKeepStatusCodes()
    {
        using var app=new SqlApplicationFactory();var admin=await app.Account("Administrador");
        var missing=await admin.Client.GetAsync("/route-that-does-not-exist");Assert.Equal(HttpStatusCode.NotFound,missing.StatusCode);string html=await missing.Content.ReadAsStringAsync();Assert.Contains("app-shell",html);Assert.Contains("404",html);Assert.DoesNotContain("StackTrace",html);
        var csrf=await admin.Client.PostAsync("/Social/Guardar",new FormUrlEncodedContent(new Dictionary<string,string>{{"id","1"}}));Assert.Equal(HttpStatusCode.BadRequest,csrf.StatusCode);Assert.Contains("400",await csrf.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK,(await admin.Client.GetAsync("/Administracion/Auditoria?to=9999-12-31")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await admin.Client.GetAsync("/Administracion/Auditoria?from=2026-10-08&to=2026-10-01")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await admin.Client.GetAsync("/Moderacion?state=invalid")).StatusCode);
        Assert.Equal(HttpStatusCode.InternalServerError,(await admin.Client.GetAsync("/Home/Error")).StatusCode);
    }
}
