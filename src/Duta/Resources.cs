using System.Runtime.CompilerServices;

namespace Duta;

using Q = IReadOnlyDictionary<string, string?>;

/// <summary>Send and read emails.</summary>
public sealed class EmailsResource
{
    private readonly DutaClient _c;
    internal EmailsResource(DutaClient c) => _c = c;

    /// <summary>Send one email.</summary>
    public Task<SendEmailResponse> SendAsync(SendEmailRequest email, SendOptions? options = null, CancellationToken ct = default) =>
        _c.SendAsync<SendEmailResponse>(HttpMethod.Post, "/emails", email,
            headers: new Dictionary<string, string?> { ["Idempotency-Key"] = options?.IdempotencyKey ?? DutaClient.NewIdempotencyKey() },
            idempotent: true, ct: ct);

    /// <summary>One email and its history.</summary>
    public Task<Email> GetAsync(string id, CancellationToken ct = default) =>
        _c.SendAsync<Email>(HttpMethod.Get, $"/emails/{DutaClient.Seg(id)}", idempotent: true, ct: ct);

    /// <summary>One page, newest first.</summary>
    public Task<Page<EmailSummary>> ListAsync(EmailListParams? p = null, CancellationToken ct = default) =>
        _c.SendAsync<Page<EmailSummary>>(HttpMethod.Get, "/emails", query: new Dictionary<string, string?>
        {
            ["limit"] = DutaClient.Num(p?.Limit), ["after"] = p?.After, ["q"] = p?.Q, ["status"] = p?.Status,
        }, idempotent: true, ct: ct);

    /// <summary>Every email matching the filters, page by page.</summary>
    public IAsyncEnumerable<EmailSummary> ListAllAsync(EmailListParams? p = null, CancellationToken ct = default) =>
        Pages.All(after => ListAsync((p ?? new EmailListParams()) with { After = after }, ct), ct);
}

/// <summary>Send up to 100 emails in one request.</summary>
public sealed class BatchResource
{
    private readonly DutaClient _c;
    internal BatchResource(DutaClient c) => _c = c;

    /// <summary>Strict by default: one invalid email and none are sent.</summary>
    public Task<BatchResponse> SendAsync(IReadOnlyList<SendEmailRequest> emails, BatchOptions? options = null, CancellationToken ct = default) =>
        _c.SendAsync<BatchResponse>(HttpMethod.Post, "/emails/batch", emails,
            headers: new Dictionary<string, string?>
            {
                ["Idempotency-Key"] = options?.IdempotencyKey ?? DutaClient.NewIdempotencyKey(),
                ["x-batch-validation"] = options?.Validation,
            },
            idempotent: true, ct: ct);
}

/// <summary>Sending domains.</summary>
public sealed class DomainsResource
{
    private readonly DutaClient _c;
    internal DomainsResource(DutaClient c) => _c = c;

    /// <summary>Adds a domain and returns the DNS records to publish.</summary>
    public Task<Domain> CreateAsync(DomainRequest domain, CancellationToken ct = default) =>
        _c.SendAsync<Domain>(HttpMethod.Post, "/domains", domain, ct: ct);

    /// <summary>Every domain on the account.</summary>
    public Task<Page<Domain>> ListAsync(CancellationToken ct = default) =>
        _c.SendAsync<Page<Domain>>(HttpMethod.Get, "/domains", idempotent: true, ct: ct);

    /// <summary>One domain with its records.</summary>
    public Task<Domain> GetAsync(string id, CancellationToken ct = default) =>
        _c.SendAsync<Domain>(HttpMethod.Get, $"/domains/{DutaClient.Seg(id)}", idempotent: true, ct: ct);

    /// <summary>Checks the domain's DNS now rather than waiting for Duta's own check.</summary>
    public Task<DomainVerification> VerifyAsync(string id, CancellationToken ct = default) =>
        _c.SendAsync<DomainVerification>(HttpMethod.Post, $"/domains/{DutaClient.Seg(id)}/verify", idempotent: true, ct: ct);

