using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using Xunit;

namespace Duta.Tests;

public class ClientTests
{
    internal static readonly SendEmailRequest Email = new()
    {
        From = "Kedai <resit@kedai.my>", To = ["siti@example.com"], Subject = "Resit", Text = "Terima kasih",
    };

    [Fact]
    public async Task SendsWithTheKeyAUserAgentAndAnIdempotencyKey()
    {
        var (duta, fake) = Fake.Create(() => Fake.Json(202, """{"id":"msg_1","status":"queued"}"""));
        var sent = await duta.Emails.SendAsync(Email);

        Assert.Equal(new SendEmailResponse("msg_1", "queued"), sent);
        var r = fake.Requests[0];
        Assert.Equal(HttpMethod.Post, r.Method);
        Assert.Equal("https://api.duta.indra.sh/v1/emails", r.RequestUri!.ToString());
        Assert.Equal("Bearer duta_test", r.Headers.Authorization!.ToString());
        Assert.StartsWith("duta-dotnet/", r.Headers.UserAgent.ToString());
        Assert.Matches(new Regex("^duta-dotnet-[0-9a-f-]{36}$"), r.Headers.GetValues("Idempotency-Key").Single());

        using var body = JsonDocument.Parse(fake.Bodies[0]!);
        Assert.Equal("Kedai <resit@kedai.my>", body.RootElement.GetProperty("from").GetString());
        Assert.Equal("Terima kasih", body.RootElement.GetProperty("text").GetString());
        Assert.False(body.RootElement.TryGetProperty("html", out _), "nulls are not sent");
    }

    [Fact]
    public async Task UsesTheIdempotencyKeyItIsGiven()
    {
        var (duta, fake) = Fake.Create();
        await duta.Emails.SendAsync(Email, new SendOptions("receipt-1042"));
        Assert.Equal("receipt-1042", fake.Requests[0].Headers.GetValues("Idempotency-Key").Single());
    }

    [Fact]
    public async Task ThrowsTheApiErrorWithItsCodeAndRequestId()
    {
        var (duta, _) = Fake.Create(() => Fake.Error(422, "validation_failed"));
        var e = await Assert.ThrowsAsync<DutaException>(() => duta.Emails.SendAsync(Email));
        Assert.Equal(422, e.StatusCode);
        Assert.Equal("validation_failed", e.Code);
        Assert.Equal("validation_failed happened", e.Message);
        Assert.Equal("req_body", e.RequestId);
    }

    [Fact]
    public async Task FallsBackToTheRequestIdHeader()
    {
        var (duta, _) = Fake.Create(new DutaClientOptions { MaxRetries = 0 },
            () => Fake.Json(502, "upstream broke", ("X-Request-Id", "req_h")));
        var e = await Assert.ThrowsAsync<DutaException>(() => duta.Domains.ListAsync());
        Assert.Equal(502, e.StatusCode);
        Assert.Equal("req_h", e.RequestId);
    }

    [Fact]
    public async Task RetriesA429ForAnyRequest()
    {
        var (duta, fake) = Fake.Create(() => Fake.Error(429, "rate_limited", ("Retry-After", "0")),
            () => Fake.Json(201, """{"id":"dom_1","name":"kedai.my","status":"pending","region":"ap-southeast-1","created_at":"x"}"""));
        var domain = await duta.Domains.CreateAsync(new DomainRequest("kedai.my"));
        Assert.Equal("dom_1", domain.Id);
        Assert.Equal(2, fake.Requests.Count);
    }

    [Fact]
    public async Task RetriesASendAfterA5xxWithTheSameIdempotencyKey()
    {
        var (duta, fake) = Fake.Create(() => Fake.Error(503, "platform_halted"),
            () => Fake.Json(202, """{"id":"msg_2","status":"queued"}"""));
        Assert.Equal("msg_2", (await duta.Emails.SendAsync(Email)).Id);
        Assert.Equal(2, fake.Requests.Count);
        Assert.Equal(fake.Requests[0].Headers.GetValues("Idempotency-Key").Single(),
            fake.Requests[1].Headers.GetValues("Idempotency-Key").Single());
    }

    [Fact]
    public async Task NeverRetriesA5xxOnAWriteThatMayHaveTakenEffect()
    {
        var (duta, fake) = Fake.Create(() => Fake.Error(500, "internal_error"), () => Fake.Json(201, "{}"));
        var e = await Assert.ThrowsAsync<DutaException>(() => duta.ApiKeys.CreateAsync(new ApiKeyRequest("CI")));
        Assert.Equal("internal_error", e.Code);
        Assert.Single(fake.Requests);
    }

    [Fact]
    public async Task RetriesAReadAfterANetworkFailureButNotAWrite()
    {
        var (read, _) = Fake.Create(() => throw new HttpRequestException("socket hang up"),
            () => Fake.Json(200, """{"id":"msg_3","from":"a","to":[],"subject":"s","status":"queued","last_event":"queued","created_at":"x"}"""));
        Assert.Equal("msg_3", (await read.Emails.GetAsync("msg_3")).Id);

        var (write, fake) = Fake.Create(() => throw new HttpRequestException("socket hang up"));
        var e = await Assert.ThrowsAsync<DutaException>(() => write.Webhooks.TestAsync("whk_1"));
        Assert.Null(e.StatusCode);
        Assert.Equal("network_error", e.Code);
        Assert.Single(fake.Requests);
    }

    [Fact]
    public async Task ListAllPagesThroughEveryEmail()
    {
        const string item = """{"from":"a","to":[],"subject":"s","status":"queued","last_event":"queued","created_at":"x"}""";
        var (duta, fake) = Fake.Create(
            () => Fake.Json(200, $$$"""{"data":[{"id":"msg_a",{{{item[1..^1]}}}},{"id":"msg_b",{{{item[1..^1]}}}}],"has_more":true,"next":"msg_b"}"""),
            () => Fake.Json(200, $$$"""{"data":[{"id":"msg_c",{{{item[1..^1]}}}}],"has_more":false,"next":null}"""));
        var ids = new List<string>();
        await foreach (var e in duta.Emails.ListAllAsync(new EmailListParams { Limit = 2 })) ids.Add(e.Id);

        Assert.Equal(["msg_a", "msg_b", "msg_c"], ids);
        var query = HttpUtility.ParseQueryString(fake.Requests[1].RequestUri!.Query);
        Assert.Equal("msg_b", query["after"]);
        Assert.Equal("2", query["limit"]);
    }

    [Fact]
    public async Task TakesABaseUrlWithOrWithoutATrailingSlash()
    {
        var (duta, fake) = Fake.Create(new DutaClientOptions { BaseUrl = "http://localhost:8787/" },
            () => Fake.Json(200, """{"period":"2026-09","plan":{},"usage":{},"counts":{}}"""));
        await duta.Usage.GetAsync();
        Assert.Equal("http://localhost:8787/v1/usage", fake.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public void RefusesToStartWithoutAKey()
    {
        var saved = Environment.GetEnvironmentVariable("DUTA_API_KEY");
        Environment.SetEnvironmentVariable("DUTA_API_KEY", null);
        try
        {
            Assert.Throws<ArgumentException>(() => new DutaClient());
        }
        finally
        {
            Environment.SetEnvironmentVariable("DUTA_API_KEY", saved);
        }
    }

    [Fact]
    public void RegistersWithDependencyInjection()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        Microsoft.Extensions.DependencyInjection.DutaServiceCollectionExtensions.AddDuta(services, o => o.ApiKey = "duta_di");
        using var provider = Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(services);
        Assert.NotNull(Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<DutaClient>(provider));
    }
}
