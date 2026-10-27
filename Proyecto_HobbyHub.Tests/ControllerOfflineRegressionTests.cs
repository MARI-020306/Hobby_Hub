using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Proyecto_HobbyHub.Controllers;
using Proyecto_HobbyHub.Models;
using Proyecto_HobbyHub.ViewModels;

namespace Proyecto_HobbyHub.Tests;

public class ControllerOfflineRegressionTests
{
    private static HobbyHubContext Context() => new(new DbContextOptionsBuilder<HobbyHubContext>()
        .UseSqlServer("Server=localhost;Database=HobbyHub_Development_OfflineTests;Integrated Security=True")
        .AddInterceptors(new OfflineDatabaseInterceptor()).Options);

    private static void Authenticated(Controller controller)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "1") }, "Test"))
        };
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        controller.TempData = new TempDataDictionary(http, new EmptyTempDataProvider());
    }

    [Fact]
    public async Task InvalidPublicationDoesNotExecuteDatabaseOrSchemaCommands()
    {
        using var context = Context();
        var controller = new ComunidadesController(context);
        Authenticated(controller);
        controller.ModelState.AddModelError("Contenido", "Invalid content");
        var result = Assert.IsType<RedirectToActionResult>(await controller.Publicar(1,
            new PublicacionFormViewModel { IdComunidad = 1 }));
        Assert.Equal("Detalle", result.ActionName);
        Assert.Equal(1, result.RouteValues!["idComunidad"]);
    }

    [Fact]
    public async Task InvalidCommentDoesNotExecuteDatabaseOrSchemaCommands()
    {
        using var context = Context();
        var controller = new HomeController(context, NullLogger<HomeController>.Instance);
        Authenticated(controller);
        var result = Assert.IsType<RedirectToActionResult>(await controller.Comentar(1, " ", null));
        Assert.Equal("Index", result.ActionName);
    }

    [Fact]
    public async Task InvalidProfileEditPreservesValidationWithoutDatabaseAccess()
    {
        using var context = Context();
        var controller = new PerfilController(context);
        Authenticated(controller);
        controller.ModelState.AddModelError("Nombre", "Required");
        var model = new Proyecto_HobbyHub.ViewModels.Perfil.EditarPerfilViewModel();
        var result = Assert.IsType<ViewResult>(await controller.Editar(model));
        Assert.Same(model, result.Model);
    }

    private sealed class EmptyTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
