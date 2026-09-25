using System.Text.Json;
using System.Text.Json.Serialization;

namespace Duta;

/// <summary>
/// One email. It needs From, To, Subject and at least one of Html or Text. From must be on a
/// verified domain or your sandbox address. Addresses are "Name &lt;email&gt;" or plain.
/// </summary>
public sealed record SendEmailRequest
{
    /// <summary>The sender.</summary>
    public required string From { get; init; }
    /// <summary>The recipients.</summary>
    public required IReadOnlyList<string> To { get; init; }
    /// <summary>The subject line.</summary>
    public required string Subject { get; init; }
    /// <summary>The HTML body.</summary>
    public string? Html { get; init; }
    /// <summary>The plain text body.</summary>
    public string? Text { get; init; }
    /// <summary>Copied recipients.</summary>
    public IReadOnlyList<string>? Cc { get; init; }
    /// <summary>Blind copied recipients.</summary>
    public IReadOnlyList<string>? Bcc { get; init; }
    /// <summary>Where replies go.</summary>
    public IReadOnlyList<string>? ReplyTo { get; init; }
    /// <summary>Custom headers.</summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }
    /// <summary>Attachments, capped per plan.</summary>
    public IReadOnlyList<Attachment>? Attachments { get; init; }
    /// <summary>Labels for your own records.</summary>
    public IReadOnlyList<Tag>? Tags { get; init; }
}

/// <summary>Exactly one of Content (base64) or Path (a public https URL; not accepted in a batch).</summary>
public sealed record Attachment
{
    /// <summary>The file name.</summary>
    public string? Filename { get; init; }
    /// <summary>The file, base64.</summary>
    public string? Content { get; init; }
    /// <summary>A public https URL Duta downloads.</summary>
    public string? Path { get; init; }
    /// <summary>Inferred from the filename when left out.</summary>
    public string? ContentType { get; init; }
    /// <summary>For inline images referenced as cid: in the HTML.</summary>
    public string? ContentId { get; init; }
}

/// <summary>A label. Names and values are ASCII letters, numbers, underscores and dashes.</summary>
public sealed record Tag(string Name, string Value);

/// <summary>Makes a send safe to repeat for 24 hours. Left empty, the SDK makes one per call.</summary>
public sealed record SendOptions(string? IdempotencyKey = null);

/// <summary>Batch options. Validation is "strict" (the default) or "permissive".</summary>
public sealed record BatchOptions(string? IdempotencyKey = null, string? Validation = null);

/// <summary>A send's result.</summary>
public sealed record SendEmailResponse(string Id, string Status);

/// <summary>An email a batch could not send.</summary>
public sealed record BatchError(int Index, string Message, string Code, string Name);

/// <summary>A batch's result.</summary>
public sealed record BatchResponse(IReadOnlyList<SendEmailResponse> Data, IReadOnlyList<BatchError>? Errors);

/// <summary>One page, newest first. Pass Next as After for the following one.</summary>
public sealed record Page<T>(IReadOnlyList<T> Data, bool HasMore, string? Next);

/// <summary>Paging parameters.</summary>
public record ListParams
{
    /// <summary>Items per page.</summary>
    public int? Limit { get; init; }
    /// <summary>The Next value of the previous page.</summary>
    public string? After { get; init; }
}

/// <summary>Email list filters.</summary>
public sealed record EmailListParams : ListParams
{
    /// <summary>Matches subject, to and from.</summary>
    public string? Q { get; init; }
    /// <summary>Duta's message status.</summary>
    public string? Status { get; init; }
}

/// <summary>Log list filters.</summary>
public sealed record LogListParams : ListParams
{
    /// <summary>Matches the path.</summary>
    public string? Q { get; init; }
    /// <summary>A code such as 404, or a class: 2, 4 or 5.</summary>
    public string? Status { get; init; }
    /// <summary>An HTTP method.</summary>
    public string? Method { get; init; }
}

/// <summary>Suppression list filters.</summary>
public sealed record SuppressionListParams : ListParams
{
    /// <summary>Matches the address.</summary>
    public string? Q { get; init; }
}

/// <summary>An email in a list.</summary>
public sealed record EmailSummary(string Id, string From, IReadOnlyList<string> To, string Subject, string Status,
    string LastEvent, string CreatedAt);

/// <summary>Something that happened to an email.</summary>
public sealed record EmailEvent(string Type, string OccurredAt, string? Payload);

