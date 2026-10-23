// Modelo usado somente nos testes. Nenhuma credencial ou equipamento real é acessado.
namespace HikvisionAbrirCatraca.Models;
public sealed class ManagedGate
{
    public string Id { get; set; } = "test";
    public string Name { get; set; } = "Simulada";
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 8080;
    public int DoorNo { get; set; } = 1;
    public string Username { get; set; } = "fake-user";
    public string Password { get; set; } = "fake-password";
    public bool UseHttps { get; set; }
    public bool VerifyTls { get; set; } = true;
    public string Endpoint => $"http://{Host}:{Port}";
}
