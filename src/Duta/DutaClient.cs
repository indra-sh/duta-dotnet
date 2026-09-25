using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Duta;

/// <summary>Client options.</summary>
public sealed class DutaClientOptions
{
    /// <summary>Your API key. Read from DUTA_API_KEY when left empty.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Defaults to https://api.duta.indra.sh, or DUTA_BASE_URL.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Retries after the first attempt. Default 2.</summary>
    public int MaxRetries { get; set; } = 2;

    /// <summary>Per attempt. Default 30 seconds.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// The Duta client. Safe to share, and meant to be: register it once with
/// <c>services.AddDuta(...)</c>, or keep one instance.
/// </summary>
/// <example>
/// <code>
/// var duta = new DutaClient(Environment.GetEnvironmentVariable("DUTA_API_KEY"));
/// var sent = await duta.Emails.SendAsync(new SendEmailRequest {
///     From = "Kedai &lt;resit@kedai.my&gt;", To = ["siti@example.com"],
///     Subject = "Resit #1042", Html = "&lt;p&gt;Terima kasih.&lt;/p&gt;" });
/// </code>
/// </example>
public sealed class DutaClient
{
    /// <summary>The SDK version, sent in the User-Agent.</summary>
    public const string Version = "0.2.0";

    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly string _key;
    private readonly string _baseUrl;
    private readonly int _maxRetries;
    internal Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = Task.Delay;

    /// <summary>Creates a client with its own HttpClient.</summary>
    public DutaClient(string? apiKey = null, DutaClientOptions? options = null)
        : this(new HttpClient(), Merge(options, apiKey))
    {
    }

    /// <summary>Creates a client over the given HttpClient, as dependency injection does.</summary>
    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public DutaClient(HttpClient httpClient, IOptions<DutaClientOptions> options)
        : this(httpClient, options.Value)
    {
    }

    internal DutaClient(HttpClient httpClient, DutaClientOptions options)
    {
        var key = options.ApiKey;
        if (string.IsNullOrEmpty(key)) key = Environment.GetEnvironmentVariable("DUTA_API_KEY");
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Missing API key. Pass it to new DutaClient(key) or set DUTA_API_KEY.");

        _key = key;
        var baseUrl = options.BaseUrl;
        if (string.IsNullOrEmpty(baseUrl)) baseUrl = Environment.GetEnvironmentVariable("DUTA_BASE_URL");
        _baseUrl = (string.IsNullOrEmpty(baseUrl) ? "https://api.duta.indra.sh" : baseUrl).TrimEnd('/');
        _maxRetries = Math.Max(0, options.MaxRetries);
        _http = httpClient;
        _http.Timeout = options.Timeout;

        Emails = new EmailsResource(this);
        Batch = new BatchResource(this);
        Domains = new DomainsResource(this);
        ApiKeys = new ApiKeysResource(this);
        Webhooks = new WebhooksResource(this);
        Suppressions = new SuppressionsResource(this);
        Logs = new LogsResource(this);
        Usage = new UsageResource(this);
    }

    /// <summary>Send and read emails.</summary>
    public EmailsResource Emails { get; }
    /// <summary>Send up to 100 emails in one request.</summary>
    public BatchResource Batch { get; }
    /// <summary>Sending domains.</summary>
    public DomainsResource Domains { get; }
    /// <summary>API keys.</summary>
    public ApiKeysResource ApiKeys { get; }
    /// <summary>Webhook endpoints.</summary>
    public WebhooksResource Webhooks { get; }
    /// <summary>Addresses Duta will not send to.</summary>
    public SuppressionsResource Suppressions { get; }
    /// <summary>The API request log.</summary>
    public LogsResource Logs { get; }
    /// <summary>This month's usage.</summary>
    public UsageResource Usage { get; }

    private static DutaClientOptions Merge(DutaClientOptions? options, string? apiKey)
    {
        var o = options ?? new DutaClientOptions();
        if (!string.IsNullOrEmpty(apiKey)) o.ApiKey = apiKey;
        return o;
    }

    internal static string NewIdempotencyKey() => $"duta-dotnet-{Guid.NewGuid()}";

