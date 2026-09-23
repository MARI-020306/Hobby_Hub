using System;
using System.Security.Cryptography;
using System.Text;

namespace Proyecto_HobbyHub.Models
{
    public static class SecurityHelper
    {
        // Convierte el texto de la contraseña a un arreglo de bytes (VARBINARY) usando SHA256
        public static byte[] HashDataToBytes(string input)
        {
            if (string.IsNullOrEmpty(input))
                return Array.Empty<byte>();

            using var sha256 = SHA256.Create();

            return sha256.ComputeHash(
                Encoding.UTF8.GetBytes(input.Trim())
            );
        }

        // Compara dos arreglos de bytes (byte[])
        public static bool VerifyBytes(byte[] hash1, byte[] hash2)
        {
            if (hash1 == null || hash2 == null)
                return false;

            if (hash1.Length != hash2.Length)
                return false;

            for (int i = 0; i < hash1.Length; i++)
            {
                if (hash1[i] != hash2[i])
                    return false;
            }

            return true;
        }

        // Genera Hash determinista (SHA256 Hexadecimal) para búsquedas o texto sensible
        public static string HashEmail(string email)
        {
            if (string.IsNullOrEmpty(email))
                return string.Empty;

            using var sha256 = SHA256.Create();

            byte[] bytes = sha256.ComputeHash(
                Encoding.UTF8.GetBytes(email.Trim().ToLower())
            );

            var builder = new StringBuilder();

            foreach (var b in bytes)
            {
                builder.Append(b.ToString("x2"));
            }

            return builder.ToString();
        }
    }
}