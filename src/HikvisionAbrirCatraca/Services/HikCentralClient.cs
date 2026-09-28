using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HikvisionAbrirCatraca.Models;

namespace HikvisionAbrirCatraca.Services;

public sealed class HikCentralException(string message) : Exception(message);

public sealed record DoorInfo(string Id, string Name);
public sealed record DoorControlResult(string DoorId, bool Success, string Description);

public sealed class HikCentralClient : IDisposable
{
    private readonly AppSettings _settings;
    private readonly HttpClient _http;

    public HikCentralClient(AppSettings settings)
    {
        _settings = settings;
        var handler = new HttpClientHandler();
        if (!settings.VerifyTls)
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

        _http = new HttpClient(handler)
        {
            BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(15)
        };
    }

    public async Task<IReadOnlyList<DoorInfo>> GetDoorsForGroupAsync(GateGroup group, CancellationToken ct = default)
    {
        for (var page = 1; page <= 100; page++)
        {
            using var doc = await PostAsync("/artemis/api/acs/v1/privilege/group",
                new { pageNo = page, pageSize = 100, type = 2 }, ct);

            var data = doc.RootElement.TryGetProperty("data", out var d) ? d : default;
            if (data.ValueKind != JsonValueKind.Object)
                throw new HikCentralException("HikCentral não retornou a lista de Access Levels.");

            var list = data.TryGetProperty("list", out var l) && l.ValueKind == JsonValueKind.Array
                ? l
                : default;

            if (list.ValueKind == JsonValueKind.Array)
            {
                foreach (var level in list.EnumerateArray())
                {
                    var name = GetString(level, "privilegeGroupName");
                    if (!group.CandidateNames().Any(x => string.Equals(x.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase)))
                        continue;

                    if (!level.TryGetProperty("ElementList", out var elements) || elements.ValueKind != JsonValueKind.Array)
                        throw new HikCentralException($"O Access Level {group.Name} existe, mas não possui catracas vinculadas.");

                    var doors = new List<DoorInfo>();
                    foreach (var entry in elements.EnumerateArray())
                    {
                        if (!entry.TryGetProperty("Element", out var element) || element.ValueKind != JsonValueKind.Object)
                            continue;

                        var id = GetString(element, "ID");
                        if (string.IsNullOrWhiteSpace(id)) continue;

                        var doorName = id;
                        if (element.TryGetProperty("BaseInfo", out var baseInfo) && baseInfo.ValueKind == JsonValueKind.Object)
                        {
                            var candidate = GetString(baseInfo, "Name");
                            if (!string.IsNullOrWhiteSpace(candidate)) doorName = candidate;
                        }
                        doors.Add(new DoorInfo(id, doorName));
                    }

                    if (doors.Count == 0)
                        throw new HikCentralException($"O Access Level {group.Name} não retornou IDs de portas.");

                    return doors
                        .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
                        .Select(x => x.First())
                        .ToArray();
                }
            }

            var total = data.TryGetProperty("total", out var totalEl) && totalEl.TryGetInt32(out var t) ? t : 0;
            if (total <= page * 100 || (list.ValueKind == JsonValueKind.Array && list.GetArrayLength() < 100))
                break;
        }

        throw new HikCentralException(
            $"Access Level '{group.Name}' não encontrado. Confira o nome no HikCentral e as permissões da OpenAPI.");
    }

    public async Task<IReadOnlyList<DoorControlResult>> OpenDoorsAsync(
        IReadOnlyList<DoorInfo> doors, int controlDirection, CancellationToken ct = default)
    {
        if (doors.Count == 0) throw new HikCentralException("Nenhuma catraca foi selecionada.");
        if (doors.Count > 10) throw new HikCentralException("A API permite no máximo 10 portas por comando.");

        using var doc = await PostAsync("/artemis/api/acs/v1/door/doControl", new
        {
            doorIndexCodes = doors.Select(x => x.Id).ToArray(),
            controlType = 2,
            controlDirection
        }, ct);

        var results = new List<DoorControlResult>();
        if (doc.RootElement.TryGetProperty("data", out var data))
            CollectDoorResults(data, results);

        if (results.Count == 0)
            return doors.Select(x => new DoorControlResult(x.Id, true, "Comando aceito pelo HikCentral")).ToArray();

        return doors.Select(door =>
        {
            var match = results.FirstOrDefault(x => string.Equals(x.DoorId, door.Id, StringComparison.OrdinalIgnoreCase));
            return match ?? new DoorControlResult(door.Id, true, "Comando aceito pelo HikCentral");
        }).ToArray();
    }