    /// <summary>Removes a domain.</summary>
    public Task<Removed> RemoveAsync(string id, CancellationToken ct = default) =>
        _c.SendAsync<Removed>(HttpMethod.Delete, $"/domains/{DutaClient.Seg(id)}", idempotent: true, ct: ct);
}

/// <summary>API keys.</summary>
public sealed class ApiKeysResource
{
    private readonly DutaClient _c;
    internal ApiKeysResource(DutaClient c) => _c = c;

    /// <summary>Makes a key. The key itself is in Token and is shown only once.</summary>
    public Task<CreatedApiKey> CreateAsync(ApiKeyRequest key, CancellationToken ct = default) =>
        _c.SendAsync<CreatedApiKey>(HttpMethod.Post, "/api-keys", key, ct: ct);

    /// <summary>Every key on the account.</summary>
    public Task<Page<ApiKey>> ListAsync(CancellationToken ct = default) =>
        _c.SendAsync<Page<ApiKey>>(HttpMethod.Get, "/api-keys", idempotent: true, ct: ct);

    /// <summary>Revokes a key.</summary>
    public Task<Removed> RemoveAsync(string id, CancellationToken ct = default) =>
        _c.SendAsync<Removed>(HttpMethod.Delete, $"/api-keys/{DutaClient.Seg(id)}", idempotent: true, ct: ct);
}

/// <summary>Webhook endpoints.</summary>
public sealed class WebhooksResource
{
    private readonly DutaClient _c;
    internal WebhooksResource(DutaClient c) => _c = c;

    /// <summary>Adds an endpoint. SigningSecret in the answer is shown only once.</summary>
    public Task<CreatedWebhook> CreateAsync(WebhookRequest webhook, CancellationToken ct = default) =>
        _c.SendAsync<CreatedWebhook>(HttpMethod.Post, "/webhooks", webhook, ct: ct);

    /// <summary>Every endpoint.</summary>
    public Task<Page<Webhook>> ListAsync(CancellationToken ct = default) =>
        _c.SendAsync<Page<Webhook>>(HttpMethod.Get, "/webhooks", idempotent: true, ct: ct);

    /// <summary>One endpoint.</summary>
    public Task<Webhook> GetAsync(string id, CancellationToken ct = default) =>
        _c.SendAsync<Webhook>(HttpMethod.Get, $"/webhooks/{DutaClient.Seg(id)}", idempotent: true, ct: ct);

    /// <summary>Deletes an endpoint.</summary>
    public Task<Removed> RemoveAsync(string id, CancellationToken ct = default) =>
        _c.SendAsync<Removed>(HttpMethod.Delete, $"/webhooks/{DutaClient.Seg(id)}", idempotent: true, ct: ct);

    /// <summary>Turns on an endpoint that failed its way to disabled.</summary>
    public Task<Removed> EnableAsync(string id, CancellationToken ct = default) =>
        _c.SendAsync<Removed>(HttpMethod.Post, $"/webhooks/{DutaClient.Seg(id)}/enable", idempotent: true, ct: ct);

    /// <summary>Sends a signed test event to the endpoint.</summary>
    public Task<WebhookTestResult> TestAsync(string id, CancellationToken ct = default) =>
        _c.SendAsync<WebhookTestResult>(HttpMethod.Post, $"/webhooks/{DutaClient.Seg(id)}/test", ct: ct);

    /// <summary>
    /// Checks a delivery's signature and returns the event. Throws WebhookVerificationException
    /// when it is not from Duta or is too old. See <see cref="WebhookSignature"/>.
    /// </summary>
    public System.Text.Json.JsonElement Verify(string payload, IEnumerable<KeyValuePair<string, string>> headers,
        string secret, TimeSpan? tolerance = null) => WebhookSignature.Verify(payload, headers, secret, tolerance);

    /// <summary>Every attempt to deliver to an endpoint, newest first.</summary>
    public Task<Page<WebhookDelivery>> DeliveriesAsync(string id, ListParams? p = null, CancellationToken ct = default) =>
        _c.SendAsync<Page<WebhookDelivery>>(HttpMethod.Get, $"/webhooks/{DutaClient.Seg(id)}/deliveries",
            query: new Dictionary<string, string?> { ["limit"] = DutaClient.Num(p?.Limit), ["after"] = p?.After },
            idempotent: true, ct: ct);
}

