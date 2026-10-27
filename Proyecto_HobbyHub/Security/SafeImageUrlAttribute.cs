using System.ComponentModel.DataAnnotations;

namespace Proyecto_HobbyHub.Security;

public sealed class SafeImageUrlAttribute : ValidationAttribute
{
    public SafeImageUrlAttribute() => ErrorMessage="La imagen debe usar una URL HTTPS sin credenciales.";
    public override bool IsValid(object? value) => value is null || value is string s &&
        (string.IsNullOrWhiteSpace(s) || Uri.TryCreate(s,UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&string.IsNullOrEmpty(uri.UserInfo));
}
