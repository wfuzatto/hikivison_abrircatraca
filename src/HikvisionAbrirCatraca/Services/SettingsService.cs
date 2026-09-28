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
                Groups = stored.Groups is { Count: > 0 } ? stored.Groups : AppSettings.DefaultGroups(),
                ManagedGates = (stored.ManagedGates ?? []).Select(ToRuntimeGate).ToList(),
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
            Groups = settings.Groups,
            ManagedGates = settings.ManagedGates.Select(ToStoredGate).ToList(),
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

    private static StoredManagedGate ToStoredGate(ManagedGate gate) => new()
    {
        Id = gate.Id,
        Name = gate.Name,
        Host = gate.Host,
        Port = gate.Port,
        UseHttps = gate.UseHttps,
        VerifyTls = gate.VerifyTls,
        Username = gate.Username,
        ProtectedPassword = Protect(gate.Password),
        DoorNo = gate.DoorNo,
        GroupName = gate.GroupName,
        Enabled = gate.Enabled
    };

    private static ManagedGate ToRuntimeGate(StoredManagedGate gate) => new()
    {
        Id = string.IsNullOrWhiteSpace(gate.Id) ? Guid.NewGuid().ToString("N") : gate.Id,
        Name = gate.Name,
        Host = gate.Host,
        Port = gate.Port is > 0 and <= 65535 ? gate.Port : (gate.UseHttps ? 443 : 80),
        UseHttps = gate.UseHttps,
        VerifyTls = gate.VerifyTls,
        Username = string.IsNullOrWhiteSpace(gate.Username) ? "admin" : gate.Username,
        Password = Unprotect(gate.ProtectedPassword),
        DoorNo = gate.DoorNo > 0 ? gate.DoorNo : 1,
        GroupName = gate.GroupName,
        Enabled = gate.Enabled
    };

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
