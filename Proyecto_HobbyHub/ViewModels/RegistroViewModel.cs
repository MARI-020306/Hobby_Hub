using System.ComponentModel.DataAnnotations;

namespace Proyecto_HobbyHub.ViewModels
{
    public class RegistroViewModel
    {
        [Required(ErrorMessage = "El nombre completo es requerido.")]
        public string Nombre { get; set; } = string.Empty;

        // Propiedad / Alias para compatibilidad con las vistas que buscan 'NombreCompleto'
        public string NombreCompleto
        {
            get => Nombre;
            set => Nombre = value;
        }

        [Required(ErrorMessage = "El correo electrónico es requerido.")]
        [EmailAddress(ErrorMessage = "Ingrese un correo válido.")]
        public string Correo { get; set; } = string.Empty;

        // Propiedad / Alias para compatibilidad con 'Email'
        public string Email
        {
            get => Correo;
            set => Correo = value;
        }

        [Required(ErrorMessage = "La contraseña es requerida.")]
        [MinLength(8, ErrorMessage = "La contraseña debe tener mínimo 8 caracteres.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        // Propiedad / Alias para compatibilidad con 'Contrasena'
        public string Contrasena
        {
            get => Password;
            set => Password = value;
        }

        [Required(ErrorMessage = "Confirme su contraseña.")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Las contraseñas no coinciden.")]
        public string ConfirmarPassword { get; set; } = string.Empty;

        // Propiedad / Alias para compatibilidad con 'ConfirmarContrasena'
        public string ConfirmarContrasena
        {
            get => ConfirmarPassword;
            set => ConfirmarPassword = value;
        }

        public string? Celular { get; set; }

        public string? Direccion { get; set; }

        [Required(ErrorMessage = "Selecciona un rol.")]
        public int RolId { get; set; }

        // Propiedad / Alias para compatibilidad con 'IdRol'
        public int IdRol
        {
            get => RolId;
            set => RolId = value;
        }
    }
}