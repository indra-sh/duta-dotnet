using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Duta;

/// <summary>Thrown when the Duta API returns an error.</summary>
public class DutaException : Exception
{
    /// <summary>Machine-readable error name, e.g. "rate_limit_exceeded".</summary>
    public string Name { get; }

    /// <summary>HTTP status code (0 for network errors).</summary>
    public int StatusCode { get; }

    /// <summary>Suppressed recipient addresses, present on some 422 errors.</summary>
    public IReadOnlyList<string>? Blocked { get; }

    public DutaException(string name, string message, int statusCode, IReadOnlyList<string>? blocked = null)
        : base(message)
    {
        Name = name;
        StatusCode = statusCode;
        Blocked = blocked;
    }

    private static readonly Dictionary<int, string> NameByStatus = new()
    {
        [400] = "validation_error",
        [401] = "authentication_error",
        [403] = "permission_denied",
        [404] = "not_found",
        [422] = "unprocessable_entity",
        [429] = "rate_limit_exceeded",
        [500] = "internal_server_error",
    };

    /// <summary>Normalise either of Duta's error shapes into a DutaException.</summary>
    public static DutaException FromResponse(int statusCode, string rawBody)
    {
        var fallback = NameByStatus.TryGetValue(statusCode, out var mapped) ? mapped : "api_error";

        if (!string.IsNullOrEmpty(rawBody))
        {
            try
            {
                using var doc = JsonDocument.Parse(rawBody);
                var root = doc.RootElement;

                IReadOnlyList<string>? blocked = null;
                if (root.TryGetProperty("blocked", out var b) && b.ValueKind == JsonValueKind.Array)
                {
                    var list = new List<string>();
                    foreach (var item in b.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String)
                        {
                            list.Add(item.GetString()!);
                        }
                    }
                    blocked = list;
                }

                // Rate-limit shape: { statusCode, name, message }
                if (root.TryGetProperty("name", out var nm) && nm.ValueKind == JsonValueKind.String
                    && root.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
                {
                    return new DutaException(nm.GetString()!, msg.GetString()!, statusCode, blocked);
                }

                // Common shape: { error: string }
                if (root.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.String)
                {
                    return new DutaException(fallback, err.GetString()!, statusCode, blocked);
                }
            }
            catch (JsonException)
            {
                // fall through to the generic message
            }
        }

        return new DutaException(fallback, $"Request failed with status {statusCode}", statusCode);
    }
}
