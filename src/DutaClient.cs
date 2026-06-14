using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Duta;

/// <summary>
/// The Duta API client.
///
/// <code>
/// var duta = new DutaClient("duta_live_xxx");
/// await duta.Emails.SendAsync(new SendEmailOptions {
///     From = "hello@yourdomain.com",
///     To = new[] { "user@example.com" },
///     Subject = "Hello",
///     Html = "&lt;p&gt;It works!&lt;/p&gt;",
/// });
/// </code>
/// </summary>
public class DutaClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly string _apiKey;
    private readonly string _baseUrl;

    /// <summary>The "emails" resource.</summary>
    public Emails Emails { get; }

    /// <param name="apiKey">Your Duta API key (duta_live_...).</param>
    /// <param name="baseUrl">Override the API base URL.</param>
    /// <param name="httpClient">Optional shared HttpClient. If omitted, one is created and disposed with the client.</param>
    public DutaClient(string apiKey, string baseUrl = "https://api.duta.indra.sh", HttpClient? httpClient = null)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new ArgumentException("A Duta API key is required. Create one at https://app.duta.indra.sh.", nameof(apiKey));
        }

        _apiKey = apiKey;
        _baseUrl = baseUrl.TrimEnd('/');
        _ownsHttp = httpClient is null;
        _http = httpClient ?? new HttpClient();
        Emails = new Emails(this);
    }

    internal async Task<T> RequestAsync<T>(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, _baseUrl + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        if (body is not null)
        {
            var json = JsonSerializer.Serialize(body, JsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw DutaException.FromResponse((int)response.StatusCode, raw);
        }

        if (string.IsNullOrEmpty(raw))
        {
            return default!;
        }

        return JsonSerializer.Deserialize<T>(raw, JsonOptions)!;
    }

    public void Dispose()
    {
        if (_ownsHttp)
        {
            _http.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}
