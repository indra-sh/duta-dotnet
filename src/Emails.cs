using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Duta;

/// <summary>The "emails" resource: send, retrieve, and list emails.</summary>
public class Emails
{
    private readonly DutaClient _client;

    internal Emails(DutaClient client)
    {
        _client = client;
    }

    /// <summary>Send a transactional email.</summary>
    public Task<SendEmailResult> SendAsync(SendEmailOptions options, CancellationToken cancellationToken = default)
        => _client.RequestAsync<SendEmailResult>(HttpMethod.Post, "/v1/email/send", options, cancellationToken);

    /// <summary>Retrieve a single email by ID. Requires a full-access API key.</summary>
    public Task<Email> GetAsync(string id, CancellationToken cancellationToken = default)
        => _client.RequestAsync<Email>(HttpMethod.Get, $"/v1/email/{id}", null, cancellationToken);

    /// <summary>List emails, most recent first. Requires a full-access API key.</summary>
    public Task<ListEmailsResult> ListAsync(int page = 1, int limit = 20, CancellationToken cancellationToken = default)
        => _client.RequestAsync<ListEmailsResult>(HttpMethod.Get, $"/v1/email?page={page}&limit={limit}", null, cancellationToken);
}
