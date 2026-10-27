using System.Security.Claims;
using Proyecto_HobbyHub.Security;

namespace Proyecto_HobbyHub.Tests;

public class ResourceAccessTests
{
    private static ClaimsPrincipal User(string role = "Usuario") => new(new ClaimsIdentity(new[]
        { new Claim(ClaimTypes.NameIdentifier, "1"), new Claim(ClaimTypes.Role, role) }, "Test"));
    [Theory]
    [InlineData(ResourceOperation.EditCommunity, 1, 0, false, "Creador", true)]
    [InlineData(ResourceOperation.EditCommunity, 2, 0, false, "Creador", false)]
    [InlineData(ResourceOperation.EditCommunity, 1, 0, false, "Usuario", false)]
    [InlineData(ResourceOperation.RemoveCommunity, 2, 0, false, "Administrador", false)]
    [InlineData(ResourceOperation.RemoveCommunity, 1, 0, false, "Usuario", true)]
    [InlineData(ResourceOperation.Publish, 0, 2, false, "Usuario", false)]
    [InlineData(ResourceOperation.Publish, 0, 2, true, "Usuario", true)]
    [InlineData(ResourceOperation.Publish, 0, 1, false, "Creador", true)]
    [InlineData(ResourceOperation.EditPublication, 2, 1, false, "Creador", false)]
    [InlineData(ResourceOperation.EditPublication, 1, 2, false, "Usuario", true)]
    [InlineData(ResourceOperation.RemovePublication, 2, 1, false, "Creador", true)]
    [InlineData(ResourceOperation.RemovePublication, 2, 3, true, "Usuario", false)]
    [InlineData(ResourceOperation.Report, 2, 1, false, "Creador", true)]
    [InlineData(ResourceOperation.Report, 1, 1, false, "Creador", false)]
    [InlineData(ResourceOperation.EditProfile, 2, 0, false, "Administrador", false)]
    [InlineData(ResourceOperation.EditProfile, 1, 0, false, "Usuario", true)]
    public void OwnershipMembershipAndRolesAreEnforced(ResourceOperation operation, int owner, int communityOwner, bool member, string role, bool expected)
    {
        Assert.Equal(expected, ResourceAccess.Allows(User(role), new(owner, communityOwner, member), operation));
    }
    [Theory]
    [InlineData(ResourceOperation.Interact)]
    [InlineData(ResourceOperation.EditPublication)]
    [InlineData(ResourceOperation.Report)]
    public void HiddenContentDoesNotAllowInteractionEditOrReport(ResourceOperation operation)
    {
        Assert.False(ResourceAccess.Allows(User(), new(1, 1, true, false), operation));
    }
    [Fact]
    public void AnonymousAndUnknownRoleCannotAccessResources()
    {
        Assert.False(ResourceAccess.Allows(new ClaimsPrincipal(), new(1), ResourceOperation.EditProfile));
        Assert.False(ResourceAccess.Allows(User("Unknown"), new(1), ResourceOperation.EditProfile));
    }
}
