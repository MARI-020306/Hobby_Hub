using System.Security.Claims;

namespace Proyecto_HobbyHub.Security;

public enum ResourceOperation { EditCommunity, RemoveCommunity, Publish, EditPublication, RemovePublication, Report, Interact, EditProfile }
public sealed record SocialResource(int OwnerId, int CommunityOwnerId = 0, bool IsMember = false, bool Visible = true);

public static class ResourceAccess
{
    public static bool Allows(ClaimsPrincipal principal, SocialResource resource, ResourceOperation operation)
    {
        if (principal.Identity?.IsAuthenticated != true ||
            !int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out int id) || id <= 0 ||
            principal.FindAll(ClaimTypes.Role).Count() != 1 ||
            !SecurityPolicies.KnownRoles.Contains(principal.FindFirstValue(ClaimTypes.Role))) return false;
        return operation switch
        {
            ResourceOperation.EditCommunity => resource.OwnerId == id && principal.IsInRole("Creador"),
            ResourceOperation.RemoveCommunity or ResourceOperation.EditProfile => resource.OwnerId == id,
            ResourceOperation.Publish => resource.CommunityOwnerId == id || resource.IsMember,
            ResourceOperation.EditPublication => resource.Visible && resource.OwnerId == id,
            ResourceOperation.RemovePublication => resource.OwnerId == id || resource.CommunityOwnerId == id,
            ResourceOperation.Report => resource.Visible && resource.OwnerId != id && resource.CommunityOwnerId == id,
            ResourceOperation.Interact => resource.Visible,
            _ => false
        };
    }
}
