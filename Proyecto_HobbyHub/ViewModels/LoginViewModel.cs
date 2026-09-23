using System.ComponentModel.DataAnnotations;

namespace Proyecto_HobbyHub.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "El correo electrónico es requerido.")]
        [EmailAddress(ErrorMessage = "Ingrese un correo válido.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es requerida.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}