/// <summary>An email and its history.</summary>
public sealed record Email(string Id, string From, IReadOnlyList<string> To, IReadOnlyList<string>? Cc,
    IReadOnlyList<string>? Bcc, IReadOnlyList<string>? ReplyTo, string Subject, string? Html, string? Text,
    IReadOnlyList<Tag>? Tags, string Status, string LastEvent, string? FailureReason, string? MessageId,
    string CreatedAt, string? LogId, IReadOnlyList<EmailEvent>? Events);

/// <summary>A domain to add.</summary>
public sealed record DomainRequest(string Name);

/// <summary>A DNS record to publish.</summary>
public sealed record DnsRecord(string Record, string Type, string Name, string Value, int? Priority, string Ttl,
    string Status, string Purpose, bool Required, string Group, bool? Found);

/// <summary>A sending domain.</summary>
public sealed record Domain(string Id, string Name, string Status, string Region, string CreatedAt,
    string? VerifiedAt, string? LastCheckedAt, IReadOnlyList<DnsRecord>? Records);

/// <summary>The result of checking a domain's DNS now.</summary>
public sealed record DomainVerification(string Id, string Name, string Status, bool DnsReady,
    [property: JsonPropertyName("ses_verified")] bool SesVerified, IReadOnlyList<DnsRecord> Records);

/// <summary>A key to create. Permission is "sending_access" (the default) or "full_access".</summary>
public sealed record ApiKeyRequest(string Name, string? Permission = null);

/// <summary>An API key.</summary>
public sealed record ApiKey(string Id, string Name, string Permission, string Prefix,
    [property: JsonPropertyName("last4")] string Last4, string? LastUsedAt, string? RevokedAt, string CreatedAt);

/// <summary>A new API key. The key is in Token and is shown only once.</summary>
public sealed record CreatedApiKey(string Id, string Name, string Permission, string Prefix,
    [property: JsonPropertyName("last4")] string Last4, string CreatedAt, string Token);

/// <summary>An endpoint to add. Events such as "email.delivered"; none means every event.</summary>
public sealed record WebhookRequest(string Endpoint, IReadOnlyList<string>? Events = null);

/// <summary>A webhook endpoint.</summary>
public sealed record Webhook(string Id, string Url, IReadOnlyList<string> Events, string Status, int FailureCount,
    string? LastFailureAt, string CreatedAt);

/// <summary>A new webhook endpoint. SigningSecret is shown only once.</summary>
public sealed record CreatedWebhook(string Id, string Url, IReadOnlyList<string> Events, string SigningSecret,
    string CreatedAt);

/// <summary>One attempt to deliver an event.</summary>
public sealed record WebhookDelivery(string Id, string EventType, string? MessageId, int Attempt, int? StatusCode,
    [property: JsonConverter(typeof(FlexBoolConverter))] bool Ok, string? Error, int? DurationMs, string CreatedAt);

/// <summary>The result of a test delivery.</summary>
public sealed record WebhookTestResult(bool Delivered, int? Status, string? Error);

/// <summary>An address Duta will not send to.</summary>
public sealed record Suppression(string Email, string Reason, string? Source, string CreatedAt);

/// <summary>An API request in the log.</summary>
public sealed record LogEntry(string Id, string Method, string Path, int Status, int? DurationMs, string? MessageId,
    string? KeyName, string? KeyScope, string CreatedAt);

/// <summary>An API request with its bodies.</summary>
public sealed record Log(string Id, string Method, string Path, int Status, int? DurationMs, string? MessageId,
    string? KeyName, string? KeyScope, string CreatedAt, string? UserAgent, string? Ip, string? RequestBody,
    string? ResponseBody);

/// <summary>This month's usage, the plan's limits and the last 30 days by status.</summary>
public sealed record Usage(string Period, JsonElement Plan,
    [property: JsonPropertyName("usage")] JsonElement Totals, IReadOnlyDictionary<string, int> Counts);

/// <summary>The answer to a delete, revoke or enable.</summary>
public sealed record Removed(string? Id, string? Email, bool? Deleted, string Status);

/// <summary>Reads true/false or 1/0, since some reads return either.</summary>
internal sealed class FlexBoolConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Number => reader.GetInt32() != 0,
            _ => throw new JsonException("expected a boolean or a number"),
        };

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options) =>
        writer.WriteBooleanValue(value);
}
