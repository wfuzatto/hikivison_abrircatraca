using System.Net;
using System.Text;
using HikvisionAbrirCatraca.Models;
using HikvisionAbrirCatraca.Services;

internal static class Program
{
    private const string OkXml = "<ResponseStatus xmlns=\"http://www.isapi.org/ver20/XMLSchema\"><statusCode>1</statusCode><statusString>OK</statusString><subStatusCode>ok</subStatusCode></ResponseStatus>";
    private const string ErrorXml = "<ResponseStatus xmlns=\"http://www.isapi.org/ver20/XMLSchema\"><statusCode>5</statusCode><statusString>Invalid XML Content</statusString><subStatusCode>badXmlContent</subStatusCode><errorMsg>unsupported command</errorMsg></ResponseStatus>";
    private static int _failures;

    public static async Task<int> Main()
    {
        await Case("LIBERAR sends alwaysOpen", async () => {
            using var f = Fake(HttpStatusCode.OK, OkXml);
            var r = await f.Client.SetFreePassageAsync(true);
            Check(r.Success, r.Description);
            RequestMatches(f.Handler, "alwaysOpen");
        });
        await Case("FECHAR sends close", async () => {
            using var f = Fake(HttpStatusCode.OK, OkXml);
            var r = await f.Client.SetFreePassageAsync(false);
            Check(r.Success, r.Description);
            RequestMatches(f.Handler, "close");
        });
        await Case("ABRIR remains open", async () => {
            using var f = Fake(HttpStatusCode.OK, OkXml);
            var r = await f.Client.OpenAsync();
            Check(r.Success, r.Description);
            RequestMatches(f.Handler, "open");
        });
        await Case("FECHAR rejects empty HTTP 200", async () => {
            using var f = Fake(HttpStatusCode.OK, "");
            var r = await f.Client.SetFreePassageAsync(false);
            Check(!r.Success && r.Description.Contains("resposta vazia"), r.Description);
        });
        await Case("LIBERAR rejects empty HTTP 200", async () => {
            using var f = Fake(HttpStatusCode.OK, "");
            var r = await f.Client.SetFreePassageAsync(true);
            Check(!r.Success, "HTTP 200 sem corpo não confirma");
        });
        await Case("FECHAR reports HTTP 400 fields", async () => {
            using var f = Fake(HttpStatusCode.BadRequest, ErrorXml);
            var r = await f.Client.SetFreePassageAsync(false);
            Check(!r.Success, "HTTP 400 não confirma");
            foreach (var s in new[] {"HTTP 400", "statusCode=5", "statusString=Invalid XML Content", "subStatusCode=badXmlContent", "errorMsg=unsupported command"})
                Check(r.Description.Contains(s), "Campo não encontrado: " + s);
        });
        await Case("FECHAR rejects ISAPI failure within HTTP 200", async () => {
            using var f = Fake(HttpStatusCode.OK, ErrorXml);
            var r = await f.Client.SetFreePassageAsync(false);
            Check(!r.Success && r.Description.Contains("statusCode=5"), r.Description);
        });
        await Case("FECHAR rejects invalid XML", async () => {
            using var f = Fake(HttpStatusCode.OK, "<ResponseStatus>");
            var r = await f.Client.SetFreePassageAsync(false);
            Check(!r.Success, "XML malformado não confirma");
        });
        await Case("FECHAR requires explicit statusCode=1", async () => {
            using var f = Fake(HttpStatusCode.OK, "<ResponseStatus><statusString>OK</statusString></ResponseStatus>");
            var r = await f.Client.SetFreePassageAsync(false);
            Check(!r.Success, "statusCode ausente não confirma");
        });
        await Case("FECHAR rejects contradictory subStatusCode", async () => {
            using var f = Fake(HttpStatusCode.OK, "<ResponseStatus><statusCode>1</statusCode><subStatusCode>error</subStatusCode></ResponseStatus>");
            var r = await f.Client.SetFreePassageAsync(false);
            Check(!r.Success, "Substatus de erro não confirma");
        });
        await Case("HTTP 401 contains no credentials", async () => {
            using var f = Fake(HttpStatusCode.Unauthorized, ErrorXml);
            var r = await f.Client.SetFreePassageAsync(false);
            Check(!r.Success && r.Description.Contains("HTTP 401"), r.Description);
            Check(!r.Description.Contains("fake-password") && !r.Description.Contains("fake-user"), "Credencial exposta");
        });
        Console.WriteLine($"Resultado: {11 - _failures}/11 aprovados");
        return _failures == 0 ? 0 : 1;
    }

    private static async Task Case(string label, Func<Task> test)
    {
        try { await test(); Console.WriteLine("PASS " + label); }
        catch (Exception ex) { _failures++; Console.Error.WriteLine("FAIL " + label + ": " + ex.Message); }
    }

    private static void Check(bool ok, string details)
    {
        if (!ok) throw new InvalidOperationException(details);
    }

    private static TestContext Fake(HttpStatusCode code, string body)
    {
        var handler = new StubHandler(code, body);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:8080/") };
        return new TestContext(new DirectIsapiClient(new ManagedGate(), http), handler);
    }

    private static void RequestMatches(StubHandler fake, string command)
    {
        Check(fake.Count == 1, "Deve haver exatamente uma chamada simulada");
        Check(fake.Method == HttpMethod.Put, "Método deve ser PUT");
        Check(fake.Path == "/ISAPI/AccessControl/RemoteControl/door/1", "Endpoint incorreto");
        Check(fake.MediaType == "application/xml" && fake.Charset == "utf-8", "Cabeçalho Content-Type incorreto");
        var xml = $"<RemoteControlDoor version=\"2.0\" xmlns=\"http://www.isapi.org/ver20/XMLSchema\"><cmd>{command}</cmd></RemoteControlDoor>";
        Check(fake.Body == xml, "XML enviado incorreto: " + fake.Body);
    }

    private sealed class TestContext(DirectIsapiClient client, StubHandler handler) : IDisposable
    {
        public DirectIsapiClient Client { get; } = client;
        public StubHandler Handler { get; } = handler;
        public void Dispose() => Client.Dispose();
    }

    // Este handler intercepta toda chamada em memória: não existe tráfego para catracas reais.
    private sealed class StubHandler(HttpStatusCode status, string xml) : HttpMessageHandler
    {
        public int Count { get; private set; }
        public HttpMethod? Method { get; private set; }
        public string? Path { get; private set; }
        public string? Body { get; private set; }
        public string? MediaType { get; private set; }
        public string? Charset { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Count++;
            Method = request.Method;
            Path = request.RequestUri?.AbsolutePath;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            MediaType = request.Content?.Headers.ContentType?.MediaType;
            Charset = request.Content?.Headers.ContentType?.CharSet;
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(xml, Encoding.UTF8, "application/xml")
            };
        }
    }
}
