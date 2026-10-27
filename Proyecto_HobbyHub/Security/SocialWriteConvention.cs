using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.RateLimiting;

namespace Proyecto_HobbyHub.Security;
public sealed class SocialWriteConvention : IActionModelConvention
{
    public void Apply(ActionModel action)
    {
        if (action.Attributes.OfType<EnableRateLimitingAttribute>().Any() ||
            action.Controller.Attributes.OfType<EnableRateLimitingAttribute>().Any()) return;
        bool write = action.Attributes.OfType<HttpPostAttribute>().Any();
        string policy = write ? (action.Controller.ControllerName == "Auth" && action.ActionName == "Registro"
            ? "Registration" : "SocialWrites") :
            action.ActionName is "Explorar" or "Index" or "Administrar" ? "Searches" : "Queries";
        foreach (var selector in action.Selectors) selector.EndpointMetadata.Add(new EnableRateLimitingAttribute(policy));
    }
}
