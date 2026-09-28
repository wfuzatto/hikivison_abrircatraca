namespace HikvisionAbrirCatraca.Models;

public sealed class AppSettings
{
    public List<GateGroup> Groups { get; set; } = DefaultGroups();
    public List<ManagedGate> ManagedGates { get; set; } = [];
    public string AdminPasswordSalt { get; set; } = "";
    public string AdminPasswordHash { get; set; } = "";

    public bool HasAdminPassword =>
        !string.IsNullOrWhiteSpace(AdminPasswordSalt) &&
        !string.IsNullOrWhiteSpace(AdminPasswordHash);

    public static List<GateGroup> DefaultGroups() =>
    [
        new()
        {
            Name = "ENTRADA ACQUAVALE",
            ButtonText = "ABRIR ENTRADA\nACQUAVALE",
            Reference = "192.168.104.12 · .13 · .14 · .15"
        },
        new()
        {
            Name = "SAIDA ACQUAVALE",
            ButtonText = "ABRIR SAÍDA\nACQUAVALE",
            Reference = "192.168.104.22 · .23 · .24"
        },
        new()
        {
            Name = "CATRACAS SABIA",
            ButtonText = "ABRIR CATRACAS\nSABIÁ",
            Reference = "192.168.81.177 · .178"
        },
        new()
        {
            Name = "LOJA ACQUAVALE",
            ButtonText = "ABRIR LOJA\nACQUAVALE",
            Reference = "192.168.104.89"
        }
    ];
}

public sealed class GateGroup
{
    public string Name { get; set; } = "";
    public string ButtonText { get; set; } = "";
    public string Reference { get; set; } = "";
}

public sealed class ManagedGate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Host { get; set; } = "";
    public int Port { get; set; } = 80;
    public bool UseHttps { get; set; }
    public bool VerifyTls { get; set; }
    public string Username { get; set; } = "admin";
    public string Password { get; set; } = "";
    public int DoorNo { get; set; } = 1;
    public string GroupName { get; set; } = "";
    public bool Enabled { get; set; } = true;

    public string Endpoint =>
        $"{(UseHttps ? "https" : "http")}://{Host}:{Port}";

    public ManagedGate Clone() => new()
    {
        Id = Id,
        Name = Name,
        Host = Host,
        Port = Port,
        UseHttps = UseHttps,
        VerifyTls = VerifyTls,
        Username = Username,
        Password = Password,
        DoorNo = DoorNo,
        GroupName = GroupName,
        Enabled = Enabled
    };
}

internal sealed class StoredSettings
{
    public List<GateGroup>? Groups { get; set; }
    public List<StoredManagedGate>? ManagedGates { get; set; }
    public string AdminPasswordSalt { get; set; } = "";
    public string AdminPasswordHash { get; set; } = "";
}

internal sealed class StoredManagedGate
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Host { get; set; } = "";
    public int Port { get; set; } = 80;
    public bool UseHttps { get; set; }
    public bool VerifyTls { get; set; }
    public string Username { get; set; } = "admin";
    public string ProtectedPassword { get; set; } = "";
    public int DoorNo { get; set; } = 1;
    public string GroupName { get; set; } = "";
    public bool Enabled { get; set; } = true;
}
