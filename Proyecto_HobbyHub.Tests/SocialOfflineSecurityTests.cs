using System.Net;
using static Proyecto_HobbyHub.Tests.AuthenticationMvcTests;

namespace Proyecto_HobbyHub.Tests;
public class SocialOfflineSecurityTests
{
    [Fact]
    public async Task SocialWriteLimiterRejectsSixtyFirstAttemptWithoutTouchingSql()
    {
        using var app=new OfflineApplicationFactory();using var client=Client(app);Authenticate(app,client,1,out var csrf);
        for(int i=0;i<60;i++)Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsync("/Social/Comentar",Body())).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests,(await client.PostAsync("/Social/Comentar",Body())).StatusCode);
        FormUrlEncodedContent Body()=>new(new Dictionary<string,string>{["id"]="1",["content"]="",["__RequestVerificationToken"]=csrf});
    }
    [Theory]
    [InlineData("/Social/Nueva")]
    [InlineData("/Social/Enviar")]
    [InlineData("/Social/Guardar")]
    [InlineData("/Social/FotoPerfil")]
    [InlineData("/Administracion/Cuenta")]
    [InlineData("/Moderacion/Resolver")]
    public async Task NewModulesDoNotPermitAnonymousWrites(string route)
    {
        using var app=new OfflineApplicationFactory();using var client=Client(app);
        var result=await client.PostAsync(route,new FormUrlEncodedContent(new Dictionary<string,string>()));
        Assert.Equal(HttpStatusCode.Redirect,result.StatusCode);Assert.Contains("/Auth/Login",result.Headers.Location!.ToString());
    }
}
