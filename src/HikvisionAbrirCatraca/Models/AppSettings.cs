namespace HikvisionAbrirCatraca.Models;

public sealed class AppSettings
{
    public string BaseUrl { get; set; } = "https://127.0.0.1";
    public string AppKey { get; set; } = "";
    public string AppSecret { get; set; } = "";
    public string UserId { get; set; } = "admin";
    public bool VerifyTls { get; set; }
    public List<GateGroup> Groups { get; set; } = DefaultGroups();
    public List<ManagedGate> ManagedGates { get; set; } = [];
    public string AdminPasswordSalt { get; set; } = "";
    public string AdminPasswordHash { get; set; } = "";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl) &&
        !string.IsNullOrWhiteSpace(AppKey) &&
        !string.IsNullOrWhiteSpace(AppSecret);

    public bool HasAdminPassword =>
        !string.IsNullOrWhiteSpace(AdminPasswordSalt) &&
        !string.IsNullOrWhiteSpace(AdminPasswordHash);

    public static List<GateGroup> DefaultGroups() =>
    [
        new()
        {
            Name = "ENTRADA ACQUAVALE",
            ButtonText = "ABRIR ENTRADA\nACQUAVALE",
            ControlDirection = 0,
            Reference = "104.12 · 104.13 · 104.14 · 104.15"
        },
        new()
        {
            Name = "SAIDA ACQUAVALE",
            Aliases = ["SAIDA AQCUAVALE"],
            ButtonText = "ABRIR SAÍDA\nACQUAVALE",
            ControlDirection = 0,
            Reference = "104.22 · 104.23 · 104.24"
        },
        new()
        {
            Name = "CATRACAS SABIA",
            ButtonText = "ABRIR CATRACAS\nSABIÁ",
            ControlDirection = 0,
            Reference = "81.177 · 81.178"
        },
        new()
        {
            Name = "LOJA ACQUAVALE",
            ButtonText = "ABRIR LOJA\nACQUAVALE",
            ControlDirection = 0,
            Reference = "104.89"
        }
    ];
}

public sealed class GateGroup
{
    public string Name { get; set; } = "";
    public List<string> Aliases { get; set; } = [];
    public string ButtonText { get; set; } = "";
    public int ControlDirection { get; set; }
    public string Reference { get; set; } = "";

    public IEnumerable<string> CandidateNames()
    {
        yield return Name;
        foreach (var alias in Aliases.Where(x => !string.IsNullOrWhiteSpace(x)))
            yield return alias;
    }
}

public sealed class ManagedGate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string DoorIndexCode { get; set; } = "";
    public string IpAddress { get; set; } = "";
    public string GroupName { get; set; } = "";
    public int ControlDirection { get; set; }
    public bool Enabled { get; set; } = true;
    public bool ImportedFromHikCentral { get; set; }

    public ManagedGate Clone() => new()
    {
        Id = Id,
        Name = Name,
        DoorIndexCode = DoorIndexCode,
        IpAddress = IpAddress,
        GroupName = GroupName,
        ControlDirection = ControlDirection,
        Enabled = Enabled,
        ImportedFromHikCentral = ImportedFromHikCentral
    };
}

internal sealed class StoredSettings
{
    public string BaseUrl { get; set; } = "https://127.0.0.1";
    public string AppKey { get; set; } = "";
    public string ProtectedAppSecret { get; set; } = "";
    public string UserId { get; set; } = "admin";
    public bool VerifyTls { get; set; }
    public List<GateGroup>? Groups { get; set; }
    public List<ManagedGate>? ManagedGates { get; set; }
    public string AdminPasswordSalt { get; set; } = "";
    public string AdminPasswordHash { get; set; } = "";
}
