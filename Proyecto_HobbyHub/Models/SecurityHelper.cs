using System;
using System.Text;
using System.Security.Cryptography;

namespace Proyecto_HobbyHub.Models
{
    public static class SecurityHelper
    {
        // Genera un hash SHA-256 hexadecimal. Se usa en columnas VARCHAR, por lo que
        // el resultado ocupa 64 caracteres y no permite recuperar el valor original.
        public static string HashPersonalData(string input)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(input);

            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input.Trim()));

            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        public static string HashEmail(string email)
        {
            return HashPersonalData(email.Trim().ToLowerInvariant());
        }

        public static byte[] HashPasswordToBytes(string password)
        {
            string bcryptHash = BCrypt.Net.BCrypt.HashPassword(password);
            return Encoding.UTF8.GetBytes(bcryptHash);
        }

        public static bool VerifyPassword(string password, byte[]? passwordHash)
        {
            if (passwordHash is null || passwordHash.Length == 0)
                return false;

            return BCrypt.Net.BCrypt.Verify(password, Encoding.UTF8.GetString(passwordHash));
        }

    }
}