    /// <summary>
    /// A 429 is retried for any request, after Retry-After. A 5xx or a network failure only
    /// when the request is safe to repeat, since the first attempt may have taken effect.
    /// </summary>
    internal async Task<T> SendAsync<T>(HttpMethod method, string path, object? body = null,
        IReadOnlyDictionary<string, string?>? query = null, IReadOnlyDictionary<string, string?>? headers = null,
        bool idempotent = false, CancellationToken ct = default)
    {
        var url = new StringBuilder(_baseUrl).Append("/v1").Append(path);
        if (query is not null)
        {
            var sep = '?';
            foreach (var (k, v) in query)
            {
                if (string.IsNullOrEmpty(v)) continue;
                url.Append(sep).Append(Uri.EscapeDataString(k)).Append('=').Append(Uri.EscapeDataString(v));
                sep = '&';
            }
        }
        var payload = body is null ? null : JsonSerializer.Serialize(body, body.GetType(), Json);

        for (var attempt = 0; ; attempt++)
        {
            using var request = new HttpRequestMessage(method, url.ToString());
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _key);
            request.Headers.UserAgent.ParseAdd($"duta-dotnet/{Version}");
            request.Headers.Accept.ParseAdd("application/json");
            if (headers is not null)
                foreach (var (k, v) in headers)
                    if (!string.IsNullOrEmpty(v)) request.Headers.TryAddWithoutValidation(k, v);
            if (payload is not null) request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                if (idempotent && attempt < _maxRetries)
                {
                    await Delay(Backoff(attempt), ct).ConfigureAwait(false);
                    continue;
                }
                var timedOut = e is TaskCanceledException;
                throw new DutaException(
                    timedOut ? "Duta did not answer in time" : $"Could not reach Duta: {e.Message}", null,
                    timedOut ? "timeout" : "network_error", timedOut ? "timeout" : "network_error", null, null, e);
            }

            using (response)
            {
                var status = (int)response.StatusCode;
                var retryable = status == 429 || (status >= 500 && idempotent);
                if (retryable && attempt < _maxRetries)
                {
                    await Delay(RetryAfter(response) ?? Backoff(attempt), ct).ConfigureAwait(false);
                    continue;
                }

                var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                    return string.IsNullOrEmpty(text) ? default! : JsonSerializer.Deserialize<T>(text, Json)!;

                throw ToException(response, status, text);
            }
        }
    }

    private static DutaException ToException(HttpResponseMessage response, int status, string text)
    {
        string? name = null, code = null, message = null, requestId = null;
        JsonElement? detail = null;
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                name = Str(root, "name");
                code = Str(root, "code");
                message = Str(root, "message");
                requestId = Str(root, "request_id");
                if (root.TryGetProperty("detail", out var d) && d.ValueKind == JsonValueKind.Object) detail = d.Clone();
            }
        }
        catch (JsonException)
        {
            // Not JSON: an upstream error page. The status says enough.
        }
        requestId ??= response.Headers.TryGetValues("X-Request-Id", out var ids) ? ids.FirstOrDefault() : null;
        return new DutaException(message ?? $"Duta answered {status}", status, name ?? "application_error",
            code ?? "internal_error", requestId, detail);
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// <summary>0.5s, 1s, 2s... with jitter, so clients that failed together do not retry together.</summary>
    private static TimeSpan Backoff(int attempt)
    {
        var baseMs = Math.Min(8000, 500 * Math.Pow(2, attempt));
        return TimeSpan.FromMilliseconds(baseMs / 2 + Random.Shared.NextDouble() * baseMs / 2);
    }

    private static TimeSpan? RetryAfter(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        if (header?.Delta is { } delta) return delta > TimeSpan.FromMinutes(1) ? TimeSpan.FromMinutes(1) : delta;
        if (header?.Date is { } date)
        {
            var wait = date - DateTimeOffset.UtcNow;
            return wait < TimeSpan.Zero ? TimeSpan.Zero : wait > TimeSpan.FromMinutes(1) ? TimeSpan.FromMinutes(1) : wait;
        }
        return null;
    }

    internal static string Seg(string value) => Uri.EscapeDataString(value);

    internal static string? Num(int? n) => n?.ToString(CultureInfo.InvariantCulture);
}
