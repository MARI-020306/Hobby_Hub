using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;

namespace Proyecto_HobbyHub.Security;

public static class SecurityPolicies
{
    public const string Administrator = "AdministratorOnly";
    public const string Moderation = "ModerationAccess";
    public const string CreateCommunity = "CreateCommunity";
    public const string LoginAttempts = "LoginAttempts";
    public static readonly string[] KnownRoles = ["Usuario", "Creador", "Moderador", "Administrador"];
    public static readonly string[] RegistrationRoles = ["Usuario", "Creador"];
}

// Secure future management controllers even if an action lacks an explicit attribute.
public sealed class ManagementAuthorizationConvention : IControllerModelConvention
{
    public void Apply(ControllerModel controller)
    {
        string? policy = controller.ControllerName switch
        {
            "Administracion" => SecurityPolicies.Administrator,
            "Moderacion" => SecurityPolicies.Moderation,
            _ => null
        };
        if (policy is not null) controller.Filters.Add(new AuthorizeFilter(policy));
    }
}
