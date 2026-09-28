using System.Net;
using System.Text;
using System.Xml.Linq;
using HikvisionAbrirCatraca.Models;

namespace HikvisionAbrirCatraca.Services;

public sealed class DirectIsapiException(string message) : Exception(message);

public sealed record DirectGateResult(
    string GateId,
    string GateName,
    string Host,
    bool Success,
    string Description);

public sealed record DeviceInfo(
    string DeviceName,
    string Model,
    string SerialNumber,
    string FirmwareVersion);

public sealed class DirectIsapiClient : IDisposable
{
    private readonly ManagedGate _gate;
    private readonly HttpClient _http;

    public DirectIsapiClient(ManagedGate gate)
    {
        _gate = gate;

        var handler = new HttpClientHandler
        {
            Credentials = new NetworkCredential(gate.Username, gate.Password),
            PreAuthenticate = false
        };

        if (gate.UseHttps && !gate.VerifyTls)
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

        _http = new HttpClient(handler)
        {
            BaseAddress = new Uri(gate.Endpoint.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(8)
        };
    }

    public async Task<DeviceInfo> GetDeviceInfoAsync(CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Get, "ISAPI/System/deviceInfo", null, ct);
        var xml = await response.Content.ReadAsStringAsync(ct);
        EnsureSuccess(response, xml);

        try
        {
            var doc = XDocument.Parse(xml);
            string Find(string name) =>
                doc.Descendants().FirstOrDefault(x => x.Name.LocalName == name)?.Value?.Trim() ?? "";

            return new DeviceInfo(
                Find("deviceName"),
                Find("model"),
                Find("serialNumber"),
                Find("firmwareVersion"));
        }
        catch
        {
            throw new DirectIsapiException("O dispositivo respondeu, mas o DeviceInfo não pôde ser interpretado.");
        }
    }

    public async Task<string> GetRemoteControlCapabilitiesAsync(CancellationToken ct = default)
    {
        using var response = await SendAsync(
            HttpMethod.Get,
            "ISAPI/AccessControl/RemoteControl/door/capabilities",
            null,
            ct);

        var raw = await response.Content.ReadAsStringAsync(ct);
        EnsureSuccess(response, raw);
        return raw;
    }

    public async Task<DirectGateResult> OpenAsync(CancellationToken ct = default)
    {
        var path = $"ISAPI/AccessControl/RemoteControl/door/{_gate.DoorNo}";
        const string xml =
            "<RemoteControlDoor version=\"2.0\" xmlns=\"http://www.isapi.org/ver20/XMLSchema\">" +
            "<cmd>open</cmd>" +
            "</RemoteControlDoor>";

        try
        {
            using var response = await SendAsync(HttpMethod.Put, path, xml, ct);
            var raw = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
                return new DirectGateResult(
                    _gate.Id, _gate.Name, _gate.Host, false,
                    DescribeFailure(response.StatusCode, raw));

            if (IsIsapiSuccess(raw))
                return new DirectGateResult(
                    _gate.Id, _gate.Name, _gate.Host, true,
                    "Catraca aberta diretamente por ISAPI.");

            return new DirectGateResult(
                _gate.Id, _gate.Name, _gate.Host, false,
                "O equipamento respondeu, mas não confirmou status OK.");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return new DirectGateResult(
                _gate.Id, _gate.Name, _gate.Host, false,
                "Timeout ao conectar diretamente na catraca.");
        }
        catch (HttpRequestException ex)
        {
            return new DirectGateResult(
                _gate.Id, _gate.Name, _gate.Host, false,
                "Falha de rede: " + ex.Message);
        }
        catch (Exception ex)
        {
            return new DirectGateResult(
                _gate.Id, _gate.Name, _gate.Host, false,
                ex.Message);
        }
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string relativePath,
        string? xml,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, relativePath);
        request.Headers.TryAddWithoutValidation("Accept", "*/*");

        if (xml is not null)
            request.Content = new StringContent(xml, Encoding.UTF8, "application/xml");

        try
        {
            return await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new DirectIsapiException($"Timeout ao acessar {_gate.Endpoint}.");
        }
        catch (HttpRequestException ex)
        {
            throw new DirectIsapiException($"Não foi possível conectar em {_gate.Endpoint}: {ex.Message}");
        }
    }

    private static void EnsureSuccess(HttpResponseMessage response, string raw)
    {
        if (response.IsSuccessStatusCode) return;
        throw new DirectIsapiException(DescribeFailure(response.StatusCode, raw));
    }

    private static bool IsIsapiSuccess(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return true;

        try
        {
            var doc = XDocument.Parse(raw);
            var statusCode = doc.Descendants()
                .FirstOrDefault(x => x.Name.LocalName == "statusCode")?.Value?.Trim();
            var statusString = doc.Descendants()
                .FirstOrDefault(x => x.Name.LocalName == "statusString")?.Value?.Trim();

            return statusCode == "1" ||
                   string.Equals(statusString, "OK", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string DescribeFailure(HttpStatusCode code, string raw)
    {
        var detail = "";
        try
        {
            var doc = XDocument.Parse(raw);
            detail =
                doc.Descendants().FirstOrDefault(x => x.Name.LocalName == "errorMsg")?.Value?.Trim() ??
                doc.Descendants().FirstOrDefault(x => x.Name.LocalName == "subStatusCode")?.Value?.Trim() ??
                doc.Descendants().FirstOrDefault(x => x.Name.LocalName == "statusString")?.Value?.Trim() ??
                "";
        }
        catch
        {
            detail = raw.Trim();
        }

        if (detail.Length > 220) detail = detail[..220];

        if (code == HttpStatusCode.Unauthorized)
            return "Usuário ou senha rejeitados pela catraca (HTTP 401).";

        return $"HTTP {(int)code}" + (string.IsNullOrWhiteSpace(detail) ? "" : ": " + detail);
    }

    public void Dispose() => _http.Dispose();
}
