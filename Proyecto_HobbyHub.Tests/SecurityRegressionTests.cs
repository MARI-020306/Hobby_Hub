using System.ComponentModel.DataAnnotations;
using Proyecto_HobbyHub.Models;
using Proyecto_HobbyHub.ViewModels;

namespace Proyecto_HobbyHub.Tests;

public class SecurityRegressionTests
{
    [Fact]
    public void ExistingEmailNormalizationRemainsCompatible()
    {
        Assert.Equal(SecurityHelper.HashEmail("person@example.test"),
            SecurityHelper.HashEmail(" PERSON@EXAMPLE.TEST "));
        Assert.Equal(64, SecurityHelper.HashEmail("person@example.test").Length);
    }

    [Fact]
    public void PasswordsUseSaltedHashesAndRejectIncorrectPasswords()
    {
        string first = SecurityHelper.HashPassword("synthetic-test-password");
        string second = SecurityHelper.HashPassword("synthetic-test-password");
        Assert.NotEqual(first, second);
        Assert.True(SecurityHelper.VerifyPassword("synthetic-test-password", first));
        Assert.False(SecurityHelper.VerifyPassword("wrong-password", first));
        Assert.False(SecurityHelper.VerifyPassword("synthetic-test-password", null));
    }

    [Fact]
    public void RegistrationRejectsMismatchedPasswords()
    {
        var model = new RegistroViewModel
        {
            Nombre = "Synthetic User", Correo = "person@example.test", RolId = 1,
            Password = "test-password-123", ConfirmarPassword = "different-password"
        };
        var errors = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(model, new ValidationContext(model), errors, true));
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(model.ConfirmarPassword)));
    }

    [Fact]
    public void PublicationValidationRejectsOversizedContentAndInvalidImageUrl()
    {
        var model = new PublicacionFormViewModel { IdComunidad = 1, Contenido = new string('x', 2001), ImagenUrl = "invalid" };
        var errors = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(model, new ValidationContext(model), errors, true));
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(model.Contenido)));
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(model.ImagenUrl)));
    }
}
