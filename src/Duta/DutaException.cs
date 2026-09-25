using System.Text.Json;

namespace Duta;

/// <summary>
/// An error from the Duta API, or from reaching it. <see cref="Code"/> is Duta's error code
/// (https://docs.duta.indra.sh/guides/errors/), <see cref="Name"/> the Resend-compatible name and
/// <see cref="RequestId"/> finds the request on the Logs screen.
/// </summary>
public class DutaException : Exception
{
    /// <summary>Creates an error.</summary>
    public DutaException(string message, int? statusCode, string name, string code, string? requestId,
        JsonElement? detail = null, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
        Name = name;
        Code = code;
        RequestId = requestId;
        Detail = detail;
    }

    /// <summary>The HTTP status, or null when Duta could not be reached.</summary>
    public int? StatusCode { get; }

    /// <summary>The Resend-compatible error name.</summary>
    public string Name { get; }

    /// <summary>Duta's error code, such as <c>validation_failed</c>.</summary>
    public string Code { get; }

    /// <summary>Finds the request on the Logs screen.</summary>
    public string? RequestId { get; }

    /// <summary>More about the error, when Duta sent it.</summary>
    public JsonElement? Detail { get; }
}

/// <summary>The delivery is not from Duta, was changed or is too old. Never trust its body.</summary>
public class WebhookVerificationException : Exception
{
    /// <summary>Creates an error.</summary>
    public WebhookVerificationException(string message) : base(message) { }
}
