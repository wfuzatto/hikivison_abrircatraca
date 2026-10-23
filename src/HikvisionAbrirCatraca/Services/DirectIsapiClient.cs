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

    // Permite teste isolado do transporte HTTP sem conexão com uma catraca física.
    internal DirectIsapiClient(ManagedGate gate, HttpClient http)
    {
        _gate = gate;
        _http = http;
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

    /// <summary>
    /// Passagem livre: alwaysOpen. Retorno ao controle normal: close.
    /// Validado em campo para DS-K6B411TX-M V3.0.0, conforme relato do operador.
    /// </summary>
    public async Task<DirectGateResult> SetFreePassageAsync(bool enable, CancellationToken ct = default)
    {
        var path = $"ISAPI/AccessControl/RemoteControl/door/{_gate.DoorNo}";
        var command = enable ? "alwaysOpen" : "close";
        var xml =
            "<RemoteControlDoor version=\"2.0\" xmlns=\"http://www.isapi.org/ver20/XMLSchema\">" +
            "<cmd>" + command + "</cmd>" +
            "</RemoteControlDoor>";
        try
        {
            using var response = await SendAsync(HttpMethod.Put, path, xml, ct);
            var raw = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
                return new DirectGateResult(
                    _gate.Id, _gate.Name, _gate.Host, false,
                    DescribeFailure(response.StatusCode, raw));

            if (!IsConfirmedDoorCommand(raw))
                return new DirectGateResult(
                    _gate.Id, _gate.Name, _gate.Host, false,
                    "Comando " + command + " nao confirmado: " + DescribeFailure(response.StatusCode, raw));

            return new DirectGateResult(
                _gate.Id, _gate.Name, _gate.Host, true,
                enable
                    ? "Passagem livre aceita pelo ISAPI (alwaysOpen)."
                    : "Controle de acesso normal restaurado pelo ISAPI (close).");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new DirectGateResult(
                _gate.Id, _gate.Name, _gate.Host, false,
                "Falha ao enviar " + command + ": " + ex.Message);
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

    // Somente status ISAPI explícito pode alterar o modo persistido da catraca.
    // Não basta HTTP 200 e não basta body vazio.
    private static bool IsConfirmedDoorCommand(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return false;

        try
        {
            var doc = XDocument.Parse(raw);
            if (doc.Root?.Name.LocalName != "ResponseStatus") return false;

            string? Field(string name) => doc.Root.Elements()
                .FirstOrDefault(x => x.Name.LocalName == name)?.Value?.Trim();

            return Field("statusCode") == "1" &&
                (string.IsNullOrWhiteSpace(Field("statusString")) ||
                    string.Equals(Field("statusString"), "OK", StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(Field("subStatusCode")) ||
                    string.Equals(Field("subStatusCode"), "ok", StringComparison.OrdinalIgnoreCase)) &&
                string.IsNullOrWhiteSpace(Field("errorMsg"));
        }
        catch
        {
            return false;
        }
    }

    private static string DescribeFailure(HttpStatusCode code, string raw)
    {
        var parts = new List<string>();
        if (string.IsNullOrWhiteSpace(raw))
        {
            parts.Add("resposta vazia, sem confirmacao ISAPI");
        }
        else
        {
            try
            {
                var xml = XDocument.Parse(raw);
                foreach (var field in new[] { "statusCode", "statusString", "subStatusCode", "errorMsg" })
                {
                    var value = xml.Descendants()
                        .FirstOrDefault(x => x.Name.LocalName == field)?.Value?.Trim();
                    if (!string.IsNullOrWhiteSpace(value))
                        parts.Add(field + "=" + SafeLogValue(value));
                }
                if (parts.Count == 0)
                    parts.Add("XML sem campos de status ISAPI");
            }
            catch
            {
                parts.Add("resposta nao e XML de status ISAPI");
            }
        }

        var prefix = code == HttpStatusCode.Unauthorized ? "Usuario ou senha rejeitados. " : "";
        return prefix + $"HTTP {(int)code}: " + string.Join("; ", parts);
    }

    // Registra somente os campos de status retornados; nunca registra request,
    // senha ou cabecalhos de autenticacao.
    private static string SafeLogValue(string value)
    {
        var clean = new string(value.Select(ch => char.IsControl(ch) ? ' ' : ch).ToArray()).Trim();
        return clean.Length <= 1024 ? clean : clean[..1024] + "…";
    }

    public void Dispose() => _http.Dispose();
}
