using System.Security.Cryptography;
using HikvisionAbrirCatraca.Models;

namespace HikvisionAbrirCatraca.Services;

public static class AdminSecurity
{
    private const int Iterations = 150000;
    private const int KeySize = 32;

    public static void SetPassword(AppSettings settings, string password)
    {
        if (password.Length < 4)
            throw new ArgumentException("A senha administrativa deve ter pelo menos 4 caracteres.");

        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);

        settings.AdminPasswordSalt = Convert.ToBase64String(salt);
        settings.AdminPasswordHash = Convert.ToBase64String(hash);
    }

    public static bool Verify(AppSettings settings, string password)
    {
        if (!settings.HasAdminPassword) return false;

        try
        {
            var salt = Convert.FromBase64String(settings.AdminPasswordSalt);
            var expected = Convert.FromBase64String(settings.AdminPasswordHash);
            var actual = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                Iterations,
                HashAlgorithmName.SHA256,
                expected.Length);

            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch
        {
            return false;
        }
    }
}
