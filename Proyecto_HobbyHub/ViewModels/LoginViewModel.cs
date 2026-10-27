using System.ComponentModel.DataAnnotations;
using Proyecto_HobbyHub.Security;

namespace Proyecto_HobbyHub.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "El correo electrónico es requerido.")]
        [EmailAddress(ErrorMessage = "Ingrese un correo válido.")]
        [StringLength(254)]
        public string Email { get => _email; set => _email = value?.Trim() ?? string.Empty; }
        private string _email = string.Empty;

        [Required(ErrorMessage = "La contraseña es requerida.")]
        [DataType(DataType.Password)]
        [BcryptPassword]
        public string Password { get; set; } = string.Empty;
        [StringLength(2048)]
        public string? ReturnUrl { get; set; }
    }
}
