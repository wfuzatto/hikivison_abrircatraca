using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HikvisionAbrirCatraca.Models;

namespace HikvisionAbrirCatraca.Services;

public static class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ValeMantiqueira",
        "HikvisionAbrirCatraca");

    public static string SettingsPath => Path.Combine(DataDirectory, "settings.json");
    public static string LogPath => Path.Combine(DataDirectory, "operations.log");

    public static AppSettings Load()
    {
        Directory.CreateDirectory(DataDirectory);
        if (!File.Exists(SettingsPath))
            return new AppSettings();

        try
        {
            var stored = JsonSerializer.Deserialize<StoredSettings>(File.ReadAllText(SettingsPath), JsonOptions)
                         ?? new StoredSettings();

            return new AppSettings
            {
                BaseUrl = stored.BaseUrl,
                AppKey = stored.AppKey,
                AppSecret = Unprotect(stored.ProtectedAppSecret),
                UserId = string.IsNullOrWhiteSpace(stored.UserId) ? "admin" : stored.UserId,
                VerifyTls = stored.VerifyTls,
                Groups = stored.Groups is { Count: > 0 } ? stored.Groups : AppSettings.DefaultGroups(),
                ManagedGates = stored.ManagedGates ?? [],
                AdminPasswordSalt = stored.AdminPasswordSalt,
                AdminPasswordHash = stored.AdminPasswordHash
            };
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(DataDirectory);
        var stored = new StoredSettings
        {
            BaseUrl = settings.BaseUrl.Trim().TrimEnd('/'),
            AppKey = settings.AppKey.Trim(),
            ProtectedAppSecret = Protect(settings.AppSecret),
            UserId = settings.UserId.Trim(),
            VerifyTls = settings.VerifyTls,
            Groups = settings.Groups,
            ManagedGates = settings.ManagedGates,
            AdminPasswordSalt = settings.AdminPasswordSalt,
            AdminPasswordHash = settings.AdminPasswordHash
        };
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(stored, JsonOptions));
    }

    public static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(DataDirectory);
            File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }

    private static string Protect(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var bytes = Encoding.UTF8.GetBytes(value);
        var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    private static string Unprotect(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        try
        {
            var bytes = Convert.FromBase64String(value);
            return Encoding.UTF8.GetString(
                ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser));
        }
        catch
        {
            return "";
        }
    }
}
