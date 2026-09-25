using System.Text.Json;
using Xunit;

namespace Duta.Tests;

/// <summary>
/// The fixture is a delivery signed by Duta's own webhook signer, so these check the SDK
/// against what Duta sends. The clock is set to the moment it was signed.
/// </summary>
public class WebhookTests
{
    private sealed record Fixture(string Secret, string Body, long Timestamp, Dictionary<string, string> Headers);

    private static readonly Fixture F = JsonSerializer.Deserialize<Fixture>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "webhook.json")),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    private static readonly TimeSpan FiveMinutes = TimeSpan.FromMinutes(5);
    private static DateTimeOffset At(long seconds) => DateTimeOffset.FromUnixTimeSeconds(seconds);

    [Fact]
    public void AcceptsADeliverySignedByDuta()
    {
        var evt = WebhookSignature.VerifyAt(F.Body, F.Headers, F.Secret, FiveMinutes, At(F.Timestamp + 10));
        Assert.Equal("email.delivered", evt.GetProperty("type").GetString());
    }

    [Fact]
    public void ReadsTheSvixHeadersInAnyCase()
    {
        var svix = new Dictionary<string, string>
        {
            ["Svix-Id"] = F.Headers["svix-id"], ["SVIX-TIMESTAMP"] = F.Headers["svix-timestamp"], ["svix-signature"] = F.Headers["svix-signature"],
        };
        Assert.Equal("email.delivered", WebhookSignature.VerifyAt(F.Body, svix, F.Secret, FiveMinutes, At(F.Timestamp)).GetProperty("type").GetString());
    }

    [Fact]
    public void RefusesABodyChangedAfterSigning() =>
        Assert.Throws<WebhookVerificationException>(() =>
            WebhookSignature.VerifyAt(F.Body.Replace("delivered", "bounced"), F.Headers, F.Secret, FiveMinutes, At(F.Timestamp)));

    [Fact]
    public void RefusesTheWrongSecret()
    {
        var e = Assert.Throws<WebhookVerificationException>(() =>
            WebhookSignature.VerifyAt(F.Body, F.Headers, "whsec_" + string.Concat(Enumerable.Repeat("cd34", 8)), FiveMinutes, At(F.Timestamp)));
        Assert.Contains("does not match", e.Message);
    }

    [Fact]
    public void RefusesAnOldDelivery()
    {
        var e = Assert.Throws<WebhookVerificationException>(() =>
            WebhookSignature.VerifyAt(F.Body, F.Headers, F.Secret, FiveMinutes, At(F.Timestamp + 3600)));
        Assert.Contains("outside the allowed window", e.Message);
    }

    [Fact]
    public void RefusesADeliveryWithNoSignatureHeaders()
    {
        var e = Assert.Throws<WebhookVerificationException>(() => WebhookSignature.Verify(F.Body, [], F.Secret));
        Assert.Contains("Missing", e.Message);
    }
}
