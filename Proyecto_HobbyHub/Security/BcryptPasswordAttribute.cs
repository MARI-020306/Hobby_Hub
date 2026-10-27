using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Proyecto_HobbyHub.Security;

public sealed class BcryptPasswordAttribute : ValidationAttribute
{
    public BcryptPasswordAttribute() => ErrorMessage = "La contraseña no puede superar 72 bytes UTF-8.";
    public override bool IsValid(object? value) => value is null ||
        value is string password && Encoding.UTF8.GetByteCount(password) <= 72;
}
