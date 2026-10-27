using Microsoft.AspNetCore.Mvc;

namespace Proyecto_HobbyHub.Tests;

// These controllers are loaded only by the test host, never the production application.
public class AdministracionController : Controller
{
    [HttpGet("/__tests/admin")]
    public IActionResult Index() => Ok();
    [HttpGet("/__tests/admin-failed")]
    public IActionResult Failed() => BadRequest();
    [HttpGet("/__tests/logout-form")]
    public IActionResult LogoutForm() => PartialView("~/Views/Shared/_LogoutForm.cshtml");
}
public class ModeracionController : Controller
{
    [HttpGet("/__tests/moderation")]
    public IActionResult Index() => Ok();
}
