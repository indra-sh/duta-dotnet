using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Duta.Tests;

/// <summary>The SDK calls exactly the operations in Duta's OpenAPI spec, no more and no fewer.</summary>
public class CoverageTests
{
    [Fact]
    public async Task CallsExactlyTheOperationsInTheSpec()
    {
        // Answers every request with an empty object, so the calls are made and their
        // results are ignored: this test is about which requests are sent.
        var (d, fake) = Fake.Create(Enumerable.Repeat<Func<HttpResponseMessage>>(() => Fake.Json(200, "{}"), 0).ToArray());
        async Task Try(Func<Task> call)
        {
            try { await call(); } catch (Exception e) when (e is JsonException or NullReferenceException or ArgumentNullException) { }
        }

        await Try(() => d.Emails.SendAsync(ClientTests.Email));
        await Try(() => d.Emails.GetAsync("msg_1"));
        await Try(() => d.Emails.ListAsync());
        await Try(() => d.Batch.SendAsync([ClientTests.Email]));
        await Try(() => d.Domains.CreateAsync(new DomainRequest("kedai.my")));
        await Try(() => d.Domains.ListAsync());
        await Try(() => d.Domains.GetAsync("dom_1"));
        await Try(() => d.Domains.VerifyAsync("dom_1"));
        await Try(() => d.Domains.RemoveAsync("dom_1"));
        await Try(() => d.ApiKeys.CreateAsync(new ApiKeyRequest("CI")));
        await Try(() => d.ApiKeys.ListAsync());
        await Try(() => d.ApiKeys.RemoveAsync("key_1"));
        await Try(() => d.Webhooks.CreateAsync(new WebhookRequest("https://kedai.my/hook")));
        await Try(() => d.Webhooks.ListAsync());
        await Try(() => d.Webhooks.GetAsync("whk_1"));
        await Try(() => d.Webhooks.RemoveAsync("whk_1"));
        await Try(() => d.Webhooks.EnableAsync("whk_1"));
        await Try(() => d.Webhooks.TestAsync("whk_1"));
        await Try(() => d.Webhooks.DeliveriesAsync("whk_1"));
        await Try(() => d.Suppressions.ListAsync());
        await Try(() => d.Suppressions.CreateAsync("gone@example.com"));
        await Try(() => d.Suppressions.RemoveAsync("gone@example.com"));
        await Try(() => d.Logs.ListAsync());
        await Try(() => d.Logs.GetAsync("req_1"));
        await Try(() => d.Usage.GetAsync());

        using var spec = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "openapi.json")));
        var paths = spec.RootElement.GetProperty("paths");

        // Fixed paths first: /emails/batch also matches /emails/{id}.
        var templates = paths.EnumerateObject().Select(p => p.Name)
            .OrderBy(t => t.Count(c => c == '{')).ThenBy(t => t, StringComparer.Ordinal).ToList();

        var made = fake.Requests.Select(r =>
        {
            var path = Regex.Replace(r.RequestUri!.AbsolutePath, "^/v1", "");
            var match = templates.FirstOrDefault(t =>
                Regex.IsMatch(path, "^" + Regex.Replace(Regex.Escape(t), @"\\\{[^}]+}", "[^/]+") + "$"));
            return $"{r.Method.Method.ToLowerInvariant()} {match ?? "UNKNOWN " + path}";
        }).Distinct().OrderBy(s => s, StringComparer.Ordinal).ToList();

        var inSpec = paths.EnumerateObject()
            .SelectMany(p => p.Value.EnumerateObject().Select(op => $"{op.Name} {p.Name}"))
            // The archived body is the dashboard's preview, not something an SDK user needs.
            .Where(op => op != "get /emails/{id}/body")
            .OrderBy(s => s, StringComparer.Ordinal).ToList();

        Assert.Equal(inSpec, made);
    }
}
