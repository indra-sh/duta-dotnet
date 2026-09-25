using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Duta;

/// <summary>
/// Verify a webhook delivery from Duta. Deliveries are signed per the Standard Webhooks spec,
/// as Resend's are: HMAC-SHA256 over "id.timestamp.body", keyed with the base64 part of the
/// whsec_ secret, sent as "v1,&lt;base64&gt;".
/// </summary>
public static class WebhookSignature
{
    /// <summary>
    /// Returns the event when the signature is valid and recent (within five minutes unless
    /// <paramref name="tolerance"/> says otherwise), and throws WebhookVerificationException
    /// otherwise. Pass the raw request body and the request headers, in any case.
    /// </summary>
    /// <example>
    /// ASP.NET Core:
    /// <code>
    /// var body = await new StreamReader(Request.Body).ReadToEndAsync();
    /// var evt = WebhookSignature.Verify(body, Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()), secret);
    /// </code>
    /// </example>
    public static JsonElement Verify(string payload, IEnumerable<KeyValuePair<string, string>> headers, string secret,
        TimeSpan? tolerance = null) =>
        VerifyAt(payload, headers, secret, tolerance ?? TimeSpan.FromMinutes(5), DateTimeOffset.UtcNow);

    /// <summary>As <see cref="Verify(string, IEnumerable{KeyValuePair{string, string}}, string, TimeSpan?)"/>, decoded into <typeparamref name="T"/>.</summary>
    public static T Verify<T>(string payload, IEnumerable<KeyValuePair<string, string>> headers, string secret,
        TimeSpan? tolerance = null) =>
        Verify(payload, headers, secret, tolerance).Deserialize<T>(DutaClient.Json)
        ?? throw new WebhookVerificationException("Payload is empty");

    internal static JsonElement VerifyAt(string payload, IEnumerable<KeyValuePair<string, string>> headers, string secret,
        TimeSpan tolerance, DateTimeOffset now)
    {
        var h = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in headers) h[k] = v;
        string? Get(string a, string b) => h.TryGetValue(a, out var x) ? x : h.TryGetValue(b, out var y) ? y : null;

        var id = Get("webhook-id", "svix-id");
        var timestamp = Get("webhook-timestamp", "svix-timestamp");
        var signature = Get("webhook-signature", "svix-signature");
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(timestamp) || string.IsNullOrEmpty(signature))
            throw new WebhookVerificationException("Missing webhook-id, webhook-timestamp or webhook-signature header");

        if (!long.TryParse(timestamp, out var sent) || Math.Abs(now.ToUnixTimeSeconds() - sent) > tolerance.TotalSeconds)
            throw new WebhookVerificationException("Timestamp is outside the allowed window");

        byte[] key;
        try
        {
            key = Convert.FromBase64String(secret.StartsWith("whsec_", StringComparison.Ordinal) ? secret[6..] : secret);
        }
        catch (FormatException)
        {
            throw new WebhookVerificationException("Signing secret is not valid");
        }

        var expected = Convert.ToBase64String(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes($"{id}.{timestamp}.{payload}")));
        var expectedBytes = Encoding.ASCII.GetBytes(expected);

        // Several signatures may be sent, space separated, during a secret rotation.
        foreach (var part in signature.Split(' '))
        {
            var comma = part.IndexOf(',');
            if (comma < 0 || part[..comma] != "v1") continue;
            if (CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(part[(comma + 1)..]), expectedBytes))
            {
                try
                {
                    using var doc = JsonDocument.Parse(payload);
                    return doc.RootElement.Clone();
                }
                catch (JsonException)
                {
                    throw new WebhookVerificationException("Payload is not JSON");
                }
            }
        }
        throw new WebhookVerificationException("Signature does not match");
    }
}
