using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Duta.Tests;

/// <summary>Records each request and answers from a queue, then with an empty page.</summary>
internal sealed class Fake : HttpMessageHandler
{
    private readonly Queue<Func<HttpResponseMessage>> _answers;
    public List<HttpRequestMessage> Requests { get; } = [];
    public List<string?> Bodies { get; } = [];

    private Fake(IEnumerable<Func<HttpResponseMessage>> answers) => _answers = new(answers);

    public static (DutaClient Client, Fake Fake) Create(params Func<HttpResponseMessage>[] answers) =>
        Create(new DutaClientOptions { ApiKey = "duta_test" }, answers);

    public static (DutaClient Client, Fake Fake) Create(DutaClientOptions options, params Func<HttpResponseMessage>[] answers)
    {
        options.ApiKey ??= "duta_test";
        var fake = new Fake(answers);
        var client = new DutaClient(new HttpClient(fake), Options.Create(options)) { Delay = (_, _) => Task.CompletedTask };
        return (client, fake);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Add(request);
        Bodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(ct));
        if (_answers.Count > 0) return _answers.Dequeue()();
        return Json(200, """{"data":[],"has_more":false,"next":null}""");
    }

    public static HttpResponseMessage Json(int status, string body, params (string, string)[] headers)
    {
        var res = new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        foreach (var (k, v) in headers) res.Headers.TryAddWithoutValidation(k, v);
        return res;
    }

    public static HttpResponseMessage Error(int status, string code, params (string, string)[] headers)
    {
        var body = JsonSerializer.Serialize(new { statusCode = status, name = "x", message = $"{code} happened", code, request_id = "req_body" });
        return Json(status, body, [("X-Request-Id", "req_header"), .. headers]);
    }
}
