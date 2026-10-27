using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Proyecto_HobbyHub.Tests.SqlApplicationFactory;

namespace Proyecto_HobbyHub.Tests;
public class SqlSocialIntegrationTests
{
    [SqlFact]
    public async Task AuditPageDisplaysRecordsWithoutMistakingMvcActionForEventFilter()
    {
        using var app=new SqlApplicationFactory();var admin=await app.Account("Administrador");
        string first="VisualAuditFirst_"+app.Run,second="VisualAuditSecond_"+app.Run;
        await app.Read(async db=>{
            db.AuditoriasSeguridad.Add(new(){Accion=first,IdActor=admin.Id,Exito=true,FechaUtc=DateTime.UtcNow});
            db.AuditoriasSeguridad.Add(new(){Accion=second,IdActor=admin.Id,Exito=false,FechaUtc=DateTime.UtcNow});
            return await db.SaveChangesAsync();
        });
        var response=await admin.Client.GetAsync("/Administracion/Auditoria?user="+admin.Id);
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);string html=await response.Content.ReadAsStringAsync();
        Assert.Contains(first,html);Assert.Contains(second,html);Assert.Contains("data-label=\"Actor\"",html);
        html=await admin.Client.GetStringAsync("/Administracion/Auditoria?user="+admin.Id+"&action="+first);
        Assert.Contains(first,html);Assert.DoesNotContain(second,html);
    }
    [SqlFact]
    public async Task EmptyOptionalSettingsCanBeSavedAndCommunityDeletionPreservesReportHistory()
    {
        using var app=new SqlApplicationFactory();var creator=await app.Account("Creador");var reporter=await app.Account();var moderator=await app.Account("Moderador");
        Assert.Equal(HttpStatusCode.Redirect,(await Post(creator.Client,"/Social/Ajustes",new(){["Name"]="Synthetic optional settings",["Bio"]="",["Interests"]="",["Messages"]="true",["Notices"]="true"})).StatusCode);
        int community=await app.Community(creator);int post=await app.Publication(creator,community,true);
        await Post(reporter.Client,"/Social/Comentar",new(){["id"]=post.ToString(),["content"]="Synthetic delete comment"});
        int comment=await app.Read(db=>db.Comentarios.Where(c=>c.IdPublicacion==post).Select(c=>c.IdComentario).SingleAsync());
        await Post(creator.Client,"/Social/Comentar",new(){["id"]=post.ToString(),["parent"]=comment.ToString(),["content"]="Synthetic delete reply"});
        await Post(reporter.Client,"/Social/Guardar",new(){["id"]=post.ToString()});
        await Post(reporter.Client,"/Social/Reportar",new(){["type"]="publicacion",["id"]=post.ToString(),["reason"]="Synthetic retained report"});
        Assert.Equal(HttpStatusCode.Conflict,(await Post(creator.Client,$"/Comunidades/Eliminar/{community}",new())).StatusCode);
        int report=await app.Read(db=>db.Reportes.Where(r=>r.IdPublicacion==post).Select(r=>r.IdReporte).SingleAsync());
        await Post(moderator.Client,"/Moderacion/Resolver",new(){["id"]=report.ToString(),["operation"]="resolver",["reason"]="Synthetic resolved report"});
        Assert.Equal(HttpStatusCode.Redirect,(await Post(creator.Client,$"/Comunidades/Eliminar/{community}",new())).StatusCode);
        Assert.False(await app.Read(db=>db.Comunidades.AnyAsync(c=>c.IdComunidad==community)));
        Assert.True(await app.Read(db=>db.Reportes.AnyAsync(r=>r.IdReporte==report&&r.IdPublicacion==null&&r.Estado=="Resuelto")));
        Assert.True(await app.Read(db=>db.AuditoriasSeguridad.AnyAsync(a=>a.IdActor==creator.Id&&a.Accion=="CommunityDeleted")));
    }
    [SqlFact]
    public async Task StoredContentIsHtmlEncodedAndSearchDoesNotInterpretSql()
    {
        using var app=new SqlApplicationFactory();var owner=await app.Account("Creador");int community=await app.Community(owner);
        const string payload="<script>alert('synthetic')</script>";
        Assert.Equal(HttpStatusCode.Redirect,(await Post(owner.Client,"/Social/Nueva",new(){["CommunityId"]=community.ToString(),["Content"]=payload})).StatusCode);
        int id=await app.Read(db=>db.Publicaciones.Where(p=>p.IdComunidad==community).Select(p=>p.IdPublicacion).SingleAsync());
        var response=await owner.Client.GetAsync($"/Social/Publicacion/{id}");string html=await response.Content.ReadAsStringAsync();
        Assert.Contains("&lt;script&gt;",html);Assert.DoesNotContain(payload,html);Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Equal("DENY",response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("nosniff",response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal(HttpStatusCode.OK,(await owner.Client.GetAsync("/Social/Explorar?q="+Uri.EscapeDataString("' OR 1=1;--"))).StatusCode);
        Assert.Equal(payload,await app.Read(db=>db.Publicaciones.Where(p=>p.IdPublicacion==id).Select(p=>p.Contenido).SingleAsync()));
    }
    [SqlFact]
    public async Task PersonalBlocksAreMutualAndMessageReportsRequireRecipientOwnership()
    {
        using var app=new SqlApplicationFactory();var sender=await app.Account("Creador");var recipient=await app.Account();var outsider=await app.Account();var moderator=await app.Account("Moderador");
        int community=await app.Community(sender);int post=await app.Publication(sender,community,true);
        await Post(recipient.Client,"/Social/Seguir",new(){["id"]=sender.Id.ToString()});
        string content="Synthetic reported message "+app.Run;
        await Post(sender.Client,"/Social/Enviar",new(){["id"]=recipient.Id.ToString(),["content"]=content});
        long message=await app.Read(db=>db.MensajesPrivados.Where(m=>m.IdRemitente==sender.Id&&m.IdDestinatario==recipient.Id).Select(m=>m.IdMensaje).SingleAsync());
        Assert.Equal(HttpStatusCode.NotFound,(await Post(outsider.Client,"/Social/ReportarMensaje",new(){["id"]=message.ToString(),["reason"]="Unauthorized"})).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect,(await Post(recipient.Client,"/Social/ReportarMensaje",new(){["id"]=message.ToString(),["reason"]="Synthetic message report"})).StatusCode);
        int report=await app.Read(db=>db.Reportes.Where(r=>r.IdMensaje==message).Select(r=>r.IdReporte).SingleAsync());
        Assert.Contains(content,await moderator.Client.GetStringAsync($"/Moderacion/Detalle/{report}"));
        await Post(moderator.Client,"/Moderacion/Resolver",new(){["id"]=report.ToString(),["operation"]="ocultar",["reason"]="Synthetic message moderation"});
        Assert.DoesNotContain(content,await recipient.Client.GetStringAsync($"/Social/Mensajes/{sender.Id}"));
        await Post(recipient.Client,"/Social/Bloquear",new(){["id"]=sender.Id.ToString()});
        Assert.Equal(HttpStatusCode.NotFound,(await recipient.Client.GetAsync($"/Social/Publicacion/{post}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await sender.Client.GetAsync($"/Social/Persona/{recipient.Id}")).StatusCode);
        Assert.Contains("/Auth/AccesoDenegado",(await Post(sender.Client,"/Social/Enviar",new(){["id"]=recipient.Id.ToString(),["content"]="Blocked message"})).Headers.Location!.ToString());
        Assert.Contains("/Auth/AccesoDenegado",(await Post(sender.Client,"/Social/Seguir",new(){["id"]=recipient.Id.ToString()})).Headers.Location!.ToString());
        Assert.False(await app.Read(db=>db.Seguimientos.AnyAsync(s=>s.IdSeguidor==recipient.Id&&s.IdSeguido==sender.Id)));
        Assert.True(await app.Read(db=>db.AuditoriasSeguridad.AnyAsync(a=>a.IdActor==recipient.Id&&a.Accion=="UserBlocked")));
        await Post(recipient.Client,"/Social/Bloquear",new(){["id"]=sender.Id.ToString(),["unblock"]="true"});
        Assert.Equal(HttpStatusCode.OK,(await recipient.Client.GetAsync($"/Social/Publicacion/{post}")).StatusCode);
    }
    [SqlFact]
    public async Task PersistentLoginLockoutRevokesCookiesAndLogoutIsAudited()
    {
        using var app=new SqlApplicationFactory();var account=await app.Account();using var attacker=app.Client();
        for(int i=0;i<5;i++)Assert.Equal(HttpStatusCode.OK,(await Post(attacker,"/Auth/Login",new(){["Email"]=account.Email,["Password"]="synthetic-wrong-password"},"/Auth/Login")).StatusCode);
        Assert.Equal(5,await app.Read(db=>db.Usuarios.Where(u=>u.IdUsuario==account.Id).Select(u=>u.IntentosFallidos).SingleAsync()));
        Assert.Contains("/Auth/Login",(await account.Client.GetAsync("/Social/Inicio")).Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.OK,(await Post(attacker,"/Auth/Login",new(){["Email"]=account.Email,["Password"]=account.Password},"/Auth/Login")).StatusCode);
        var other=await app.Account();Assert.Equal(HttpStatusCode.Redirect,(await Post(other.Client,"/Auth/Logout",new())).StatusCode);
        Assert.True(await app.Read(db=>db.AuditoriasSeguridad.AnyAsync(a=>a.IdActor==other.Id&&a.Accion=="Logout")));
        Assert.True(await app.Read(db=>db.SesionesUsuario.Where(s=>s.IdUsuario==other.Id).AllAsync(s=>s.RevocadaUtc!=null)));
    }
    [SqlFact]
    public async Task RoleChangesRevokeExistingSessionAndNewLoginGetsCurrentRole()
    {
        using var app=new SqlApplicationFactory();var administrator=await app.Account("Administrador");var user=await app.Account();
        int role=await app.Read(db=>db.Roles.Where(r=>r.Nombre=="Moderador").Select(r=>r.IdRol).SingleAsync());
        Assert.Equal(HttpStatusCode.Redirect,(await Post(administrator.Client,"/Administracion/Cuenta",new(){["id"]=user.Id.ToString(),["role"]=role.ToString(),["state"]="Activo"})).StatusCode);
        Assert.Contains("/Auth/Login",(await user.Client.GetAsync("/Social/Inicio")).Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.Redirect,(await Post(user.Client,"/Auth/Login",new(){["Email"]=user.Email,["Password"]=user.Password},"/Auth/Login")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await user.Client.GetAsync("/Moderacion")).StatusCode);
        Assert.Contains("/Auth/AccesoDenegado",(await user.Client.GetAsync("/Administracion")).Headers.Location!.ToString());
        Assert.True(await app.Read(db=>db.AuditoriasSeguridad.AnyAsync(a=>a.IdObjetivo==user.Id&&a.Accion=="AccountRoleChanged")));
    }
    [SqlFact]
    public async Task SearchPersonalizedFeedAndSavedContentUsePersistedRelations()
    {
        using var app=new SqlApplicationFactory();var owner=await app.Account("Creador");var member=await app.Account();int community=await app.Community(owner);int post=await app.Publication(owner,community);
        Assert.DoesNotContain("Synthetic hobby post "+app.Run,await member.Client.GetStringAsync("/Social/Inicio?mode=siguiendo"));
        await Post(member.Client,$"/Comunidades/Unirse/{community}",new());
        Assert.Contains("Synthetic hobby post "+app.Run,await member.Client.GetStringAsync("/Social/Inicio?mode=siguiendo"));
        Assert.Contains("Synthetic hobby post "+app.Run,await member.Client.GetStringAsync("/Social/Explorar?q="+app.Run));
        Assert.DoesNotContain("Synthetic hobby post "+app.Run,await member.Client.GetStringAsync("/Social/Explorar?q=no-matching-hobby-"+app.Run));
        await Post(member.Client,"/Social/Guardar",new(){["id"]=post.ToString()});Assert.Contains("Synthetic hobby post "+app.Run,await member.Client.GetStringAsync("/Social/Guardados"));
        await Post(member.Client,"/Social/Guardar",new(){["id"]=post.ToString()});Assert.DoesNotContain("Synthetic hobby post "+app.Run,await member.Client.GetStringAsync("/Social/Guardados"));
    }
    [SqlFact]
    public async Task RecoveryExpiredSingleUseConcurrentSqlTransactionsAndAudit()
    {
        using var app=new SqlApplicationFactory();var account=await app.Account();using var anonymous=app.Client();
        await Post(anonymous,"/Cuenta/Recuperar",new(){["Email"]=account.Email},"/Auth/Login");
        string expired=app.Mailbox.Messages.Last().Token;
        using(var scope=app.Services.CreateScope()){
            var db=scope.ServiceProvider.GetRequiredService<Proyecto_HobbyHub.Models.HobbyHubContext>();
            string hash=Proyecto_HobbyHub.Models.SecurityHelper.HashPersonalData(expired);
            await db.RecuperacionesCuenta.Where(t=>t.TokenHash==hash).ExecuteUpdateAsync(s=>s.SetProperty(t=>t.CreadaUtc,DateTime.UtcNow.AddHours(-1)).SetProperty(t=>t.ExpiraUtc,DateTime.UtcNow.AddMinutes(-1)));
        }
        var denied=await Post(anonymous,"/Cuenta/Restablecer",new(){["Token"]=expired,["Password"]="synthetic-recovery-password",["ConfirmPassword"]="synthetic-recovery-password"},"/Auth/Login");Assert.Equal(HttpStatusCode.OK,denied.StatusCode);
        await Post(anonymous,"/Cuenta/Recuperar",new(){["Email"]=account.Email},"/Auth/Login");string token=app.Mailbox.Messages.Last().Token;
        string csrf=await Token(anonymous,"/Auth/Login");
        var results=await Task.WhenAll(anonymous.PostAsync("/Cuenta/Restablecer",Body()),anonymous.PostAsync("/Cuenta/Restablecer",Body()));
        Assert.Single(results,r=>r.StatusCode==HttpStatusCode.Redirect);Assert.Single(results,r=>r.StatusCode==HttpStatusCode.OK);
        Assert.Equal(1,await app.Read(db=>db.AuditoriasSeguridad.CountAsync(a=>a.IdObjetivo==account.Id&&a.Accion=="PasswordReset")));
        Assert.True(await app.Read(db=>db.SesionesUsuario.Where(s=>s.IdUsuario==account.Id).AllAsync(s=>s.RevocadaUtc!=null)));
        FormUrlEncodedContent Body()=>new(new Dictionary<string,string>{["Token"]=token,["Password"]="synthetic-recovery-password",["ConfirmPassword"]="synthetic-recovery-password",["__RequestVerificationToken"]=csrf});
    }
    [SqlFact]
    public async Task PrivateProfilePostsCannotLeakThroughOldOrNewViews()
    {
        using var app=new SqlApplicationFactory();var creator=await app.Account("Creador");var reader=await app.Account();int community=await app.Community(creator);int post=await app.Publication(creator,community);
        await Post(creator.Client,"/Social/Ajustes",new(){["Name"]="Private synthetic author",["Private"]="true",["Messages"]="true",["Notices"]="true"});
        Assert.Equal(HttpStatusCode.NotFound,(await reader.Client.GetAsync($"/Social/Publicacion/{post}")).StatusCode);
        Assert.DoesNotContain("Synthetic hobby post "+app.Run,await reader.Client.GetStringAsync($"/Comunidades/Detalle/{community}"));
        Assert.DoesNotContain("Synthetic hobby post "+app.Run,await reader.Client.GetStringAsync("/Home/InicioClasico"));
        await Post(reader.Client,"/Social/Seguir",new(){["id"]=creator.Id.ToString()});
        await Post(creator.Client,"/Social/Solicitud",new(){["id"]=reader.Id.ToString(),["accept"]="true"});
        Assert.Equal(HttpStatusCode.OK,(await reader.Client.GetAsync($"/Social/Publicacion/{post}")).StatusCode);
    }
    [SqlFact]
    public async Task SocialFormsOwnerChecksGalleryRemovalAndAvatarPersistence()
    {
        using var app=new SqlApplicationFactory();var owner=await app.Account("Creador");var stranger=await app.Account();int community=await app.Community(owner);int post=await app.Publication(owner,community,true);
        var image=await app.Read(db=>db.PublicacionImagenes.SingleAsync(i=>i.IdPublicacion==post));
        Assert.Contains("/Auth/AccesoDenegado",(await Post(stranger.Client,"/Social/RetirarImagen",new(){["id"]=image.IdImagen.ToString()})).Headers.Location!.ToString());
        var avatar=new MultipartFormDataContent();avatar.Add(new StringContent(await Token(owner.Client)),"__RequestVerificationToken");avatar.Add(new StringContent("Perfil"),"purpose");avatar.Add(new ByteArrayContent(ImageValidationTests.Png()),"image","avatar.png");
        Assert.Equal(HttpStatusCode.Redirect,(await owner.Client.PostAsync("/Social/FotoPerfil",avatar)).StatusCode);
        var picture=await app.Read(db=>db.UsuarioImagenes.SingleAsync(i=>i.IdUsuario==owner.Id&&i.Tipo=="Perfil"));Assert.Equal(HttpStatusCode.OK,(await owner.Client.GetAsync(picture.Url)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect,(await Post(owner.Client,"/Perfil/Editar",new(){["Nombre"]="Synthetic avatar preserved",["Celular"]="",["Direccion"]="",["ImagenPerfilUrl"]="",["PortadaUrl"]=""})).StatusCode);
        Assert.True(await app.Read(db=>db.UsuarioImagenes.AnyAsync(i=>i.IdImagen==picture.IdImagen&&i.EsPrincipal)));
        await Post(owner.Client,"/Social/RetirarImagen",new(){["id"]=image.IdImagen.ToString()});Assert.False(await app.Read(db=>db.PublicacionImagenes.AnyAsync(i=>i.IdImagen==image.IdImagen)));
    }
    [SqlFact]
    public async Task EveryModuleViewRendersAndSocialWritesRejectMissingCsrf()
    {
        using var app=new SqlApplicationFactory();var creator=await app.Account("Creador");var admin=await app.Account("Administrador");int community=await app.Community(creator);int post=await app.Publication(creator,community);
        foreach(string route in new[]{"/Social/Inicio","/Social/Explorar","/Social/Nueva","/Social/Guardados","/Social/Seguidores","/Social/Ajustes","/Social/Mensajes","/Social/Notificaciones","/Social/Reportes","/Social/Actividad",$"/Social/Persona/{creator.Id}",$"/Social/Publicacion/{post}",$"/Comunidades/Administrar/{community}","/Perfil/Editar"})
            Assert.Equal(HttpStatusCode.OK,(await creator.Client.GetAsync(route)).StatusCode);
        foreach(string route in new[]{"/Administracion","/Administracion/Auditoria","/Administracion/Comunidades","/Moderacion"})Assert.Equal(HttpStatusCode.OK,(await admin.Client.GetAsync(route)).StatusCode);
        foreach(string route in new[]{"/Social/Seguir","/Social/Reportar","/Social/Enviar","/Social/Guardar","/Social/LeerNotificacion","/Social/CerrarSesion"})Assert.Equal(HttpStatusCode.BadRequest,(await creator.Client.PostAsync(route,new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode);
    }
    [SqlFact]
    public async Task TemporarySuspensionExpiresButOldSessionsStayRevoked()
    {
        using var app=new SqlApplicationFactory();var creator=await app.Account("Creador");var reporter=await app.Account();var moderator=await app.Account("Moderador");int community=await app.Community(creator);int post=await app.Publication(creator,community);
        await Post(reporter.Client,"/Social/Reportar",new(){["type"]="publicacion",["id"]=post.ToString(),["reason"]="Synthetic suspension report"});
        int report=await app.Read(db=>db.Reportes.Where(r=>r.IdPublicacion==post).Select(r=>r.IdReporte).SingleAsync());
        await Post(moderator.Client,"/Moderacion/Resolver",new(){["id"]=report.ToString(),["operation"]="suspender",["reason"]="Synthetic suspension",["days"]="1"});
        Assert.Equal("Suspendido",await app.Read(db=>db.Usuarios.Where(u=>u.IdUsuario==creator.Id).Select(u=>u.Estado).SingleAsync()));
        using(var scope=app.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<Proyecto_HobbyHub.Models.HobbyHubContext>();await db.Usuarios.Where(u=>u.IdUsuario==creator.Id).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.SuspensionHastaUtc,DateTime.UtcNow.AddMinutes(-1)));}
        Assert.Contains("/Auth/Login",(await creator.Client.GetAsync("/Social/Inicio")).Headers.Location!.ToString());
        Assert.Equal("Activo",await app.Read(db=>db.Usuarios.Where(u=>u.IdUsuario==creator.Id).Select(u=>u.Estado).SingleAsync()));
        Assert.True(await app.Read(db=>db.AuditoriasSeguridad.AnyAsync(a=>a.IdObjetivo==creator.Id&&a.Accion=="SuspensionExpired")));
    }
    [SqlFact]
    public async Task RegistrationCommunityGalleryInteractionsAndPersistence()
    {
        using var app=new SqlApplicationFactory();var creator=await app.Account("Creador",true);var member=await app.Account("Usuario",true);
        int community=await app.Community(creator);
        Assert.Equal(HttpStatusCode.Redirect,(await Post(member.Client,$"/Comunidades/Unirse/{community}",new())).StatusCode);
        int post=await app.Publication(member,community,true);
        Assert.Equal(HttpStatusCode.OK,(await creator.Client.GetAsync($"/Social/Publicacion/{post}")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect,(await Post(creator.Client,"/Home/ToggleLike",new(){["idPublicacion"]=post.ToString()})).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect,(await Post(creator.Client,"/Social/Guardar",new(){["id"]=post.ToString()})).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect,(await Post(creator.Client,"/Social/Comentar",new(){["id"]=post.ToString(),["content"]="Synthetic comment"})).StatusCode);
        int comment=await app.Read(db=>db.Comentarios.Where(c=>c.IdPublicacion==post).Select(c=>c.IdComentario).SingleAsync());
        Assert.Equal(HttpStatusCode.Redirect,(await Post(member.Client,"/Social/Comentar",new(){["id"]=post.ToString(),["parent"]=comment.ToString(),["content"]="Synthetic reply"})).StatusCode);
        Assert.True(await app.Read(db=>db.PublicacionesGuardadas.AnyAsync(g=>g.IdUsuario==creator.Id&&g.IdPublicacion==post)));
        Assert.Equal(2,await app.Read(db=>db.Comentarios.CountAsync(c=>c.IdPublicacion==post)));
        var image=await app.Read(db=>db.PublicacionImagenes.SingleAsync(i=>i.IdPublicacion==post));
        var response=await creator.Client.GetAsync($"/Social/Imagen/{image.IdImagen}");Assert.Equal(HttpStatusCode.OK,response.StatusCode);Assert.Equal("image/png",response.Content.Headers.ContentType!.MediaType);
        Assert.True(await app.Read(db=>db.Notificaciones.AnyAsync(n=>n.IdUsuario==member.Id&&n.Tipo=="Comentario")));
        Assert.Contains("Synthetic hobby post "+app.Run,await creator.Client.GetStringAsync("/Social/Reacciones"));
        Assert.Contains("Synthetic comment",await creator.Client.GetStringAsync("/Social/MisComentarios"));
        Assert.DoesNotContain("Synthetic comment",await member.Client.GetStringAsync("/Social/MisComentarios"));
    }
    [SqlFact]
    public async Task PrivateCommunityRequestsInvitationsExpulsionAndLegacyUrlsAreProtected()
    {
        using var app=new SqlApplicationFactory();var owner=await app.Account("Creador");var member=await app.Account();var outsider=await app.Account();int community=await app.Community(owner,true);int post=await app.Publication(owner,community,true);
        Assert.Equal(HttpStatusCode.NotFound,(await outsider.Client.GetAsync($"/Social/Publicacion/{post}")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect,(await outsider.Client.GetAsync($"/Comunidades/Detalle/{community}")).StatusCode);
        Assert.Contains("/Auth/AccesoDenegado",(await Post(outsider.Client,"/Social/Nueva",new(){["CommunityId"]=community.ToString(),["Content"]="Denied post"})).Headers.Location!.ToString());
        await Post(member.Client,$"/Comunidades/Unirse/{community}",new());
        Assert.Equal("Pendiente",await app.Read(db=>db.MiembrosComunidad.Where(m=>m.IdComunidad==community&&m.IdUsuario==member.Id).Select(m=>m.Estado).SingleAsync()));
        await Post(owner.Client,$"/Comunidades/Miembro/{community}",new(){["user"]=member.Id.ToString(),["operation"]="aceptar"});
        Assert.Equal(HttpStatusCode.OK,(await member.Client.GetAsync($"/Social/Publicacion/{post}")).StatusCode);
        var moderator=await app.Account("Moderador");var evidence=await app.Read(db=>db.PublicacionImagenes.SingleAsync(i=>i.IdPublicacion==post));
        Assert.Equal(HttpStatusCode.NotFound,(await moderator.Client.GetAsync($"/Social/Imagen/{evidence.IdImagen}")).StatusCode);
        await Post(member.Client,"/Social/Reportar",new(){["type"]="publicacion",["id"]=post.ToString(),["reason"]="Synthetic private image evidence"});
        Assert.Equal(HttpStatusCode.OK,(await moderator.Client.GetAsync($"/Social/Imagen/{evidence.IdImagen}")).StatusCode);
        int memberPost=await app.Publication(member,community);
        Assert.Equal(HttpStatusCode.Redirect,(await Post(member.Client,$"/Comunidades/EditarPublicacion/{memberPost}",new(){["IdComunidad"]=community.ToString(),["Contenido"]="Synthetic updated post"})).StatusCode);
        await Post(owner.Client,$"/Comunidades/Miembro/{community}",new(){["user"]=member.Id.ToString(),["operation"]="expulsar"});
        Assert.Equal(HttpStatusCode.NotFound,(await member.Client.GetAsync($"/Social/Publicacion/{post}")).StatusCode);
        Assert.Contains("/Auth/AccesoDenegado",(await Post(member.Client,$"/Comunidades/EditarPublicacion/{memberPost}",new(){["IdComunidad"]=community.ToString(),["Contenido"]="Denied update after expulsion"})).Headers.Location!.ToString());
        Assert.Contains("/Auth/AccesoDenegado",(await Post(member.Client,"/Comunidades/ReportarUsuario",new(){["IdComunidad"]=community.ToString(),["IdPublicacion"]=post.ToString(),["IdUsuarioReportado"]=owner.Id.ToString(),["Motivo"]="Denied report outside private community"})).Headers.Location!.ToString());
        await Post(owner.Client,$"/Comunidades/Miembro/{community}",new(){["user"]=outsider.Id.ToString(),["operation"]="invitar"});
        await Post(outsider.Client,$"/Comunidades/Unirse/{community}",new());
        Assert.Equal(HttpStatusCode.OK,(await outsider.Client.GetAsync($"/Social/Publicacion/{post}")).StatusCode);
        var image=await app.Read(db=>db.PublicacionImagenes.SingleAsync(i=>i.IdPublicacion==post));Assert.Equal(HttpStatusCode.NotFound,(await member.Client.GetAsync($"/Social/Imagen/{image.IdImagen}")).StatusCode);
    }
    [SqlFact]
    public async Task PrivateFollowersMessagesAndNotificationOwnership()
    {
        using var app=new SqlApplicationFactory();var a=await app.Account();var b=await app.Account();var c=await app.Account();
        await Post(b.Client,"/Social/Ajustes",new(){["Name"]="Synthetic private person",["Bio"]="Synthetic biography",["Private"]="true",["Messages"]="true",["Notices"]="true"});
        Assert.Contains("/Auth/AccesoDenegado",(await Post(a.Client,"/Social/Enviar",new(){["id"]=b.Id.ToString(),["content"]="Before approval"})).Headers.Location!.ToString());
        await Post(a.Client,"/Social/Seguir",new(){["id"]=b.Id.ToString()});Assert.False(await app.Read(db=>db.Seguimientos.Where(s=>s.IdSeguidor==a.Id&&s.IdSeguido==b.Id).Select(s=>s.Aceptado).SingleAsync()));
        await Post(b.Client,"/Social/Solicitud",new(){["id"]=a.Id.ToString(),["accept"]="true"});
        string text="Synthetic private message "+app.Run;Assert.Equal(HttpStatusCode.Redirect,(await Post(a.Client,"/Social/Enviar",new(){["id"]=b.Id.ToString(),["content"]=text})).StatusCode);
        Assert.Contains(text,await b.Client.GetStringAsync($"/Social/Mensajes/{a.Id}"));Assert.DoesNotContain(text,await c.Client.GetStringAsync($"/Social/Mensajes/{a.Id}"));
        await Post(b.Client,"/Social/LeerMensajes",new(){["id"]=a.Id.ToString()});Assert.True(await app.Read(db=>db.MensajesPrivados.AnyAsync(m=>m.IdRemitente==a.Id&&m.IdDestinatario==b.Id&&m.LeidoUtc!=null)));
        var notice=await app.Read(db=>db.Notificaciones.Where(n=>n.IdUsuario==b.Id).FirstAsync());
        Assert.Equal(HttpStatusCode.NotFound,(await Post(c.Client,"/Social/LeerNotificacion",new(){["id"]=notice.IdNotificacion.ToString()})).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect,(await Post(b.Client,"/Social/LeerNotificacion",new(){["id"]=notice.IdNotificacion.ToString()})).StatusCode);
    }
    [SqlFact]
    public async Task ReportsModerationRolesSuspensionAndAudit()
    {
        using var app=new SqlApplicationFactory();var owner=await app.Account("Creador");var user=await app.Account();var moderator=await app.Account("Moderador");var admin=await app.Account("Administrador");int community=await app.Community(owner);int post=await app.Publication(owner,community);
        Assert.Equal(HttpStatusCode.Redirect,(await Post(user.Client,"/Social/Reportar",new(){["type"]="publicacion",["id"]=post.ToString(),["reason"]="Synthetic moderation report"})).StatusCode);
        int report=await app.Read(db=>db.Reportes.Where(r=>r.IdPublicacion==post&&r.IdUsuarioReporta==user.Id).Select(r=>r.IdReporte).SingleAsync());
        Assert.Equal(HttpStatusCode.Conflict,(await Post(user.Client,"/Social/Reportar",new(){["type"]="publicacion",["id"]=post.ToString(),["reason"]="Duplicate"})).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect,(await user.Client.GetAsync("/Moderacion")).StatusCode);Assert.Equal(HttpStatusCode.Redirect,(await moderator.Client.GetAsync("/Administracion")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await moderator.Client.GetAsync($"/Moderacion/Detalle/{report}")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect,(await Post(moderator.Client,"/Moderacion/Resolver",new(){["id"]=report.ToString(),["operation"]="ocultar",["reason"]="Synthetic decision"})).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await user.Client.GetAsync($"/Social/Publicacion/{post}")).StatusCode);
        int role=await app.Read(db=>db.Roles.Where(r=>r.Nombre=="Usuario").Select(r=>r.IdRol).SingleAsync());
        Assert.Equal(HttpStatusCode.Redirect,(await Post(admin.Client,"/Administracion/Cuenta",new(){["id"]=user.Id.ToString(),["role"]=role.ToString(),["state"]="Suspendido"})).StatusCode);
        Assert.Contains("/Auth/Login",(await user.Client.GetAsync("/Social/Inicio")).Headers.Location!.ToString());
        Assert.True(await app.Read(db=>db.AuditoriasSeguridad.AnyAsync(a=>a.Accion=="ModerationAction"&&a.IdActor==moderator.Id)));
        Assert.True(await app.Read(db=>db.AuditoriasSeguridad.AnyAsync(a=>a.Accion=="AccountStateChanged"&&a.IdObjetivo==user.Id)));
        Assert.Equal(HttpStatusCode.OK,(await admin.Client.GetAsync("/Administracion/Auditoria")).StatusCode);
    }
    [SqlFact]
    public async Task ExistingProfileEditPasswordChangeAndSqlSessionRevocation()
    {
        using var app=new SqlApplicationFactory();var account=await app.Account();
        Assert.Equal(HttpStatusCode.OK,(await account.Client.GetAsync("/Perfil")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect,(await Post(account.Client,"/Perfil/Editar",new(){["Nombre"]="Synthetic edited profile",["Celular"]="",["Direccion"]=""})).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect,(await Post(account.Client,"/Cuenta/Seguridad",new(){["CurrentPassword"]=account.Password,["Password"]="synthetic-new-password",["ConfirmPassword"]="synthetic-new-password"})).StatusCode);
        Assert.True(await app.Read(db=>db.SesionesUsuario.Where(s=>s.IdUsuario==account.Id).AllAsync(s=>s.RevocadaUtc!=null)));
        Assert.True(await app.Read(db=>db.AuditoriasSeguridad.AnyAsync(a=>a.IdObjetivo==account.Id&&a.Accion=="PasswordChanged")));
    }
}
