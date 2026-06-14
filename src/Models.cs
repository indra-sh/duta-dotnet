using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Duta;

/// <summary>Options for <see cref="Emails.SendAsync"/>.</summary>
public class SendEmailOptions
{
    /// <summary>Sender address. Its domain must be verified in your Duta account.</summary>
    [JsonPropertyName("from")]
    public string From { get; set; } = "";

    /// <summary>One or more recipients.</summary>
    [JsonPropertyName("to")]
    public IReadOnlyList<string> To { get; set; } = new List<string>();

    [JsonPropertyName("subject")]
    public string Subject { get; set; } = "";

    /// <summary>HTML body. Provide <see cref="Html"/>, <see cref="Text"/>, or both.</summary>
    [JsonPropertyName("html")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Html { get; set; }

    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }

    [JsonPropertyName("replyTo")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReplyTo { get; set; }

    [JsonPropertyName("tags")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Tags { get; set; }
}

/// <summary>Returned by <see cref="Emails.SendAsync"/>.</summary>
public class SendEmailResult
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";
}

/// <summary>A single email record.</summary>
public class Email
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("to")]
    public IReadOnlyList<string> To { get; set; } = new List<string>();

    [JsonPropertyName("from")]
    public string From { get; set; } = "";

    [JsonPropertyName("subject")]
    public string Subject { get; set; } = "";

    [JsonPropertyName("html")]
    public string? Html { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("createdAt")]
    public string? CreatedAt { get; set; }
}

/// <summary>Returned by <see cref="Emails.ListAsync"/>.</summary>
public class ListEmailsResult
{
    [JsonPropertyName("emails")]
    public IReadOnlyList<Email> Emails { get; set; } = new List<Email>();

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; }
}