/// <summary>Addresses Duta will not send to.</summary>
public sealed class SuppressionsResource
{
    private readonly DutaClient _c;
    internal SuppressionsResource(DutaClient c) => _c = c;

    /// <summary>One page, newest first.</summary>
    public Task<Page<Suppression>> ListAsync(SuppressionListParams? p = null, CancellationToken ct = default) =>
        _c.SendAsync<Page<Suppression>>(HttpMethod.Get, "/suppressions", query: new Dictionary<string, string?>
        {
            ["limit"] = DutaClient.Num(p?.Limit), ["after"] = p?.After, ["q"] = p?.Q,
        }, idempotent: true, ct: ct);

    /// <summary>Every suppressed address, page by page.</summary>
    public IAsyncEnumerable<Suppression> ListAllAsync(SuppressionListParams? p = null, CancellationToken ct = default) =>
        Pages.All(after => ListAsync((p ?? new SuppressionListParams()) with { After = after }, ct), ct);

    /// <summary>Suppresses an address.</summary>
    public Task<Suppression> CreateAsync(string email, CancellationToken ct = default) =>
        _c.SendAsync<Suppression>(HttpMethod.Post, "/suppressions", new Dictionary<string, string> { ["email"] = email },
            idempotent: true, ct: ct);

    /// <summary>Removes a manual entry. Bounces, complaints and unsubscribes are permanent.</summary>
    public Task<Removed> RemoveAsync(string email, CancellationToken ct = default) =>
        _c.SendAsync<Removed>(HttpMethod.Delete, $"/suppressions/{DutaClient.Seg(email)}", idempotent: true, ct: ct);
}

/// <summary>The API request log.</summary>
public sealed class LogsResource
{
    private readonly DutaClient _c;
    internal LogsResource(DutaClient c) => _c = c;

    /// <summary>One page, newest first.</summary>
    public Task<Page<LogEntry>> ListAsync(LogListParams? p = null, CancellationToken ct = default) =>
        _c.SendAsync<Page<LogEntry>>(HttpMethod.Get, "/logs", query: new Dictionary<string, string?>
        {
            ["limit"] = DutaClient.Num(p?.Limit), ["after"] = p?.After, ["q"] = p?.Q, ["status"] = p?.Status,
            ["method"] = p?.Method,
        }, idempotent: true, ct: ct);

    /// <summary>Every request matching the filters, page by page.</summary>
    public IAsyncEnumerable<LogEntry> ListAllAsync(LogListParams? p = null, CancellationToken ct = default) =>
        Pages.All(after => ListAsync((p ?? new LogListParams()) with { After = after }, ct), ct);

    /// <summary>One request, by the id from an error or the X-Request-Id header.</summary>
    public Task<Log> GetAsync(string id, CancellationToken ct = default) =>
        _c.SendAsync<Log>(HttpMethod.Get, $"/logs/{DutaClient.Seg(id)}", idempotent: true, ct: ct);
}

/// <summary>This month's usage.</summary>
public sealed class UsageResource
{
    private readonly DutaClient _c;
    internal UsageResource(DutaClient c) => _c = c;

    /// <summary>This month's usage, the plan's limits and the last 30 days by status.</summary>
    public Task<Usage> GetAsync(CancellationToken ct = default) =>
        _c.SendAsync<Usage>(HttpMethod.Get, "/usage", idempotent: true, ct: ct);
}

internal static class Pages
{
    public static async IAsyncEnumerable<T> All<T>(Func<string?, Task<Page<T>>> page,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        string? after = null;
        do
        {
            ct.ThrowIfCancellationRequested();
            var result = await page(after).ConfigureAwait(false);
            foreach (var item in result.Data) yield return item;
            after = result.HasMore && !string.IsNullOrEmpty(result.Next) ? result.Next : null;
        } while (after is not null);
    }
}
