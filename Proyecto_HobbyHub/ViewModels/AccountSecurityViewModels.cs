using System.ComponentModel.DataAnnotations;
using Proyecto_HobbyHub.Security;

namespace Proyecto_HobbyHub.ViewModels;

public class RequestRecoveryViewModel
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get => _email; set => _email = value?.Trim() ?? string.Empty; }
    private string _email = string.Empty;
}
public class ResetPasswordViewModel
{
    [Required, StringLength(43, MinimumLength = 43), RegularExpression("^[A-Za-z0-9_-]{43}$")]
    public string Token { get; set; } = string.Empty;
    [Required, MinLength(8), BcryptPassword, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
    [Required, Compare(nameof(Password)), DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;
}
public class ChangePasswordViewModel
{
    [Required, BcryptPassword, DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;
    [Required, MinLength(8), BcryptPassword, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
    [Required, Compare(nameof(Password)), DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;
}