    public async Task<Dictionary<string, int>> TestGroupsAsync(IEnumerable<GateGroup> groups, CancellationToken ct = default)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups)
            result[group.Name] = (await GetDoorsForGroupAsync(group, ct)).Count;
        return result;
    }

    private async Task<JsonDocument> PostAsync(string path, object body, CancellationToken ct)
    {
        if (!_settings.IsConfigured)
            throw new HikCentralException("Configure URL, AppKey e AppSecret primeiro.");

        var json = JsonSerializer.Serialize(body);
        var bodyBytes = Encoding.UTF8.GetBytes(json);
        var contentMd5Bytes = MD5.HashData(bodyBytes);
        var contentMd5 = Convert.ToBase64String(contentMd5Bytes);
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        const string accept = "*/*";
        const string contentType = "application/json";
        const string signatureHeaders = "x-ca-key,x-ca-nonce,x-ca-timestamp";

        var stringToSign =
            $"POST\n{accept}\n{contentMd5}\n{contentType}\n" +
            $"x-ca-key:{_settings.AppKey}\n" +
            $"x-ca-nonce:{nonce}\n" +
            $"x-ca-timestamp:{timestamp}\n" +
            path;

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_settings.AppSecret));
        var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));

        using var request = new HttpRequestMessage(HttpMethod.Post, path.TrimStart('/'));
        request.Content = new ByteArrayContent(bodyBytes);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        request.Content.Headers.ContentMD5 = contentMd5Bytes;

        request.Headers.TryAddWithoutValidation("Accept", accept);
        request.Headers.TryAddWithoutValidation("x-ca-key", _settings.AppKey);
        request.Headers.TryAddWithoutValidation("x-ca-nonce", nonce);
        request.Headers.TryAddWithoutValidation("x-ca-timestamp", timestamp);
        request.Headers.TryAddWithoutValidation("x-ca-signature-headers", signatureHeaders);
        request.Headers.TryAddWithoutValidation("x-ca-signature", signature);
        request.Headers.TryAddWithoutValidation("userId",
            string.IsNullOrWhiteSpace(_settings.UserId) ? "admin" : _settings.UserId);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new HikCentralException("Tempo esgotado ao comunicar com o HikCentral.");
        }
        catch (HttpRequestException ex)
        {
            throw new HikCentralException("Falha de comunicação com o HikCentral: " + ex.Message);
        }

        using (response)
        {
            var raw = await response.Content.ReadAsStringAsync(ct);
            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(raw);
            }
            catch
            {
                var summary = raw.Length > 240 ? raw[..240] : raw;
                throw new HikCentralException($"HikCentral HTTP {(int)response.StatusCode}: resposta inválida: {summary}");
            }

            var root = doc.RootElement;
            var code = GetString(root, "code");
            var success = response.IsSuccessStatusCode &&
                          (string.IsNullOrWhiteSpace(code) || code == "0" || code.Equals("success", StringComparison.OrdinalIgnoreCase));

            if (!success)
            {
                var msg = GetString(root, "msg");
                doc.Dispose();
                throw new HikCentralException($"HikCentral ({(string.IsNullOrWhiteSpace(code) ? ((int)response.StatusCode).ToString() : code)}): {msg}");
            }

            return doc;
        }
    }

    private static void CollectDoorResults(JsonElement element, List<DoorControlResult> results)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
                CollectDoorResults(child, results);
            return;
        }

        if (element.ValueKind != JsonValueKind.Object) return;

        var doorId = GetString(element, "doorIndexCode");
        if (!string.IsNullOrWhiteSpace(doorId))
        {
            var code = element.TryGetProperty("controlResultCode", out var c) && c.TryGetInt32(out var n) ? n : 0;
            var desc = GetString(element, "controlResultDesc");
            results.Add(new DoorControlResult(doorId, code == 0, string.IsNullOrWhiteSpace(desc) ? (code == 0 ? "Success" : $"Erro {code}") : desc));
        }

        foreach (var property in element.EnumerateObject())
            if (property.Value.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
                CollectDoorResults(property.Value, results);
    }

    private static string GetString(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var value))
            return "";
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ToString();
    }

    public void Dispose() => _http.Dispose();
}
